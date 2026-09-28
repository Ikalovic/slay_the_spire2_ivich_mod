using System.Runtime.CompilerServices;
using System.Threading;
using BaseLib.Abstracts;
using HarmonyLib;
using Ivich.Core;
using Ivich.Mod.Character;
using Ivich.Mod.Resources;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Ivich.Mod.Mechanics;

/// <summary>Temporary form and exact HP state. Permanent form remains on the saved starter relic.</summary>
public static class FormRuntime
{
    private sealed class PlayerState(FormHealthState health)
    {
        public FormHealthState Health { get; } = health;
        public DoomDeathGate Death { get; } = new();
        public bool AncientDrawUsed;
    }
    private sealed class EnemyState(int normalMaximum)
    {
        public int NormalMaximum = normalMaximum;
        public int Doom;
        public DoomDeathGate Death { get; } = new();
    }
    private sealed class MutationState { public int Depth; }
    private sealed class FlatMaximumChange(Creature creature, int delta)
    {
        public Creature Creature { get; } = creature;
        public int Delta { get; } = delta;
        public bool Consumed;
    }
    private static readonly AsyncLocal<FlatMaximumChange?> CurrentFlatChange = new();
    private sealed class MutationScope(Creature creature) : IDisposable
    {
        private readonly MutationState _state = Mutations.GetOrCreateValue(creature);
        public MutationScope Enter() { _state.Depth++; return this; }
        public void Dispose() => _state.Depth--;
    }
    private static readonly ConditionalWeakTable<Player, PlayerState> Players = new();
    private static readonly ConditionalWeakTable<Creature, EnemyState> Enemies = new();
    private static readonly ConditionalWeakTable<Creature, MutationState> Mutations = new();
    private static bool IsInternal(Creature creature) => Mutations.TryGetValue(creature, out var state) && state.Depth > 0;
    private static IDisposable Changing(Creature creature) => new MutationScope(creature).Enter();
    private static Form PermanentForm(Player player) => (Form)(IvichRuntime.Relic(player)?.FormValue ?? 0);
    private static PlayerState State(Player player) => Players.GetValue(player,
        p => new PlayerState(new FormHealthState(PermanentForm(p), p.Creature.CurrentHp, p.Creature.MaxHp)));
    public static Form CurrentForm(Player player) => Players.TryGetValue(player, out var state) ? state.Health.Form : PermanentForm(player);
    public static void BeginCombat(Player player)
    {
        Players.Remove(player);
        _ = State(player);
    }
    public static bool TryUseAncientUpgrade(Player player)
    {
        var state = State(player);
        if (state.AncientDrawUsed) return false;
        state.AncientDrawUsed = true;
        return true;
    }
    public static Task SwitchForm(Player player, Form form)
    {
        if (form is not (Form.Mage or Form.Dragon)) throw new ArgumentOutOfRangeException(nameof(form));
        var state = State(player);
        if (state.Health.Form == form) return Task.CompletedTask;
        var pcs = player.PlayerCombatState;
        int inventory = pcs is null ? 0 : state.Health.Form == Form.Dragon
            ? CustomResources<RageResource>.Get(pcs).Amount : CustomResources<ManaResource>.Get(pcs).Amount;
        if (pcs is not null)
        {
            CustomResources<ManaResource>.Get(pcs).Amount = 0;
            CustomResources<RageResource>.Get(pcs).Amount = 0;
        }
        state.Health.SwitchForm(form);
        ApplyHealth(player.Creature, state.Health);
        // Direct assignment intentionally emits neither production nor payment hooks.
        if (pcs is not null)
        {
            if (form == Form.Dragon) CustomResources<RageResource>.Get(pcs).Amount = inventory;
            else CustomResources<ManaResource>.Get(pcs).Amount = inventory;
        }
        return Task.CompletedTask;
    }
    public static Task EndCombat(Player player)
    {
        if (!Players.TryGetValue(player, out var state)) return Task.CompletedTask;
        state.Health.SwitchForm(PermanentForm(player));
        state.Health.SetDoom(0);
        ApplyHealth(player.Creature, state.Health);
        Players.Remove(player);
        return Task.CompletedTask;
    }
    public static Task IncreaseMaximum(Player player, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        var state = State(player);
        state.Health.IncreaseMaximum(amount);
        ApplyHealth(player.Creature, state.Health);
        return Task.CompletedTask;
    }
    private static void ApplyHealth(Creature creature, FormHealthState health)
    {
        using var change = Changing(creature);
        creature.SetMaxHpInternal(health.MaximumHealth);
        creature.SetCurrentHpInternal(health.CurrentHealth);
    }
    public static async Task ApplyDoom(Creature creature, int stacks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(stacks);
        if (creature.Player is { Character: IvichCharacter } player)
        {
            var state = State(player);
            state.Health.SetDoom(stacks);
            if (state.Death.TryRequestDeath(stacks)) await CreatureCmd.Kill(creature);
            else if (!state.Health.RequiresDeathProcessing) ApplyHealth(creature, state.Health);
        }
        else
        {
            var state = Enemies.GetValue(creature, c => new EnemyState(c.MaxHp));
            state.Doom = stacks;
            if (state.Death.TryRequestDeath(stacks)) await CreatureCmd.Kill(creature);
            else if (stacks < 100)
            {
                using var change = Changing(creature);
                creature.SetMaxHpInternal(DoomRules.MaximumHealth(state.NormalMaximum, stacks));
            }
        }
    }
    public static Task RemoveDoom(Creature creature)
    {
        if (creature.Player is { } player && Players.TryGetValue(player, out var state))
        {
            state.Health.SetDoom(0);
            state.Death.TryRequestDeath(0);
            ApplyHealth(creature, state.Health);
        }
        if (Enemies.TryGetValue(creature, out var enemy))
        {
            using var change = Changing(creature);
            creature.SetMaxHpInternal(enemy.NormalMaximum);
            Enemies.Remove(creature);
        }
        return Task.CompletedTask;
    }
    private static void ObserveHealth(Creature creature, int previous)
    {
        if (IsInternal(creature) || creature.CurrentHp == previous || creature.Player is not { } player) return;
        if (Players.TryGetValue(player, out var state)) state.Health.ObserveHealth(creature.CurrentHp);
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.SetCurrentHpInternal))]
    private static class HealthSetPatch
    {
        private static void Prefix(Creature __instance, out int __state) => __state = __instance.CurrentHp;
        private static void Postfix(Creature __instance, int __state) => ObserveHealth(__instance, __state);
    }
    [HarmonyPatch(typeof(Creature), nameof(Creature.LoseHpInternal))]
    private static class HealthLossPatch
    {
        private static void Prefix(Creature __instance, out int __state) => __state = __instance.CurrentHp;
        private static void Postfix(Creature __instance, int __state) => ObserveHealth(__instance, __state);
    }
    [HarmonyPatch(typeof(Creature), nameof(Creature.SetMaxHpInternal))]
    private static class MaximumPatch
    {
        private static void Postfix(Creature __instance)
        {
            if (!IsInternal(__instance) && __instance.MaxHp == 0 && __instance.Player is { } player && Players.TryGetValue(player, out var state))
                state.Health.ObserveHealth(0);
        }
        private static void Prefix(Creature __instance, ref decimal amount)
        {
            if (IsInternal(__instance) || amount <= 0) return;
            int requested = (int)Math.Min(amount, 999999999m);
            var flat = CurrentFlatChange.Value;
            bool isFlat = flat is { Consumed: false } && flat.Creature == __instance;
            if (isFlat) flat!.Consumed = true;
            if (__instance.Player is { } player && Players.TryGetValue(player, out var state))
            {
                if (isFlat) state.Health.IncreaseMaximum(flat!.Delta);
                else state.Health.SetNormalMaximum(requested);
                amount = state.Health.MaximumHealth;
            }
            else if (Enemies.TryGetValue(__instance, out var enemy))
            {
                enemy.NormalMaximum = isFlat ? Math.Max(1, checked(enemy.NormalMaximum + flat!.Delta)) : requested;
                amount = enemy.Doom < 100 ? DoomRules.MaximumHealth(enemy.NormalMaximum, enemy.Doom) : enemy.NormalMaximum;
            }
        }
    }

    // The original async body captures the marker in its ExecutionContext. The postfix restores
    // the caller immediately, so unrelated commands after the returned Task never inherit it.
    [HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.GainMaxHp))]
    private static class GainMaximumPatch
    {
        private static void Prefix(Creature creature, decimal amount, out FlatMaximumChange? __state)
        {
            __state = CurrentFlatChange.Value;
            CurrentFlatChange.Value = new FlatMaximumChange(creature, (int)Math.Clamp(decimal.Floor(amount), 0, 999999999));
        }
        private static void Postfix(FlatMaximumChange? __state) => CurrentFlatChange.Value = __state;
        private static void Finalizer(Exception? __exception, FlatMaximumChange? __state)
        { if (__exception is not null) CurrentFlatChange.Value = __state; }
    }
    [HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.LoseMaxHp))]
    private static class LoseMaximumPatch
    {
        private static void Prefix(Creature creature, decimal amount, out FlatMaximumChange? __state)
        {
            __state = CurrentFlatChange.Value;
            CurrentFlatChange.Value = new FlatMaximumChange(creature, -(int)Math.Clamp(decimal.Ceiling(amount), 0, 999999999));
        }
        private static void Postfix(FlatMaximumChange? __state) => CurrentFlatChange.Value = __state;
        private static void Finalizer(Exception? __exception, FlatMaximumChange? __state)
        { if (__exception is not null) CurrentFlatChange.Value = __state; }
    }
    // LoseMaxHp can deal damage before assigning its final maximum. A death/phase change inside
    // that damage is an absolute operation of its own, not part of the pending flat reduction.
    [HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Damage), [typeof(PlayerChoiceContext), typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel)])]
    private static class SuspendMaximumDuringDamagePatch
    {
        private static void Prefix(out FlatMaximumChange? __state)
        { __state = CurrentFlatChange.Value; CurrentFlatChange.Value = null; }
        private static void Postfix(FlatMaximumChange? __state) => CurrentFlatChange.Value = __state;
        private static void Finalizer(Exception? __exception, FlatMaximumChange? __state)
        { if (__exception is not null) CurrentFlatChange.Value = __state; }
    }
}

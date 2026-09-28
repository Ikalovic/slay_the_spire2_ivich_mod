using System.Runtime.CompilerServices;
using BaseLib.Abstracts;
using Ivich.Core;
using Ivich.Mod.Cards;
using Ivich.Mod.Powers;
using Ivich.Mod.Relics;
using Ivich.Mod.Resources;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
namespace Ivich.Mod.Mechanics;

public static class IvichRuntime
{
    internal sealed class TurnState
    {
        public bool ScythePlayed, LostLife, Healed;
        public int RageRemainder;
        public HashSet<int> UsedInscriptions = [];
        public Dictionary<CardModel, HashSet<Creature>> HitTargets = [];
        public int TemporaryDexterity, TemporaryInvigoration, TemporaryThorns;
    }
    private sealed class FreezeState { public long Threshold = 10; }
    private static readonly ConditionalWeakTable<Player, TurnState> Turns = new();
    private static readonly ConditionalWeakTable<Creature, FreezeState> FreezeStates = new();
    internal static TurnState State(Player player) => Turns.GetOrCreateValue(player);
    public static MoonEyeScythe? Relic(Player player) => player.GetRelic<MoonEyeScythe>();
    public static Form GetForm(Player player) => FormRuntime.CurrentForm(player);
    public static bool IsDragon(Player player) => GetForm(player) == Form.Dragon;
    public static bool ScythePlayedThisTurn(Player player) => State(player).ScythePlayed;
    public static bool LostLifeThisTurn(Player player) => State(player).LostLife;
    public static bool HealedThisTurn(Player player) => State(player).Healed;
    internal static bool HasInscription(Player p, int value) => Relic(p) is { } r && (r.FirstInscription == value || r.SecondInscription == value);
    internal static bool UseInscription(Player p, int value) => HasInscription(p, value) && State(p).UsedInscriptions.Add(value);
    internal static void ResetCombat(Player player) { Turns.Remove(player); FreezeStates.Remove(player.Creature); AbilityRuntime.ResetCombat(player); SpellRuntime.ResetCombat(player); }
    internal static async Task BeginTurn(Player p, PlayerChoiceContext context)
    {
        var s = State(p); s.ScythePlayed = s.LostLife = s.Healed = false; s.UsedInscriptions.Clear(); s.HitTargets.Clear();
        if (s.TemporaryThorns > 0) { await RemoveContribution<ThornsPower>(context, p, s.TemporaryThorns); s.TemporaryThorns = 0; }
        if (GetForm(p) == Form.Mage && p.PlayerCombatState is { } pcs) CustomResources<ManaResource>.Get(pcs).ModifyAmount(5);
        await AbilityRuntime.BeginTurn(context, p);
    }
    internal static async Task EndTurn(Player p, PlayerChoiceContext context)
    {
        await AbilityRuntime.EndTurn(context, p);
        var s = State(p);
        await RemoveContribution<DexterityPower>(context, p, s.TemporaryDexterity); s.TemporaryDexterity = 0;
        await RemoveContribution<InvigorationPower>(context, p, s.TemporaryInvigoration); s.TemporaryInvigoration = 0;
    }
    private static async Task RemoveContribution<T>(PlayerChoiceContext ctx, Player p, int amount) where T : PowerModel
    {
        if (amount <= 0) return;
        using var removal = DamagePatches.RawPowerAdjustment<T>(p.Creature);
        await PowerCmd.Apply<T>(ctx, p.Creature, -amount, null, null);
    }
    public static async Task GainMana(Player p, int amount)
    {
        if (p.PlayerCombatState is not { } pcs || IsDragon(p) || amount <= 0) return;
        var mana = CustomResources<ManaResource>.Get(pcs); int old = mana.Amount; mana.ModifyAmount(amount);
        if (mana.Amount > old && UseInscription(p, 4)) await CardPileCmd.Draw(new BlockingPlayerChoiceContext(), 1, p);
    }
    public static Task GainRage(Player p, int amount)
    {
        if (p.PlayerCombatState is { } pcs && IsDragon(p) && amount > 0) CustomResources<RageResource>.Get(pcs).ModifyAmount(amount);
        return Task.CompletedTask;
    }
    public static Task GainActiveResource(Player p, int amount) => IsDragon(p) ? GainRage(p, amount) : GainMana(p, amount);
    public static async Task SetDistance(Player p, int value, PlayerChoiceContext? context = null, CardModel? card = null)
    {
        context ??= new BlockingPlayerChoiceContext();
        int old = p.Creature.GetPowerAmount<DistancePower>();
        if (value == old) return;
        using var movement = DamagePatches.RawPowerAdjustment<DistancePower>(p.Creature);
        if (p.Creature.GetPower<DistancePower>() is { } power) await PowerCmd.ModifyAmount(context, power, value - old, p.Creature, card);
        else if (value != 0) await PowerCmd.Apply<DistancePower>(context, p.Creature, value, p.Creature, card);
        if (old <= 0 && value > 0 && UseInscription(p, 1)) await CreatureCmd.GainBlock(p.Creature, 4, ValueProp.Unpowered, null);
        await AbilityRuntime.AfterMovement(context, p, old, value, card);
    }
    public static async Task GainInvigoration(PlayerChoiceContext ctx, Player p, int amount, bool temporary, CardModel card)
    {
        int before = p.Creature.GetPowerAmount<InvigorationPower>();
        await PowerCmd.Apply<InvigorationPower>(ctx, p.Creature, amount, p.Creature, card);
        if (temporary) State(p).TemporaryInvigoration += Math.Max(0, p.Creature.GetPowerAmount<InvigorationPower>() - before);
    }
    public static async Task GainTemporaryDexterity(PlayerChoiceContext ctx, Player p, int amount, CardModel card)
    {
        int before = p.Creature.GetPowerAmount<DexterityPower>();
        await PowerCmd.Apply<DexterityPower>(ctx, p.Creature, amount, p.Creature, card);
        State(p).TemporaryDexterity += Math.Max(0, p.Creature.GetPowerAmount<DexterityPower>() - before);
    }
    public static async Task GainTemporaryThorns(PlayerChoiceContext ctx, Player p, int amount, CardModel card)
    {
        int before = p.Creature.GetPowerAmount<ThornsPower>();
        await PowerCmd.Apply<ThornsPower>(ctx, p.Creature, amount, p.Creature, card);
        State(p).TemporaryThorns += Math.Max(0, p.Creature.GetPowerAmount<ThornsPower>() - before);
    }
    public static async Task ApplyFrost(PlayerChoiceContext ctx, Creature target, int amount, Creature? source, CardModel? card)
    {
        if (!target.IsAlive || amount <= 0) return;
        int previous = target.GetPowerAmount<FrostPower>();
        await PowerCmd.Apply<FrostPower>(ctx, target, amount, source, card);
        if (target.GetPower<FrostPower>() is not { } frost || frost.Amount <= previous) return;
        AbilityRuntime.MarkDirectDebuff(card);
        var state = FreezeStates.GetOrCreateValue(target);
        if (frost.Amount < state.Threshold) return;
        int layers = frost.Amount;
        int loss = (int)Math.Min(target.CurrentHp, decimal.Floor(target.CurrentHp * (decimal)layers / 100));
        state.Threshold = Math.Min(int.MaxValue, state.Threshold * 2);
        using (DamagePatches.RawPowerAdjustment<FrostPower>(target))
            await PowerCmd.ModifyAmount(ctx, frost, layers / 2 - layers, null, card);
        if (loss > 0) await ExactDamage(ctx, target, loss, null, null, false);
        if (target.IsAlive)
        {
            using var frozenEvent = DamagePatches.RawPowerAdjustment<FrozenTurnPower>(target);
            await PowerCmd.Apply<FrozenTurnPower>(ctx, target, 1, null, card);
        }
        await AbilityRuntime.AfterEnemyFrozen(ctx, target, source);
    }
    public static async Task ApplyDebuff<T>(PlayerChoiceContext ctx, Creature target, int amount, Creature? source, CardModel? card) where T : PowerModel
    {
        if (!target.IsAlive || amount <= 0) return;
        int before = target.GetPowerAmount<T>();
        var power = await PowerCmd.Apply<T>(ctx, target, amount, source, card);
        if (power is not null && power.Amount > before && AbilityRuntime.IsSpecifiedDebuff(power))
            AbilityRuntime.MarkDirectDebuff(card);
    }
    public static async Task PayLife(PlayerChoiceContext ctx, Player p, int amount, CardModel card)
    {
        if (amount < 0 || p.Creature.CurrentHp <= amount) throw new InvalidOperationException("生命代价必须至少留下1生命。");
        if (amount > 0) await ExactDamage(ctx, p.Creature, amount, null, card, false);
    }
    public static async Task<IEnumerable<DamageResult>> Attack(PlayerChoiceContext context, CardModel card, Creature target, decimal baseDamage, bool magic = false, bool trueDamage = false)
    {
        if (!target.IsAlive || CombatManager.Instance.IsOverOrEnding) return [];
        RecordHit(card, target);
        if (trueDamage)
        {
            decimal outgoing = Hook.ModifyDamage(card.Owner.RunState, card.CombatState, null, card.Owner.Creature, baseDamage, ValueProp.Move, card, ModifyDamageHookType.All, CardPreviewMode.None, out _);
            using var exact = DamagePatches.Exact(target, card.Owner.Creature, card);
            var attack = DamageCmd.Attack(outgoing).FromCard(card).Targeting(target);
            AccessTools.PropertySetter(typeof(AttackCommand), nameof(AttackCommand.DamageProps)).Invoke(attack, [ValueProp.Move | ValueProp.Unblockable]);
            await attack.Execute(context);
            return attack.Results.SelectMany(x => x);
        }
        var ordinary = await DamageCmd.Attack(baseDamage).FromCard(card).Targeting(target).Execute(context);
        return ordinary.Results.SelectMany(x => x);
    }
    public static async Task AttackAll(PlayerChoiceContext ctx, CardModel card, decimal damage, bool magic = false, bool trueDamage = false)
        => await AttackAllResults(ctx, card, damage, magic, trueDamage);
    public static async Task<IEnumerable<DamageResult>> AttackAllResults(PlayerChoiceContext ctx, CardModel card, decimal damage, bool magic = false, bool trueDamage = false)
    {
        var results = new List<DamageResult>();
        if (CombatManager.Instance.IsOverOrEnding) return results;
        var targets = card.CombatState?.HittableEnemies.ToArray() ?? [];
        foreach (var target in targets) RecordHit(card, target);
        using var snapshot = DamagePatches.SnapshotSource(card);
        if (trueDamage)
        {
            decimal outgoing = Hook.ModifyDamage(card.Owner.RunState, card.CombatState, null, card.Owner.Creature, damage, ValueProp.Move, card, ModifyDamageHookType.All, CardPreviewMode.None, out _);
            foreach (var target in targets.Where(x => x.IsAlive)) results.AddRange(await ExactDamage(ctx, target, outgoing, card.Owner.Creature, card, true));
        }
        else if (card.CombatState is { } combat)
        {
            var attack = await DamageCmd.Attack(damage).FromCard(card).TargetingAllOpponents(combat).Execute(ctx);
            results.AddRange(attack.Results.SelectMany(x => x));
        }
        return results;
    }
    private static void RecordHit(CardModel card, Creature target)
    {
        if (card is not IvichCard { IsScythe: true } || card.Type != CardType.Attack) return;
        var s = State(card.Owner); if (!s.HitTargets.TryGetValue(card, out var targets)) s.HitTargets[card] = targets = []; targets.Add(target);
    }
    internal static async Task<IEnumerable<DamageResult>> ExactDamage(PlayerChoiceContext ctx, Creature target, decimal amount, Creature? dealer, CardModel? card, bool attack)
    {
        using var scope = DamagePatches.Exact(target, dealer, card);
        return await CreatureCmd.Damage(ctx, target, Math.Max(0, decimal.Floor(amount)), ValueProp.Unblockable | (attack ? ValueProp.Move : ValueProp.Unpowered), dealer, card);
    }
    internal static async Task AfterCompletedPlay(PlayerChoiceContext ctx, CardModel card)
    {
        if (card is not IvichCard { IsScythe: true } || card.Type != CardType.Attack || card.Owner.Creature.IsDead || CombatManager.Instance.IsOverOrEnding) return;
        var p = card.Owner; var s = State(p);
        if (s.HitTargets.Remove(card, out var hitTargets) && UseInscription(p, 3)) foreach (var target in hitTargets.Where(x => x.IsAlive)) await ApplyFrost(ctx, target, 2, p.Creature, card);
        if (UseInscription(p, 2) && p.PlayerCombatState is { } pcs)
        {
            var candidates = pcs.DiscardPile.Cards.Where(c => c is IvichCard { IsScythe: true }).ToArray();
            if (candidates.Length > 0)
            {
                var chosen = (await CardSelectCmd.FromSimpleGrid(ctx, candidates, p, new CardSelectorPrefs(new MegaCrit.Sts2.Core.Localization.LocString("cards", card.Id.Entry + ".title"), 1))).FirstOrDefault();
                if (chosen is not null) await CardPileCmd.Add(chosen, PileType.Hand);
            }
        }
    }
    internal static async Task AfterHeal(Player p, int actual, int overflow)
    {
        await AbilityRuntime.AfterHeal(p, actual, overflow);
        if (actual > 0) { State(p).Healed = true; if (UseInscription(p, 5)) await CreatureCmd.GainBlock(p.Creature, 3, ValueProp.Unpowered, null); }
        if (overflow > 0 && UseInscription(p, 6)) await CardPileCmd.Draw(new BlockingPlayerChoiceContext(), 1, p);
    }
}

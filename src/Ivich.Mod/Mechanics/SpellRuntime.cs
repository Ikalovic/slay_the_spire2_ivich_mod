using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using BaseLib.Abstracts;
using HarmonyLib;
using Ivich.Core;
using Ivich.Mod.Cards;
using Ivich.Mod.Powers;
using Ivich.Mod.Resources;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using DoomPower = Ivich.Mod.Powers.DoomPower;

namespace Ivich.Mod.Mechanics;

/// <summary>Paid values are immutable; execution-only values are captured when this particular effect starts.</summary>
public sealed class EffectExecutionSnapshot
{
    public required IvichCard SourceCard { get; init; }
    public required IReadOnlyDictionary<string, decimal> BaseValues { get; init; }
    public bool Upgraded { get; init; }
    public int PaidX { get; init; }
    public int PermanentMagicCount { get; init; }
    public int CombatGrowth { get; init; }
    public bool IsOriginalCast { get; init; }
    public int PositiveKinds { get; private set; }
    public int BlockAtStart { get; private set; }
    private readonly Dictionary<Creature, int> _negativeKinds = [];
    public int NegativeKinds(Creature target) => _negativeKinds.GetValueOrDefault(target);

    internal static EffectExecutionSnapshot Capture(IvichCard card, CardPlay play, Dictionary<string, decimal> values, int growth)
        => new()
        {
            SourceCard = card,
            Upgraded = card.IsUpgraded,
            BaseValues = new ReadOnlyDictionary<string, decimal>(values),
            PaidX = card.DesignId switch
            {
                "R13" => play.Resources.EnergySpent,
                "R14" => CustomResources<ManaResource>.AmountSpent(play),
                "R15" => CustomResources<RageResource>.AmountSpent(play),
                _ => 0
            },
            PermanentMagicCount = card.Owner.Deck.Cards.Count(c => c is IvichCard { IsMagic: true }),
            CombatGrowth = growth,
            IsOriginalCast = play.IsFirstInSeries
        };

    internal EffectExecutionSnapshot ForExecution(bool original) => new()
    {
        SourceCard = SourceCard,
        BaseValues = BaseValues,
        Upgraded = Upgraded,
        PaidX = PaidX,
        PermanentMagicCount = PermanentMagicCount,
        CombatGrowth = CombatGrowth,
        IsOriginalCast = original
    };

    internal void CaptureExecutionState(Player player)
    {
        var creature = player.Creature;
        PositiveKinds = new[] { creature.GetPowerAmount<StrengthPower>(), creature.GetPowerAmount<DexterityPower>(),
            creature.GetPowerAmount<InvigorationPower>(), creature.GetPowerAmount<ArtifactPower>(),
            creature.GetPowerAmount<RegenPower>(), creature.GetPowerAmount<ThornsPower>() }.Count(x => x > 0);
        BlockAtStart = creature.Block;
        _negativeKinds.Clear();
        foreach (var target in creature.CombatState?.Creatures ?? [])
            _negativeKinds[target] = new[] { target.GetPowerAmount<FrostPower>(), target.GetPowerAmount<DoomPower>(),
                target.GetPowerAmount<WeakPower>(), target.GetPowerAmount<VulnerablePower>(), target.GetPowerAmount<FrailPower>() }.Count(x => x > 0);
    }
}

public static class SpellRuntime
{
    private sealed record SpellRecord(IvichCard Card, CardPlay PaidPlay, EffectExecutionSnapshot Snapshot);
    private sealed class CombatSpells(Player owner)
    {
        public readonly AsyncSpellQueue<SpellRecord> Queue = new(record => Resolve(owner, record), () => CanStartEffect(owner));
        public PlayerChoiceContext? Context;
        public bool UpgradedReleaseUsed;
    }
    private sealed class DeferredState { public bool Deferred; }
    private static readonly ConditionalWeakTable<Player, CombatSpells> States = new();
    private static readonly ConditionalWeakTable<CardModel, DeferredState> Deferred = new();
    private static CombatSpells State(Player p) => States.GetValue(p, static owner => new(owner));
    private static bool CanStartEffect(Player p) => p.Creature.IsAlive && p.Creature.CombatState is not null && !CombatManager.Instance.IsOverOrEnding;
    public static bool WasEffectDeferred(CardModel card) => Deferred.TryGetValue(card, out var state) && state.Deferred;
    public static bool StorageInstalled(Player p) => States.TryGetValue(p, out var state) && state.Queue.StorageEnabled;
    public static int ReadyCount(Player p) => States.TryGetValue(p, out var state) ? state.Queue.Ready.Count : 0;
    public static int ChantCount(Player p) => States.TryGetValue(p, out var state) ? state.Queue.Charging.Count : 0;
    public static bool ShouldPlay(CardModel card)
    {
        if (card is not IvichCard ivich) return true;
        if (ivich.DesignId == "T03") return State(card.Owner).Queue.CanRelease;
        if (card.Owner.PlayerCombatState is not { } combat) return ivich.DesignId is not ("R14" or "R15");
        return ivich.DesignId switch
        {
            "R14" => CustomResources<ManaResource>.Get(combat).Amount >= 5,
            "R15" => CustomResources<RageResource>.Get(combat).Amount >= 1,
            _ => true
        };
    }

    public static void ResetCombat(Player player)
    {
        if (States.TryGetValue(player, out var old)) old.Queue.EndCombat();
        States.Remove(player);
    }

    internal static async Task RecordPlay(PlayerChoiceContext context, IvichCard card, CardPlay play, EffectExecutionSnapshot snapshot)
    {
        int chant = card.DesignId is "U17" or "R01" or "R03" or "R12" or "R14" ? 1 : 0;
        bool defer = card.IsMagic && (chant > 0 || StorageInstalled(card.Owner));
        var deferred = Deferred.GetOrCreateValue(card);
        deferred.Deferred = play.IsFirstInSeries ? defer : deferred.Deferred && defer;
        if (!defer)
        {
            await card.ExecuteRecordedEffect(context, play, snapshot);
            return;
        }
        // This clone is an execution model only: never register it with CardScope or a combat pile.
        var frozenCard = (IvichCard)card.ClonePreservingMutability();
        frozenCard.RecordedSnapshot = snapshot;
        foreach (var (name, value) in snapshot.BaseValues) frozenCard.DynamicVars[name].BaseValue = value;
        var record = new SpellRecord(frozenCard, play, snapshot);
        var state = State(card.Owner);
        var previous = state.Context; state.Context = context;
        try { await state.Queue.Submit(record, chant, card.Owner.PlayerCombatState?.TurnNumber); }
        finally { state.Context = previous; }
        await RefreshCounters(context, card.Owner, state);
    }

    public static async Task BeginTurn(PlayerChoiceContext context, Player player)
    {
        var state = State(player); var previous = state.Context; state.Context = context;
        try { await state.Queue.BeginTurn(Math.Max(1, player.PlayerCombatState?.TurnNumber ?? 1)); }
        finally { state.Context = previous; }
        if (state.Queue.StorageEnabled && CanStartEffect(player)) await EnsureControlCard(player);
        await RefreshCounters(context, player, state);
    }

    public static async Task InstallStorage(PlayerChoiceContext context, Player player, CardModel source)
    {
        var state = State(player);
        bool first = state.Queue.InstallStorage();
        if (first)
        {
            await PowerCmd.Apply<SpellStoragePower>(context, player.Creature, 1, player.Creature, source);
            await EnsureControlCard(player);
        }
    }

    public static async Task Release(PlayerChoiceContext context, IvichCard source)
    {
        var player = source.Owner; var state = State(player);
        if (!state.Queue.CanRelease) return;
        if (source.IsUpgraded && !state.UpgradedReleaseUsed)
        {
            state.UpgradedReleaseUsed = true;
            await CreatureCmd.GainBlock(player.Creature, 6, ValueProp.Move, null);
        }
        var previous = state.Context; state.Context = context;
        try { await state.Queue.Release(); }
        finally { state.Context = previous; }
        await RefreshCounters(context, player, state);
    }

    public static async Task CompleteChant(PlayerChoiceContext context, Player player)
    {
        var state = State(player); var entries = state.Queue.Charging;
        if (entries.Count == 0 || !CanStartEffect(player)) return;
        var candidates = entries.Select(item => (CardModel)item.Effect.Card).ToArray();
        var picked = (await CardSelectCmd.FromSimpleGrid(context, candidates, player,
            new CardSelectorPrefs(new LocString("cards", ModelDb.Card<SpellTargetChoice>().Id.Entry + ".chantPrompt"), 0, 1)
            { Cancelable = true })).FirstOrDefault();
        if (picked is null) return;
        var entry = entries.First(item => item.Effect.Card == picked);
        var previous = state.Context; state.Context = context;
        try { await state.Queue.CompleteChant(entry.Sequence); }
        finally { state.Context = previous; }
        await RefreshCounters(context, player, state);
    }

    private static async Task Resolve(Player player, SpellRecord record)
    {
        if (!CanStartEffect(player)) return;
        var card = record.Card;
        var context = State(player).Context ?? new BlockingPlayerChoiceContext();
        Creature? target = record.PaidPlay.Target;
        if (card.TargetType == TargetType.AnyEnemy)
        {
            var candidates = player.Creature.CombatState!.HittableEnemies.Where(card.IsValidTarget).ToArray();
            target = candidates.Length == 0 ? null : await SelectTarget(context, player, candidates);
            if (target is { IsAlive: false }) target = null;
        }
        else if (card.TargetType == TargetType.Self) target = player.Creature;
        if (!CanStartEffect(player)) return;
        var play = new CardPlay
        {
            Card = card,
            Target = target,
            ResultPile = PileType.None,
            Resources = record.PaidPlay.Resources,
            IsAutoPlay = record.PaidPlay.IsAutoPlay,
            PlayIndex = record.PaidPlay.PlayIndex,
            PlayCount = record.PaidPlay.PlayCount
        };
        AccessTools.PropertySetter(typeof(CardModel), nameof(CardModel.CurrentTarget)).Invoke(card, [target]);
        AccessTools.PropertySetter(typeof(CardModel), nameof(CardModel.CurrentPlayIndex)).Invoke(card, [play.PlayIndex]);
        CombatManager.Instance.BeginCardOrPotionEffect(player);
        try
        {
            await card.ExecuteRecordedEffect(context, play, record.Snapshot);
            await IvichRuntime.AfterCompletedPlay(context, card);
        }
        finally
        {
            CombatManager.Instance.EndCardOrPotionEffect(player);
            AccessTools.PropertySetter(typeof(CardModel), nameof(CardModel.CurrentTarget)).Invoke(card, [null]);
        }
        if (CanStartEffect(player)) await CombatManager.Instance.CheckForEmptyHand(context, player);
    }

    private static async Task<Creature?> SelectTarget(PlayerChoiceContext context, Player player, Creature[] targets)
    {
        if (targets.Length == 1) return targets[0];
        var choices = targets.Select((target, index) => SpellTargetChoice.ForTarget(player, target, index + 1)).ToArray();
        var selected = (await CardSelectCmd.FromSimpleGrid(context, choices, player,
            new CardSelectorPrefs(new LocString("cards", ModelDb.Card<SpellTargetChoice>().Id.Entry + ".targetPrompt"), 1))).FirstOrDefault();
        return (selected as SpellTargetChoice)?.Target;
    }

    private static async Task EnsureControlCard(Player player)
    {
        if (player.PlayerCombatState is not { } combat || combat.Hand.Cards.Any(c => c is T03SpellRelease)) return;
        var existing = combat.AllPiles.SelectMany(p => p.Cards).OfType<T03SpellRelease>().FirstOrDefault();
        if (existing is not null) await CardPileCmd.Add(existing, PileType.Hand);
        else if (player.Creature.CombatState is { } state)
            await CardPileCmd.AddGeneratedCardToCombat(state.CreateCard<T03SpellRelease>(player), PileType.Hand, player);
    }

    private static async Task RefreshCounters(PlayerChoiceContext context, Player p, CombatSpells state)
    {
        if (!CanStartEffect(p)) return;
        await SetCounter<ChantingSpellsPower>(context, p, state.Queue.Charging.Count);
        await SetCounter<StoredSpellsPower>(context, p, state.Queue.Ready.Count);
    }
    private static async Task SetCounter<T>(PlayerChoiceContext context, Player p, int amount) where T : PowerModel
    {
        var power = p.Creature.GetPower<T>();
        if (amount == 0) { if (power is not null) await PowerCmd.Remove(power); return; }
        if (power is null) await PowerCmd.Apply<T>(context, p.Creature, amount, null, null);
        else if (power.Amount != amount) await PowerCmd.ModifyAmount(context, power, amount - power.Amount, null, null);
    }

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.IsUpgraded), MethodType.Getter)]
    private static class RecordedUpgrade
    {
        private static void Postfix(CardModel __instance, ref bool __result)
        {
            if (__instance is IvichCard card && (card.ActiveSnapshot ?? card.RecordedSnapshot) is { } snapshot)
                __result = snapshot.Upgraded;
        }
    }

    // Detached execution models intentionally have no pile. Commands must still see their real combat.
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.CombatState), MethodType.Getter)]
    private static class RecordedCardCombatState
    {
        private static void Postfix(CardModel __instance, ref ICombatState? __result)
        {
            if (__result is null && __instance is IvichCard { ActiveSnapshot: not null })
                __result = __instance.Owner.Creature.CombatState;
        }
    }
}

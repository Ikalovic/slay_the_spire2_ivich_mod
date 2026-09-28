using System.Threading;
using HarmonyLib;
using Ivich.Mod.Cards;
using Ivich.Mod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
namespace Ivich.Mod.Mechanics;

internal static class DamagePatches
{
    private record ExactRequest(Creature Target, Creature? Dealer, CardModel? Card);
    internal record SourceSnapshot(CardModel Card, int Strength, int Distance, int Invigoration, decimal WeakMultiplier);
    private static readonly AsyncLocal<ExactRequest?> CurrentExact = new();
    private static readonly AsyncLocal<(Creature target, Type type)?> CurrentPowerAdjustment = new();
    internal static IDisposable RawPowerAdjustment<T>(Creature target) where T : PowerModel
    {
        var before = CurrentPowerAdjustment.Value; CurrentPowerAdjustment.Value = (target, typeof(T));
        return new Restore(() => CurrentPowerAdjustment.Value = before);
    }
    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyPowerAmountReceived))]
    private static class ExactPowerAmount
    {
        [HarmonyPrefix]
        private static bool Prefix(PowerModel canonicalPower, Creature target, decimal amount, ref decimal __result, ref IEnumerable<AbstractModel> modifiers)
        {
            if (CurrentPowerAdjustment.Value is not { } p || p.target != target || p.type != canonicalPower.GetType()) return true;
            __result = amount; modifiers = []; return false;
        }
    }
    internal static readonly AsyncLocal<SourceSnapshot?> CurrentSnapshot = new();
    private sealed class Restore(Action restore) : IDisposable { public void Dispose() => restore(); }
    internal static IDisposable Exact(Creature target, Creature? dealer, CardModel? card)
    {
        var before = CurrentExact.Value; CurrentExact.Value = new(target, dealer, card); return new Restore(() => CurrentExact.Value = before);
    }
    internal static IDisposable SnapshotSource(CardModel card)
    {
        var before = CurrentSnapshot.Value; var c = card.Owner.Creature;
        CurrentSnapshot.Value = new(card, c.GetPowerAmount<StrengthPower>(), c.GetPowerAmount<DistancePower>(), c.GetPowerAmount<InvigorationPower>(), c.GetPower<WeakPower>()?.ModifyDamageMultiplicative(null, 0, MegaCrit.Sts2.Core.ValueProps.ValueProp.Move, c, card) ?? 1m);
        return new Restore(() => CurrentSnapshot.Value = before);
    }
    private static bool IsExact(Creature? target, Creature? dealer, CardModel? card) => CurrentExact.Value is { } r && r.Target == target && r.Dealer == dealer && r.Card == card;

    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyDamage))]
    private static class ExactDamageCalculation
    {
        [HarmonyPrefix]
        private static bool Prefix(Creature? target, Creature? dealer, decimal damage, CardModel? cardSource, ref decimal __result, ref IEnumerable<AbstractModel> modifiers)
        {
            if (!IsExact(target, dealer, cardSource)) return true;
            __result = damage; modifiers = []; return false;
        }
    }
    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyHpLost))]
    private static class ExactHealthCalculation
    {
        [HarmonyPrefix]
        private static bool Prefix(Creature target, Creature? dealer, decimal amount, CardModel? cardSource, ref decimal __result, ref IEnumerable<AbstractModel> modifiers)
        {
            if (!IsExact(target, dealer, cardSource)) return true;
            __result = amount; modifiers = []; return false;
        }
    }
    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyUnblockedDamageTarget))]
    private static class ExactTarget
    {
        [HarmonyPrefix]
        private static bool Prefix(Creature originalTarget, Creature? dealer, ref Creature __result)
        {
            if (CurrentExact.Value is not { } r || r.Target != originalTarget || r.Dealer != dealer) return true;
            __result = originalTarget; return false;
        }
    }
    [HarmonyPatch(typeof(StrengthPower), nameof(StrengthPower.ModifyDamageAdditive))]
    private static class MagicIgnoresStrength
    {
        [HarmonyPostfix]
        private static void Postfix(StrengthPower __instance, Creature? dealer, CardModel? cardSource, ref decimal __result)
        {
            if (dealer != __instance.Owner || cardSource is not IvichCard card) return;
            if (card.IsMagic) __result = 0;
            else if (CurrentSnapshot.Value is { } s && s.Card == cardSource) __result = s.Strength;
        }
    }
    [HarmonyPatch(typeof(WeakPower), nameof(WeakPower.ModifyDamageMultiplicative))]
    private static class MagicIgnoresWeak
    {
        [HarmonyPostfix]
        private static void Postfix(WeakPower __instance, Creature? dealer, CardModel? cardSource, ref decimal __result)
        {
            if (dealer != __instance.Owner || cardSource is not IvichCard card) return;
            if (card.IsMagic) __result = 1;
            else if (CurrentSnapshot.Value is { } snapshot && snapshot.Card == cardSource) __result = snapshot.WeakMultiplier;
        }
    }
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
    private static class CompletedCardEffects
    {
        [HarmonyPostfix]
        private static void Postfix(CardModel __instance, PlayerChoiceContext choiceContext, ref Task __result)
        {
            if (__instance is not IvichCard) return;
            __result = Complete(__result, __instance, choiceContext);
        }
        private static async Task Complete(Task original, CardModel card, PlayerChoiceContext context)
        {
            await original;
            if (card.CombatState is not null && !SpellRuntime.WasEffectDeferred(card)) await IvichRuntime.AfterCompletedPlay(context, card);
        }
    }
    private sealed class HealMeasurement(Creature creature, HealMeasurement? parent)
    {
        public Creature Creature { get; } = creature;
        public HealMeasurement? Parent { get; } = parent;
        public int Actual, Overflow;
    }
    private static readonly AsyncLocal<HealMeasurement?> CurrentHealing = new();
    [HarmonyPatch(typeof(Creature), nameof(Creature.HealInternal))]
    private static class EffectiveHealingMeasurement
    {
        [HarmonyPrefix]
        private static void Prefix(Creature __instance, out int __state) => __state = __instance.CurrentHp;
        [HarmonyPostfix]
        private static void Postfix(Creature __instance, decimal amount, int __state)
        {
            if (CurrentHealing.Value is not { } measurement || measurement.Creature != __instance) return;
            int actual = Math.Max(0, __instance.CurrentHp - __state);
            measurement.Actual += actual;
            if (actual > 0 && __instance.CombatState is not null && __instance.Player is { } player && IvichRuntime.Relic(player) is not null)
                IvichRuntime.State(player).Healed = true;
            measurement.Overflow += Math.Max(0, (int)amount - actual);
        }
    }
    [HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Heal))]
    private static class HealingAccounting
    {
        [HarmonyPrefix]
        private static void Prefix(Creature creature, out HealMeasurement __state)
        {
            __state = new HealMeasurement(creature, CurrentHealing.Value);
            CurrentHealing.Value = __state;
        }
        [HarmonyPostfix]
        private static void Postfix(Creature creature, HealMeasurement __state, ref Task __result)
        {
            // The async Heal body captured this measurement; restore the caller immediately so
            // independent or nested healing commands each retain their own measurement.
            CurrentHealing.Value = __state.Parent;
            if (creature.Player is not { } p || IvichRuntime.Relic(p) is null || creature.CombatState is null) return;
            __result = Finish(__result, p, __state);
        }
        private static async Task Finish(Task original, MegaCrit.Sts2.Core.Entities.Players.Player p, HealMeasurement measurement)
        { await original; await IvichRuntime.AfterHeal(p, measurement.Actual, measurement.Overflow); }
    }
}

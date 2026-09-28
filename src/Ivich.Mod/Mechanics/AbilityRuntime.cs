using System.Runtime.CompilerServices;
using System.Threading;
using Ivich.Core;
using Ivich.Mod.Cards;
using Ivich.Mod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using DoomPower = Ivich.Mod.Powers.DoomPower;

namespace Ivich.Mod.Mechanics;

public static class AbilityRuntime
{
    private sealed class AbilityState { public readonly TurnTriggerLedger Triggers = new(); public int TemporaryStrength; }
    private sealed class Execution(IvichCard card) { public IvichCard Card { get; } = card; public bool DirectDebuff; }
    private sealed class Restore(Action action) : IDisposable { public void Dispose() => action(); }
    private static readonly ConditionalWeakTable<Player, AbilityState> States = new();
    private static readonly AsyncLocal<Execution?> CurrentEffect = new();
    private static readonly AsyncLocal<int> PassiveDepth = new();
    private static AbilityState State(Player p) => States.GetOrCreateValue(p);
    public static void ResetCombat(Player p) => States.Remove(p);
    public static IDisposable BeginEffect(IvichCard card)
    {
        var old = CurrentEffect.Value;
        int oldDepth = PassiveDepth.Value;
        CurrentEffect.Value = new Execution(card);
        PassiveDepth.Value = 0;
        return new Restore(() => { CurrentEffect.Value = old; PassiveDepth.Value = oldDepth; });
    }
    public static IDisposable PassiveScope()
    {
        int old = PassiveDepth.Value; PassiveDepth.Value = old + 1;
        return new Restore(() => PassiveDepth.Value = old);
    }
    public static void MarkDirectDebuff(CardModel? card)
    {
        if (PassiveDepth.Value == 0 && CurrentEffect.Value is { } effect && effect.Card == card)
            effect.DirectDebuff = true;
    }
    public static bool IsSpecifiedDebuff(PowerModel power)
        => power is FrostPower or DoomPower or WeakPower or VulnerablePower or FrailPower;

    public static Task Install(PlayerChoiceContext ctx, Player p, string id, bool upgraded, CardModel card) => id switch
    {
        "U03" => Add<ScaleCoagulationPower>(ctx, p, upgraded, card),
        "U04" => Add<BloodForgePower>(ctx, p, upgraded, card),
        "U05" => Add<ColdBloodPower>(ctx, p, upgraded, card),
        "U07" => Add<HungerPower>(ctx, p, upgraded, card),
        "U08" => Add<FrostKnowledgePower>(ctx, p, upgraded, card),
        "U10" => Add<IceMirrorPower>(ctx, p, upgraded, card),
        "U11" => Add<ColdWavePower>(ctx, p, upgraded, card),
        "U16" => Add<DistanceReasonPower>(ctx, p, upgraded, card),
        "U19" => Add<StarCurrentPower>(ctx, p, upgraded, card),
        "U24" => Add<SpellbladeResonancePower>(ctx, p, upgraded, card),
        "U25" => Add<CurseEchoPower>(ctx, p, upgraded, card),
        "U26" => Add<ManyFormsPower>(ctx, p, upgraded, card),
        "R04" => Add<EternalWinterPower>(ctx, p, upgraded, card),
        "R11" => Add<DoomCurtainPower>(ctx, p, upgraded, card),
        _ => throw new ArgumentException($"Unknown ability {id}.", nameof(id))
    };
    private static async Task Add<T>(PlayerChoiceContext ctx, Player p, bool upgraded, CardModel card) where T : AdvancedAbilityPower
    {
        var power = await PowerCmd.Apply<T>(ctx, p.Creature, 1, p.Creature, card);
        if (upgraded && power is not null) power.UpgradedInstallations++;
    }
    public static Task BeginTurn(PlayerChoiceContext ctx, Player p)
    {
        State(p).Triggers.BeginTurn();
        return Task.CompletedTask;
    }
    public static async Task AfterTurnStart(PlayerChoiceContext ctx, Player p)
    {
        if (p.Creature.GetPower<IceMirrorPower>() is { } mirror)
            await CreatureCmd.GainBlock(p.Creature, p.Creature.GetPowerAmount<DistancePower>() > 0 ? mirror.Benefit(6, 8) : mirror.Benefit(3, 4), ValueProp.Unpowered, null);
    }
    public static async Task EndTurn(PlayerChoiceContext ctx, Player p)
    {
        using var passive = PassiveScope();
        if (p.Creature.GetPower<EternalWinterPower>() is { } winter)
            foreach (var enemy in Enemies(p)) await IvichRuntime.ApplyFrost(ctx, enemy, winter.Benefit(3, 5), p.Creature, null);
        var state = State(p);
        if (state.TemporaryStrength > 0)
        {
            using var adjustment = DamagePatches.RawPowerAdjustment<StrengthPower>(p.Creature);
            await PowerCmd.Apply<StrengthPower>(ctx, p.Creature, -state.TemporaryStrength, null, null);
            state.TemporaryStrength = 0;
        }
    }
    public static async Task GainTemporaryStrength(PlayerChoiceContext ctx, Player p, int amount, CardModel? card)
    {
        int before = p.Creature.GetPowerAmount<StrengthPower>();
        await PowerCmd.Apply<StrengthPower>(ctx, p.Creature, amount, p.Creature, card);
        State(p).TemporaryStrength += Math.Max(0, p.Creature.GetPowerAmount<StrengthPower>() - before);
    }
    public static async Task AfterMovement(PlayerChoiceContext ctx, Player p, int old, int current, CardModel? card)
    {
        if (p.Creature.GetPower<DistanceReasonPower>() is not { } reason) return;
        using var passive = PassiveScope();
        if (old >= 0 && current < 0) await GainTemporaryStrength(ctx, p, reason.Benefit(1, 2), card);
        if (old <= 0 && current > 0) await CreatureCmd.GainBlock(p.Creature, reason.Benefit(4, 6), ValueProp.Unpowered, null);
    }
    public static async Task AfterHeal(Player p, int actual, int overflow)
    {
        using var passive = PassiveScope();
        if (actual > 0 && p.Creature.GetPower<ScaleCoagulationPower>() is { } scales)
            await CreatureCmd.GainBlock(p.Creature, actual * scales.Amount, ValueProp.Unpowered, null);
        if (overflow > 0 && p.Creature.GetPower<BloodForgePower>() is { } forge)
            await GainTemporaryStrength(new BlockingPlayerChoiceContext(), p, AbilityRules.BloodForgeStrength(overflow, forge.Amount), null);
    }
    public static async Task AfterLostLife(PlayerChoiceContext ctx, Player p)
    {
        if (p.Creature.GetPower<ColdBloodPower>() is not { } blood) return;
        using var passive = PassiveScope();
        foreach (var enemy in Enemies(p)) await IvichRuntime.ApplyFrost(ctx, enemy, blood.Benefit(2, 3), p.Creature, null);
    }
    public static async Task AfterEnemyFrozen(PlayerChoiceContext ctx, Creature target, Creature? source)
    {
        if (target.IsPlayer || source?.Player is not { } p) return;
        using var passive = PassiveScope();
        if (p.Creature.GetPower<FrostKnowledgePower>() is { } knowledge)
            await PowerCmd.Apply<InvigorationPower>(ctx, p.Creature, knowledge.Amount, p.Creature, null);
        if (p.Creature.GetPower<EternalWinterPower>() is { } winter)
            await IvichRuntime.GainMana(p, 2 * winter.Amount);
    }
    public static async Task AfterExhaust(PlayerChoiceContext ctx, Player p, CardModel card)
    {
        if (card.Owner != p || p.Creature.IsDead) return;
        using var passive = PassiveScope();
        if (p.Creature.GetPower<HungerPower>() is { } hunger) await IvichRuntime.GainRage(p, hunger.Amount);
        if (p.Creature.GetPower<DoomCurtainPower>() is { } curtain)
        {
            foreach (var enemy in Enemies(p)) await IvichRuntime.ApplyDebuff<DoomPower>(ctx, enemy, curtain.Benefit(1, 2), p.Creature, card);
            await CreatureCmd.GainBlock(p.Creature, curtain.Benefit(2, 3), ValueProp.Unpowered, null);
        }
    }
    public static async Task OnPaidPlay(PlayerChoiceContext ctx, IvichCard card, int mana, int rage)
    {
        if (card.Owner.Creature.GetPower<SpellbladeResonancePower>() is not { } resonance) return;
        using var passive = PassiveScope();
        if (mana > 0) await GainTemporaryStrength(ctx, card.Owner, resonance.Amount, card);
        if (rage > 0 && card.NativeRageCost != 0)
            await IvichRuntime.GainInvigoration(ctx, card.Owner, resonance.Amount, true, card);
    }
    public static async Task AfterPositiveIncrease(PlayerChoiceContext ctx, Player p, PowerModel power, decimal amount)
    {
        if (power.Owner != p.Creature || amount <= 0 || power.Amount <= 0 ||
            power is not (StrengthPower or DexterityPower or InvigorationPower or ArtifactPower or RegenPower or ThornsPower)) return;
        var protection = p.Creature.GetPower<ManyFormsPower>();
        if (!State(p).Triggers.TryManyForms(power.GetType().Name, protection is not null)) return;
        using var passive = PassiveScope();
        await CardPileCmd.Draw(ctx, protection!.Amount, p);
        await CreatureCmd.GainBlock(p.Creature, protection.Benefit(3, 5), ValueProp.Unpowered, null);
    }
    public static async Task OnEffectCompleted(PlayerChoiceContext ctx, IvichCard card)
    {
        var p = card.Owner;
        if (p.Creature.IsDead || CombatManager.Instance.IsOverOrEnding) return;
        bool directDebuff = CurrentEffect.Value is { DirectDebuff: true } effect && effect.Card == card;
        using var passive = PassiveScope();
        if (card.IsScythe && card.Type == CardType.Attack && p.Creature.GetPower<BladeWardPower>() is { } ward)
            await CreatureCmd.GainBlock(p.Creature, ward.Amount, ValueProp.Unpowered, null);
        if (card.IsIce && card.Type is CardType.Attack or CardType.Skill && p.Creature.GetPower<ColdWavePower>() is { } wave)
            foreach (var enemy in Enemies(p)) await IvichRuntime.ApplyFrost(ctx, enemy, wave.Benefit(1, 2), p.Creature, null);
        if (card.IsMagic && card.Type == CardType.Attack)
        {
            var current = p.Creature.GetPower<StarCurrentPower>();
            if (State(p).Triggers.TryStarCurrent(current is not null)) await CardPileCmd.Draw(ctx, 2 * current!.Amount, p);
        }
        if (directDebuff && p.Creature.GetPower<CurseEchoPower>() is { } echo &&
            State(p).Triggers.TryCurseEcho(echo.UpgradedInstallations > 0 ? 3 : 2))
        {
            await CardPileCmd.Draw(ctx, echo.Amount, p);
            await IvichRuntime.GainMana(p, echo.Amount);
        }
    }
    private static IEnumerable<Creature> Enemies(Player p) => p.Creature.CombatState?.HittableEnemies.ToArray() ?? [];
}

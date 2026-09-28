using BaseLib.Abstracts;
using BaseLib.Utils;
using Ivich.Core;
using Ivich.Mod.Assets;
using Ivich.Mod.Character;
using Ivich.Mod.Mechanics;
using Ivich.Mod.Powers;
using Ivich.Mod.Resources;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Ivich.Mod.Cards;

/// <summary>Common display, form requirements and commands for implemented Ivich cards.</summary>
[Pool(typeof(IvichCardPool))]
public abstract class IvichCard : CustomCardModel
{
    private readonly CardDefinition _definition;
    public string DesignId => _definition.Id;
    public bool IsMagic => _definition.Magic;
    public bool IsScythe => _definition.Scythe;
    public bool IsIce => _definition.Ice;
    public int NativeRageCost => _definition.Rage;
    public int LifeCost => _definition.Life;
    private EffectExecutionSnapshot? _seriesSnapshot;
    internal EffectExecutionSnapshot? ActiveSnapshot { get; private set; }
    internal EffectExecutionSnapshot? RecordedSnapshot { get; set; }
    protected internal bool IsRecordedEffect => RecordedSnapshot is not null;
    protected EffectExecutionSnapshot Snapshot => ActiveSnapshot ?? RecordedSnapshot ?? throw new InvalidOperationException("No card effect is executing.");
    protected virtual int CombatGrowthCount => 0;
    protected virtual void OnRealPlayRecorded(CardPlay play) { }
    protected virtual decimal CaptureBaseValue(string name, decimal value) => value;
    protected override bool HasEnergyCostX => DesignId == "R13";

    protected IvichCard(string id) : this(CardDefinitions.All[id]) { }

    private IvichCard(CardDefinition definition)
        : base(definition.Energy, definition.Type, definition.Rarity, definition.Target)
    {
        _definition = definition;
        if (definition.Mana < 0) CustomResources<ManaResource>.SetXCost(this);
        else if (definition.Mana > 0) CustomResources<ManaResource>.SetCanonicalCost(this, definition.Mana);
        if (definition.Rage < 0) CustomResources<RageResource>.SetXCost(this);
        else if (definition.Rage > 0) CustomResources<RageResource>.SetCanonicalCost(this, definition.Rage);
    }

    // Borrow native portraits until approved art is imported. The card's identity and text remain Ivich's.
    private string FallbackPortraitPath => _definition.Type == CardType.Attack
        ? ModelDb.Card<StrikeIronclad>().PortraitPath : ModelDb.Card<DefendIronclad>().PortraitPath;
    public override string PortraitPath => ArtPaths.CardPortrait(DesignId, false, FallbackPortraitPath);
    public override string? CustomPortraitPath => ArtPaths.CardPortrait(DesignId, true, FallbackPortraitPath);
    public override string BetaPortraitPath => PortraitPath;
    public override bool CanBeGeneratedInCombat => _definition.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare;
    public override List<(string, string)> Localization => new CardLoc(_definition.Name, _definition.Description);
    public override IEnumerable<CardKeyword> CanonicalKeywords
    {
        get
        {
            if (_definition.Exhaust) yield return CardKeyword.Exhaust;
            if (DesignId is "U14" or "U17" or "T02" or "T03" or "A01") yield return CardKeyword.Retain;
            if (DesignId == "A01") yield return CardKeyword.Innate;
        }
    }
    protected override HashSet<CardTag> CanonicalTags => DesignId switch
    {
        "B01" => [CardTag.Strike],
        "B02" => [CardTag.Defend],
        _ => []
    };

    protected override IEnumerable<DynamicVar> CanonicalVars => _definition.Variables.Select(v => v.Name switch
    {
        "Damage" when DesignId is "C04" or "C11" or "C19" or "U06" or "U09" or "U17" or "U27" or "U28" or "R01" or "R05" or "R06" or "R09" or "R13" or "R14" => new IvichDamageVar(v.Base, DesignId is "C04" or "C19" or "U06" or "U09" or "U17" or "U28" or "R01" or "R05" or "R09" or "R13" or "R14"),
        "Damage" => (DynamicVar)new DamageVar(v.Base, ValueProp.Move),
        "Block" => new BlockVar(v.Base, ValueProp.Move),
        _ => new DynamicVar(v.Name, v.Base)
    });

    protected override bool IsPlayable =>
        (!IsMagic || IvichRuntime.GetForm(Owner) != Form.Dragon) &&
        (NativeRageCost == 0 || IvichRuntime.GetForm(Owner) == Form.Dragon) &&
        Owner.Creature.CurrentHp > LifeCost && SpellRuntime.ShouldPlay(this) &&
        (!_definition.NeedsOtherCard || PileType.Hand.GetPile(Owner).Cards.Any(card => card != this));

    protected sealed override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        // A native Replay repeats an effect, so its life cost is only paid on the first play.
        if (LifeCost > 0 && play.IsFirstInSeries)
            await IvichRuntime.PayLife(context, Owner, LifeCost, this);
        if (play.IsFirstInSeries || _seriesSnapshot is null)
        {
            var values = _definition.Variables.ToDictionary(v => v.Name,
                v => CaptureBaseValue(v.Name, DynamicVars[v.Name].BaseValue));
            _seriesSnapshot = EffectExecutionSnapshot.Capture(this, play, values, CombatGrowthCount);
            if (play.IsFirstInSeries) OnRealPlayRecorded(play);
        }
        var snapshot = _seriesSnapshot.ForExecution(play.IsFirstInSeries);
        await SpellRuntime.RecordPlay(context, this, play, snapshot);
    }

    internal async Task ExecuteRecordedEffect(PlayerChoiceContext context, CardPlay play, EffectExecutionSnapshot snapshot)
    {
        var old = ActiveSnapshot; ActiveSnapshot = snapshot;
        snapshot.CaptureExecutionState(Owner);
        using var effect = AbilityRuntime.BeginEffect(this);
        try
        {
            await PlayEffects(context, play);
            await AbilityRuntime.OnEffectCompleted(context, this);
        }
        finally { ActiveSnapshot = old; }
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _seriesSnapshot = null;
        ActiveSnapshot = null;
        RecordedSnapshot = null;
    }

    protected abstract Task PlayEffects(PlayerChoiceContext context, CardPlay play);

    protected override void OnUpgrade()
    {
        if (DesignId is "U19" or "R02") EnergyCost.UpgradeBy(-1);
        foreach (var value in _definition.Variables)
            if (value.Upgrade != value.Base)
                DynamicVars[value.Name].UpgradeValueBy(value.Upgrade - value.Base);
    }

    protected int V(string name) => ActiveSnapshot is { } snapshot && snapshot.BaseValues.TryGetValue(name, out decimal value) ? (int)value : DynamicVars[name].IntValue;
    internal virtual decimal PreviewBaseDamage(decimal baseValue) => baseValue;
    protected Task Hit(PlayerChoiceContext context, Creature? target, bool trueDamage = false, int? amount = null)
        => target is { IsAlive: true }
            ? IvichRuntime.Attack(context, this, target, amount ?? V("Damage"), IsMagic, trueDamage)
            : Task.CompletedTask;
    protected Task HitAll(PlayerChoiceContext context, int? amount = null, bool trueDamage = false)
        => IvichRuntime.AttackAll(context, this, amount ?? V("Damage"), IsMagic, trueDamage);
    protected Task Block(CardPlay play) => CreatureCmd.GainBlock(Owner.Creature, new BlockVar(V("Block"), ValueProp.Move), play);
    protected Task Draw(PlayerChoiceContext context, int? count = null)
        => CardPileCmd.Draw(context, count ?? V("Cards"), Owner);
    protected Task Heal() => CreatureCmd.Heal(Owner.Creature, V("Heal"));
    protected Task Frost(PlayerChoiceContext context, Creature? target)
        => target is { IsAlive: true } ? IvichRuntime.ApplyFrost(context, target, V("Frost"), Owner.Creature, this) : Task.CompletedTask;
    protected Task Status<T>(PlayerChoiceContext context, Creature? target, string variable) where T : PowerModel
        => target is { IsAlive: true } ? IvichRuntime.ApplyDebuff<T>(context, target, V(variable), Owner.Creature, this) : Task.CompletedTask;
    protected Task Move(PlayerChoiceContext context, int distance)
        => IvichRuntime.SetDistance(Owner, distance, context, this);
    protected IEnumerable<Creature> LivingEnemies => CombatState?.HittableEnemies.ToArray() ?? [];

    protected int PositiveKinds()
    {
        var creature = Owner.Creature;
        return new[] { creature.GetPowerAmount<StrengthPower>(), creature.GetPowerAmount<DexterityPower>(),
            creature.GetPowerAmount<InvigorationPower>(), creature.GetPowerAmount<ArtifactPower>(),
            creature.GetPowerAmount<RegenPower>(), creature.GetPowerAmount<ThornsPower>() }.Count(value => value > 0);
    }

    protected async Task<bool> ExhaustAnother(PlayerChoiceContext context)
    {
        var selected = await CardSelectCmd.FromHand(context, Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1), card => card != this && card != Snapshot.SourceCard, this);
        var card = selected.FirstOrDefault();
        if (card is null) return false;
        await CardCmd.Exhaust(context, card);
        return card.Pile?.Type == PileType.Exhaust;
    }

    protected async Task<int> ChooseDirection(PlayerChoiceContext context)
    {
        var combat = CombatState ?? throw new InvalidOperationException("Distance choice requires active combat.");
        CardModel[] options = [combat.CreateCard<DistanceNearChoice>(Owner), combat.CreateCard<DistanceFarChoice>(Owner)];
        var selected = await CardSelectCmd.FromChooseACardScreen(context, options, Owner);
        return selected is DistanceFarChoice ? 5 : -5;
    }
}

/// <summary>Choice-only cards use the native synchronized choice screen and never enter a reward pool.</summary>
public abstract class DistanceChoice(bool far) : CustomCardModel(0, CardType.Skill, CardRarity.Token, TargetType.Self, false, false)
{
    public override CardPoolModel Pool => ModelDb.CardPool<IvichCardPool>();
    public override bool CanBeGeneratedInCombat => false;
    public override List<(string, string)> Localization => new CardLoc(far ? "拉开距离" : "接近敌人",
        far ? "选择远距方向（+5）。" : "选择近距方向（−5）。");
    public override string PortraitPath => ModelDb.Card<DefendIronclad>().PortraitPath;
    public override string? CustomPortraitPath => PortraitPath;
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play)
        => IvichRuntime.SetDistance(Owner, Owner.Creature.GetPowerAmount<DistancePower>() + (far ? 5 : -5), context, this);
}

public sealed class DistanceNearChoice() : DistanceChoice(false);
public sealed class DistanceFarChoice() : DistanceChoice(true);

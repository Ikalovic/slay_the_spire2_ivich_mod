using BaseLib.Abstracts;
using Ivich.Mod.Character;
using Ivich.Mod.Mechanics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace Ivich.Mod.Cards;

public sealed class R02TimeStorage() : IvichCard("R02")
{
    protected override Task PlayEffects(PlayerChoiceContext context, CardPlay play)
        => SpellRuntime.InstallStorage(context, Owner, this);
}

public sealed class T03SpellRelease() : IvichCard("T03")
{
    protected override PileType GetResultPileTypeForCardPlay()
    {
        var native = base.GetResultPileTypeForCardPlay();
        return native == PileType.Discard ? PileType.Hand : native;
    }
    protected override Task PlayEffects(PlayerChoiceContext context, CardPlay play)
        => SpellRuntime.Release(context, this);
}

/// <summary>Native synchronized grid entries for choosing a creature when a recorded spell executes.</summary>
public sealed class SpellTargetChoice() : CustomCardModel(0, CardType.Skill, CardRarity.Token, TargetType.None, false, false)
{
    public Creature? Target { get; private set; }
    private int _index;
    public override string Title => Target is null ? "法术目标" : $"{_index}. {Target.Name}";
    public override CardPoolModel Pool => ModelDb.CardPool<IvichCardPool>();
    public override bool CanBeGeneratedInCombat => false;
    protected override bool IsPlayable => false;
    public override string PortraitPath => ModelDb.Card<StrikeIronclad>().PortraitPath;
    public override string? CustomPortraitPath => PortraitPath;
    public override List<(string, string)> Localization => new CardLoc("法术目标", "生命：{Hp}/{Maximum}。格挡：{Guard}。",
        ("targetPrompt", "选择此法术的目标"), ("chantPrompt", "可选择一项吟唱，使其立即完成"));
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Hp", 0), new DynamicVar("Maximum", 0), new DynamicVar("Guard", 0)];
    internal static SpellTargetChoice ForTarget(Player player, Creature target, int index)
    {
        var choice = (SpellTargetChoice)ModelDb.Card<SpellTargetChoice>().ToMutable();
        choice.Owner = player; choice.Target = target; choice._index = index;
        choice.DynamicVars["Hp"].BaseValue = target.CurrentHp;
        choice.DynamicVars["Maximum"].BaseValue = target.MaxHp;
        choice.DynamicVars["Guard"].BaseValue = target.Block;
        return choice;
    }
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Task.CompletedTask;
}

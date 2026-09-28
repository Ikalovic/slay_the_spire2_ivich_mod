using BaseLib.Abstracts;
using Ivich.Core;
using Ivich.Mod.Character;
using Ivich.Mod.Mechanics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace Ivich.Mod.Cards;

public sealed class A01DualAspect() : IvichCard("A01")
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Innate, CardKeyword.Retain];
    protected override PileType GetResultPileTypeForCardPlay()
    {
        var native = base.GetResultPileTypeForCardPlay();
        return native == PileType.Discard ? PileType.Hand : native;
    }
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        var current = FormRuntime.CurrentForm(Owner);
        Form target;
        if (current == Form.Initial)
        {
            if (CombatState is not { } combat) return;
            var selected = await CardSelectCmd.FromChooseACardScreen(context,
                [combat.CreateCard<ChooseMageForm>(Owner), combat.CreateCard<ChooseDragonForm>(Owner)], Owner);
            if (selected is not FormChoice choice) return;
            target = choice.Form;
        }
        else target = current == Form.Dragon ? Form.Mage : Form.Dragon;
        await FormRuntime.SwitchForm(Owner, target);
        if (IsUpgraded && FormRuntime.TryUseAncientUpgrade(Owner)) await CardPileCmd.Draw(context, 2, Owner);
    }
}

public abstract class FormChoice(Form form, string title, string description)
    : CustomCardModel(0, CardType.Skill, CardRarity.Token, TargetType.Self, false, false)
{
    public Form Form => form;
    public override CardPoolModel Pool => ModelDb.CardPool<IvichCardPool>();
    public override bool CanBeGeneratedInCombat => false;
    protected override bool IsPlayable => false;
    public override List<(string, string)> Localization => new CardLoc(title, description);
    public override string PortraitPath => ModelDb.Card<DefendIronclad>().PortraitPath;
    public override string? CustomPortraitPath => PortraitPath;
    public override string BetaPortraitPath => PortraitPath;
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Task.CompletedTask;
}
public sealed class ChooseMageForm() : FormChoice(Form.Mage, "魔法使", "本场战斗切换为魔法使；保留当前魔力，不额外补给。生命按精确比例换算。");
public sealed class ChooseDragonForm() : FormChoice(Form.Dragon, "半龙", "本场战斗切换为半龙；当前魔力全额转为怒气，不额外补给。生命按精确比例换算。");

using BaseLib.Abstracts;
using Ivich.Mod.Character;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace Ivich.Mod.RestSites;

// UI choices, never obtainable cards. Choosing one changes the saved relic in RestSiteIntegration.
public abstract class InscriptionChoice(int id, string name, string text)
    : CustomCardModel(0, CardType.Skill, CardRarity.Token, TargetType.Self, false, false)
{
    public int InscriptionId => id;
    public override CardPoolModel Pool => ModelDb.CardPool<IvichCardPool>();
    public override bool CanBeGeneratedInCombat => false;
    protected override bool IsPlayable => false;
    public override List<(string, string)> Localization => new CardLoc(name, text);
    public override string PortraitPath => ModelDb.Card<DefendIronclad>().PortraitPath;
    public override string? CustomPortraitPath => PortraitPath;
    public override string BetaPortraitPath => PortraitPath;
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Task.CompletedTask;
}

public sealed class FoldBoundaryInscription() : InscriptionChoice(1, "折界", "每回合首次从非正距离进入正距离时，获得4格挡。");
public sealed class LoopInscription() : InscriptionChoice(2, "回环", "每回合首次镰刀攻击完成并入堆后，从弃牌堆选择1张镰刀牌加入手牌，不减费。");
public sealed class FrostInscription() : InscriptionChoice(3, "凝霜", "每回合首次镰刀攻击完成后，对它实际命中过且仍存活的敌人各施加2冰冻。多段不重复计算同一目标。");
public sealed class SiphonInscription() : InscriptionChoice(4, "引流", "每回合首次通过卡牌或能力实际获得魔力时，抽1张牌。自动回魔与形态转换不触发。");
public sealed class BloodPaymentInscription() : InscriptionChoice(5, "血偿", "每回合首次实际恢复生命时，获得3格挡。");
public sealed class SweetnessInscription() : InscriptionChoice(6, "余甘", "每回合首次产生溢疗时，抽1张牌。");

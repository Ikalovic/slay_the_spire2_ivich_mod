using BaseLib.Abstracts;
using Ivich.Mod.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Ivich.Mod.Powers;

public sealed class BladeWardPower : IvichPower
{
    public override PowerType Type => PowerType.Buff;
    public override List<(string, string)> Localization => new PowerLoc("刃上结界",
        "每张镰刀攻击结算后，获得{Amount}格挡。", "镰刀攻击结算后获得{Amount}格挡，不受敏捷影响。");

    // Effect completion is dispatched by AbilityRuntime for both ordinary and stored attacks.
}

public sealed class BoilingBloodPower : IvichPower
{
    public override PowerType Type => PowerType.Buff;
    public override List<(string, string)> Localization => new PowerLoc("沸血",
        "每次实际失去生命，获得{Amount}力量。", "每次实际失血事件获得{Amount}力量，不按失血点数重复触发。");

    public override async Task AfterDamageReceived(PlayerChoiceContext context, Creature target, DamageResult result,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || result.UnblockedDamage <= 0 || !Owner.IsAlive) return;
        Flash();
        await PowerCmd.Apply<StrengthPower>(context, Owner, Amount, Owner, null);
    }
}

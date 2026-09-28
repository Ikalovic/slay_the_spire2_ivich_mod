using Ivich.Mod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Ivich.Mod.Cards;

public sealed class U01BladeWard() : IvichCard("U01")
{
    protected override Task PlayEffects(PlayerChoiceContext context, CardPlay play)
        => PowerCmd.Apply<BladeWardPower>(context, Owner.Creature, V("Ward"), Owner.Creature, this);
}

public sealed class U02BoilingBlood() : IvichCard("U02")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await PowerCmd.Apply<BoilingBloodPower>(context, Owner.Creature, 1, Owner.Creature, this);
        if (V("Strength") > 0) await Status<StrengthPower>(context, Owner.Creature, "Strength");
    }
}

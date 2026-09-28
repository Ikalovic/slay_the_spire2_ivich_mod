using BaseLib.Abstracts;
using Ivich.Mod.Mechanics;
using Ivich.Mod.Resources;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Ivich.Mod.Cards;

public sealed class B01Strike() : IvichCard("B01")
{
    protected override Task PlayEffects(PlayerChoiceContext context, CardPlay play) => Hit(context, play.Target);
}

public sealed class B02Defend() : IvichCard("B02")
{
    protected override Task PlayEffects(PlayerChoiceContext context, CardPlay play) => Block(play);
}

public sealed class B03ScytheCharge() : IvichCard("B03"), ITranscendenceCard
{
    public CardModel GetTranscendenceTransformedCard() => ModelDb.Card<A01DualAspect>();
    protected override Task PlayEffects(PlayerChoiceContext context, CardPlay play)
        => IvichRuntime.GainActiveResource(Owner, V("Resource"));
}

public sealed class B04BiteFingertip() : IvichCard("B04")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Draw(context);
        await IvichRuntime.GainRage(Owner, V("Rage"));
    }
}

public sealed class B05FrostLine() : IvichCard("B05")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Hit(context, play.Target);
        await Frost(context, play.Target);
    }
}

public sealed class B06RetreatEdge() : IvichCard("B06")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Hit(context, play.Target);
        await Move(context, 5);
    }
}

public sealed class T01DragonBite() : IvichCard("T01")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Hit(context, play.Target);
        if (IvichRuntime.IsDragon(Owner) && Owner.PlayerCombatState is { } state && CombatState is { } combat)
        {
            var rage = CustomResources<RageResource>.Get(state);
            if (rage.Amount >= 1 && await rage.Spend<RageResource>(combat, this, 1, optional: true))
                await Heal();
        }
    }
}

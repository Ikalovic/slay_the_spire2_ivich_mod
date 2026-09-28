using Ivich.Mod.Mechanics;
using Ivich.Mod.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using DoomPower = Ivich.Mod.Powers.DoomPower;

namespace Ivich.Mod.Cards;

public sealed class U03ScaleCondensation() : IvichCard("U03")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await AbilityRuntime.Install(context, Owner, DesignId, IsUpgraded, this);
        await Block(play);
    }
}
public sealed class U04BloodForge() : IvichCard("U04")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await AbilityRuntime.Install(context, Owner, DesignId, IsUpgraded, this);
        await Heal();
    }
}
public sealed class U05ColdBlood() : IvichCard("U05")
{
    protected override Task PlayEffects(PlayerChoiceContext context, CardPlay play)
        => AbilityRuntime.Install(context, Owner, DesignId, IsUpgraded, this);
}
public sealed class U06HuntingBlink() : IvichCard("U06")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Move(context, -5);
        int frost = play.Target?.GetPowerAmount<FrostPower>() ?? 0;
        await Hit(context, play.Target, trueDamage: true, amount: V("Damage") + V("Factor") * frost);
        await Draw(context);
    }
}
public sealed class U07Hunger() : IvichCard("U07")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await AbilityRuntime.Install(context, Owner, DesignId, IsUpgraded, this);
        if (V("Rage") > 0) await IvichRuntime.GainRage(Owner, V("Rage"));
    }
}
public sealed class U08FrostKnowledge() : IvichCard("U08")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await AbilityRuntime.Install(context, Owner, DesignId, IsUpgraded, this);
        if (V("Invigoration") > 0) await IvichRuntime.GainInvigoration(context, Owner, V("Invigoration"), false, this);
    }
}
public sealed class U09IceJudgment() : IvichCard("U09")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        int removed = await CardEffects.RemoveLayers<FrostPower>(context, play.Target, 8, this);
        await Hit(context, play.Target, trueDamage: true, amount: V("Damage") + V("Factor") * removed);
    }
}
public sealed class U10IceMirror() : IvichCard("U10")
{
    protected override Task PlayEffects(PlayerChoiceContext context, CardPlay play)
        => AbilityRuntime.Install(context, Owner, DesignId, IsUpgraded, this);
}
public sealed class U11ColdTide() : IvichCard("U11")
{
    protected override Task PlayEffects(PlayerChoiceContext context, CardPlay play)
        => AbilityRuntime.Install(context, Owner, DesignId, IsUpgraded, this);
}
public sealed class U12FrostConfluence() : IvichCard("U12")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Target is { IsAlive: true } target)
        {
            int total = V("Frost");
            foreach (var enemy in LivingEnemies.Where(enemy => enemy != target))
                total += await CardEffects.RemoveLayers<FrostPower>(context, enemy, V("Transfer"), this);
            // One packet means one freeze check and one recipient Artifact check.
            await IvichRuntime.ApplyFrost(context, target, total, Owner.Creature, this);
        }
        await Draw(context);
    }
}
public sealed class U13DoomEtching() : IvichCard("U13")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Hit(context, play.Target);
        await Status<DoomPower>(context, play.Target, "Doom");
    }
}
public sealed class U14FinalDeclaration() : IvichCard("U14")
{
    protected override Task PlayEffects(PlayerChoiceContext context, CardPlay play)
        => CardEffects.ConditionalDoom(context, this, play.Target, V("Doom"));
}
public sealed class U15OmitChant() : IvichCard("U15")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        if (!await ExhaustAnother(context)) return;
        await Draw(context);
        await SpellRuntime.CompleteChant(context, Owner);
    }
}
public sealed class U16NearAndFar() : IvichCard("U16")
{
    protected override Task PlayEffects(PlayerChoiceContext context, CardPlay play)
        => AbilityRuntime.Install(context, Owner, DesignId, IsUpgraded, this);
}
public sealed class U17RiftChant() : IvichCard("U17")
{
    protected override Task PlayEffects(PlayerChoiceContext context, CardPlay play)
        => Hit(context, play.Target, trueDamage: true, amount: V("Damage") + V("Factor") * Snapshot.PermanentMagicCount);
}
public sealed class U18TransferBlame() : IvichCard("U18")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Block(play);
        await CardEffects.TransferDebuff(context, this, V("Transfer"));
    }
}
public sealed class U19StarFlow() : IvichCard("U19")
{
    protected override Task PlayEffects(PlayerChoiceContext context, CardPlay play)
        => AbilityRuntime.Install(context, Owner, DesignId, IsUpgraded, this);
}
public sealed class U20RingBlade() : IvichCard("U20")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        for (int i = 0; i < 3; i++) await Hit(context, play.Target);
        if (IvichRuntime.HealedThisTurn(Owner)) await Hit(context, play.Target);
    }
}
public sealed class U21UnrelentingBite() : IvichCard("U21")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        for (int i = 0; i < 2; i++)
        {
            await Hit(context, play.Target);
            await Heal();
        }
    }
}
public sealed class U22SpatialBookmark() : IvichCard("U22")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Draw(context);
        var selected = await CardSelectCmd.FromHand(context, Owner,
            new CardSelectorPrefs(CardEffects.SelectionPrompt, 1), null, this);
        if (selected.FirstOrDefault() is { } card)
            await CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Top);
    }
}
public sealed class U23BloodyRest() : IvichCard("U23")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Heal();
        await Status<RegenPower>(context, Owner.Creature, "Regen");
        await Draw(context);
    }
}
public sealed class U24MagicBladeResonance() : IvichCard("U24")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await AbilityRuntime.Install(context, Owner, DesignId, IsUpgraded, this);
        if (V("Bonus") <= 0) return;
        await AbilityRuntime.GainTemporaryStrength(context, Owner, V("Bonus"), this);
        await IvichRuntime.GainInvigoration(context, Owner, V("Bonus"), true, this);
    }
}
public sealed class U25CurseEcho() : IvichCard("U25")
{
    protected override Task PlayEffects(PlayerChoiceContext context, CardPlay play)
        => AbilityRuntime.Install(context, Owner, DesignId, IsUpgraded, this);
}
public sealed class U26ManyfoldBlessing() : IvichCard("U26")
{
    protected override Task PlayEffects(PlayerChoiceContext context, CardPlay play)
        => AbilityRuntime.Install(context, Owner, DesignId, IsUpgraded, this);
}
public sealed class U27SpiralHunt() : IvichCard("U27")
{
    private int _plays;
    protected override int CombatGrowthCount => _plays;
    internal override decimal PreviewBaseDamage(decimal baseValue) => baseValue * (1m + .5m * (IsRecordedEffect ? Snapshot.CombatGrowth : _plays));
    protected override void OnRealPlayRecorded(CardPlay play)
    {
        _plays++;
        DynamicVars["Growth"].BaseValue = _plays;
    }
    protected override Task PlayEffects(PlayerChoiceContext context, CardPlay play)
        => Hit(context, play.Target, amount: V("Damage") * (2 + Snapshot.CombatGrowth) / 2);
    protected override void AfterCloned()
    {
        base.AfterCloned();
        _plays = 0;
        DynamicVars["Growth"].BaseValue = 0;
    }
    public override Task AfterCombatEnd(CombatRoom room)
    {
        _plays = 0;
        DynamicVars["Growth"].BaseValue = 0;
        return Task.CompletedTask;
    }
}
public sealed class U28DisasterCompilation() : IvichCard("U28")
{
    protected override Task PlayEffects(PlayerChoiceContext context, CardPlay play)
        => Hit(context, play.Target, trueDamage: true,
            amount: V("Damage") + V("Factor") * (play.Target is { } target ? Snapshot.NegativeKinds(target) : 0));
}

using Ivich.Mod.Mechanics;
using Ivich.Mod.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;

namespace Ivich.Mod.Cards;

public sealed class C01DrawScythe() : IvichCard("C01")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Move(context, -5);
        await Hit(context, play.Target);
    }
}

public sealed class C02GlideStep() : IvichCard("C02")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        int direction = await ChooseDirection(context);
        await Move(context, Owner.Creature.GetPowerAmount<DistancePower>() + direction);
        await Block(play);
    }
}

public sealed class C03RetreatingBlade() : IvichCard("C03")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Hit(context, play.Target);
        await Move(context, 5);
        await Block(play);
    }
}

public sealed class C04RiftEtching() : IvichCard("C04")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Hit(context, play.Target, trueDamage: true);
        if (Owner.Creature.GetPowerAmount<DistancePower>() < 0)
        {
            await Status<VulnerablePower>(context, play.Target, "Debuff");
            await Status<FrailPower>(context, play.Target, "Debuff");
        }
    }
}

public sealed class C05ScytheParry() : IvichCard("C05")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        bool earlierScythe = IvichRuntime.ScythePlayedThisTurn(Owner);
        await IvichRuntime.GainTemporaryDexterity(context, Owner, V("Dexterity"), this);
        await Block(play);
        if (earlierScythe) await Draw(context, 1);
    }
}

public sealed class C06DrawMana() : IvichCard("C06")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await IvichRuntime.GainMana(Owner, V("Mana"));
        await IvichRuntime.GainInvigoration(context, Owner, V("Invigoration"), true, this);
    }
}

public sealed class C07CookieTime() : IvichCard("C07")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Heal();
        await IvichRuntime.GainRage(Owner, V("Rage"));
    }
}

public sealed class C08GrindTeeth() : IvichCard("C08")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Block(play);
        await IvichRuntime.GainTemporaryThorns(context, Owner, V("Thorns"), this);
        await IvichRuntime.GainRage(Owner, V("Rage"));
    }
}

public sealed class C09BloodForEdge() : IvichCard("C09")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await PlayerCmd.GainEnergy(1, Owner);
        await IvichRuntime.GainRage(Owner, V("Rage"));
        if (V("Cards") > 0) await Draw(context);
    }
}

public sealed class C10BiteDown() : IvichCard("C10")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Hit(context, play.Target);
        await Heal();
    }
}

public sealed class C11CrossCut() : IvichCard("C11")
{
    internal override decimal PreviewBaseDamage(decimal baseValue)
        => baseValue + (IsMutable && Owner is not null ? PositiveKinds() : 0);

    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        int amount = V("Damage") + Snapshot.PositiveKinds;
        await HitAll(context, amount);
        await HitAll(context, amount);
    }
}

public sealed class C12FrostBreath() : IvichCard("C12")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await HitAll(context);
        foreach (var enemy in LivingEnemies) await Frost(context, enemy);
    }
}

public sealed class C13FrostBlade() : IvichCard("C13")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Hit(context, play.Target);
        await Frost(context, play.Target);
    }
}

public sealed class C14IceNeedle() : IvichCard("C14")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Hit(context, play.Target);
        await Hit(context, play.Target);
        await Frost(context, play.Target);
    }
}

public sealed class C15FrostWall() : IvichCard("C15")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Block(play);
        await Frost(context, play.Target);
    }
}

public sealed class C16FrostBind() : IvichCard("C16")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Frost(context, play.Target);
        await Status<WeakPower>(context, play.Target, "Weak");
    }
}

public sealed class C17BlinkStep() : IvichCard("C17")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Move(context, await ChooseDirection(context));
        await Draw(context);
        // No gain-block event at base rank: zero base block must not accidentally receive Dexterity.
        if (IsUpgraded) await Block(play);
    }
}

public sealed class C18Deconstruct() : IvichCard("C18")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        if (!await ExhaustAnother(context)) return;
        await IvichRuntime.GainMana(Owner, V("Mana"));
        await Draw(context);
    }
}

public sealed class C19CrossSectionRay() : IvichCard("C19")
{
    private int _growth;
    protected override int CombatGrowthCount => _growth;
    internal override decimal PreviewBaseDamage(decimal baseValue)
        => baseValue + (IsRecordedEffect ? Snapshot.CombatGrowth : _growth);
    protected override void OnRealPlayRecorded(CardPlay play)
    {
        _growth += 4;
        DynamicVars["Growth"].BaseValue = _growth;
    }
    protected override Task PlayEffects(PlayerChoiceContext context, CardPlay play)
        => Hit(context, play.Target, trueDamage: true, amount: V("Damage") + Snapshot.CombatGrowth);
    protected override void AfterCloned()
    {
        base.AfterCloned();
        _growth = 0;
        DynamicVars["Growth"].BaseValue = 0;
    }
    public override Task AfterCombatEnd(CombatRoom room)
    {
        _growth = 0;
        DynamicVars["Growth"].BaseValue = 0;
        return Task.CompletedTask;
    }
}

public sealed class C20Doze() : IvichCard("C20")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Block(play);
        await Status<RegenPower>(context, Owner.Creature, "Regen");
        await Draw(context);
    }
}

public sealed class C21PalePrayer() : IvichCard("C21")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await IvichRuntime.GainInvigoration(context, Owner, V("Invigoration"), true, this);
        await Draw(context);
    }
}

public sealed class C22LickWounds() : IvichCard("C22")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Heal();
        await Block(play);
    }
}

public sealed class C23RecallScythe() : IvichCard("C23")
{
    public override List<(string, string)> Localization => [.. base.Localization, ("selectionPrompt", "选择一张镰刀牌加入手牌。")];

    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        var chosen = await CardSelectCmd.FromCombatPile(context, PileType.Discard.GetPile(Owner), Owner,
            new CardSelectorPrefs(new LocString("cards", Id.Entry + ".selectionPrompt"), 1),
            card => card is IvichCard { IsScythe: true });
        foreach (var card in chosen) await CardPileCmd.Add(card, PileType.Hand);
        await Block(play);
    }
}

public sealed class C24DragonStomach() : IvichCard("C24")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        if (!await ExhaustAnother(context)) return;
        await Heal();
        await IvichRuntime.GainRage(Owner, V("Rage"));
    }
}

public sealed class C25ChillingAssault() : IvichCard("C25")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        foreach (var enemy in LivingEnemies)
        {
            await Status<WeakPower>(context, enemy, "Debuff");
            await Status<VulnerablePower>(context, enemy, "Debuff");
        }
    }
}

public sealed class C26DragonDread() : IvichCard("C26")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Status<WeakPower>(context, play.Target, "Debuff");
        if (IvichRuntime.LostLifeThisTurn(Owner))
        {
            await Status<VulnerablePower>(context, play.Target, "Debuff");
            await Draw(context);
        }
    }
}

public sealed class C27WeaveStarRobe() : IvichCard("C27")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Status<DexterityPower>(context, Owner.Creature, "Dexterity");
        await Status<ArtifactPower>(context, Owner.Creature, "Artifact");
    }
}

public sealed class C28BreathTuning() : IvichCard("C28")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Draw(context);
        if (IvichRuntime.HealedThisTurn(Owner)) await PlayerCmd.GainEnergy(1, Owner);
    }
}

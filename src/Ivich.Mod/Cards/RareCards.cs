using System.Runtime.CompilerServices;
using Ivich.Mod.Mechanics;
using Ivich.Mod.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace Ivich.Mod.Cards;

public sealed class R01WorldCrossSection() : IvichCard("R01")
{
    protected override Task PlayEffects(PlayerChoiceContext context, CardPlay play)
        => HitAll(context, trueDamage: true);
}
public sealed class R03ZeroCoronation() : IvichCard("R03")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await HitAll(context);
        foreach (var enemy in LivingEnemies) await Frost(context, enemy);
    }
}
public sealed class R04EternalWinter() : IvichCard("R04")
{
    protected override Task PlayEffects(PlayerChoiceContext context, CardPlay play)
        => AbilityRuntime.Install(context, Owner, DesignId, IsUpgraded, this);
}
public sealed class R05WorldEatingScythe() : IvichCard("R05")
{
    [SavedProperty] public int PermanentGrowth { get; set; }
    protected override decimal CaptureBaseValue(string name, decimal value)
        => name == "Damage" ? value + PermanentGrowth : value;
    internal override decimal PreviewBaseDamage(decimal baseValue)
        => IsRecordedEffect ? baseValue : baseValue + PermanentGrowth;
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Hit(context, play.Target, trueDamage: true);
        if (Snapshot.IsOriginalCast && Snapshot.SourceCard is R05WorldEatingScythe original && !original.IsClone && !original.IsDupe)
            original.AddPermanentGrowth(3);
    }
    private void AddPermanentGrowth(int amount)
    {
        PermanentGrowth += amount;
        if (DeckVersion is R05WorldEatingScythe deck && deck != this) deck.PermanentGrowth = PermanentGrowth;
        Owner.PlayerCombatState?.RecalculateCardValues();
    }
}
public sealed class R06ScarletFeast() : IvichCard("R06")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        var results = await IvichRuntime.AttackAllResults(context, this, V("Damage") + Snapshot.BlockAtStart / 2);
        int heal = results.Sum(result => result.UnblockedDamage) / 3;
        if (heal > 0) await CreatureCmd.Heal(Owner.Creature, heal);
    }
}
public sealed class R07Devour() : IvichCard("R07")
{
    private sealed class Consumed;
    private static readonly ConditionalWeakTable<Creature, Consumed> ConsumedEnemies = new();
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Target is not { IsAlive: true } target) return;
        // This is the native Feed fatal gate: minions, phase changes and nonfatal deaths opt out.
        bool qualified = target.IsMonster && target.Powers.All(power => power.ShouldOwnerDeathTriggerFatal())
            && !ConsumedEnemies.TryGetValue(target, out _);
        var results = await IvichRuntime.Attack(context, this, target, V("Damage"));
        if (!qualified || !results.Any(result => result.Receiver == target && result.WasTargetKilled)) return;
        ConsumedEnemies.GetValue(target, _ => new Consumed());
        await FormRuntime.IncreaseMaximum(Owner, V("Growth"));
        await Heal();
    }
}
public sealed class R08DonutPocket() : IvichCard("R08")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        for (int i = 0; i < V("Cards"); i++)
        {
            var card = CombatState!.CreateCard<T02Donut>(Owner);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
        }
    }
}
public sealed class R09AbandonThisShore() : IvichCard("R09")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        var hand = PileType.Hand.GetPile(Owner).Cards.Where(card => card != this && card != Snapshot.SourceCard).ToArray();
        int exhausted = 0;
        foreach (var card in hand)
        {
            if (card.Pile?.Type != PileType.Hand) continue;
            await CardCmd.Exhaust(context, card);
            if (card.Pile?.Type == PileType.Exhaust) exhausted++;
        }
        for (int i = 0; i < exhausted && LivingEnemies.Any() && !CombatManager.Instance.IsOverOrEnding; i++) await HitAll(context, trueDamage: true);
    }
}
public sealed class R10AbsoluteRecovery() : IvichCard("R10")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        var cards = await CardSelectCmd.FromCombatPile(context, PileType.Discard.GetPile(Owner), Owner,
            new CardSelectorPrefs(CardEffects.SelectionPrompt, 0, V("Cards")));
        foreach (var card in cards)
        {
            if (!card.EnergyCost.CostsX)
            {
                card.EnergyCost.AddThisTurn(-1, true);
                card.InvokeEnergyCostChanged();
            }
            await CardPileCmd.Add(card, PileType.Hand);
        }
    }
}
public sealed class R11DoomCurtain() : IvichCard("R11")
{
    protected override Task PlayEffects(PlayerChoiceContext context, CardPlay play)
        => AbilityRuntime.Install(context, Owner, DesignId, IsUpgraded, this);
}
public sealed class R12FinalChant() : IvichCard("R12")
{
    protected override Task PlayEffects(PlayerChoiceContext context, CardPlay play)
        => CardEffects.ConditionalDoom(context, this, play.Target, V("Doom"));
}
public sealed class R13StarsAndFrost() : IvichCard("R13")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        for (int i = 0; i < Snapshot.PaidX && LivingEnemies.Any() && !CombatManager.Instance.IsOverOrEnding; i++)
        {
            await HitAll(context, trueDamage: true);
            foreach (var enemy in LivingEnemies) await Frost(context, enemy);
        }
    }
}
public sealed class R14Skyfall() : IvichCard("R14")
{
    protected override Task PlayEffects(PlayerChoiceContext context, CardPlay play)
        => Hit(context, play.Target, trueDamage: true, amount: V("Damage") * Snapshot.PaidX);
}
public sealed class R15GluttonousDragonDance() : IvichCard("R15")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        for (int i = 0; i < Snapshot.PaidX && play.Target is { IsAlive: true } && !CombatManager.Instance.IsOverOrEnding; i++)
        {
            await Hit(context, play.Target);
            // The lethal segment still heals. Only segments not yet started are skipped.
            await Heal();
        }
    }
}
public sealed class R16AllFormsReturn() : IvichCard("R16")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        int hits = 1 + Snapshot.PositiveKinds;
        for (int i = 0; i < hits && LivingEnemies.Any() && !CombatManager.Instance.IsOverOrEnding; i++) await HitAll(context);
    }
}
public sealed class T02Donut() : IvichCard("T02")
{
    protected override async Task PlayEffects(PlayerChoiceContext context, CardPlay play)
    {
        await Heal();
        await IvichRuntime.GainActiveResource(Owner, V("Resource"));
    }
}

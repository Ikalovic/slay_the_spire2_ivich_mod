using Ivich.Core;

internal static partial class TestRunner
{
    private static void TriggerTests()
    {
        Test("star current allowance is shared across installation and consumed only on qualifying resolution", () =>
        {
            var turn = new TurnTriggerLedger();
            False(turn.TryStarCurrent(false));
            True(turn.TryStarCurrent(true));
            False(turn.TryStarCurrent(true));
            turn.BeginTurn();
            True(turn.TryStarCurrent(true));
        });
        Test("curse echo quota can rise from two to three without resetting earlier usage", () =>
        {
            var turn = new TurnTriggerLedger();
            True(turn.TryCurseEcho(2)); True(turn.TryCurseEcho(2)); False(turn.TryCurseEcho(2));
            True(turn.TryCurseEcho(3)); False(turn.TryCurseEcho(3));
            turn.BeginTurn(); True(turn.TryCurseEcho(2));
        });
        Test("no curse echo installed leaves turn quota untouched", () =>
        {
            var turn = new TurnTriggerLedger();
            False(turn.TryCurseEcho(0)); True(turn.TryCurseEcho(2)); True(turn.TryCurseEcho(2));
        });
        Test("many forms shares three distinct positive kinds per turn and ignores inactive observations", () =>
        {
            var turn = new TurnTriggerLedger();
            False(turn.TryManyForms("Strength", false));
            True(turn.TryManyForms("Strength", true)); False(turn.TryManyForms("Strength", true));
            True(turn.TryManyForms("Dexterity", true)); True(turn.TryManyForms("Regen", true));
            False(turn.TryManyForms("Artifact", true));
            turn.BeginTurn(); True(turn.TryManyForms("Strength", true));
        });
        Test("overheal blood forge rounds each healing event separately", () =>
        {
            Equal(0, AbilityRules.BloodForgeStrength(1, 2));
            Equal(2, AbilityRules.BloodForgeStrength(3, 2));
            Equal(4, AbilityRules.BloodForgeStrength(4, 2));
        });
    }
}

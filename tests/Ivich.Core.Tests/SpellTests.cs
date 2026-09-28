using Ivich.Core;

internal static partial class TestRunner
{
    private static void SpellTests()
    {
        Test("cast snapshots retain copied base values and paid X", () =>
        {
            var values = new Dictionary<string, int> { ["damage"] = 12, ["block"] = 8 };
            var spell = new SpellSnapshot("R14", upgraded: true, sourceEntityId: "permanent-7", paidX: 9,
                permanentMagicCount: 12, combatGrowth: 2, baseValues: values);
            values["damage"] = 999; values.Clear();
            Equal(12, spell.BaseValues["damage"]); Equal(8, spell.BaseValues["block"]);
            Equal(9, spell.PaidX); Equal(12, spell.PermanentMagicCount); Equal(2, spell.CombatGrowth);
            Equal("permanent-7", spell.SourceEntityId); True(spell.Upgraded);
        });
        Test("ordinary ready spell executes once and is absent from both queues", () =>
        {
            var executed = new List<string>();
            var queue = new SpellQueue(spell => executed.Add(spell.CardId));
            queue.Submit(new SpellSnapshot("B05"));
            Equal("B05", string.Join(',', executed)); Equal(0, queue.Charging.Count); Equal(0, queue.Ready.Count);
            False(queue.CanRelease); Equal(0, queue.Release());
        });
        Test("chant waits until next player turn without repaying or changing snapshot", () =>
        {
            var executed = new List<SpellSnapshot>(); var queue = new SpellQueue(executed.Add);
            queue.BeginTurn(1); var original = new SpellSnapshot("R14", paidX: 9);
            queue.Submit(original, chantTurns: 1);
            False(queue.CanRelease); Equal(0, queue.Release()); Equal(1, queue.Charging.Count);
            queue.BeginTurn(1); Equal(0, executed.Count);
            queue.BeginTurn(2); Equal(1, executed.Count); True(ReferenceEquals(original, executed[0]));
            Equal(9, executed[0].PaidX); Equal(0, queue.Charging.Count);
        });
        Test("new chant created inside turn-start resolution waits another turn", () =>
        {
            var executed = new List<string>(); SpellQueue queue = null!;
            queue = new SpellQueue(spell =>
            {
                executed.Add(spell.CardId);
                if (spell.CardId == "first") queue.Submit(new SpellSnapshot("next"), chantTurns: 1);
            });
            queue.BeginTurn(1); queue.Submit(new SpellSnapshot("first"), chantTurns: 1);
            queue.BeginTurn(2); Equal("first", string.Join(',', executed)); Equal(1, queue.Charging.Count);
            queue.BeginTurn(2); Equal(1, queue.Charging.Count);
            queue.BeginTurn(3); Equal("first,next", string.Join(',', executed));
        });
        Test("storage acquired during chant decides routing at completion", () =>
        {
            var executed = new List<string>(); var queue = new SpellQueue(spell => executed.Add(spell.CardId));
            queue.BeginTurn(1); queue.Submit(new SpellSnapshot("R01"), chantTurns: 1);
            True(queue.InstallStorage()); queue.BeginTurn(2);
            Equal(0, executed.Count); Equal(0, queue.Charging.Count); Equal(1, queue.Ready.Count);
            False(queue.InstallStorage()); Equal(1, queue.Ready.Count);
            True(queue.CanRelease); Equal(1, queue.Release()); Equal("R01", string.Join(',', executed));
        });
        Test("simultaneous chants observe storage installed by the earlier resolution", () =>
        {
            var executed = new List<string>(); SpellQueue queue = null!;
            queue = new SpellQueue(spell => { executed.Add(spell.CardId); queue.InstallStorage(); });
            queue.BeginTurn(1); queue.Submit(new SpellSnapshot("first"), chantTurns: 1);
            queue.Submit(new SpellSnapshot("second"), chantTurns: 1); queue.BeginTurn(2);
            Equal("first", string.Join(',', executed)); Equal(1, queue.Ready.Count);
            Equal("second", queue.Ready[0].CardId);
        });
        Test("release detaches its batch and preserves records created during release", () =>
        {
            var executed = new List<string>(); SpellQueue queue = null!;
            queue = new SpellQueue(spell =>
            {
                executed.Add(spell.CardId);
                if (spell.CardId == "first") queue.Submit(new SpellSnapshot("new"));
            });
            queue.InstallStorage(); queue.Submit(new SpellSnapshot("first")); queue.Submit(new SpellSnapshot("second"));
            Equal(2, queue.Release()); Equal("first,second", string.Join(',', executed));
            Equal(1, queue.Ready.Count); Equal("new", queue.Ready[0].CardId);
            Equal(1, queue.Release()); Equal("first,second,new", string.Join(',', executed)); Equal(0, queue.Release());
        });
        Test("explicit chant completion respects current storage and removes only the selected record", () =>
        {
            var executed = new List<string>(); var queue = new SpellQueue(spell => executed.Add(spell.CardId));
            queue.BeginTurn(1); queue.Submit(new SpellSnapshot("first"), chantTurns: 2);
            queue.Submit(new SpellSnapshot("second"), chantTurns: 1);
            var second = queue.Charging[1].Sequence; queue.InstallStorage();
            True(queue.CompleteChant(second)); Equal(1, queue.Charging.Count); Equal(1, queue.Ready.Count);
            False(queue.CompleteChant(second)); Equal(0, executed.Count);
            queue.BeginTurn(2); Equal(1, queue.Charging[0].TurnsRemaining);
            queue.BeginTurn(3); Equal(0, queue.Charging.Count); Equal(2, queue.Ready.Count);
            queue.Release(); Equal("second,first", string.Join(',', executed));
        });
        Test("combat completion cancels unstarted records in the detached release batch", () =>
        {
            var executed = new List<string>(); SpellQueue queue = null!;
            queue = new SpellQueue(spell => { executed.Add(spell.CardId); queue.EndCombat(); });
            queue.InstallStorage(); queue.Submit(new SpellSnapshot("lethal")); queue.Submit(new SpellSnapshot("unstarted"));
            Equal(1, queue.Release()); Equal("lethal", string.Join(',', executed));
            Equal(0, queue.Ready.Count); Equal(0, queue.Charging.Count);
            Throws<InvalidOperationException>(() => queue.Submit(new SpellSnapshot("late")));
        });
        Test("new combat clears storage, turn counters and all records", () =>
        {
            var queue = new SpellQueue(_ => { }); queue.BeginTurn(7); queue.InstallStorage();
            queue.Submit(new SpellSnapshot("stored")); queue.Submit(new SpellSnapshot("waiting"), chantTurns: 1);
            queue.EndCombat(); queue.StartCombat();
            False(queue.StorageEnabled); Equal(0, queue.CurrentTurn); Equal(0, queue.Charging.Count); Equal(0, queue.Ready.Count);
            queue.BeginTurn(1); Equal(1, queue.CurrentTurn);
        });
        Test("malformed snapshot and queue inputs are rejected", () =>
        {
            Throws<ArgumentException>(() => new SpellSnapshot(" "));
            Throws<ArgumentOutOfRangeException>(() => new SpellSnapshot("R14", paidX: -1));
            var queue = new SpellQueue(_ => { }); queue.BeginTurn(2);
            Throws<ArgumentOutOfRangeException>(() => queue.BeginTurn(1));
            Throws<ArgumentOutOfRangeException>(() => queue.Submit(new SpellSnapshot("R14"), chantTurns: -1));
        });
    }
}

using Ivich.Core;

var tests = new List<(string, Func<Task>)>();
void Check(bool value, string message = "assertion failed") { if (!value) throw new Exception(message); }
tests.Add(("asynchronous effects finish before the next record starts", async () =>
{
    var events = new List<string>();
    var queue = new AsyncSpellQueue<string>(async x => { events.Add(x + "start"); await Task.Yield(); events.Add(x + "end"); });
    queue.InstallStorage(); await queue.Submit("a"); await queue.Submit("b");
    Check(await queue.Release() == 2); Check(string.Join(',', events) == "astart,aend,bstart,bend");
}
));
tests.Add(("storage installed during an awaited chant controls the following chant", async () =>
{
    var events = new List<string>(); AsyncSpellQueue<string> queue = null!;
    queue = new(async x => { await Task.Yield(); events.Add(x); queue.InstallStorage(); });
    await queue.BeginTurn(1); await queue.Submit("a", 1); await queue.Submit("b", 1); await queue.BeginTurn(2);
    Check(events.SequenceEqual(["a"])); Check(queue.Ready.SequenceEqual(["b"]));
}
));
tests.Add(("release detaches batch and preserves newly stored effects", async () =>
{
    var events = new List<string>(); AsyncSpellQueue<string> queue = null!;
    queue = new(async x => { events.Add(x); await Task.Yield(); if (x == "a") await queue.Submit("new"); });
    queue.InstallStorage(); await queue.Submit("a"); await queue.Submit("b"); await queue.Release();
    Check(events.SequenceEqual(["a", "b"])); Check(queue.Ready.SequenceEqual(["new"]));
}
));
tests.Add(("combat ending cancels later records even when release already detached them", async () =>
{
    var events = new List<string>(); AsyncSpellQueue<string> queue = null!;
    queue = new(async x => { events.Add(x); await Task.Yield(); queue.EndCombat(); });
    queue.InstallStorage(); await queue.Submit("lethal"); await queue.Submit("later");
    Check(await queue.Release() == 1); Check(events.SequenceEqual(["lethal"])); Check(!queue.CanRelease);
}
));
tests.Add(("fresh chant created during start phase cannot complete in the same phase", async () =>
{
    var events = new List<string>(); AsyncSpellQueue<string> queue = null!;
    queue = new(async x => { events.Add(x); await Task.Yield(); if (x == "a") await queue.Submit("fresh", 1); });
    await queue.BeginTurn(1); await queue.Submit("a", 1); await queue.BeginTurn(2); await queue.BeginTurn(2);
    Check(events.SequenceEqual(["a"])); Check(queue.Charging.Count == 1); await queue.BeginTurn(3);
    Check(events.SequenceEqual(["a", "fresh"]));
}
));
tests.Add(("chant acceleration selects one record and obeys storage", async () =>
{
    var events = new List<string>(); var queue = new AsyncSpellQueue<string>(x => { events.Add(x); return Task.CompletedTask; });
    await queue.Submit("first", 2); await queue.Submit("second", 1); queue.InstallStorage();
    Check(await queue.CompleteChant(queue.Charging[1].Sequence)); Check(queue.Ready.SequenceEqual(["second"]));
    Check(queue.Charging.Single().Effect == "first"); Check(events.Count == 0);
}
));
tests.Add(("combat availability stops unstarted healing after a lethal spell", async () =>
{
    bool alive = true; var events = new List<string>();
    var queue = new AsyncSpellQueue<string>(async x => { await Task.Yield(); events.Add(x); alive = false; }, () => alive);
    queue.InstallStorage(); await queue.Submit("lethal"); await queue.Submit("heal"); await queue.Release();
    Check(events.SequenceEqual(["lethal"])); Check(queue.Ready.Count == 0);
}
));
tests.Add(("a new combat version cannot inherit detached records from the previous combat", async () =>
{
    var events = new List<string>(); AsyncSpellQueue<string> queue = null!;
    queue = new(async x => { events.Add(x); await Task.Yield(); queue.EndCombat(); queue.StartCombat(); });
    queue.InstallStorage(); await queue.Submit("old"); await queue.Submit("stale"); await queue.Release();
    Check(events.SequenceEqual(["old"])); Check(!queue.StorageEnabled); Check(queue.CurrentTurn == 0);
}
));
tests.Add(("chant created before this mod's turn-start hook still waits for the next actual turn", async () =>
{
    var events = new List<string>(); var queue = new AsyncSpellQueue<string>(x => { events.Add(x); return Task.CompletedTask; });
    await queue.BeginTurn(1);
    await queue.Submit("old", 1, createdTurn: 1);
    await queue.Submit("created-by-earlier-start-hook", 1, createdTurn: 2);
    await queue.BeginTurn(2);
    Check(events.SequenceEqual(["old"])); Check(queue.Charging.Single().Effect == "created-by-earlier-start-hook");
    await queue.BeginTurn(3); Check(events.SequenceEqual(["old", "created-by-earlier-start-hook"]));
}
));
int failed = 0; foreach (var (name, test) in tests) try { await test(); Console.WriteLine("PASS " + name); } catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); }
Console.WriteLine($"{tests.Count - failed}/{tests.Count} async spell tests passed"); return failed == 0 ? 0 : 1;

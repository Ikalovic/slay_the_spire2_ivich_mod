namespace Ivich.Core;

public sealed record ChargingEffect<T>(long Sequence, T Effect, int TurnsRemaining, int CreatedTurn);

/// <summary>Ordered queue of already paid effects; async callbacks finish before later effects begin.</summary>
public sealed class AsyncSpellQueue<T>(Func<T, Task> resolve, Func<bool>? canContinue = null)
{
    private readonly List<ChargingEffect<T>> _charging = [];
    private readonly List<T> _ready = [];
    private bool _inCombat = true;
    private long _sequence, _version;
    public bool StorageEnabled { get; private set; }
    public int CurrentTurn { get; private set; }
    public bool CanRelease => _inCombat && _ready.Count > 0 && Available;
    public IReadOnlyList<ChargingEffect<T>> Charging => _charging.ToArray();
    public IReadOnlyList<T> Ready => _ready.ToArray();
    private bool Available => canContinue?.Invoke() ?? true;
    public bool InstallStorage() { RequireCombat(); if (StorageEnabled) return false; StorageEnabled = true; return true; }
    public void StartCombat() { _version++; _inCombat = true; StorageEnabled = false; CurrentTurn = 0; _sequence = 0; _charging.Clear(); _ready.Clear(); }
    public void EndCombat() { _version++; _inCombat = false; StorageEnabled = false; _charging.Clear(); _ready.Clear(); }
    public async Task Submit(T effect, int chantTurns = 0, int? createdTurn = null)
    {
        RequireCombat(); ArgumentOutOfRangeException.ThrowIfNegative(chantTurns);
        int creationTurn = createdTurn ?? CurrentTurn;
        ArgumentOutOfRangeException.ThrowIfLessThan(creationTurn, CurrentTurn);
        if (chantTurns > 0) _charging.Add(new(++_sequence, effect, chantTurns, creationTurn)); else await Route(effect);
    }
    public async Task BeginTurn(int turn)
    {
        RequireCombat(); ArgumentOutOfRangeException.ThrowIfLessThan(turn, 1); ArgumentOutOfRangeException.ThrowIfLessThan(turn, CurrentTurn);
        if (turn == CurrentTurn) return; CurrentTurn = turn; long version = _version;
        foreach (var item in _charging.Where(x => x.CreatedTurn < turn).ToArray())
        {
            if (!CanContinue(version)) break;
            int index = _charging.FindIndex(x => x.Sequence == item.Sequence); if (index < 0) continue;
            if (item.TurnsRemaining > 1) _charging[index] = item with { TurnsRemaining = item.TurnsRemaining - 1 };
            else { _charging.RemoveAt(index); await Route(item.Effect); }
        }
    }
    public async Task<bool> CompleteChant(long sequence)
    {
        RequireCombat(); int index = _charging.FindIndex(x => x.Sequence == sequence); if (index < 0) return false;
        T effect = _charging[index].Effect; _charging.RemoveAt(index); await Route(effect); return true;
    }
    public async Task<int> Release()
    {
        RequireCombat(); T[] batch = _ready.ToArray(); _ready.Clear(); long version = _version; int started = 0;
        foreach (T effect in batch) { if (!CanContinue(version)) break; started++; await resolve(effect); }
        return started;
    }
    private bool CanContinue(long version)
    {
        if (!_inCombat || version != _version) return false;
        if (Available) return true; EndCombat(); return false;
    }
    private async Task Route(T effect)
    {
        if (!CanContinue(_version)) return;
        if (StorageEnabled) _ready.Add(effect); else await resolve(effect);
    }
    private void RequireCombat() { if (!_inCombat) throw new InvalidOperationException("Combat has ended."); }
}

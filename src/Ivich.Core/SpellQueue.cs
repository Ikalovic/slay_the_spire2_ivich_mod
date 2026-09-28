using System.Collections.Frozen;

namespace Ivich.Core;

public sealed class SpellSnapshot
{
    public SpellSnapshot(string cardId, bool upgraded = false, string? sourceEntityId = null, int paidX = 0,
        int permanentMagicCount = 0, int combatGrowth = 0, IReadOnlyDictionary<string, int>? baseValues = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);
        ArgumentOutOfRangeException.ThrowIfNegative(paidX);
        ArgumentOutOfRangeException.ThrowIfNegative(permanentMagicCount);
        ArgumentOutOfRangeException.ThrowIfNegative(combatGrowth);
        CardId = cardId;
        Upgraded = upgraded;
        SourceEntityId = sourceEntityId;
        PaidX = paidX;
        PermanentMagicCount = permanentMagicCount;
        CombatGrowth = combatGrowth;
        BaseValues = (baseValues ?? new Dictionary<string, int>()).ToFrozenDictionary(StringComparer.Ordinal);
    }

    public string CardId { get; }
    public bool Upgraded { get; }
    public string? SourceEntityId { get; }
    public int PaidX { get; }
    public int PermanentMagicCount { get; }
    public int CombatGrowth { get; }
    public IReadOnlyDictionary<string, int> BaseValues { get; }
}

public sealed record PendingSpell(long Sequence, SpellSnapshot Snapshot, int TurnsRemaining, int CreatedTurn);

public sealed class SpellQueue
{
    private readonly Action<SpellSnapshot> _resolve;
    private readonly List<PendingSpell> _charging = [];
    private readonly List<SpellSnapshot> _ready = [];
    private bool _inCombat;
    private long _sequence;
    private long _combatVersion;

    /// <summary>The resolver must finish an entire effect and its event reactions before returning.</summary>
    public SpellQueue(Action<SpellSnapshot> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        _resolve = resolve;
        StartCombat();
    }

    public bool StorageEnabled { get; private set; }
    public bool CanRelease => _inCombat && _ready.Count > 0;
    public int CurrentTurn { get; private set; }
    public IReadOnlyList<PendingSpell> Charging => _charging.ToArray();
    public IReadOnlyList<SpellSnapshot> Ready => _ready.ToArray();

    /// <returns>True only for the first installation, when the adapter should obtain T03.</returns>
    public bool InstallStorage()
    {
        RequireCombat();
        if (StorageEnabled) return false;
        StorageEnabled = true;
        return true;
    }

    public void StartCombat()
    {
        _combatVersion++;
        _charging.Clear();
        _ready.Clear();
        _sequence = 0;
        CurrentTurn = 0;
        StorageEnabled = false;
        _inCombat = true;
    }

    public void EndCombat()
    {
        _combatVersion++;
        _inCombat = false;
        _charging.Clear();
        _ready.Clear();
        StorageEnabled = false;
    }

    /// <summary>Already paid effects only. This queue never pays, plays or exhausts an original card.</summary>
    public void Submit(SpellSnapshot snapshot, int chantTurns = 0)
    {
        RequireCombat();
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentOutOfRangeException.ThrowIfNegative(chantTurns);
        if (chantTurns == 0) RouteReady(snapshot);
        else _charging.Add(new PendingSpell(checked(++_sequence), snapshot, chantTurns, CurrentTurn));
    }

    public void BeginTurn(int turnNumber)
    {
        RequireCombat();
        ArgumentOutOfRangeException.ThrowIfLessThan(turnNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(turnNumber, CurrentTurn);
        if (turnNumber == CurrentTurn) return;
        CurrentTurn = turnNumber;
        var version = _combatVersion;
        // Take this list before executing any effect, which may append/complete chants itself.
        var existing = _charging.Where(item => item.CreatedTurn < turnNumber).ToArray();
        foreach (var item in existing)
        {
            if (!_inCombat || version != _combatVersion) break;
            var index = _charging.FindIndex(candidate => candidate.Sequence == item.Sequence);
            if (index < 0) continue;
            if (item.TurnsRemaining > 1)
                _charging[index] = item with { TurnsRemaining = item.TurnsRemaining - 1 };
            else
            {
                _charging.RemoveAt(index);
                // Consult storage after each previous effect has fully finished.
                RouteReady(item.Snapshot);
            }
        }
    }

    /// <summary>Explicit acceleration, e.g. U15. Unlike automatic advancement this may complete a fresh chant.</summary>
    public bool CompleteChant(long sequence)
    {
        RequireCombat();
        var index = _charging.FindIndex(item => item.Sequence == sequence);
        if (index < 0) return false;
        var snapshot = _charging[index].Snapshot;
        _charging.RemoveAt(index);
        RouteReady(snapshot);
        return true;
    }

    /// <returns>Number of effects actually started, stopping if the resolver ends combat.</returns>
    public int Release()
    {
        RequireCombat();
        var batch = _ready.ToArray();
        _ready.Clear();
        var started = 0;
        var version = _combatVersion;
        foreach (var snapshot in batch)
        {
            if (!_inCombat || version != _combatVersion) break;
            started++;
            _resolve(snapshot);
        }
        return started;
    }

    private void RouteReady(SpellSnapshot snapshot)
    {
        if (StorageEnabled) _ready.Add(snapshot);
        else _resolve(snapshot);
    }

    private void RequireCombat()
    {
        if (!_inCombat) throw new InvalidOperationException("Combat has ended.");
    }
}

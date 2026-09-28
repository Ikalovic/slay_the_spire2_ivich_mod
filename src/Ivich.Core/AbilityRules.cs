namespace Ivich.Core;

/// <summary>Per-turn allowances belong to the player, not an installation of a power.</summary>
public sealed class TurnTriggerLedger
{
    private bool _starUsed;
    private int _curseUsed;
    private readonly HashSet<string> _positiveKinds = [];

    public void BeginTurn() { _starUsed = false; _curseUsed = 0; _positiveKinds.Clear(); }
    public bool TryStarCurrent(bool active)
    {
        if (!active || _starUsed) return false;
        _starUsed = true;
        return true;
    }
    public bool TryCurseEcho(int allowance)
    {
        if (_curseUsed >= allowance) return false;
        _curseUsed++;
        return true;
    }
    public bool TryManyForms(string kind, bool active)
        => active && _positiveKinds.Count < 3 && _positiveKinds.Add(kind);
}

public static class AbilityRules
{
    public static int BloodForgeStrength(int overflow, int installations)
        => checked(Math.Max(0, overflow) / 2 * Math.Max(0, installations));
}

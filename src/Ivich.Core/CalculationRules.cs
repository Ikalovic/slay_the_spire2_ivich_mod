using System.Numerics;

namespace Ivich.Core;

public enum DamageKind { Physical, Magic }
public sealed record MovementResult(bool EnteredNear, bool EnteredFar);
public static class DamageRules
{
    public static int CalculateDirect(int baseDamage, DamageKind kind, int strength = 0, bool weak = false,
        int encouragement = 0, int distance = 0, decimal targetMultiplier = 1m, bool trueDamage = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseDamage);
        ArgumentOutOfRangeException.ThrowIfNegative(encouragement);
        ArgumentOutOfRangeException.ThrowIfNegative(targetMultiplier);
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        BigInteger numerator = kind == DamageKind.Physical ? Math.Max(0L, (long)baseDamage + strength) : baseDamage;
        BigInteger denominator = 1;
        if (kind == DamageKind.Physical && weak) { numerator *= 3; denominator *= 4; }
        if (kind == DamageKind.Magic) { numerator *= 5L + encouragement; denominator *= 5; }
        var absoluteDistance = Math.Abs((long)distance);
        numerator *= distance >= 0 ? 10 : 2 * absoluteDistance + 10;
        denominator *= absoluteDistance + 10;
        if (!trueDamage)
        {
            // Decimal decomposition avoids intermediate rounding even at exact integer boundaries.
            var bits = decimal.GetBits(targetMultiplier);
            var targetNumerator = (BigInteger)(uint)bits[0] + ((BigInteger)(uint)bits[1] << 32) +
                ((BigInteger)(uint)bits[2] << 64);
            numerator *= targetNumerator;
            denominator *= BigInteger.Pow(10, (bits[3] >> 16) & 0x7f);
        }
        return checked((int)(numerator / denominator));
    }

    public static MovementResult ClassifyMovement(int from, int to) => new(from >= 0 && to < 0, from <= 0 && to > 0);
}
public sealed record FreezeState(int Ice = 0, long Threshold = 10, int SkippedTurns = 0);
public sealed record FreezeResult(FreezeState State, int HealthLoss, bool Triggered);
public static class FreezeRules
{
    public static FreezeResult Apply(FreezeState state, int delta, int currentHealth) => ApplyBatch(state, [delta], currentHealth);

    /// <summary>Call once after a complete application or transfer batch, including U12's appended ice.</summary>
    public static FreezeResult ApplyBatch(FreezeState state, IEnumerable<int> deltas, int currentHealth)
    {
        Validate(state);
        ArgumentNullException.ThrowIfNull(deltas);
        ArgumentOutOfRangeException.ThrowIfNegative(currentHealth);
        long sum = 0;
        foreach (var delta in deltas) sum = checked(sum + delta);
        var ice = checked((int)Math.Max(0, checked(state.Ice + sum)));
        var updated = state with { Ice = ice };
        if (ice == state.Ice || ice < state.Threshold || currentHealth == 0)
            return new FreezeResult(updated, 0, false);
        var lost = (int)Math.Min(currentHealth, (long)currentHealth * ice / 100);
        updated = updated with
        {
            Ice = ice / 2,
            Threshold = checked(state.Threshold * 2),
            SkippedTurns = checked(state.SkippedTurns + 1)
        };
        return new FreezeResult(updated, lost, true);
    }

    public static bool TrySkipTurn(FreezeState state, out FreezeState remaining)
    {
        Validate(state);
        var skip = state.SkippedTurns > 0;
        remaining = skip ? state with { SkippedTurns = state.SkippedTurns - 1 } : state;
        return skip;
    }

    private static void Validate(FreezeState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentOutOfRangeException.ThrowIfNegative(state.Ice);
        ArgumentOutOfRangeException.ThrowIfLessThan(state.Threshold, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(state.SkippedTurns);
    }
}
public sealed record DoomResult(int MaximumHealth, int CurrentHealth, bool RequiresDeathProcessing);
public static class DoomRules
{
    public static int MaximumHealth(int normalMaximum, int stacks)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(normalMaximum, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(stacks);
        return stacks >= 100 ? 0 : (int)Math.Max(1, (long)normalMaximum * (100 - stacks) / 100);
    }

    /// <summary>At 100 stacks the adapter must invoke normal death/phase processing, not bypass it.</summary>
    public static DoomResult Evaluate(int normalMaximum, int stacks, int currentHealth)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(currentHealth);
        var maximum = MaximumHealth(normalMaximum, stacks);
        return new DoomResult(maximum, Math.Min(currentHealth, maximum), stacks >= 100);
    }
}

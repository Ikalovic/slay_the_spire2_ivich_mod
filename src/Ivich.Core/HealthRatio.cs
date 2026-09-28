namespace Ivich.Core;

public sealed record HealingResult(int Actual, int Overflow);
public sealed class DoomDeathGate
{
    private int _lastStacks = -1;
    public bool TryRequestDeath(int stacks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(stacks);
        if (stacks == _lastStacks) return false;
        _lastStacks = stacks;
        return stacks >= 100;
    }
}

public sealed class FormHealthState
{
    private readonly HealthRatio _health;
    private int _permanentBonus;
    public FormHealthState(Form form, int currentHealth, int normalMaximum)
    {
        _permanentBonus = checked(normalMaximum - BaseMaximum(form));
        Form = form;
        _health = new HealthRatio(currentHealth, normalMaximum);
    }
    public Form Form { get; private set; }
    public int CurrentHealth => _health.CurrentHealth;
    public int MaximumHealth => _health.MaximumHealth;
    public int NormalMaximum => Math.Max(1, checked(BaseMaximum(Form) + _permanentBonus));
    public int DoomStacks { get; private set; }
    public bool RequiresDeathProcessing => DoomStacks >= 100;
    private int EffectiveMaximum => DoomRules.MaximumHealth(NormalMaximum, DoomStacks);
    private static int BaseMaximum(Form form) => form switch
    {
        Form.Initial => 88,
        Form.Mage => 50,
        Form.Dragon => 100,
        _ => throw new ArgumentOutOfRangeException(nameof(form))
    };
    public void SwitchForm(Form form)
    {
        _ = BaseMaximum(form);
        Form = form;
        _health.SwitchMaximum(RequiresDeathProcessing ? NormalMaximum : EffectiveMaximum);
    }
    public void ObserveHealth(int health) => _health.RecordActualHealth(health);
    public void IncreaseMaximum(int amount)
    {
        _permanentBonus = checked(_permanentBonus + amount);
        _health.SetMaximumWithoutHealing(RequiresDeathProcessing ? NormalMaximum : EffectiveMaximum);
    }
    public void SetNormalMaximum(int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximum, 1);
        _permanentBonus = checked(maximum - BaseMaximum(Form));
        _health.SetMaximumWithoutHealing(RequiresDeathProcessing ? NormalMaximum : EffectiveMaximum);
    }
    public void SetDoom(int stacks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(stacks);
        DoomStacks = stacks;
        // The game adapter must use its regular death/phase handling at 100 stacks.
        if (!RequiresDeathProcessing) _health.SetMaximumWithoutHealing(EffectiveMaximum);
    }
}

public sealed class HealthRatio
{
    private int _numerator;
    private int _denominator;

    public HealthRatio(int currentHealth, int maximumHealth)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumHealth, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(currentHealth);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(currentHealth, maximumHealth);
        MaximumHealth = maximumHealth;
        _numerator = currentHealth;
        _denominator = maximumHealth;
    }

    // Products of two positive Int32 values fit Int64; no floating point or accumulated rounding.
    public int CurrentHealth => (int)(((long)_numerator * MaximumHealth + _denominator - 1) / _denominator);
    public int MaximumHealth { get; private set; }

    /// <summary>For form changes only; never writes the rounded display back into the fraction.</summary>
    public void SwitchMaximum(int maximumHealth)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumHealth, 1);
        MaximumHealth = maximumHealth;
    }

    /// <summary>For genuine max-HP changes such as Doom, preserving current HP up to the new cap.</summary>
    public void SetMaximumWithoutHealing(int maximumHealth)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumHealth, 1);
        if (maximumHealth == MaximumHealth) return;
        var health = Math.Min(CurrentHealth, maximumHealth);
        MaximumHealth = maximumHealth;
        SetActual(health);
    }

    public int LoseHealth(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        var actual = Math.Min(CurrentHealth, amount);
        if (actual > 0) SetActual(CurrentHealth - actual);
        return actual;
    }

    public HealingResult Heal(int amount, bool allowed = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (!allowed) return new HealingResult(0, 0);
        var actual = Math.Min(MaximumHealth - CurrentHealth, amount);
        if (actual > 0) SetActual(CurrentHealth + actual);
        return new HealingResult(actual, amount - actual);
    }

    public void RecordActualHealth(int health)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(health);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(health, MaximumHealth);
        SetActual(health);
    }

    public static int FormMaximum(Form form, int permanentBonus = 0, int doomStacks = 0)
    {
        var baseline = form switch
        {
            Form.Initial => 88,
            Form.Mage => 50,
            Form.Dragon => 100,
            _ => throw new ArgumentOutOfRangeException(nameof(form))
        };
        return DoomRules.MaximumHealth(checked(baseline + permanentBonus), doomStacks);
    }

    private void SetActual(int health)
    {
        _numerator = health;
        _denominator = MaximumHealth;
    }
}

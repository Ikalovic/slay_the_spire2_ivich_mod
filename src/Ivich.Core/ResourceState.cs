namespace Ivich.Core;

public enum Form { Initial, Mage, Dragon }
public enum PaymentFailure { None, FormUnavailable, InsufficientEnergy, InsufficientMana, InsufficientRage, InsufficientHealth }
public sealed record CardCost(int Energy = 0, int Mana = 0, int Rage = 0, int Health = 0, bool IsMagic = false, bool RequiresDragon = false);
public sealed record PaymentResult(PaymentFailure Failure, int RemainingHealth, int EnergyPaid = 0, int ManaPaid = 0, int RagePaid = 0, int HealthPaid = 0)
{
    public bool Succeeded => Failure == PaymentFailure.None;
}

public sealed class ResourceState
{
    private bool _inCombat;
    private int _lastTurn;

    public ResourceState(Form form = Form.Initial) => StartCombat(form);
    public Form Form { get; private set; }
    public int Energy { get; private set; }
    public int Mana { get; private set; }
    public int Rage { get; private set; }
    public int OddHealthLoss { get; private set; }

    public void StartCombat(Form form)
    {
        ValidateForm(form);
        Form = form;
        Energy = 3;
        Mana = form switch { Form.Initial => 3, Form.Mage => 5, _ => 0 };
        Rage = form == Form.Dragon ? 3 : 0;
        OddHealthLoss = 0;
        _lastTurn = 0;
        _inCombat = true;
    }

    public void EndCombat(Form permanentForm)
    {
        ValidateForm(permanentForm);
        Energy = Mana = Rage = OddHealthLoss = 0;
        Form = permanentForm;
        _lastTurn = 0;
        _inCombat = false;
    }

    /// <summary>Use the player's monotonically increasing turn index. Repeated hooks are idempotent.</summary>
    public void BeginTurn(int turnNumber, bool frozen = false)
    {
        RequireCombat();
        ArgumentOutOfRangeException.ThrowIfLessThan(turnNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(turnNumber, _lastTurn);
        if (turnNumber == _lastTurn) return;
        // Entry mana already includes turn one. A temporary switch never grants entry supply.
        var mana = Form == Form.Mage && turnNumber > 1 ? checked(Mana + 5) : Mana;
        if (!frozen) Energy = 3;
        Mana = mana;
        _lastTurn = turnNumber;
    }

    public int GainEnergy(int amount)
    {
        ValidateGain(amount);
        Energy = checked(Energy + amount);
        return amount;
    }

    public int GainMana(int amount)
    {
        ValidateGain(amount);
        if (Form == Form.Dragon) return 0;
        var actual = Form == Form.Initial ? Math.Min(amount, 5 - Mana) : amount;
        Mana = checked(Mana + actual);
        return actual;
    }

    public int GainRage(int amount)
    {
        ValidateGain(amount);
        if (Form != Form.Dragon) return 0;
        Rage = checked(Rage + amount);
        return amount;
    }

    /// <summary>Call exactly once per actual HP loss, including paid HP; never for max-HP clamps.</summary>
    public int RecordHealthLoss(int amount)
    {
        ValidateGain(amount);
        if (Form != Form.Dragon) return 0;
        var total = (long)amount + OddHealthLoss;
        var generated = (int)(total / 2);
        var rage = checked(Rage + generated);
        OddHealthLoss = (int)(total % 2);
        Rage = rage;
        return generated;
    }

    /// <summary>A01 only: change resource kind 1:1, with no production/payment event.</summary>
    public void SwitchForm(Form form)
    {
        RequireCombat();
        ValidateForm(form);
        if (form == Form.Initial) throw new ArgumentException("A01 cannot switch back to Initial.", nameof(form));
        if (form == Form) return;
        var inventory = Mana + Rage;
        Mana = form == Form.Dragon ? 0 : inventory;
        Rage = form == Form.Dragon ? inventory : 0;
        Form = form;
    }

    /// <summary>HP is returned for the engine to pay. Record its actual loss separately after engine settlement.</summary>
    public bool TryPay(CardCost cost, int currentHealth, out PaymentResult result)
    {
        RequireCombat();
        ArgumentNullException.ThrowIfNull(cost);
        ArgumentOutOfRangeException.ThrowIfNegative(cost.Energy);
        ArgumentOutOfRangeException.ThrowIfNegative(cost.Mana);
        ArgumentOutOfRangeException.ThrowIfNegative(cost.Rage);
        ArgumentOutOfRangeException.ThrowIfNegative(cost.Health);
        ArgumentOutOfRangeException.ThrowIfNegative(currentHealth);
        var failure = PaymentFailure.None;
        if ((cost.IsMagic || cost.Mana > 0) && Form == Form.Dragon ||
            (cost.RequiresDragon || cost.Rage > 0) && Form != Form.Dragon)
            failure = PaymentFailure.FormUnavailable;
        else if (Energy < cost.Energy) failure = PaymentFailure.InsufficientEnergy;
        else if (Mana < cost.Mana) failure = PaymentFailure.InsufficientMana;
        else if (Rage < cost.Rage) failure = PaymentFailure.InsufficientRage;
        else if (currentHealth <= cost.Health) failure = PaymentFailure.InsufficientHealth;
        if (failure != PaymentFailure.None)
        {
            result = new PaymentResult(failure, currentHealth);
            return false;
        }
        Energy -= cost.Energy;
        Mana -= cost.Mana;
        Rage -= cost.Rage;
        result = new PaymentResult(PaymentFailure.None, currentHealth - cost.Health,
            cost.Energy, cost.Mana, cost.Rage, cost.Health);
        return true;
    }

    private void ValidateGain(int amount)
    {
        RequireCombat();
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
    }

    private void RequireCombat()
    {
        if (!_inCombat) throw new InvalidOperationException("Combat has ended.");
    }

    private static void ValidateForm(Form form)
    {
        if (!Enum.IsDefined(form)) throw new ArgumentOutOfRangeException(nameof(form));
    }
}

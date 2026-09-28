namespace Ivich.Core;

public enum Inscription { BoundaryFold, ReturnLoop, Frost, Flow, BloodRepayment, SweetAftertaste }
public sealed class RunProgress
{
    private readonly List<Inscription> _inscriptions = [];
    public Form PermanentForm { get; private set; } = Form.Initial;
    public long ManaSpent { get; private set; }
    public long EnergySpent { get; private set; }
    public long HealthLost { get; private set; }
    public IReadOnlyList<Inscription> Inscriptions => _inscriptions.AsReadOnly();

    /// <summary>Records spending only. Report actual HP loss separately once the engine settles it.</summary>
    public void RecordPayment(PaymentResult payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        if (!payment.Succeeded) return;
        ArgumentOutOfRangeException.ThrowIfNegative(payment.ManaPaid);
        ArgumentOutOfRangeException.ThrowIfNegative(payment.EnergyPaid);
        var mana = checked(ManaSpent + payment.ManaPaid);
        var energy = checked(EnergySpent + payment.EnergyPaid);
        ManaSpent = mana;
        EnergySpent = energy;
    }

    public void RecordHealthLoss(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        HealthLost = checked(HealthLost + amount);
    }

    /// <summary>The adapter must offer this as one campfire action outside combat.</summary>
    public bool TryAdvance(Form form, Inscription firstInscription)
    {
        if (!Enum.IsDefined(form)) throw new ArgumentOutOfRangeException(nameof(form));
        ValidateInscription(firstInscription);
        if (PermanentForm != Form.Initial) return false;
        var qualifies = form switch
        {
            Form.Mage => ManaSpent >= 50,
            Form.Dragon => HealthLost >= 50 && EnergySpent >= 50,
            _ => false
        };
        if (!qualifies) return false;
        PermanentForm = form;
        _inscriptions.Add(firstInscription);
        return true;
    }

    /// <summary>The second distinct inscription consumes a later campfire action.</summary>
    public bool TryAddInscription(Inscription inscription)
    {
        ValidateInscription(inscription);
        if (PermanentForm == Form.Initial || _inscriptions.Count >= 2 || _inscriptions.Contains(inscription)) return false;
        _inscriptions.Add(inscription);
        return true;
    }

    private static void ValidateInscription(Inscription inscription)
    {
        if (!Enum.IsDefined(inscription)) throw new ArgumentOutOfRangeException(nameof(inscription));
    }
}

using Ivich.Core;

internal static partial class TestRunner
{
    private static void ProgressTests()
    {
        Test("mage advancement requires actual successful fifty mana payment", () =>
        {
            var run = new RunProgress();
            run.RecordPayment(new PaymentResult(PaymentFailure.InsufficientEnergy, 88, ManaPaid: 100));
            Equal(0L, run.ManaSpent);
            run.RecordPayment(new PaymentResult(PaymentFailure.None, 88, ManaPaid: 49));
            False(run.TryAdvance(Form.Mage, Inscription.Flow));
            run.RecordPayment(new PaymentResult(PaymentFailure.None, 88, ManaPaid: 1));
            True(run.TryAdvance(Form.Mage, Inscription.Flow)); Equal(Form.Mage, run.PermanentForm);
            Equal(1, run.Inscriptions.Count);
            False(run.TryAdvance(Form.Dragon, Inscription.Frost));
        });
        Test("dragon advancement independently requires fifty energy and actual HP loss", () =>
        {
            var run = new RunProgress();
            run.RecordPayment(new PaymentResult(PaymentFailure.None, 38, EnergyPaid: 50, HealthPaid: 50));
            False(run.TryAdvance(Form.Dragon, Inscription.BloodRepayment));
            run.RecordHealthLoss(49); False(run.TryAdvance(Form.Dragon, Inscription.BloodRepayment));
            run.RecordHealthLoss(1); True(run.TryAdvance(Form.Dragon, Inscription.BloodRepayment));
            Equal(50L, run.HealthLost); Equal(50L, run.EnergySpent);
        });
        Test("inscriptions require advancement and permit two distinct choices", () =>
        {
            var run = new RunProgress(); False(run.TryAddInscription(Inscription.Frost));
            run.RecordPayment(new PaymentResult(PaymentFailure.None, 88, ManaPaid: 50));
            True(run.TryAdvance(Form.Mage, Inscription.Frost));
            False(run.TryAddInscription(Inscription.Frost));
            True(run.TryAddInscription(Inscription.ReturnLoop));
            False(run.TryAddInscription(Inscription.BoundaryFold)); Equal(2, run.Inscriptions.Count);
        });
    }
}

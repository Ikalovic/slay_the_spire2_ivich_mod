using Ivich.Core;

internal static partial class TestRunner
{
    private static void CalculationTests()
    {
        Test("absolute maximum changes and flat gains use the unsuppressed doom baseline", () =>
        {
            var absolute = new FormHealthState(Form.Dragon, 90, 100);
            absolute.SetDoom(10); Equal(90, absolute.MaximumHealth);
            absolute.SetNormalMaximum(200); Equal(200, absolute.NormalMaximum); Equal(180, absolute.MaximumHealth);
            Equal(90, absolute.CurrentHealth);
            var flat = new FormHealthState(Form.Dragon, 90, 100);
            flat.SetDoom(10); flat.IncreaseMaximum(5);
            Equal(105, flat.NormalMaximum); Equal(94, flat.MaximumHealth); Equal(90, flat.CurrentHealth);
            flat.IncreaseMaximum(-5); Equal(100, flat.NormalMaximum); Equal(90, flat.MaximumHealth);
            absolute.SetDoom(99); absolute.SetDoom(100);
            absolute.SetNormalMaximum(300); Equal(300, absolute.MaximumHealth);
            absolute.ObserveHealth(300); // Native death prevention may install a fresh phase maximum and HP.
            absolute.SetDoom(0); Equal(300, absolute.CurrentHealth);
        });
        Test("duplicate doom callbacks cannot consume two death-prevention phases", () =>
        {
            var death = new DoomDeathGate();
            True(death.TryRequestDeath(100)); False(death.TryRequestDeath(100));
            True(death.TryRequestDeath(101)); False(death.TryRequestDeath(101));
            False(death.TryRequestDeath(25)); True(death.TryRequestDeath(100));
            Throws<ArgumentOutOfRangeException>(() => death.TryRequestDeath(-1));
        });
        Test("form health retains permanent gains through doom and combat restoration", () =>
        {
            var health = new FormHealthState(Form.Mage, 30, 50);
            health.IncreaseMaximum(3);
            Equal(30, health.CurrentHealth); Equal(53, health.NormalMaximum);
            health.SwitchForm(Form.Dragon);
            Equal(59, health.CurrentHealth); Equal(103, health.MaximumHealth);
            health.SetDoom(25);
            Equal(77, health.MaximumHealth); Equal(59, health.CurrentHealth);
            health.SwitchForm(Form.Mage);
            Equal(30, health.CurrentHealth); Equal(39, health.MaximumHealth);
            health.SetDoom(0);
            Equal(53, health.MaximumHealth); Equal(30, health.CurrentHealth);
        });
        Test("form health observes real damage and healing between temporary switches", () =>
        {
            var health = new FormHealthState(Form.Dragon, 1, 100);
            health.SwitchForm(Form.Mage); Equal(1, health.CurrentHealth);
            health.ObserveHealth(2); // Real healing establishes 2/50, even though entry was 1/100.
            health.ObserveHealth(1); // A later real loss establishes 1/50.
            health.SwitchForm(Form.Dragon); Equal(2, health.CurrentHealth);
        });
        Test("form health roundtrips preserve sub-display fractions with doom", () =>
        {
            var health = new FormHealthState(Form.Dragon, 1, 100);
            health.SetDoom(25);
            for (var index = 0; index < 100; index++)
            {
                health.SwitchForm(Form.Mage); Equal(1, health.CurrentHealth); Equal(37, health.MaximumHealth);
                health.SwitchForm(Form.Dragon); Equal(1, health.CurrentHealth); Equal(75, health.MaximumHealth);
            }
            health.IncreaseMaximum(3); Equal(77, health.MaximumHealth); Equal(1, health.CurrentHealth);
            health.SetDoom(0); Equal(103, health.MaximumHealth); Equal(1, health.CurrentHealth);
        });
        Test("one hundred doom requests normal death instead of bypassing it", () =>
        {
            var health = new FormHealthState(Form.Initial, 88, 88);
            health.SetDoom(100); True(health.RequiresDeathProcessing);
            Equal(88, health.CurrentHealth); // The adapter must call the engine's death processing.
            health.ObserveHealth(0); health.SetDoom(0); Equal(0, health.CurrentHealth);
        });
        Test("low-HP repeated switches preserve the exact fraction", () =>
        {
            var hp = new HealthRatio(1, 100);
            for (var i = 0; i < 1000; i++)
            {
                hp.SwitchMaximum(50); Equal(1, hp.CurrentHealth);
                hp.SwitchMaximum(100); Equal(1, hp.CurrentHealth);
            }
        });
        Test("all initial life totals survive complete form roundtrips", () =>
        {
            for (var current = 0; current <= 88; current++)
            {
                var hp = new HealthRatio(current, 88);
                hp.SwitchMaximum(50); hp.SwitchMaximum(100); hp.SwitchMaximum(88);
                Equal(current, hp.CurrentHealth);
            }
        });
        Test("real healing establishes a new exact ratio", () =>
        {
            var hp = new HealthRatio(60, 100); hp.SwitchMaximum(50);
            Equal(30, hp.CurrentHealth); Equal(new HealingResult(5, 0), hp.Heal(5));
            hp.SwitchMaximum(100); Equal(70, hp.CurrentHealth);
        });
        Test("damage clamps to available HP and death stays zero on switch", () =>
        {
            var hp = new HealthRatio(1, 100); hp.SwitchMaximum(50);
            Equal(1, hp.LoseHealth(5)); Equal(0, hp.CurrentHealth);
            hp.SwitchMaximum(100); Equal(0, hp.CurrentHealth);
        });
        Test("healing separates actual and overflow and blocked healing gives neither", () =>
        {
            var hp = new HealthRatio(47, 50);
            Equal(new HealingResult(3, 7), hp.Heal(10));
            Equal(new HealingResult(0, 10), hp.Heal(10));
            Equal(new HealingResult(0, 0), hp.Heal(10, allowed: false));
        });
        Test("zero changes cannot turn display rounding into healing", () =>
        {
            var hp = new HealthRatio(1, 100); hp.SwitchMaximum(50);
            hp.Heal(0); hp.LoseHealth(0); hp.SwitchMaximum(100);
            Equal(1, hp.CurrentHealth);
        });
        Test("ordinary maximum changes clamp without healing", () =>
        {
            var hp = new HealthRatio(90, 100); hp.SetMaximumWithoutHealing(75);
            Equal(75, hp.CurrentHealth); hp.SetMaximumWithoutHealing(100);
            Equal(75, hp.CurrentHealth);
            Equal(53, HealthRatio.FormMaximum(Form.Mage, 3));
            Equal(77, HealthRatio.FormMaximum(Form.Dragon, 3, 25));
        });
        Test("recalculating an unchanged maximum cannot write back display rounding", () =>
        {
            var hp = new HealthRatio(1, 100); hp.SwitchMaximum(50);
            hp.SetMaximumWithoutHealing(50); hp.SwitchMaximum(100);
            Equal(1, hp.CurrentHealth);
        });
        Test("distance modifies each hit with exact final floor", () =>
        {
            Equal(4, DamageRules.CalculateDirect(12, DamageKind.Physical, distance: 20));
            Equal(18, DamageRules.CalculateDirect(12, DamageKind.Physical, distance: -10));
            Equal(7, DamageRules.CalculateDirect(7, DamageKind.Physical));
            Equal(0, DamageRules.CalculateDirect(12, DamageKind.Physical, strength: -20));
        });
        Test("physical weakness and magic encouragement have separate rules", () =>
        {
            Equal(10, DamageRules.CalculateDirect(10, DamageKind.Physical, strength: 4, weak: true, encouragement: 10));
            Equal(18, DamageRules.CalculateDirect(10, DamageKind.Magic, strength: 100, weak: true, encouragement: 4));
            Equal(13, DamageRules.CalculateDirect(11, DamageKind.Magic, encouragement: 1));
        });
        Test("target multipliers apply before floor and true damage bypasses them", () =>
        {
            Equal(8, DamageRules.CalculateDirect(5, DamageKind.Physical, distance: -10, targetMultiplier: 1.1m));
            Equal(7, DamageRules.CalculateDirect(5, DamageKind.Physical, distance: -10, targetMultiplier: 0m, trueDamage: true));
            Equal(0, DamageRules.CalculateDirect(5, DamageKind.Physical, targetMultiplier: 0m));
        });
        Test("distance signs use only start and end of one movement", () =>
        {
            Equal(new MovementResult(true, false), DamageRules.ClassifyMovement(0, -5));
            Equal(new MovementResult(false, true), DamageRules.ClassifyMovement(-5, 5));
            Equal(new MovementResult(false, false), DamageRules.ClassifyMovement(-5, 0));
            Equal(new MovementResult(false, false), DamageRules.ClassifyMovement(2, 8));
        });
        Test("freeze loses percentage HP then halves ice and doubles local threshold", () =>
        {
            var first = FreezeRules.Apply(new FreezeState(), 10, 90);
            True(first.Triggered); Equal(9, first.HealthLoss);
            Equal(5, first.State.Ice); Equal(20L, first.State.Threshold); Equal(1, first.State.SkippedTurns);
            var second = FreezeRules.Apply(first.State, 15, 81);
            Equal(16, second.HealthLoss); Equal(10, second.State.Ice);
            Equal(40L, second.State.Threshold); Equal(2, second.State.SkippedTurns);
        });
        Test("a large ice application freezes once and zero application does not retrigger", () =>
        {
            var first = FreezeRules.Apply(new FreezeState(), 1000, 90);
            Equal(90, first.HealthLoss); Equal(500, first.State.Ice);
            Equal(20L, first.State.Threshold); Equal(1, first.State.SkippedTurns);
            var noEvent = FreezeRules.Apply(first.State, 0, 90);
            False(noEvent.Triggered); Equal(first.State, noEvent.State);
        });
        Test("ice removal preserves threshold and freeze batch checks only once", () =>
        {
            var state = new FreezeState(15, 20, 2);
            var removed = FreezeRules.Apply(state, -15, 90);
            Equal(0, removed.State.Ice); Equal(20L, removed.State.Threshold);
            var added = FreezeRules.ApplyBatch(removed.State, new[] { 10, 10, 20 }, 90);
            Equal(36, added.HealthLoss); Equal(20, added.State.Ice); Equal(40L, added.State.Threshold);
            True(FreezeRules.TrySkipTurn(added.State, out var afterSkip)); Equal(2, afterSkip.SkippedTurns);
            False(FreezeRules.TrySkipTurn(new FreezeState(), out _));
        });
        Test("doom always uses normal baseline and removal does not heal", () =>
        {
            var first = DoomRules.Evaluate(100, 25, 90);
            Equal(75, first.MaximumHealth); Equal(75, first.CurrentHealth); False(first.RequiresDeathProcessing);
            var second = DoomRules.Evaluate(100, 30, first.CurrentHealth);
            Equal(70, second.MaximumHealth); Equal(70, second.CurrentHealth);
            var removed = DoomRules.Evaluate(100, 0, second.CurrentHealth);
            Equal(100, removed.MaximumHealth); Equal(70, removed.CurrentHealth);
            Equal(1, DoomRules.MaximumHealth(3, 99));
            True(DoomRules.Evaluate(100, 100, 100).RequiresDeathProcessing);
        });
        Test("invalid health, damage and state inputs fail explicitly", () =>
        {
            Throws<ArgumentOutOfRangeException>(() => new HealthRatio(3, 2));
            Throws<ArgumentOutOfRangeException>(() => new HealthRatio(0, 0));
            Throws<ArgumentOutOfRangeException>(() => DamageRules.CalculateDirect(5, DamageKind.Magic, encouragement: -1));
            Throws<ArgumentOutOfRangeException>(() => DoomRules.MaximumHealth(100, -1));
            Throws<ArgumentOutOfRangeException>(() => FreezeRules.Apply(new FreezeState(0, 0), 1, 100));
        });
    }
}

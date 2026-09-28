using Ivich.Core;

internal static partial class TestRunner
{
    private static void ResourceTests()
    {
        Test("initial resources reject rage and cap mana", () =>
        {
            var state = new ResourceState();
            Equal(3, state.Energy); Equal(3, state.Mana); Equal(0, state.Rage);
            Equal(2, state.GainMana(20)); Equal(0, state.GainRage(20));
            Equal(5, state.Mana); Equal(0, state.Rage);
            state.BeginTurn(1); state.BeginTurn(2); Equal(5, state.Mana);
        });
        Test("mage first turn has one supply and later turns add five", () =>
        {
            var state = new ResourceState(Form.Mage);
            Equal(5, state.Mana); state.BeginTurn(1); Equal(5, state.Mana);
            state.BeginTurn(1); Equal(5, state.Mana);
            state.BeginTurn(2); Equal(10, state.Mana);
            Equal(20, state.GainMana(20)); Equal(30, state.Mana);
        });
        Test("energy can exceed three but normally refreshes", () =>
        {
            var state = new ResourceState(); state.BeginTurn(1);
            Equal(5, state.GainEnergy(5)); Equal(8, state.Energy);
            state.BeginTurn(2); Equal(3, state.Energy);
        });
        Test("frozen mage still receives mana but retains current energy", () =>
        {
            var state = new ResourceState(Form.Mage); state.BeginTurn(1);
            True(state.TryPay(new CardCost(Energy: 2), 50, out _));
            state.BeginTurn(2, frozen: true); Equal(1, state.Energy); Equal(10, state.Mana);
        });
        Test("dragon accumulates odd losses across temporary forms", () =>
        {
            var state = new ResourceState(Form.Dragon); state.BeginTurn(1);
            Equal(0, state.GainMana(4)); Equal(0, state.RecordHealthLoss(1));
            state.SwitchForm(Form.Mage); Equal(0, state.RecordHealthLoss(5));
            state.SwitchForm(Form.Dragon); Equal(2, state.RecordHealthLoss(3));
            Equal(5, state.Rage); Equal(0, state.OddHealthLoss);
            state.BeginTurn(2); Equal(5, state.Rage);
        });
        Test("conversion preserves inventory without supplies or energy refresh", () =>
        {
            var state = new ResourceState(); state.BeginTurn(1); state.GainMana(2);
            True(state.TryPay(new CardCost(Energy: 2), 88, out _));
            state.SwitchForm(Form.Dragon); Equal(5, state.Rage); Equal(0, state.Mana);
            True(state.TryPay(new CardCost(Rage: 2, RequiresDragon: true), 88, out _));
            state.SwitchForm(Form.Mage); Equal(3, state.Mana); Equal(0, state.Rage);
            Equal(1, state.Energy);
            True(state.TryPay(new CardCost(Mana: 3), 88, out _));
            for (var i = 0; i < 20; i++) { state.SwitchForm(Form.Dragon); state.SwitchForm(Form.Mage); }
            Equal(0, state.Mana); Equal(0, state.Rage);
        });
        Test("all costs are checked before any payment", () =>
        {
            var state = new ResourceState();
            False(state.TryPay(new CardCost(Energy: 2, Mana: 4, Health: 5), 20, out var failed));
            Equal(PaymentFailure.InsufficientMana, failed.Failure);
            Equal(3, state.Energy); Equal(3, state.Mana); Equal(20, failed.RemainingHealth);
            True(state.TryPay(new CardCost(Energy: 2, Mana: 3, Health: 5), 20, out var paid));
            Equal(1, state.Energy); Equal(0, state.Mana); Equal(15, paid.RemainingHealth);
            Equal(2, paid.EnergyPaid); Equal(3, paid.ManaPaid); Equal(5, paid.HealthPaid);
        });
        Test("health cost must leave one HP and cannot finance rage cost", () =>
        {
            var state = new ResourceState(Form.Dragon);
            False(state.TryPay(new CardCost(Energy: 1, Health: 3), 3, out _)); Equal(3, state.Energy);
            False(state.TryPay(new CardCost(Rage: 4, Health: 2), 10, out _)); Equal(3, state.Rage);
            True(state.TryPay(new CardCost(Rage: 3, Health: 2), 3, out var paid));
            Equal(1, paid.RemainingHealth); Equal(0, state.Rage);
            Equal(1, state.RecordHealthLoss(paid.HealthPaid)); Equal(1, state.Rage);
        });
        Test("free magic and discounted required rage retain form gates", () =>
        {
            var state = new ResourceState(Form.Dragon);
            False(state.TryPay(new CardCost(IsMagic: true), 20, out _));
            True(state.TryPay(new CardCost(RequiresDragon: true), 20, out _));
            state.SwitchForm(Form.Mage);
            False(state.TryPay(new CardCost(RequiresDragon: true), 20, out _));
            False(state.TryPay(new CardCost(Rage: 1), 20, out _));
            True(state.TryPay(new CardCost(IsMagic: true), 20, out _));
        });
        Test("battle cleanup clears resources and odd loss without conversion", () =>
        {
            var state = new ResourceState(Form.Dragon); state.RecordHealthLoss(1);
            state.EndCombat(Form.Initial); Equal(Form.Initial, state.Form);
            Equal(0, state.Mana); Equal(0, state.Rage); Equal(0, state.OddHealthLoss);
            Throws<InvalidOperationException>(() => state.GainMana(1));
            state.StartCombat(Form.Dragon); Equal(3, state.Rage); Equal(0, state.OddHealthLoss);
        });
        Test("negative resources and backward turns are rejected", () =>
        {
            var state = new ResourceState(); state.BeginTurn(2);
            Throws<ArgumentOutOfRangeException>(() => state.GainMana(-1));
            Throws<ArgumentOutOfRangeException>(() => state.RecordHealthLoss(-1));
            Throws<ArgumentOutOfRangeException>(() => state.TryPay(new CardCost(Energy: -1), 50, out _));
            Throws<ArgumentOutOfRangeException>(() => state.BeginTurn(1));
            Throws<ArgumentException>(() => state.SwitchForm(Form.Initial));
        });
    }
}

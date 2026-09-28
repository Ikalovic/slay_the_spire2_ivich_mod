using BaseLib.Abstracts;
using Ivich.Mod.Cards;
using Ivich.Mod.Mechanics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
namespace Ivich.Mod.Powers;

public abstract class IvichPower : CustomPowerModel
{
    public override PowerStackType StackType => PowerStackType.Counter;
    public override string CustomPackedIconPath => ModelDb.Power<FocusPower>().PackedIconPath;
    public override string CustomBigIconPath => ModelDb.Power<FocusPower>().ResolvedBigIconPath;
}
public sealed class DistancePower : IvichPower
{
    public override PowerType Type => PowerType.Buff;
    public override bool AllowNegative => true;
    public override List<(string, string)> Localization => new PowerLoc("距离", "正距离降低双方直接伤害；负距离提高双方直接伤害。跨回合保留。", "当前距离：{Amount}。正距离倍率10/(10+距离)，负距离倍率1+|距离|/(10+|距离|)。");
    public static decimal Multiplier(int distance) => distance >= 0 ? 10m / (10m + distance) : 1m + -(decimal)distance / (10m - (decimal)distance);
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!props.HasFlag(ValueProp.Move) || (dealer != Owner && target != Owner)) return 1m;
        int distance = dealer == Owner && DamagePatches.CurrentSnapshot.Value is { } snapshot && snapshot.Card == cardSource ? snapshot.Distance : Amount;
        return Multiplier(distance);
    }
}
public sealed class InvigorationPower : IvichPower
{
    public override PowerType Type => PowerType.Buff;
    public override List<(string, string)> Localization => new PowerLoc("振奋", "每层使魔法伤害增加20%。", "当前{Amount}层，每层使魔法伤害增加20%。");
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (dealer != Owner || cardSource is not IvichCard { IsMagic: true }) return 1m;
        int stacks = DamagePatches.CurrentSnapshot.Value is { } snapshot && snapshot.Card == cardSource ? snapshot.Invigoration : Amount;
        return 1m + .2m * stacks;
    }
}
public sealed class FrostPower : IvichPower
{
    public override PowerType Type => PowerType.Debuff;
    public override List<(string, string)> Localization => new PowerLoc("冰冻", "初始阈值10。达标扣除当前生命×层数%的生命，冰冻减半，阈值翻倍，跳过下个行动回合。", "当前冰冻{Amount}。冻结后层数减半，下一阈值翻倍。");
}
public sealed class FrozenTurnPower : IvichPower
{
    private bool _skipping;
    public override PowerType Type => PowerType.Debuff;
    public override List<(string, string)> Localization => new PowerLoc("冻结", "跳过接下来的行动回合。", "还需跳过{Amount}个行动回合。");
    public override async Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side, IReadOnlyList<Creature> participants, ICombatState state)
    {
        if (!participants.Contains(Owner) || Amount <= 0) return;
        _skipping = true;
        if (Owner.IsMonster) await CreatureCmd.Stun(Owner);
    }
    public override bool ShouldDraw(Player player, bool fromHandDraw) => !(player.Creature == Owner && _skipping && fromHandDraw);
    public override bool ShouldPlayerResetEnergy(Player player) => !(player.Creature == Owner && _skipping);
    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType) => !(card.Owner.Creature == Owner && _skipping);
    public override Task AfterPlayerTurnStartLate(PlayerChoiceContext context, Player player)
    {
        if (_skipping && player.Creature == Owner && Owner.IsAlive)
            PlayerCmd.EndTurn(player, canBackOut: false);
        return Task.CompletedTask;
    }
    public override async Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!_skipping || !participants.Contains(Owner)) return;
        _skipping = false;
        await PowerCmd.Decrement(this);
    }
}
public sealed class DoomPower : IvichPower
{
    public override PowerType Type => PowerType.Debuff;
    public override List<(string, string)> Localization => new PowerLoc("终焉", "每层降低1%生命上限；100层进入正常死亡流程。移除仅恢复上限。", "生命上限降低{Amount}%。");
    public override Task AfterApplied(Creature? applier, CardModel? cardSource) => FormRuntime.ApplyDoom(Owner, Amount);
    public override Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
        => power == this ? FormRuntime.ApplyDoom(Owner, Amount) : Task.CompletedTask;
    public override Task AfterRemoved(Creature oldOwner) => FormRuntime.RemoveDoom(oldOwner);
}

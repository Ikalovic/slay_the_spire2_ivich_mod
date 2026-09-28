using BaseLib.Abstracts;
using BaseLib.Hooks;
using BaseLib.Utils;
using Ivich.Mod.Cards;
using Ivich.Mod.Assets;
using Ivich.Mod.Character;
using Ivich.Mod.Mechanics;
using Ivich.Mod.Resources;
using Ivich.Mod.Powers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using Ivich.Mod.RestSites;
using Ivich.Mod.Rewards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
namespace Ivich.Mod.Relics;

[Pool(typeof(IvichRelicPool))]
public sealed class MoonEyeScythe : CustomRelicModel, IAfterSpendResource<ManaResource>, IAfterSpendResource<RageResource>
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    public override string PackedIconPath => ArtPaths.Available(ArtPaths.Scythe, ModelDb.Relic<BurningBlood>().PackedIconPath);
    protected override string PackedIconOutlinePath => ArtPaths.Available(ArtPaths.Scythe, "res://images/atlases/relic_outline_atlas.sprites/burning_blood.tres");
    protected override string BigIconPath => ArtPaths.Available(ArtPaths.Scythe, "res://images/relics/burning_blood.png");
    public override List<(string, string)> Localization => new RelicLoc("月眼之镰", "记录依维希的形态与本局成长。初始形态每战获得3魔力，容量5，不自动回魔。\n累计支付50魔力可在营火进阶魔法使；累计失血50且支付50能量可进阶半龙。", "银白的镰刃之中，一只眼睛静静注视。");
    [SavedProperty] public int FormValue { get; set; }
    [SavedProperty] public int ManaSpent { get; set; }
    [SavedProperty] public int EnergySpent { get; set; }
    [SavedProperty] public int HealthLost { get; set; }
    [SavedProperty] public int FirstInscription { get; set; }
    [SavedProperty] public int SecondInscription { get; set; }
    [SavedProperty] public int AdvancementFloor { get; set; }
    [SavedProperty] public int AncientFormUnlocked { get; set; }
    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            if (!IsMutable) return [];
            string form = IvichRuntime.GetForm(Owner) switch { Ivich.Core.Form.Mage => "魔法使", Ivich.Core.Form.Dragon => "半龙", _ => "初始" };
            string Name(int id) => id switch { 1 => "折界", 2 => "回环", 3 => "凝霜", 4 => "引流", 5 => "血偿", 6 => "余甘", _ => "未选择" };
            return [new HoverTip(Title, $"当前形态：{form}\n本局支付魔力：{ManaSpent}/50\n本局支付能量：{EnergySpent}/50\n本局实际失血：{HealthLost}/50\n镰刀铭刻：{Name(FirstInscription)} / {Name(SecondInscription)}")];
        }
    }
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (dealer == Owner.Creature && props.HasFlag(ValueProp.Move) && cardSource is IvichCard { IsMagic: false }
           && DamagePatches.CurrentSnapshot.Value is { } snapshot && snapshot.Card == cardSource && !dealer.HasPower<StrengthPower>()) return snapshot.Strength;
        return 0;
    }
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (dealer != Owner.Creature || !props.HasFlag(ValueProp.Move) || DamagePatches.CurrentSnapshot.Value is not { } snapshot || snapshot.Card != cardSource) return 1;
        decimal result = 1;
        if (!dealer.HasPower<DistancePower>()) result *= DistancePower.Multiplier(snapshot.Distance);
        if (cardSource is IvichCard { IsMagic: true } && !dealer.HasPower<InvigorationPower>()) result *= 1m + .2m * snapshot.Invigoration;
        if (cardSource is IvichCard { IsMagic: false } && !dealer.HasPower<WeakPower>()) result *= snapshot.WeakMultiplier;
        return result;
    }
    public override Task BeforeCombatStart() { IvichRuntime.ResetCombat(Owner); FormRuntime.BeginCombat(Owner); return Task.CompletedTask; }
    public override async Task BeforeSideTurnStart(PlayerChoiceContext ctx, CombatSide side, IReadOnlyList<Creature> participants, ICombatState state)
    { if (participants.Contains(Owner.Creature)) await IvichRuntime.BeginTurn(Owner, ctx); }
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext ctx, Player player)
    {
        if (player != Owner) return;
        await AbilityRuntime.AfterTurnStart(ctx, player);
        await SpellRuntime.BeginTurn(ctx, player);
    }
    public override async Task AfterSideTurnEndLate(PlayerChoiceContext ctx, CombatSide side, IEnumerable<Creature> participants)
    { if (participants.Contains(Owner.Creature)) await IvichRuntime.EndTurn(Owner, ctx); }
    public override Task AfterCardPlayed(PlayerChoiceContext ctx, CardPlay play)
    {
        if (play.Card.Owner == Owner && play.Card is IvichCard { IsScythe: true }) IvichRuntime.State(Owner).ScythePlayed = true;
        return Task.CompletedTask;
    }
    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        if (card.Owner != Owner || card is not IvichCard ivich) return true;
        if (ivich.IsMagic && IvichRuntime.IsDragon(Owner)) return false;
        if (ivich.NativeRageCost != 0 && !IvichRuntime.IsDragon(Owner)) return false;
        if (Owner.Creature.CurrentHp <= ivich.LifeCost) return false;
        if (!SpellRuntime.ShouldPlay(card)) return false;
        return ivich.DesignId is not ("C18" or "C24" or "U15") || PileType.Hand.GetPile(Owner).Cards.Any(c => c != card);
    }
    public override Task AfterEnergySpent(CardModel card, int amount)
    { if (card.Owner == Owner) EnergySpent += Math.Max(0, amount); return Task.CompletedTask; }
    public async Task AfterSpendResource(ICombatState state, ManaResource resource, AbstractModel? spender, int amount)
    {
        if (resource.Owner != Owner) return;
        ManaSpent += Math.Max(0, amount);
        if (spender is IvichCard card && amount > 0)
            await AbilityRuntime.OnPaidPlay(new BlockingPlayerChoiceContext(), card, amount, 0);
    }
    public async Task AfterSpendResource(ICombatState state, RageResource resource, AbstractModel? spender, int amount)
    {
        if (resource.Owner == Owner && spender is IvichCard card && amount > 0)
            await AbilityRuntime.OnPaidPlay(new BlockingPlayerChoiceContext(), card, 0, amount);
    }
    public override Task AfterCardExhausted(PlayerChoiceContext ctx, CardModel card, bool causedByEthereal)
        => card.Owner == Owner ? AbilityRuntime.AfterExhaust(ctx, Owner, card) : Task.CompletedTask;
    public override Task AfterPowerAmountChanged(PlayerChoiceContext ctx, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
        => AbilityRuntime.AfterPositiveIncrease(ctx, Owner, power, amount);
    public override async Task AfterDamageReceived(PlayerChoiceContext ctx, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? card)
    {
        if (target != Owner.Creature || result.UnblockedDamage <= 0) return;
        HealthLost += result.UnblockedDamage;
        var s = IvichRuntime.State(Owner); s.LostLife = true;
        if (IvichRuntime.IsDragon(Owner)) { int total = s.RageRemainder + result.UnblockedDamage; s.RageRemainder = total % 2; await IvichRuntime.GainRage(Owner, total / 2); }
        await AbilityRuntime.AfterLostLife(ctx, Owner);
    }
    public override async Task AfterCombatEnd(CombatRoom room)
    {
        if (Owner.PlayerCombatState is { } pcs) { CustomResources<ManaResource>.Get(pcs).Amount = 0; CustomResources<RageResource>.Get(pcs).Amount = 0; }
        await FormRuntime.EndCombat(Owner);
        IvichRuntime.ResetCombat(Owner);
    }
    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
        => player == Owner && RestSiteIntegration.AddOptions(player, options);
    public override IEnumerable<CardModel> ModifyMerchantCardPool(Player player, IEnumerable<CardModel> options)
        => player == Owner ? options.Where(c => IvichRewardFilter.IsCompatible(player, c)) : options;
    public override CardCreationOptions ModifyCardRewardCreationOptions(Player player, CardCreationOptions options)
        => IvichRewardFilter.Apply(Owner, player, options);
}

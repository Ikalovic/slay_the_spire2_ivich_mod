using BaseLib.Abstracts;
using Godot;
using Ivich.Core;
using Ivich.Mod.Character;
using Ivich.Mod.Mechanics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
namespace Ivich.Mod.Resources;

public sealed class ManaResource() : BasicCustomResource("IVICH_MANA")
{
    public override bool ApplySharedModification => false;
    public override Color MainColor => new("79bbff");
    public override string TexturePath => ModelDb.Power<FocusPower>().PackedIconPath;
    public override string IconPath => TexturePath;
    public override HoverTip? MakeTip() => ResourceTips.Make("IVICH_MANA", "魔力", "初始形态每战3魔力，容量5，无自动恢复。魔法使每回合开始获得5魔力，无上限。半龙无法持有或支付魔力。");
    public override int Amount { get => base.Amount; set => base.Amount = Owner is null || IvichRuntime.IsDragon(Owner) ? 0 : Math.Clamp(value, 0, IvichRuntime.GetForm(Owner) == Form.Initial ? 5 : int.MaxValue); }
    public override void PrepForCombat<T>(PlayerCombatState state) { base.PrepForCombat<T>(state); Amount = Owner?.Character is IvichCharacter && IvichRuntime.GetForm(Owner) == Form.Initial ? 3 : 0; }
    public override bool ShouldShowDisplay() => Owner?.Character is IvichCharacter && !IvichRuntime.IsDragon(Owner);
    public override bool CanAfford(CardModel card, int cost) => Owner is not null && !IvichRuntime.IsDragon(Owner) && base.CanAfford(card, cost);
}
public sealed class RageResource() : BasicCustomResource("IVICH_RAGE")
{
    public override bool ApplySharedModification => false;
    public override Color MainColor => new("e98c9f");
    public override string TexturePath => ModelDb.Power<StrengthPower>().PackedIconPath;
    public override string IconPath => TexturePath;
    public override HoverTip? MakeTip() => ResourceTips.Make("IVICH_RAGE", "怒气", "仅半龙可以持有。每战开始获得3怒气，每实际失血2点获得1怒气，奇数余量在本场累计。跨回合保留。");
    public override int Amount { get => base.Amount; set => base.Amount = Owner is not null && IvichRuntime.IsDragon(Owner) ? Math.Max(0, value) : 0; }
    public override void PrepForCombat<T>(PlayerCombatState state) { base.PrepForCombat<T>(state); Amount = Owner?.Character is IvichCharacter && IvichRuntime.IsDragon(Owner) ? 3 : 0; }
    public override bool ShouldShowDisplay() => Owner?.Character is IvichCharacter && IvichRuntime.IsDragon(Owner);
    public override bool CanAfford(CardModel card, int cost) => Owner is not null && IvichRuntime.IsDragon(Owner) && base.CanAfford(card, cost);
}

internal static class ResourceTips
{
    public static HoverTip Make(string id, string title, string description)
    {
        LocManager.Instance.GetTable("static_hover_tips").MergeWith(new Dictionary<string, string> { [id + ".title"] = title, [id + ".description"] = description });
        return new HoverTip(new LocString("static_hover_tips", id + ".title"), new LocString("static_hover_tips", id + ".description"));
    }
}

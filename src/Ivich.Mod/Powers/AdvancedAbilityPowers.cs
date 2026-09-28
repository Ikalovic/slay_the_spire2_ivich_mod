using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace Ivich.Mod.Powers;

/// <summary>Each installation keeps its upgrade: stacking adds benefits, not turn allowances.</summary>
public abstract class AdvancedAbilityPower(string title, string text) : IvichPower
{
    internal int UpgradedInstallations { get; set; }
    public override PowerType Type => PowerType.Buff;
    public override List<(string, string)> Localization => new PowerLoc(title, text, text + " 已激活{Amount}份。");
    internal int Benefit(int normal, int upgraded)
        => Math.Max(0, Amount - UpgradedInstallations) * normal + Math.Min(Amount, UpgradedInstallations) * upgraded;
}

public sealed class ScaleCoagulationPower() : AdvancedAbilityPower("凝鳞", "每次实际恢复生命，获得等量格挡。");
public sealed class BloodForgePower() : AdvancedAbilityPower("血铸", "每次治疗每溢出2点获得1本回合力量；逐次向下取整。");
public sealed class ColdBloodPower() : AdvancedAbilityPower("寒血", "每次实际失血，对所有敌人施加2冰冻；升级版3。");
public sealed class HungerPower() : AdvancedAbilityPower("饥渴", "半龙期间每实际消耗一张牌，获得1怒气。");
public sealed class FrostKnowledgePower() : AdvancedAbilityPower("霜之学识", "每当一名敌人被你冻结，获得1振奋。");
public sealed class IceMirrorPower() : AdvancedAbilityPower("冰镜庇护", "自己回合开始，远距获得6格挡，否则3；升级版8/4。");
public sealed class ColdWavePower() : AdvancedAbilityPower("寒潮", "每张冰系攻击或技能实际结算后，对所有敌人施加1冰冻；升级版2。");
public sealed class DistanceReasonPower() : AdvancedAbilityPower("远近之理", "入近获得本回合1力量，入远获得4格挡；升级版2/6。");
public sealed class StarCurrentPower() : AdvancedAbilityPower("星流灌注", "每回合第一张魔法攻击实际结算后抽2张牌。");
public sealed class SpellbladeResonancePower() : AdvancedAbilityPower("魔刃共鸣", "实际支付必需魔力的牌获得本回合1力量；必需怒气的牌获得本回合1振奋。");
public sealed class CurseEchoPower() : AdvancedAbilityPower("诅咒余韵", "每回合前2张直接施加指定负面的牌结算后抽1张牌，初始/魔法使再回1魔力；升级版额度3。");
public sealed class ManyFormsPower() : AdvancedAbilityPower("百相加护", "每回合前3种不同指定正面状态实际增加时，抽1张牌并获得3格挡；升级版格挡5。");
public sealed class EternalWinterPower() : AdvancedAbilityPower("永冬", "回合结束对所有敌人施加3冰冻，升级版5。每冻结一敌，初始/魔法使获得2魔力。");
public sealed class DoomCurtainPower() : AdvancedAbilityPower("终焉帷幕", "每实际消耗一张牌，对所有敌人施加1终焉并获得2格挡；升级版2/3。");

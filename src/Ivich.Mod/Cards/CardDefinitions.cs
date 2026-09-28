using MegaCrit.Sts2.Core.Entities.Cards;

namespace Ivich.Mod.Cards;

internal record CardValue(string Name, int Base, int Upgrade);
internal record CardDefinition(string Id, string Name, string Description, int Energy, CardType Type,
    CardRarity Rarity, TargetType Target, CardValue[] Variables, int Mana = 0, int Rage = 0,
    bool Magic = false, bool Scythe = false, bool Ice = false, int Life = 0,
    bool Exhaust = false, bool NeedsOtherCard = false);

/// <summary>Runtime definitions for the complete 82-card design catalog.</summary>
internal static partial class CardDefinitions
{
    private const CardType Attack = CardType.Attack, Skill = CardType.Skill, Power = CardType.Power;
    private const CardRarity Basic = CardRarity.Basic, Common = CardRarity.Common, Uncommon = CardRarity.Uncommon, Token = CardRarity.Token;
    private const TargetType Enemy = TargetType.AnyEnemy, AllEnemies = TargetType.AllEnemies, Self = TargetType.Self;
    private static CardValue V(string name, int value, int upgraded) => new(name, value, upgraded);

    public static readonly IReadOnlyDictionary<string, CardDefinition> All = new CardDefinition[]
    {
        new("B01", "打击", "武技·镰刀。造成{Damage:diff()}点伤害。", 1, Attack, Basic, Enemy,
            [V("Damage", 6, 9)], Scythe: true),
        new("B02", "防御", "获得{Block:diff()}点格挡。", 1, Skill, Basic, Self,
            [V("Block", 5, 8)]),
        new("B03", "镰柄蓄能", "镰刀。初始／魔法使获得{Resource:diff()}魔力；半龙获得{Resource:diff()}怒气。", 1, Skill, Basic, Self,
            [V("Resource", 3, 4)], Scythe: true),
        new("B04", "咬破指尖", "血。生命代价4，至少留下1生命。抽{Cards:diff()}张牌；仅半龙额外获得{Rage:diff()}怒气。", 0, Skill, Basic, Self,
            [V("Cards", 1, 2), V("Rage", 2, 3)], Life: 4),
        new("B05", "霜线", "魔法·冰。造成{Damage:diff()}点伤害，然后施加{Frost:diff()}冰冻。", 1, Attack, Basic, Enemy,
            [V("Damage", 7, 10), V("Frost", 3, 4)], Mana: 1, Magic: true, Ice: true),
        new("B06", "退锋", "武技·镰刀·空间。造成{Damage:diff()}点伤害，然后将距离设为+5。", 1, Attack, Basic, Enemy,
            [V("Damage", 6, 10)], Scythe: true),
        new("C01", "引镰", "武技·镰刀·空间。将距离设为−5，再造成{Damage:diff()}点伤害。", 1, Attack, Common, Enemy,
            [V("Damage", 7, 10)], Scythe: true),
        new("C02", "掠步", "空间。选择距离增加5或减少5，获得{Block:diff()}点格挡。", 0, Skill, Common, Self,
            [V("Block", 4, 7)]),
        new("C03", "退身回刃", "武技·镰刀·空间。造成{Damage:diff()}点伤害，将距离设为+5，获得{Block:diff()}点格挡。", 1, Attack, Common, Enemy,
            [V("Damage", 8, 11), V("Block", 5, 7)], Scythe: true),
        new("C04", "刻隙", "武技·镰刀·空间。造成{Damage:diff()}点真实伤害；若距离为负，施加{Debuff:diff()}易伤和{Debuff:diff()}脆弱。", 1, Attack, Common, Enemy,
            [V("Damage", 8, 11), V("Debuff", 1, 2)], Scythe: true),
        new("C05", "镰面招架", "镰刀。获得本回合{Dexterity:diff()}敏捷，再获得{Block:diff()}点格挡。若此前本回合已打出镰刀牌，抽1张牌。", 1, Skill, Common, Self,
            [V("Dexterity", 1, 2), V("Block", 6, 9)], Scythe: true),
        new("C06", "引魔", "获得{Mana:diff()}魔力与本回合{Invigoration:diff()}振奋。半龙不获得魔力。", 1, Skill, Common, Self,
            [V("Mana", 4, 5), V("Invigoration", 2, 3)]),
        new("C07", "碎饼时间", "食物。恢复{Heal:diff()}生命；仅半龙额外获得{Rage:diff()}怒气。", 1, Skill, Common, Self,
            [V("Heal", 3, 5), V("Rage", 2, 3)], Exhaust: true),
        new("C08", "磨牙", "身体。获得{Block:diff()}点格挡与{Thorns:diff()}荆棘，荆棘持续至下次自己回合开始。仅半龙额外获得{Rage:diff()}怒气。", 1, Skill, Common, Self,
            [V("Block", 5, 8), V("Thorns", 3, 5), V("Rage", 3, 4)]),
        new("C09", "血换锋", "血。生命代价6，至少留下1生命。获得1能量；仅半龙额外获得{Rage:diff()}怒气。{IfUpgraded:show:抽1张牌。|}", 0, Skill, Common, Self,
            [V("Rage", 2, 3), V("Cards", 0, 1)], Life: 6, Exhaust: true),
        new("C10", "咬合", "武技·身体。造成{Damage:diff()}点伤害，然后恢复{Heal:diff()}生命。", 1, Attack, Common, Enemy,
            [V("Damage", 10, 14), V("Heal", 2, 3)], Rage: 1),
        new("C11", "横断", "武技·镰刀。对所有敌人造成{Damage:diff()}点伤害两次。每段基础为{Damage.BaseValue}＋S，已计入上述伤害。S为力量、敏捷、振奋、人工制品、再生、荆棘中为正的种类数。", 1, Attack, Common, AllEnemies,
            [V("Damage", 4, 6)], Scythe: true),
        new("C12", "冰霜龙息", "武技·身体·冰。对所有敌人造成{Damage:diff()}点伤害，然后各施加{Frost:diff()}冰冻。", 1, Attack, Common, AllEnemies,
            [V("Damage", 7, 10), V("Frost", 3, 4)], Rage: 2, Ice: true),
        new("C13", "霜刃", "武技·镰刀·冰。造成{Damage:diff()}点伤害，施加{Frost:diff()}冰冻。", 1, Attack, Common, Enemy,
            [V("Damage", 8, 11), V("Frost", 4, 5)], Rage: 1, Scythe: true, Ice: true),
        new("C14", "冰针", "魔法·冰。造成{Damage:diff()}点伤害两次，然后施加{Frost:diff()}冰冻。", 0, Attack, Common, Enemy,
            [V("Damage", 4, 6), V("Frost", 2, 3)], Mana: 2, Magic: true, Ice: true),
        new("C15", "霜墙", "魔法·冰。获得{Block:diff()}点格挡，对一敌施加{Frost:diff()}冰冻。", 1, Skill, Common, Enemy,
            [V("Block", 10, 14), V("Frost", 2, 3)], Mana: 1, Magic: true, Ice: true),
        new("C16", "霜缚", "魔法·冰。施加{Frost:diff()}冰冻与{Weak:diff()}虚弱。", 1, Skill, Common, Enemy,
            [V("Frost", 4, 6), V("Weak", 1, 2)], Mana: 1, Magic: true, Ice: true),
        new("C17", "刹那折跃", "魔法·空间。将距离设为−5或+5；抽1张牌。{IfUpgraded:show:获得{Block:diff()}点格挡。|}", 0, Skill, Common, Self,
            [V("Cards", 1, 1), V("Block", 0, 5)], Mana: 1, Magic: true),
        new("C18", "解构", "空间。消耗另一张手牌，获得{Mana:diff()}魔力，抽{Cards:diff()}张牌。半龙不获得魔力。", 1, Skill, Common, Self,
            [V("Mana", 3, 4), V("Cards", 1, 2)], NeedsOtherCard: true),
        new("C19", "断面射线", "魔法·空间。造成{Damage:diff()}点真实伤害。本战此张牌已成长{Growth}点基础伤害，已计入上述伤害。每次真实打出后，以后基础伤害再加4。", 1, Attack, Common, Enemy,
            [V("Damage", 8, 12), V("Growth", 0, 0)], Mana: 2, Magic: true),
        new("C20", "假寐", "获得{Block:diff()}点格挡与{Regen:diff()}再生，抽2张牌。", 1, Skill, Common, Self,
            [V("Block", 6, 9), V("Regen", 1, 2), V("Cards", 2, 2)]),
        new("C21", "苍白祝祷", "魔法。获得本回合{Invigoration:diff()}振奋，抽1张牌。", 0, Skill, Common, Self,
            [V("Invigoration", 3, 5), V("Cards", 1, 1)], Mana: 2, Magic: true),
        new("C22", "舔舐伤口", "身体。恢复{Heal:diff()}生命，获得{Block:diff()}点格挡。", 1, Skill, Common, Self,
            [V("Heal", 6, 8), V("Block", 6, 8)], Rage: 1),
        new("C23", "收镰", "空间。从弃牌堆选择1张镰刀牌加入手牌；获得{Block:diff()}点格挡。没有可选牌时仍获得格挡。", 0, Skill, Common, Self,
            [V("Block", 4, 7)], Rage: 1),
        new("C24", "龙胃", "身体。消耗另一张手牌，恢复{Heal:diff()}生命；仅半龙额外获得{Rage:diff()}怒气。", 0, Skill, Common, Self,
            [V("Heal", 2, 3), V("Rage", 2, 3)], NeedsOtherCard: true),
        new("C25", "寒意侵袭", "魔法·冰。对所有敌人施加{Debuff:diff()}虚弱与{Debuff:diff()}易伤。", 1, Skill, Common, AllEnemies,
            [V("Debuff", 1, 2)], Mana: 1, Magic: true, Ice: true),
        new("C26", "龙威", "身体。施加{Debuff:diff()}虚弱；若本回合已实际失去生命，再施加{Debuff:diff()}易伤并抽1张牌。", 0, Skill, Common, Enemy,
            [V("Debuff", 2, 3), V("Cards", 1, 1)], Rage: 1),
        new("C27", "星衣编织", "魔法。获得{Dexterity:diff()}敏捷与{Artifact:diff()}人工制品。", 1, Skill, Common, Self,
            [V("Dexterity", 2, 3), V("Artifact", 1, 2)], Mana: 2, Magic: true, Exhaust: true),
        new("C28", "龙息调律", "身体。抽{Cards:diff()}张牌；若本回合已实际恢复生命，获得1能量。", 0, Skill, Common, Self,
            [V("Cards", 2, 3)], Rage: 1, Exhaust: true),
        new("U01", "刃上结界", "魔法。每张镰刀攻击结算后，获得{Ward:diff()}点格挡。此格挡不受敏捷影响。", 1, Power, Uncommon, Self,
            [V("Ward", 4, 6)], Mana: 1, Magic: true),
        new("U02", "沸血", "血。每次实际失去生命，获得1力量。每次失血事件触发一次。{IfUpgraded:show:激活时额外获得2力量。|}", 1, Power, Uncommon, Self,
            [V("Strength", 0, 2)], Rage: 1),
        new("T01", "龙咬", "武技·身体。造成{Damage:diff()}点伤害；攻击后若有怒气，自动支付1怒气并恢复1生命。无怒气也能打出。", 1, Attack, Token, Enemy,
            [V("Damage", 8, 11), V("Heal", 1, 1)])
    }.Concat(Remaining).ToDictionary(definition => definition.Id);
}

public static class ImplementedCards
{
    public static IReadOnlyList<string> EnabledCardIds { get; } = CardDefinitions.All.Keys.ToArray();
}

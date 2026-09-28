using MegaCrit.Sts2.Core.Entities.Cards;

namespace Ivich.Mod.Cards;

internal static partial class CardDefinitions
{
    private const CardRarity Rare = CardRarity.Rare;
    private static IEnumerable<CardDefinition> Remaining =>
    [
        new("U03", "凝鳞", "身体。每次实际恢复生命，获得等量格挡，此格挡不受敏捷影响。激活时获得{Block:diff()}点格挡。", 1, Power, Uncommon, Self, [V("Block", 6, 10)], Rage: 1),
        new("U04", "血铸", "血。每次治疗每溢出2点，获得1本回合力量，向下取整、不跨次积余。激活后恢复{Heal:diff()}生命。", 1, Power, Uncommon, Self, [V("Heal", 3, 6)], Rage: 2),
        new("U05", "寒血", "血·冰。每次实际失去生命，对所有敌人施加{Frost:diff()}冰冻；每次失血事件触发一次。", 1, Power, Uncommon, Self, [V("Frost", 2, 3)], Rage: 1, Ice: true),
        new("U06", "追猎折跃", "武技·身体·空间。将距离设为−5，对一敌造成{Damage:diff()}真实伤害，不移除冰冻；抽1张牌。基础为{Damage.BaseValue}＋{Factor:diff()}×其当前冰冻层数，已计入上述伤害。", 1, Attack, Uncommon, Enemy, [V("Damage", 12, 16), V("Factor", 1, 2), V("Cards", 1, 1)], Rage: 1),
        new("U07", "饥渴", "身体。每当实际消耗一张牌，仅在半龙形态获得1怒气。{IfUpgraded:show:激活时额外获得3怒气。|}", 1, Power, Uncommon, Self, [V("Rage", 0, 3)], Rage: 1),
        new("U08", "霜之学识", "魔法·冰。每当敌人被你冻结，获得1振奋。{IfUpgraded:show:激活时额外获得2振奋。|}", 1, Power, Uncommon, Self, [V("Invigoration", 0, 2)], Mana: 2, Magic: true, Ice: true),
        new("U09", "碎冰裁决", "魔法·空间·冰。移除一敌至多8冰冻，对其造成{Damage:diff()}真实伤害。基础为{Damage.BaseValue}＋{Factor:diff()}×移除层数，已计入上述伤害。0层也可使用。", 1, Attack, Uncommon, Enemy, [V("Damage", 10, 14), V("Factor", 3, 4)], Mana: 2, Magic: true, Ice: true),
        new("U10", "冰镜庇护", "魔法·冰。每回合开始，距离为正时获得{FarBlock:diff()}格挡，否则获得{NearBlock:diff()}格挡。此格挡不受敏捷影响。", 1, Power, Uncommon, Self, [V("FarBlock", 6, 8), V("NearBlock", 3, 4)], Mana: 2, Magic: true, Ice: true),
        new("U11", "寒潮", "魔法·冰。每张冰系攻击或技能完成效果后，对所有敌人施加{Frost:diff()}冰冻。", 1, Power, Uncommon, Self, [V("Frost", 1, 2)], Mana: 2, Magic: true, Ice: true),
        new("U12", "霜河归流", "魔法·空间·冰。把其他每个敌人至多{Transfer:diff()}冰冻搬给所选敌人，再加{Frost:diff()}冰冻，合并检查一次冻结；抽1张牌。", 1, Skill, Uncommon, Enemy, [V("Transfer", 6, 8), V("Frost", 4, 6), V("Cards", 1, 1)], Mana: 2, Magic: true, Ice: true),
        new("U13", "终焉刻痕", "魔法·镰刀·空间。造成{Damage:diff()}伤害，再施加{Doom:diff()}终焉。", 1, Attack, Uncommon, Enemy, [V("Damage", 10, 14), V("Doom", 4, 6)], Mana: 2, Magic: true, Scythe: true),
        new("U14", "终末宣告", "魔法·空间。施加{Doom:diff()}终焉；若目标有冰冻，额外施加4终焉。", 1, Skill, Uncommon, Enemy, [V("Doom", 4, 6)], Mana: 2, Magic: true),
        new("U15", "省略咏唱", "消耗另一张手牌，抽{Cards:diff()}张牌；可令一项正在进行的吟唱立即完成。", 1, Skill, Uncommon, Self, [V("Cards", 2, 3)], Exhaust: true, NeedsOtherCard: true),
        new("U16", "远近之理", "魔法·空间。每当入近，获得{Strength:diff()}本回合力量；每当入远，获得{Ward:diff()}格挡，此格挡不受敏捷影响。", 1, Power, Uncommon, Self, [V("Strength", 1, 2), V("Ward", 4, 6)], Mana: 1, Magic: true),
        new("U17", "裂界长吟", "魔法·镰刀·空间。吟唱1。造成{Damage:diff()}真实伤害。基础为{Damage.BaseValue}＋{Factor:diff()}N，已计入上述伤害；N为真实打出时永久牌组中的魔法牌张数。", 2, Attack, Uncommon, Enemy, [V("Damage", 18, 24), V("Factor", 4, 5)], Mana: 5, Magic: true, Scythe: true),
        new("U18", "借位嫁祸", "魔法·空间。获得{Block:diff()}格挡；可从自己或一敌选择冰冻、终焉、虚弱、易伤、脆弱中的一种，将至多{Transfer:diff()}层移给另一个敌人。", 1, Skill, Uncommon, Self, [V("Block", 8, 11), V("Transfer", 8, 12)], Mana: 2, Magic: true),
        new("U19", "星流灌注", "魔法。每回合你的第一张魔法攻击完成效果后，抽2张牌。", 1, Power, Uncommon, Self, [], Mana: 2, Magic: true),
        new("U20", "环刃", "武技·镰刀。造成{Damage:diff()}伤害三次；若本回合已经实际恢复生命，再攻击一次。", 1, Attack, Uncommon, Enemy, [V("Damage", 4, 6)], Rage: 2, Scythe: true),
        new("U21", "撕咬不休", "武技·身体。造成{Damage:diff()}伤害并恢复{Heal:diff()}生命，重复两次。目标已死时跳过剩余伤害，仍完成治疗。", 1, Attack, Uncommon, Enemy, [V("Damage", 7, 9), V("Heal", 2, 3)], Rage: 2),
        new("U22", "空间书签", "魔法·空间。抽{Cards:diff()}张牌，再选择1张手牌放回抽牌堆顶。", 0, Skill, Uncommon, Self, [V("Cards", 3, 4)], Mana: 2, Magic: true),
        new("U23", "血色休止", "身体·血。恢复{Heal:diff()}生命，获得{Regen:diff()}再生，抽2张牌。", 1, Skill, Uncommon, Self, [V("Heal", 6, 10), V("Regen", 3, 4), V("Cards", 2, 2)], Rage: 2),
        new("U24", "魔刃共鸣", "魔法·空间。每张实际支付必需魔力费用的牌，使你获得1本回合力量；每张实际支付必需怒气费用的牌，使你获得1本回合振奋。{IfUpgraded:show:激活时额外获得本回合2力量与2振奋。|}", 1, Power, Uncommon, Self, [V("Bonus", 0, 2)], Mana: 1, Magic: true),
        new("U25", "诅咒余韵", "魔法。每回合前{Limit:diff()}张直接成功施加或搬运冰冻、终焉、虚弱、易伤、脆弱的牌完成效果后，抽1张牌；初始／魔法使再获得1魔力。每张牌最多触发一次，同名能力共享名额。", 1, Power, Uncommon, Self, [V("Limit", 2, 3)], Mana: 2, Magic: true),
        new("U26", "百相加护", "每回合前3种不同的力量、敏捷、振奋、人工制品、再生、荆棘实际增加时，抽1张牌并获得{Ward:diff()}格挡。此格挡不受敏捷影响；同名能力共享名额。", 1, Power, Uncommon, Self, [V("Ward", 3, 5)]),
        new("U27", "螺旋猎舞", "武技·镰刀·空间。造成{Damage:diff()}伤害。本战此牌已有{Growth}次真实打出；上述基础伤害已包含每次50%初始基础的成长。重复结算不成长。", 1, Attack, Uncommon, Enemy, [V("Damage", 12, 16), V("Growth", 0, 0)], Rage: 2, Scythe: true),
        new("U28", "灾厄汇编", "魔法·空间。造成{Damage:diff()}真实伤害。基础为{Damage.BaseValue}＋{Factor:diff()}D，已计入上述伤害；D为目标冰冻、终焉、虚弱、易伤、脆弱中为正的种类数。", 2, Attack, Uncommon, Enemy, [V("Damage", 8, 12), V("Factor", 6, 8)], Mana: 3, Magic: true),
        new("R01", "世界切面", "魔法·空间。吟唱1。对所有敌人造成{Damage:diff()}真实伤害。", 3, Attack, Rare, AllEnemies, [V("Damage", 72, 96)], Mana: 8, Magic: true),
        new("R02", "时隙封存", "魔法·空间。本场所有魔法准备完成后改为储存，获得1张术式解放。吟唱未完成的大魔法不能入库；同名能力不叠加。", 2, Power, Rare, Self, [], Mana: 2, Magic: true),
        new("R03", "零度加冕", "魔法·冰。吟唱1。对所有敌人造成{Damage:diff()}伤害，再各施加{Frost:diff()}冰冻。", 2, Attack, Rare, AllEnemies, [V("Damage", 20, 28), V("Frost", 10, 14)], Mana: 6, Magic: true, Ice: true),
        new("R04", "永冬", "魔法·冰。自己回合结束时，对所有敌人施加{Frost:diff()}冰冻；每当敌人被你冻结，初始／魔法使获得2魔力。", 2, Power, Rare, Self, [V("Frost", 3, 5)], Mana: 4, Magic: true, Ice: true),
        new("R05", "食界之镰", "魔法·镰刀·空间。造成{Damage:diff()}真实伤害。真实施放的效果完成后，本局此张原牌永久增加3基础伤害；单纯复制效果不写入原牌。", 2, Attack, Rare, Enemy, [V("Damage", 18, 24)], Mana: 3, Magic: true, Scythe: true, Exhaust: true),
        new("R06", "赤镰盛宴", "武技·镰刀·血。对所有敌人造成{Damage:diff()}伤害，基础为{Damage.BaseValue}＋⌊当前格挡÷2⌋，已计入上述伤害，不消费格挡；随后恢复本次直接实际总生命伤害的三分之一，向下取整。", 2, Attack, Rare, AllEnemies, [V("Damage", 8, 12)], Rage: 5, Scythe: true),
        new("R07", "饕餮", "武技·身体。造成{Damage:diff()}伤害。合格斩杀时，本局生命上限永久增加{Growth:diff()}，然后恢复{Heal:diff()}生命；召唤物及复活后的重复击杀不计入。", 1, Attack, Rare, Enemy, [V("Damage", 14, 18), V("Growth", 3, 4), V("Heal", 3, 4)], Rage: 2, Exhaust: true),
        new("R08", "装满甜甜圈的口袋", "食物。将{Cards:diff()}张临时、未升级的甜甜圈加入手牌。", 1, Skill, Rare, Self, [V("Cards", 3, 4)], Exhaust: true),
        new("R09", "舍弃此岸", "魔法·空间。消耗当前其余手牌；每实际消耗一张，对所有敌人造成{Damage:diff()}真实伤害。本牌自身的消耗不计入。", 2, Attack, Rare, AllEnemies, [V("Damage", 7, 9)], Mana: 4, Magic: true, Exhaust: true),
        new("R10", "绝对回收", "魔法·空间。从弃牌堆选择至多{Cards:diff()}张牌加入手牌；这些牌的固定能量费用本回合降低1，最低0。", 1, Skill, Rare, Self, [V("Cards", 3, 4)], Mana: 3, Magic: true, Exhaust: true),
        new("R11", "终焉帷幕", "魔法·空间。每当实际消耗一张牌，对所有敌人施加{Doom:diff()}终焉，并获得{Ward:diff()}格挡；此格挡不受敏捷影响。", 2, Power, Rare, Self, [V("Doom", 1, 2), V("Ward", 2, 3)], Mana: 3, Magic: true),
        new("R12", "终末咏唱", "魔法·空间。吟唱1。施加{Doom:diff()}终焉；若目标有冰冻，额外施加4终焉。", 2, Skill, Rare, Enemy, [V("Doom", 12, 16)], Mana: 5, Magic: true),
        new("R13", "星霜齐落", "魔法·冰·空间。对所有敌人造成{Damage:diff()}真实伤害，再各施加1冰冻，重复X次；X为实际支付的能量，无吟唱。", -1, Attack, Rare, AllEnemies, [V("Damage", 7, 10), V("Frost", 1, 1)], Mana: 3, Magic: true, Ice: true),
        new("R14", "穹界坠落", "魔法·空间。至少需要5魔力；吟唱1。造成{Damage:diff()}真实伤害。基础为{Damage.BaseValue}X，已计入上述伤害；X为实际支付的全部魔力。", 2, Attack, Rare, Enemy, [V("Damage", 10, 12)], Mana: -1, Magic: true, Exhaust: true),
        new("R15", "暴食龙舞", "武技·身体。至少需要1怒气。造成{Damage:diff()}伤害，再恢复2生命，重复X次；X为实际支付的全部怒气。目标死亡后，尚未发动的攻击及治疗跳过。", 1, Attack, Rare, Enemy, [V("Damage", 3, 5), V("Heal", 2, 2)], Rage: -1),
        new("R16", "万象归刃", "武技·镰刀。对所有敌人造成{Damage:diff()}伤害，重复1＋S次；S为开始结算时力量、敏捷、振奋、人工制品、再生、荆棘中为正的种类数。", 2, Attack, Rare, AllEnemies, [V("Damage", 8, 11)], Scythe: true),
        new("T02", "甜甜圈", "食物。恢复{Heal:diff()}生命；初始／魔法使获得{Resource:diff()}魔力，半龙获得{Resource:diff()}怒气，只执行当前形态分支。", 0, Skill, Token, Self, [V("Heal", 3, 5), V("Resource", 1, 2)], Exhaust: true),
        new("T03", "术式解放", "按储存顺序释放全部已储存魔法，清空本次取出的记录；打出后回到手牌。储存为空时不能打出。{IfUpgraded:show:本场首次打出升级版时获得6格挡，按玩家记录一次。|}", 0, Skill, Token, Self, [V("Block", 0, 6)]),
        new("A01", "二相一身", "在魔法使与半龙之间切换；现有魔力与怒气随换形全额1∶1转化，原资源清空。初始形态可选择其一。打出后回手，变化仅限当前战斗。{IfUpgraded:show:本场首次打出升级版时抽2张牌，按玩家记录一次。|}", 0, Skill, CardRarity.Ancient, Self, [V("Cards", 0, 2)])
    ];
}

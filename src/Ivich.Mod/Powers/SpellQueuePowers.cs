using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;
namespace Ivich.Mod.Powers;

public sealed class SpellStoragePower : IvichPower
{
    public override PowerType Type => PowerType.Buff;
    public override List<(string, string)> Localization => new PowerLoc("时隙封存", "魔法准备完成后进入法库，以术式解放释放。", "魔法准备完成后进入法库；每回合开始取回术式解放。");
}
public sealed class ChantingSpellsPower : IvichPower
{
    public override PowerType Type => PowerType.Buff;
    public override List<(string, string)> Localization => new PowerLoc("吟唱中", "大魔法在下个自己的回合开始完成吟唱。", "有{Amount}项吟唱尚未完成；术式解放不能提前释放。");
}
public sealed class StoredSpellsPower : IvichPower
{
    public override PowerType Type => PowerType.Buff;
    public override List<(string, string)> Localization => new PowerLoc("已封存术式", "准备完成的魔法；打出术式解放后按顺序执行。", "有{Amount}项术式可释放；不再次付费，目标在执行时选择。");
}

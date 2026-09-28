using BaseLib.Abstracts;
using Godot;
using Ivich.Mod.Assets;
using Ivich.Mod.Cards;
using Ivich.Mod.Relics;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
namespace Ivich.Mod.Character;

public sealed class IvichCharacter : PlaceholderCharacterModel
{
    public override string CustomCharacterSelectBg => ArtPaths.Available(ArtPaths.SelectionScene, base.CustomCharacterSelectBg);
    // Native character-select getters require CompressedTexture2D. The button patch below
    // substitutes the cropped Texture2D only after the native initialization has finished.
    public override string? CustomCharacterSelectIconPath => ArtPaths.Available(ArtPaths.InitialPortrait, base.CustomCharacterSelectIconPath!);
    public override string? CustomCharacterSelectLockedIconPath => ArtPaths.Available(ArtPaths.InitialPortrait, base.CustomCharacterSelectLockedIconPath!);
    public override string CustomIconPath => ArtPaths.Available(ArtPaths.IconScene, base.CustomIconPath);
    public override string? CustomIconTexturePath => ArtPaths.Available(ArtPaths.Avatar, base.CustomIconTexturePath!);
    public override string? CustomIconOutlineTexturePath => ArtPaths.Available(ArtPaths.Avatar, base.CustomIconOutlineTexturePath!);
    public override CharacterGender Gender => CharacterGender.Feminine;
    public override Color NameColor => new("b9ddff");
    public override int StartingHp => 88;
    public override IEnumerable<CardModel> StartingDeck => [
        ModelDb.Card<B01Strike>(), ModelDb.Card<B01Strike>(), ModelDb.Card<B01Strike>(),
        ModelDb.Card<B02Defend>(), ModelDb.Card<B02Defend>(), ModelDb.Card<B02Defend>(),
        ModelDb.Card<B03ScytheCharge>(), ModelDb.Card<B04BiteFingertip>(),
        ModelDb.Card<B05FrostLine>(), ModelDb.Card<B06RetreatEdge>(), ModelDb.Card<C07CookieTime>()];
    public override IReadOnlyList<RelicModel> StartingRelics => [ModelDb.Relic<MoonEyeScythe>()];
    public override CardPoolModel CardPool => ModelDb.CardPool<IvichCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<IvichRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<IvichPotionPool>();
    public override List<(string, string)> Localization => new CharacterLoc(
        "依维希", "依维希", "以空间与寒冰编织术式，挥动凝视世界的镰刀。\n初始持有3魔力，容量5；跨回合保留，不自动恢复。",
        "她", "她", "她的", "她的", "冰霜与甜饼", "轮到你了。", "还没有结束。", "镰刀挡下了致命一击。", "可以买甜饼了。", "依维希的卡牌", "将依维希的卡牌加入牌池。");
}

public sealed class IvichCardPool : CustomCardPoolModel
{
    public override string Title => "Ivich";
    public override string BigEnergyIconPath => ModelDb.CardPool<MegaCrit.Sts2.Core.Models.CardPools.IroncladCardPool>().EnergyIconPath;
    public override string TextEnergyIconPath => BigEnergyIconPath;
    public override float H => 0.58f;
    public override float S => 0.45f;
    public override float V => 1f;
    public override Color DeckEntryCardColor => new("b9ddff");
    public override bool IsColorless => false;
}
public sealed class IvichRelicPool : CustomRelicPoolModel
{
    public override string EnergyColorName => ModelDb.CardPool<MegaCrit.Sts2.Core.Models.CardPools.IroncladCardPool>().EnergyColorName;
    public override Color LabOutlineColor => new("b9ddff");
}
public sealed class IvichPotionPool : CustomPotionPoolModel
{
    public override string EnergyColorName => ModelDb.CardPool<MegaCrit.Sts2.Core.Models.CardPools.IroncladCardPool>().EnergyColorName;
    public override Color LabOutlineColor => new("b9ddff");
}

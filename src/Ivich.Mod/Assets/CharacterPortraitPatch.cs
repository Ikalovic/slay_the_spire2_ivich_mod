using Godot;
using HarmonyLib;
using Ivich.Mod.Character;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace Ivich.Mod.Assets;

/// <summary>
/// The native icon property is CompressedTexture2D, but its UI accepts Texture2D.
/// Use the face atlas at the UI boundary without changing the source image or other characters.
/// </summary>
[HarmonyPatch(typeof(NCharacterSelectButton), nameof(NCharacterSelectButton.Init))]
internal static class CharacterPortraitPatch
{
    private static void Prefix(CharacterModel character, TextureRect ____icon)
    {
        if (character is IvichCharacter && ResourceLoader.Exists(ArtPaths.Avatar))
            ____icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
    }

    private static void Postfix(CharacterModel character, TextureRect ____icon)
    {
        if (character is not IvichCharacter || !ResourceLoader.Exists(ArtPaths.Avatar)) return;
        ____icon.Texture = ResourceLoader.Load<Texture2D>(ArtPaths.Avatar);
    }
}

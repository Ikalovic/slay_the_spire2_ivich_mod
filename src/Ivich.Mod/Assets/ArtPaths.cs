using Godot;
using Ivich.Core;

namespace Ivich.Mod.Assets;

public static class ArtPaths
{
    public const string Avatar = "res://Ivich/images/ui/avatar.tres";
    public const string InitialPortrait = "res://Ivich/images/characters/initial.png";
    public const string SelectionScene = "res://Ivich/scenes/character_select.tscn";
    public const string IconScene = "res://Ivich/scenes/character_icon.tscn";
    public const string Scythe = "res://Ivich/images/relics/moon_eye_scythe.png";

    public static string Available(string path, string fallback)
        => ResourceLoader.Exists(path) ? path : fallback;

    public static string CardPortrait(string designId, bool large, string fallback)
    {
        var stem = $"res://Ivich/images/card_portraits/{(large ? "big/" : "")}{designId.ToLowerInvariant()}";
        // AtlasTexture preserves the complete square/vertical source inside the normal card window.
        // Keep PNG support for the original explicit-size import tool.
        return Available(stem + ".tres", Available(stem + ".png", fallback));
    }

    public static string FormPortrait(Form form, string fallback)
        => Available($"res://Ivich/images/card_portraits/forms/{form.ToString().ToLowerInvariant()}.tres", fallback);
}

using Godot;

namespace Ivich.Mod.Assets;

public static class ArtPaths
{
    public static string CardPortrait(string designId, bool large, string fallback)
    {
        var path = $"res://Ivich/images/card_portraits/{(large ? "big/" : "")}{designId.ToLowerInvariant()}.png";
        return ResourceLoader.Exists(path) ? path : fallback;
    }
}

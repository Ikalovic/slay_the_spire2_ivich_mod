using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
namespace Ivich.Mod;

[ModInitializer(nameof(Initialize))]
public static class MainFile
{
    public const string ModId = "Ivich";
    public static void Initialize() => new Harmony(ModId).PatchAll(Assembly.GetExecutingAssembly());
}

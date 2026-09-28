using HarmonyLib;
using Ivich.Mod.Cards;
using Ivich.Mod.Mechanics;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace Ivich.Mod.Ancients;

/// <summary>Orobas grants ArchaicTooth; BaseLib's ITranscendenceCard supplies B03 → A01.</summary>
public static class AncientFormEntry
{
    public static bool HasDualFormAccess(Player player)
    {
        if (IvichRuntime.Relic(player) is not { } relic) return false;
        if (relic.AncientFormUnlocked != 0) return true;
        if (player.Deck.Cards.Any(card => card is A01DualAspect) ||
            player.GetRelic<ArchaicTooth>()?.AncientCard?.Id == ModelDb.Card<A01DualAspect>().Id)
        {
            relic.AncientFormUnlocked = 1;
            return true;
        }
        return false;
    }

    [HarmonyPatch(typeof(ArchaicTooth), nameof(ArchaicTooth.AfterObtained))]
    private static class AfterToothObtainedPatch
    {
        private static void Postfix(ArchaicTooth __instance, ref Task __result) => __result = RecordObtained(__result, __instance.Owner);
        private static async Task RecordObtained(Task original, Player player)
        {
            await original;
            _ = HasDualFormAccess(player);
        }
    }
}

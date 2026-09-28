using Ivich.Core;
using Ivich.Mod.Ancients;
using Ivich.Mod.Cards;
using Ivich.Mod.Mechanics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace Ivich.Mod.Rewards;

public static class IvichRewardFilter
{
    public static bool IsCompatible(Player player, CardModel card)
        => card is not IvichCard || IsCompatible(IvichRuntime.GetForm(player), card, AncientFormEntry.HasDualFormAccess(player));

    public static bool IsCompatible(Form form, CardModel card, bool dualFormAccess)
    {
        if (card is not IvichCard ivich) return true;
        if (dualFormAccess) return true;
        if (form == Form.Dragon)
            return !ivich.IsMagic && ivich.DesignId is not ("C06" or "C18");
        return ivich.NativeRageCost == 0;
    }

    public static CardCreationOptions Apply(Player owner, Player player, CardCreationOptions options)
    {
        if (owner != player) return options;
        var original = options.GetPossibleCards(player).ToArray();
        if (!original.Any(card => card is IvichCard)) return options;
        var candidates = original
            .Where(card => card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
            .Where(card => IsCompatible(player, card)).ToArray();
        // Narrow special pools may omit rarities. The full 72-card pool retains native rarity odds.
        // Preserve creation flags, source, RNG override and the already-applied upstream filter.
        var rarityOdds = candidates.Select(card => card.Rarity).Distinct().Count() < 3
            ? CardRarityOddsType.Uniform : options.RarityOdds;
        return options.WithCustomPool(candidates, rarityOdds);
    }
}

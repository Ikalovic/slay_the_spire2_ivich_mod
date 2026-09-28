using BaseLib.Abstracts;
using Ivich.Mod.Powers;
using Ivich.Mod.Resources;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using DoomPower = Ivich.Mod.Powers.DoomPower;

namespace Ivich.Mod.Cards;

/// <summary>Complete next-hit preview; the underlying upgrade base remains unchanged.</summary>
internal sealed class IvichDamageVar(decimal baseDamage, bool trueDamage)
    : DamageVar(baseDamage, trueDamage ? ValueProp.Move | ValueProp.Unblockable : ValueProp.Move)
{
    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    {
        decimal basis = card is IvichCard ivich ? ivich.PreviewBaseDamage(BaseValue) : BaseValue;
        if (card is IvichCard custom)
        {
            var owner = custom.IsMutable ? custom.Owner : null;
            var recorded = custom.RecordedSnapshot;
            int factor = custom.DynamicVars.TryGetValue("Factor", out var multiplier) ? multiplier.IntValue : 0;
            basis = custom.DesignId switch
            {
                "U06" => basis + factor * (target?.GetPowerAmount<FrostPower>() ?? 0),
                "U09" => basis + factor * Math.Min(8, target?.GetPowerAmount<FrostPower>() ?? 0),
                "U17" => basis + factor * (recorded?.PermanentMagicCount ?? owner?.Deck.Cards.Count(c => c is IvichCard { IsMagic: true }) ?? 0),
                "U28" => basis + factor * NegativeKinds(target),
                "R06" => basis + (owner?.Creature.Block ?? 0) / 2,
                "R14" => basis * (recorded?.PaidX ?? (owner?.PlayerCombatState is { } combat ? CustomResources<ManaResource>.Get(combat).Amount : 0)),
                _ => basis
            };
        }
        decimal enchanted = basis;
        if (card.Enchantment is { } enchantment)
        {
            enchanted += enchantment.EnchantDamageAdditive(enchanted, ValueProp.Move);
            enchanted *= enchantment.EnchantDamageMultiplicative(enchanted, ValueProp.Move);
        }
        if (!card.IsEnchantmentPreview) EnchantedValue = enchanted;

        decimal preview = runGlobalHooks
            ? Hook.ModifyDamage(card.Owner.RunState, card.CombatState, trueDamage ? null : target,
                card.Owner.Creature, basis, ValueProp.Move, card, ModifyDamageHookType.All,
                trueDamage ? CardPreviewMode.None : previewMode, out _)
            : enchanted;
        // U06 moves before dealing damage, so its preview uses the resulting near distance.
        if (runGlobalHooks && card is IvichCard { DesignId: "U06" })
            preview *= DistancePower.Multiplier(-5) / DistancePower.Multiplier(card.Owner.Creature.GetPowerAmount<DistancePower>());
        PreviewValue = Math.Max(0m, decimal.Floor(preview));
    }

    private static int NegativeKinds(Creature? target)
        => target is null ? 0 : new[] { target.GetPowerAmount<FrostPower>(), target.GetPowerAmount<DoomPower>(),
            target.GetPowerAmount<WeakPower>(), target.GetPowerAmount<VulnerablePower>(), target.GetPowerAmount<FrailPower>() }.Count(value => value > 0);
}

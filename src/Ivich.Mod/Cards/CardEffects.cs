using BaseLib.Abstracts;
using BaseLib.Utils;
using Ivich.Mod.Character;
using Ivich.Mod.Mechanics;
using Ivich.Mod.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using DoomPower = Ivich.Mod.Powers.DoomPower;

namespace Ivich.Mod.Cards;

internal static class CardEffects
{
    internal static LocString SelectionPrompt => new("cards", ModelDb.Card<TransferChoice>().Id.Entry + ".selection");

    internal static async Task<int> RemoveLayers<T>(PlayerChoiceContext context, Creature? target, int max, CardModel source) where T : PowerModel
    {
        if (target is not { IsAlive: true } || target.GetPower<T>() is not { Amount: > 0 } power) return 0;
        int before = power.Amount;
        using (DamagePatches.RawPowerAdjustment<T>(target))
            await PowerCmd.ModifyAmount(context, power, -Math.Min(max, before), source.Owner.Creature, source);
        return Math.Max(0, before - target.GetPowerAmount<T>());
    }

    internal static Task ConditionalDoom(PlayerChoiceContext context, CardModel card, Creature? target, int amount)
        => target is { IsAlive: true }
            ? IvichRuntime.ApplyDebuff<DoomPower>(context, target, amount + (target.GetPowerAmount<FrostPower>() > 0 ? 4 : 0), card.Owner.Creature, card)
            : Task.CompletedTask;

    private static IEnumerable<PowerModel> Transferable(Creature creature)
        => creature.Powers.Where(power => power.Amount > 0 && power is FrostPower or DoomPower or WeakPower or VulnerablePower or FrailPower);

    internal static async Task TransferDebuff(PlayerChoiceContext context, IvichCard card, int max)
    {
        var enemies = card.CombatState?.HittableEnemies.ToArray() ?? [];
        if (enemies.Length == 0) return;
        var sources = new[] { card.Owner.Creature }.Concat(enemies)
            .Where(source => Transferable(source).Any() && enemies.Any(enemy => enemy != source)).ToArray();
        if (sources.Length == 0) return;
        var sourceChoice = await Choose(context, card, sources.Select(source =>
            new ChoiceData(source.Name, "从此角色选择一种负面状态搬走。", source, null)), optional: true);
        if (sourceChoice?.Creature is not { } source) return;
        var powerChoice = await Choose(context, card, Transferable(source).Select(power =>
            new ChoiceData(power.Title.GetFormattedText(), $"将至多{max}层搬给另一名敌人；当前{power.Amount}层。", source, power)), optional: true);
        if (powerChoice?.Power is not { Amount: > 0 } power) return;
        var targetChoice = await Choose(context, card, enemies.Where(enemy => enemy != source && enemy.IsAlive).Select(enemy =>
            new ChoiceData(enemy.Name, "把选中的负面状态搬给此敌人。", enemy, null)), optional: true);
        if (targetChoice?.Creature is not { IsAlive: true } target) return;
        switch (power)
        {
            case FrostPower:
                int frost = await RemoveLayers<FrostPower>(context, source, max, card);
                if (frost > 0) await IvichRuntime.ApplyFrost(context, target, frost, card.Owner.Creature, card);
                break;
            case DoomPower: await Move<DoomPower>(context, card, source, target, max); break;
            case WeakPower: await Move<WeakPower>(context, card, source, target, max); break;
            case VulnerablePower: await Move<VulnerablePower>(context, card, source, target, max); break;
            case FrailPower: await Move<FrailPower>(context, card, source, target, max); break;
        }
    }

    private static async Task Move<T>(PlayerChoiceContext context, IvichCard card, Creature from, Creature to, int max) where T : PowerModel
    {
        int amount = await RemoveLayers<T>(context, from, max, card);
        if (amount > 0) await IvichRuntime.ApplyDebuff<T>(context, to, amount, card.Owner.Creature, card);
    }

    private sealed record ChoiceData(string Name, string Text, Creature Creature, PowerModel? Power);
    private static async Task<ChoiceData?> Choose(PlayerChoiceContext context, IvichCard card, IEnumerable<ChoiceData> items, bool optional)
    {
        var data = items.ToArray();
        var options = data.Select(item =>
        {
            var option = card.CombatState!.CreateCard<TransferChoice>(card.Owner);
            option.ChoiceName = item.Name;
            option.ChoiceText = item.Text;
            return (CardModel)option;
        }).ToArray();
        var result = (await CardSelectCmd.FromSimpleGrid(context, options, card.Owner,
            new CardSelectorPrefs(SelectionPrompt, optional ? 0 : 1, 1) { RequireManualConfirmation = true })).FirstOrDefault();
        int index = result is null ? -1 : Array.IndexOf(options, result);
        return index >= 0 ? data[index] : null;
    }
}

/// <summary>Ephemeral, synchronized choice presentation; never a collectible card.</summary>
public sealed class TransferChoice() : CustomCardModel(0, CardType.Skill, CardRarity.Token, TargetType.Self, false, false)
{
    internal string ChoiceName = "选择对象";
    internal string ChoiceText = "请选择需要的对象。";
    public override string Title => ChoiceName;
    public override CardPoolModel Pool => ModelDb.CardPool<IvichCardPool>();
    public override bool CanBeGeneratedInCombat => false;
    public override List<(string, string)> Localization
    {
        get
        {
            List<(string, string)> values = new CardLoc("选择对象", "{ChoiceText}");
            values.Add(("selection", "选择一张牌；若可选数量为0，可确认跳过。"));
            return values;
        }
    }
    public override string PortraitPath => ModelDb.Card<DefendIronclad>().PortraitPath;
    public override string? CustomPortraitPath => PortraitPath;
    protected override void AddExtraArgsToDescription(LocString description) => description.Add("ChoiceText", ChoiceText);
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Task.CompletedTask;
}

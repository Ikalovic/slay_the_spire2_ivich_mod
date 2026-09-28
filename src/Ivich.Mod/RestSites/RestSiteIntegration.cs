using BaseLib.Abstracts;
using Ivich.Core;
using Ivich.Mod.Cards;
using Ivich.Mod.Mechanics;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace Ivich.Mod.RestSites;

public static class RestSiteIntegration
{
    public static bool AddOptions(Player player, ICollection<RestSiteOption> options)
    {
        var relic = IvichRuntime.Relic(player);
        if (relic is null) return false;
        RegisterText();
        if (relic.FormValue == (int)Form.Initial)
        {
            options.Add(new AdvanceOption(player, Form.Mage));
            options.Add(new AdvanceOption(player, Form.Dragon));
            return true;
        }
        if (relic.FirstInscription != 0 && relic.SecondInscription == 0)
        {
            options.Add(new InscribeOption(player));
            return true;
        }
        return false;
    }

    private static void RegisterText()
    {
        LocManager.Instance.GetTable("rest_site_ui").MergeWith(new Dictionary<string, string>
        {
            ["OPTION_IVICH_MAGE.name"] = "进阶：魔法使",
            ["OPTION_IVICH_MAGE.description"] = "本局累计支付魔力：{ManaSpent}/50。基础生命上限变为50；每回合获得5魔力，魔力无上限。保留生命比例，并选择第一条镰刀铭刻。占用本次篝火行动。",
            ["OPTION_IVICH_DRAGON.name"] = "进阶：半龙",
            ["OPTION_IVICH_DRAGON.description"] = "本局累计失血：{HealthLost}/50；支付能量：{EnergySpent}/50。基础生命上限变为100；每战3怒气，每失血2点获得1怒气。保留生命比例，打击替换为龙咬，并选择第一条镰刀铭刻。半龙期间不能施放魔法。",
            ["OPTION_IVICH_INSCRIBE.name"] = "镰刀铭刻",
            ["OPTION_IVICH_INSCRIBE.description"] = "在进阶之后的另一处篝火，选择第二条不同的铭刻。每局最多两条；占用本次篝火行动。",
            ["IVICH_INSCRIPTION_PROMPT"] = "选择1条镰刀铭刻；持续本次爬塔。"
        });
    }

    internal static async Task<int?> ChooseInscription(Player player)
    {
        var relic = IvichRuntime.Relic(player)!;
        CardModel[] all = [ModelDb.Card<FoldBoundaryInscription>(), ModelDb.Card<LoopInscription>(),
            ModelDb.Card<FrostInscription>(), ModelDb.Card<SiphonInscription>(),
            ModelDb.Card<BloodPaymentInscription>(), ModelDb.Card<SweetnessInscription>()];
        var options = all.Where(card => ((InscriptionChoice)card).InscriptionId != relic.FirstInscription &&
                                       ((InscriptionChoice)card).InscriptionId != relic.SecondInscription)
            .Select(card => card.ToMutable()).ToArray();
        foreach (var card in options) card.Owner = player;
        var prefs = new CardSelectorPrefs(new LocString("rest_site_ui", "IVICH_INSCRIPTION_PROMPT"), 1)
        {
            Cancelable = true,
            RequireManualConfirmation = true
        };
        var selected = await CardSelectCmd.FromSimpleGrid(new BlockingPlayerChoiceContext(), options, player, prefs);
        return selected.FirstOrDefault() is InscriptionChoice choice ? choice.InscriptionId : null;
    }

    internal static async Task<bool> Advance(Player player, Form form)
    {
        var relic = IvichRuntime.Relic(player);
        if (relic is null || relic.FormValue != 0 ||
            (form == Form.Mage ? relic.ManaSpent < 50 : relic.HealthLost < 50 || relic.EnergySpent < 50))
            return false;
        var inscription = await ChooseInscription(player);
        if (inscription is null) return false;

        var creature = player.Creature;
        int maximum = Math.Max(1, (form == Form.Mage ? 50 : 100) + creature.MaxHp - 88);
        var health = new HealthRatio(creature.CurrentHp, creature.MaxHp);
        health.SwitchMaximum(maximum);
        // A form change neither heals nor causes damage. Commands that heal/lose max HP would emit those events.
        await CreatureCmd.SetMaxHp(creature, maximum);
        creature.SetCurrentHpInternal(health.CurrentHealth);
        relic.FormValue = (int)form;
        relic.FirstInscription = inscription.Value;
        relic.AdvancementFloor = player.RunState.TotalFloor;

        if (form == Form.Dragon)
        {
            foreach (var strike in player.Deck.Cards.OfType<B01Strike>().ToArray())
            {
                var bite = player.RunState.CreateCard<T01DragonBite>(player);
                if (strike.IsUpgraded) CardCmd.Upgrade(bite);
                await CardCmd.Transform(strike, bite);
            }
        }
        return true;
    }
}

internal sealed class AdvanceOption(Player owner, Form form) : CustomRestSiteOption(owner)
{
    public override string OptionId => form == Form.Mage ? "IVICH_MAGE" : "IVICH_DRAGON";
    public override string CustomIconPath => "res://images/ui/rest_site/option_smith.png";
    public override LocString Description
    {
        get
        {
            var text = base.Description;
            var relic = IvichRuntime.Relic(Owner);
            text.Add("ManaSpent", relic?.ManaSpent ?? 0);
            text.Add("EnergySpent", relic?.EnergySpent ?? 0);
            text.Add("HealthLost", relic?.HealthLost ?? 0);
            return text;
        }
    }
    public override bool IsEnabled => IvichRuntime.Relic(Owner) is { FormValue: 0 } relic &&
        (form == Form.Mage ? relic.ManaSpent >= 50 : relic.HealthLost >= 50 && relic.EnergySpent >= 50);
    public override Task<bool> OnSelect() => IsEnabled ? RestSiteIntegration.Advance(Owner, form) : Task.FromResult(false);
}

internal sealed class InscribeOption(Player owner) : CustomRestSiteOption(owner)
{
    public override string OptionId => "IVICH_INSCRIBE";
    public override string CustomIconPath => "res://images/ui/rest_site/option_smith.png";
    public override bool IsEnabled => IvichRuntime.Relic(Owner) is { FormValue: > 0, FirstInscription: > 0, SecondInscription: 0 } relic &&
        Owner.RunState.TotalFloor > relic.AdvancementFloor;
    public override async Task<bool> OnSelect()
    {
        if (!IsEnabled) return false;
        var inscription = await RestSiteIntegration.ChooseInscription(Owner);
        if (inscription is null) return false;
        IvichRuntime.Relic(Owner)!.SecondInscription = inscription.Value;
        return true;
    }
}

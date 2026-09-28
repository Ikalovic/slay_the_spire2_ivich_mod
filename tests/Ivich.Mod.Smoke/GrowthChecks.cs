using System.Reflection;

/// <summary>Native model checks requiring no Player, combat UI, or engine startup.</summary>
internal static class GrowthChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static void Run(Assembly game, Func<string, object> canonical)
    {
        foreach (var (id, first, step, upgraded) in new[] { ("C19", 8, 4, 12), ("U27", 12, 6, 16) })
        {
            var card = Call(canonical(id), "ToMutable");
            var record = card.GetType().GetMethod("OnRealPlayRecorded", Hidden)!;
            var play = Activator.CreateInstance(game.GetType("MegaCrit.Sts2.Core.Entities.Cards.CardPlay", true)!)!;
            record.Invoke(card, [play]);
            record.Invoke(card, [play]);
            Equal(first + 2 * step, Preview(card));
            var clone = Call(card, "ClonePreservingMutability");
            Equal(first, Preview(clone));
            Call(card, "UpgradeInternal");
            Call(card, "FinalizeUpgradeInternal");
            Equal(id == "C19" ? upgraded + 8 : upgraded * 2, Preview(card));
            card.GetType().GetMethod("AfterCombatEnd")!.Invoke(card, [null]);
            Equal(upgraded, Preview(card));
        }

        var growing = Call(canonical("R05"), "ToMutable");
        growing.GetType().GetProperty("PermanentGrowth")!.SetValue(growing, 9);
        Equal(27, Preview(growing));
        Call(growing, "UpgradeInternal");
        Call(growing, "FinalizeUpgradeInternal");
        Equal(33, Preview(growing));
        // The smoke harness bypasses BaseLib's loader. Its installed LatePostInit calls
        // CacheSavedProperties for every mod type with SavedProperty attributes.
        // Use the native public injection entry point to perform that required registration.
        game.GetType("MegaCrit.Sts2.Core.Saves.Runs.SavedPropertiesTypeCache", true)!
            .GetMethod("InjectTypeIntoCache", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [growing.GetType()]);
        var serialized = Call(growing, "ToSerializable");
        var restored = game.GetType("MegaCrit.Sts2.Core.Models.CardModel", true)!
            .GetMethod("FromSerializable", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [serialized])!;
        Equal(9, (int)restored.GetType().GetProperty("PermanentGrowth")!.GetValue(restored)!);
        Equal(33, Preview(restored));
        Equal(0, (int)canonical("R05").GetType().GetProperty("PermanentGrowth")!.GetValue(canonical("R05"))!);

        // Every custom formula has a safe out-of-combat, no-target preview. R14 has X=0 here.
        var mode = Enum.Parse(game.GetType("MegaCrit.Sts2.Core.Entities.Cards.CardPreviewMode", true)!, "None");
        foreach (var id in new[] { "C04", "C11", "C19", "U06", "U09", "U17", "U27", "U28", "R01", "R05", "R06", "R09", "R13", "R14" })
        {
            var card = Call(canonical(id), "ToMutable");
            var damage = Variable(card, "Damage");
            damage.GetType().GetMethod("UpdateCardPreview")!.Invoke(damage, [card, mode, null, false]);
            decimal value = (decimal)damage.GetType().GetProperty("PreviewValue")!.GetValue(damage)!;
            if (value < 0) throw new InvalidOperationException($"Negative preview on {id}");
        }
    }

    private static int Preview(object card)
    {
        decimal basis = (decimal)Variable(card, "Damage").GetType().GetProperty("BaseValue")!.GetValue(Variable(card, "Damage"))!;
        return (int)(decimal)card.GetType().GetMethod("PreviewBaseDamage", Hidden)!.Invoke(card, [basis])!;
    }
    private static object Variable(object card, string name)
    {
        var values = card.GetType().GetProperty("DynamicVars")!.GetValue(card)!;
        return values.GetType().GetProperty("Item")!.GetValue(values, [name])!;
    }
    private static object Call(object value, string method)
        => value.GetType().GetMethod(method, Type.EmptyTypes)!.Invoke(value, null)!;
    private static void Equal(int expected, int actual)
    {
        if (expected != actual) throw new InvalidOperationException($"Expected {expected}, got {actual}");
    }
}

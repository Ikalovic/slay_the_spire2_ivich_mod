using System.Collections;
using System.Reflection;
using System.Runtime.Loader;

return Smoke.Run(args);

internal static class Smoke
{
    private static Assembly _game = null!;
    private static Assembly _baseLib = null!;
    private static Assembly _mod = null!;
    private static Type _modelDb = null!;
    private static readonly Dictionary<Type, object> Models = [];
    private static object[] _cards = [];
    private static object _pool = null!;
    private static Type? _harmonyType;

    public static int Run(string[] args)
    {
        if (args.Length > 1)
        {
            Console.Error.WriteLine("Usage: dotnet run --project tests/Ivich.Mod.Smoke -c Release -- [Ivich.dll]");
            return 2;
        }
        try
        {
            Load(args.FirstOrDefault());
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"SETUP FAILED: {Unwrap(error)}");
            return 2;
        }

        (string Name, Action Body)[] tests =
        [
            ("all concrete models construct through the real game ModelDb", RegisterModels),
            ("BaseLib pool registration contains every enabled card and no UI choices", PoolRegistration),
            ("starter deck has eleven legal cards with three Strikes and three Defends", StarterDeck),
            ("starter relic and starting HP match the approved foundation", StarterRelic),
            ("all choice cards remain hidden, non-generated, and renderable through a pool", ChoiceMetadata),
            ("ancient form card uses the native tooth upgrade route and returns to hand", AncientContract),
            ("returning control cards retain native duplicate and forced-exhaust semantics", ReturningCards),
            ("every enabled card can clone and upgrade without changing its canonical model", CloneAndUpgrade),
            ("representative base and upgrade values match the design", DesignValues),
            ("native growth clones, upgrades, save roundtrip and formula previews preserve card identity", () => GrowthChecks.Run(_game, Card)),
            ("BaseLib receives mandatory mana and rage costs; Dragon Bite has no mandatory rage", ResourceCosts),
            ("both form reward categories include a Power for the native merchant slot", MerchantPowerCoverage),
            ("dual-form rewards restore all 72 ordinary cards while preserving resource gates", RewardCompatibility),
            ("Harmony installs the actual mod patches against the installed game API", InstallPatches),
            ("mixed upgraded ability installations sum their printed benefits", AbilityStacking)
        ];
        var failed = 0;
        var passed = 0;
        var attempted = 0;
        try
        {
            foreach (var (name, body) in tests)
            {
                attempted++;
                try { body(); passed++; Console.WriteLine($"PASS {name}"); }
                catch (Exception error)
                {
                    failed++;
                    Console.Error.WriteLine($"FAIL {name}: {Unwrap(error)}");
                    if (Models.Count == 0) break;
                }
            }
        }
        finally
        {
            if (_harmonyType is not null)
            {
                try
                {
                    var harmony = Activator.CreateInstance(_harmonyType, "Ivich")!;
                    // The game's installed Harmony exposes the legacy owner-filtered UnpatchAll API.
                    _harmonyType.GetMethod("UnpatchAll", [typeof(string)])!.Invoke(harmony, ["Ivich"]);
                    Require(OwnedPatchedMethods().Length == 0, "Ivich patches remained after cleanup");
                    Console.WriteLine("PASS Harmony cleanup removed every Ivich patch");
                }
                catch (Exception error)
                {
                    failed++;
                    Console.Error.WriteLine($"FAIL Harmony cleanup: {Unwrap(error)}");
                }
            }
        }
        Console.WriteLine($"{passed}/{attempted} adapter smoke checks passed; {_cards.Length} enabled cards inspected.");
        Console.WriteLine("Scope: real managed constructors, registration, metadata, upgrades and Harmony installation. Godot startup, assets, UI and combat execution still require the game.");
        return failed == 0 ? 0 : 1;
    }

    private static void Load(string? explicitModPath)
    {
        var config = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(item => item.Key, item => item.Value!);
        var gameDir = Path.GetFullPath(config["Sts2DataDir"]);
        var baseLibDir = Path.GetFullPath(config["BaseLibPath"]);
        var modPath = Path.GetFullPath(explicitModPath ?? config["IvichAssemblyPath"]);
        foreach (var path in new[] { Path.Combine(gameDir, "sts2.dll"), Path.Combine(baseLibDir, "BaseLib.dll"), modPath })
            if (!File.Exists(path)) throw new FileNotFoundException("Build the mod and configure Local.props before running adapter smoke checks.", path);
        string[] directories = [gameDir, baseLibDir, Path.GetDirectoryName(modPath)!];
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            foreach (var directory in directories)
            {
                var candidate = Path.Combine(directory, name.Name + ".dll");
                if (File.Exists(candidate)) return context.LoadFromAssemblyPath(candidate);
            }
            return null;
        };
        _game = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(gameDir, "sts2.dll"));
        _baseLib = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(baseLibDir, "BaseLib.dll"));
        _mod = AssemblyLoadContext.Default.LoadFromAssemblyPath(modPath);
        _modelDb = _game.GetType("MegaCrit.Sts2.Core.Models.ModelDb", true)!;
        Console.WriteLine($"Inspecting {modPath}");
        Console.WriteLine($"Dependencies: {_game.GetName().Name} {_game.GetName().Version}; {_baseLib.GetName().Name} {_baseLib.GetName().Version}");
    }

    private static void RegisterModels()
    {
        var modelBase = _game.GetType("MegaCrit.Sts2.Core.Models.AbstractModel", true)!;
        var inject = _modelDb.GetMethod("Inject")!;
        var getId = _modelDb.GetMethod("GetId", [typeof(Type)])!;
        var getById = _modelDb.GetMethod("GetById")!;
        foreach (var type in _mod.GetTypes().Where(type => !type.IsAbstract && modelBase.IsAssignableFrom(type)))
        {
            // This is the game's public entry point documented for mods and tests, not a replacement model database.
            inject.Invoke(null, [type]);
            var id = getId.Invoke(null, [type]);
            Models.Add(type, getById.MakeGenericMethod(type).Invoke(null, [id])!);
        }
        Require(Models.Count > 0, "The DLL did not register any game models");
        var ids = Models.Values.Select(model => Get(model, "Id").ToString()).ToArray();
        Require(ids.Distinct().Count() == ids.Length, "Two models share a native model ID");
        _pool = Model("Ivich.Mod.Character.IvichCardPool");
        _cards = Items(Get(_pool, "AllCards"));
    }

    private static void PoolRegistration()
    {
        var declared = Items(_mod.GetType("Ivich.Mod.Cards.ImplementedCards", true)!
            .GetProperty("EnabledCardIds")!.GetValue(null)!).Cast<string>().ToHashSet();
        var actual = _cards.Select(card => (string)Get(card, "DesignId")).ToArray();
        Require(actual.Distinct().Count() == actual.Length, "A card is duplicated in the actual BaseLib pool");
        Require(declared.SetEquals(actual), "Enabled manifest and actual registered pool differ");
        Equal(82, actual.Length);
        Equal(72, _cards.Count(card => Get(card, "Rarity").ToString() is "Common" or "Uncommon" or "Rare"));
        Require(_cards.All(card => Get(card, "ShouldShowInCardLibrary") is true), "An enabled card is hidden from the library");
    }

    private static void StarterDeck()
    {
        var deck = Items(Get(Model("Ivich.Mod.Character.IvichCharacter"), "StartingDeck"));
        Equal(11, deck.Length);
        var ids = deck.Select(card => (string)Get(card, "DesignId")).ToArray();
        Equal(3, ids.Count(id => id == "B01"));
        Equal(3, ids.Count(id => id == "B02"));
        foreach (var id in new[] { "B03", "B04", "B05", "B06", "C07" }) Equal(1, ids.Count(value => value == id));
        Require(deck.All(card => (int)Get(card, "NativeRageCost") == 0), "Initial deck contains a mandatory Dragon-only card");
    }

    private static void StarterRelic()
    {
        var character = Model("Ivich.Mod.Character.IvichCharacter");
        Equal(88, (int)Get(character, "StartingHp"));
        var relics = Items(Get(character, "StartingRelics"));
        Equal(1, relics.Length);
        Require(ReferenceEquals(relics[0], Model("Ivich.Mod.Relics.MoonEyeScythe")), "Starter relic differs from MoonEyeScythe");
        Equal("Starter", Get(relics[0], "Rarity").ToString()!);
    }

    private static void ChoiceMetadata()
    {
        var cardBase = _game.GetType("MegaCrit.Sts2.Core.Models.CardModel", true)!;
        var choiceCards = Models.Values.Where(model => cardBase.IsInstanceOfType(model) && !_cards.Contains(model)).ToArray();
        foreach (var required in new[] { "ChooseMageForm", "ChooseDragonForm", "FoldBoundaryInscription", "SweetnessInscription" })
            Require(choiceCards.Any(card => card.GetType().Name == required), $"Required choice {required} is missing");
        foreach (var choice in choiceCards)
        {
            Equal(false, (bool)Get(choice, "ShouldShowInCardLibrary"));
            Equal(false, (bool)Get(choice, "CanBeGeneratedInCombat"));
            Equal("Token", Get(choice, "Rarity").ToString()!);
            Require(ReferenceEquals(_pool, Get(choice, "Pool")), $"Choice {choice.GetType().Name} cannot resolve its visual pool");
            Call(choice, "ToMutable");
        }
    }

    private static void AbilityStacking()
    {
        var canonical = Model("Ivich.Mod.Powers.ColdBloodPower");
        var power = canonical.GetType().GetMethod("ToMutable", [typeof(int)])!.Invoke(canonical, [0])!;
        // Exercise the real model's benefit calculation with native Amount storage.
        // Applying a power needs a Player/Creature; Player construction requires the Godot engine.
        var nativePower = _game.GetType("MegaCrit.Sts2.Core.Models.PowerModel", true)!;
        nativePower.GetField("_amount", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(power, 3);
        var advanced = _mod.GetType("Ivich.Mod.Powers.AdvancedAbilityPower", true)!;
        advanced.GetProperty("UpgradedInstallations", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(power, 1);
        Equal(7, (int)advanced.GetMethod("Benefit", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(power, [2, 3])!);
        var frozen = _mod.GetType("Ivich.Mod.Powers.FrozenTurnPower", true)!;
        foreach (var method in new[] { "BeforeSideTurnStart", "ShouldDraw", "ShouldPlayerResetEnergy", "ShouldPlay", "AfterPlayerTurnStartLate", "AfterSideTurnEnd" })
            Require(frozen.GetMethod(method)!.GetBaseDefinition().DeclaringType != frozen, $"Frozen turn hook {method} does not override the actual game hook");
    }

    private static void AncientContract()
    {
        var ancient = Card("A01");
        Equal("Ancient", Get(ancient, "Rarity").ToString()!);
        Equal(false, (bool)Get(ancient, "CanBeGeneratedInCombat"));
        Equal(false, (bool)Get(ancient, "IsMagic"));
        var starter = Card("B03");
        var route = _baseLib.GetType("BaseLib.Abstracts.ITranscendenceCard", true)!;
        Require(route.IsInstanceOfType(starter), "B03 does not participate in BaseLib's real Archaic Tooth route");
        Require(ReferenceEquals(ancient, route.GetMethod("GetTranscendenceTransformedCard")!.Invoke(starter, null)), "The ancient upgrade route does not lead to A01");
        var keywords = Items(Get(ancient, "CanonicalKeywords")).Select(value => value.ToString()).ToArray();
        Require(keywords.Contains("Innate") && keywords.Contains("Retain"), "A01 lacks innate/retain keywords");
        Equal("Hand", ancient.GetType().GetMethod("GetResultPileTypeForCardPlay", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(ancient, null)!.ToString()!);
    }

    private static void ReturningCards()
    {
        string Destination(object card) => card.GetType().GetMethod("GetResultPileTypeForCardPlay", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(card, null)!.ToString()!;
        var native = _game.GetType("MegaCrit.Sts2.Core.Models.CardModel", true)!;
        foreach (var id in new[] { "A01", "T03" })
        {
            var ordinary = Call(Card(id), "ToMutable"); Equal("Hand", Destination(ordinary));
            var duplicate = Call(Card(id), "ToMutable");
            native.GetProperty("IsDupe")!.SetValue(duplicate, true); Equal("None", Destination(duplicate));
            var exhausted = Call(Card(id), "ToMutable");
            native.GetProperty("ExhaustOnNextPlay")!.SetValue(exhausted, true); Equal("Exhaust", Destination(exhausted));
            Equal(false, (bool)Get(exhausted, "ExhaustOnNextPlay"));
            var keyword = Enum.Parse(_game.GetType("MegaCrit.Sts2.Core.Entities.Cards.CardKeyword", true)!, "Exhaust");
            ordinary.GetType().GetMethod("AddKeyword")!.Invoke(ordinary, [keyword]); Equal("Exhaust", Destination(ordinary));
        }
    }

    private static void CloneAndUpgrade()
    {
        foreach (var card in _cards)
        {
            var mutable = Call(card, "ToMutable");
            Require(!ReferenceEquals(card, mutable), "Native card clone returned its canonical instance");
            Equal(true, (bool)Get(mutable, "IsMutable"));
            Call(mutable, "UpgradeInternal");
            Call(mutable, "FinalizeUpgradeInternal");
            Equal(true, (bool)Get(mutable, "IsUpgraded"));
            Equal(false, (bool)Get(card, "IsUpgraded"));
        }
    }

    private static void DesignValues()
    {
        // Independent examples from the current design document, including zero-block upgrade and growing damage.
        (string Id, string Variable, int Base, int Upgraded)[] examples =
        [ ("B01", "Damage", 6, 9), ("B02", "Block", 5, 8), ("B05", "Damage", 7, 10),
          ("B05", "Frost", 3, 4), ("C17", "Block", 0, 5), ("C19", "Damage", 8, 12),
          ("C19", "Growth", 0, 0), ("T01", "Damage", 8, 11) ];
        foreach (var example in examples)
        {
            var card = Card(example.Id);
            Equal(example.Base, Value(card, example.Variable));
            var mutable = Call(card, "ToMutable");
            Call(mutable, "UpgradeInternal");
            Call(mutable, "FinalizeUpgradeInternal");
            Equal(example.Upgraded, Value(mutable, example.Variable));
            Equal(example.Base, Value(card, example.Variable));
        }
    }

    private static void ResourceCosts()
    {
        var resourceApi = _baseLib.GetType("BaseLib.Abstracts.CustomResources`1", true)!;
        int Cost(string resource, string card) => (int)resourceApi.MakeGenericType(_mod.GetType("Ivich.Mod.Resources." + resource, true)!)
            .GetMethod("CanonicalCost")!.Invoke(null, [Card(card)])!;
        Equal(1, Cost("ManaResource", "B05"));
        Equal(2, Cost("ManaResource", "C19"));
        Equal(2, Cost("RageResource", "C12"));
        Equal(-1, Cost("RageResource", "T01"));
        Equal(-1, Cost("ManaResource", "B01"));
    }

    private static void MerchantPowerCoverage()
    {
        var powers = _cards.Where(card => Get(card, "Type").ToString() == "Power" &&
            Get(card, "Rarity").ToString() is "Common" or "Uncommon" or "Rare").ToArray();
        Require(powers.Any(card => (int)Get(card, "NativeRageCost") == 0), "Initial/Mage pool lacks a native merchant Power candidate");
        Require(powers.Any(card => Get(card, "IsMagic") is false), "Dragon pool lacks a native merchant Power candidate");
    }

    private static void RewardCompatibility()
    {
        var filter = _mod.GetType("Ivich.Mod.Rewards.IvichRewardFilter", true)!;
        var formType = _mod.GetType("Ivich.Core.Form", true)!;
        var model = _game.GetType("MegaCrit.Sts2.Core.Models.CardModel", true)!;
        var compatible = filter.GetMethod("IsCompatible", [formType, model, typeof(bool)])
            ?? throw new InvalidOperationException("Pure form compatibility boundary is missing");
        bool Allows(string form, object card, bool dual) => (bool)compatible.Invoke(null, [Enum.Parse(formType, form), card, dual])!;
        foreach (var card in _cards.Where(card => Get(card, "Rarity").ToString() is "Common" or "Uncommon" or "Rare"))
        {
            Require(Allows("Mage", card, true) && Allows("Dragon", card, true), "A01 did not restore a side of the normal reward pool");
        }
        Equal(false, Allows("Mage", Card("R15"), false)); // Native rage X remains a form requirement.
        Equal(true, Allows("Dragon", Card("R15"), false));
        Equal(false, Allows("Dragon", Card("R14"), false));
        Equal(true, Allows("Mage", Card("R14"), false));
        Equal(false, Allows("Dragon", Card("C06"), false));
        Equal(false, Allows("Dragon", Card("C18"), false));
        Equal(true, Allows("Dragon", Card("U26"), false));
        Equal(true, Allows("Initial", Card("U26"), false));
    }

    private static void InstallPatches()
    {
        _harmonyType = Assembly.Load("0Harmony").GetType("HarmonyLib.Harmony", true)!;
        _mod.GetType("Ivich.Mod.MainFile", true)!.GetMethod("Initialize")!.Invoke(null, null);
        var patched = OwnedPatchedMethods();
        Require(patched.Length > 0, "Initialize did not install any Ivich Harmony patches");
        foreach (var target in new[] { "Hook", "CardModel", "CreatureCmd", "Creature", "ArchaicTooth", "StrengthPower", "WeakPower", "NCharacterSelectButton" })
            Require(patched.Any(method => method.DeclaringType?.Name == target), $"Expected API boundary {target} was not patched");
        Console.WriteLine($"Installed {patched.Length} actual Ivich patch targets; removing them at exit.");
    }

    private static MethodBase[] OwnedPatchedMethods() => Items(_harmonyType!.GetMethod("GetAllPatchedMethods")!.Invoke(null, null)!)
        .Cast<MethodBase>().Where(method =>
        {
            var info = _harmonyType.GetMethod("GetPatchInfo")!.Invoke(null, [method]);
            return info is not null && Items(Get(info, "Owners")).Contains("Ivich");
        }).ToArray();

    private static object Card(string id) => _cards.Single(card => (string)Get(card, "DesignId") == id);
    private static object Model(string name) => Models[_mod.GetType(name, true)!];
    private static object Get(object value, string property) => value.GetType().GetProperty(property)!.GetValue(value)!;
    private static object Call(object value, string method) => value.GetType().GetMethod(method, Type.EmptyTypes)!.Invoke(value, null)!;
    private static object[] Items(object value) => ((IEnumerable)value).Cast<object>().ToArray();
    private static int Value(object card, string name)
    {
        var vars = Get(card, "DynamicVars");
        var variable = vars.GetType().GetProperty("Item")!.GetValue(vars, [name])!;
        return (int)Get(variable, "IntValue");
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Equal<T>(T expected, T actual) => Require(EqualityComparer<T>.Default.Equals(expected, actual), $"expected {expected}, got {actual}");
    private static Exception Unwrap(Exception error) => error is TargetInvocationException { InnerException: { } inner } ? Unwrap(inner) : error;
}

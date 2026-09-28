using Ivich.Core;

return TestRunner.Run();

internal static partial class TestRunner
{
    private static readonly List<(string Name, Action Body)> Tests = [];

    public static int Run()
    {
        ResourceTests();
        CalculationTests();
        ProgressTests();
        SpellTests();
        TriggerTests();
        var failed = 0;
        foreach (var (name, body) in Tests)
        {
            try { body(); Console.WriteLine($"PASS {name}"); }
            catch (Exception error) { failed++; Console.Error.WriteLine($"FAIL {name}: {error.Message}"); }
        }
        Console.WriteLine($"{Tests.Count - failed}/{Tests.Count} tests passed");
        return failed == 0 ? 0 : 1;
    }

    private static void Test(string name, Action body) => Tests.Add((name, body));
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"expected {expected}, got {actual}");
    }
    private static void True(bool condition) => Equal(true, condition);
    private static void False(bool condition) => Equal(false, condition);
    private static void Throws<T>(Action body) where T : Exception
    {
        try { body(); }
        catch (T) { return; }
        throw new Exception($"expected {typeof(T).Name}");
    }
}

using System.Reflection;
using Godot;
using TheHaunt.Systems;

namespace TheHaunt.Tests;

/// <summary>
/// Root of scenes/tests/TestRunner.tscn. Discovers every [SimTest] method in this assembly,
/// runs them sequentially in name-sorted order, prints one PASS/FAIL line per test plus a
/// RESULT summary, and quits with exit code 0 (all passed) or 1.
/// </summary>
public partial class TestRunner : Node
{
    // Guard against silent discovery breakage: the suite ships 145 tests
    // (63 phase-2/3 + 24 phase-3b + 1 economy invariant + 5 town art + 12 farm/interior
    // art + 2 TileSet guards + 5 map recipe format + 2 map seed + 4 map editor + 2 source
    // rules + 5 road strip/wrap + 6 road cast + 6 motel/signage + 7 scooter + 1 travel
    // carry). Re-pin to the exact count whenever tests ship.
    private const int MinimumExpectedTests = 145;

    public override async void _Ready()
    {
        int exitCode;
        try
        {
            exitCode = await RunAllAsync();
        }
        catch (Exception e)
        {
            GD.PushError($"Test harness crashed: {e}");
            exitCode = 1;
        }
        GetTree().Quit(exitCode);
    }

    private async Task<int> RunAllAsync()
    {
        // Isolate all save IO under test_* slots and start from a clean slate.
        SaveService.DefaultSlot = "test_autosave";
        DeleteTestSaves();

        List<MethodInfo> tests = DiscoverTests();
        if (tests.Count == 0)
        {
            GD.PushError("Test discovery found zero [SimTest] methods.");
            GD.Print("RESULT: 0 passed, 1 failed");
            DeleteTestSaves();
            return 1;
        }
        if (tests.Count < MinimumExpectedTests)
        {
            GD.PushError($"Test discovery found only {tests.Count} [SimTest] methods " +
                $"(expected at least {MinimumExpectedTests}) — discovery is likely broken.");
            GD.Print("RESULT: 0 passed, 1 failed");
            DeleteTestSaves();
            return 1;
        }

        var context = new TestContext(this, GetTree());
        int passCount = 0;
        int failCount = 0;

        foreach (MethodInfo test in tests)
        {
            string name = $"{test.DeclaringType!.Name}.{test.Name}";
            try
            {
                object? result = test.Invoke(null, new object[] { context });
                if (result is Task task)
                {
                    await task;
                }
                passCount++;
                GD.Print($"PASS {name}");
            }
            catch (Exception e)
            {
                failCount++;
                GD.Print($"FAIL {name}: {DescribeFailure(e)}");
            }
        }

        GD.Print($"RESULT: {passCount} passed, {failCount} failed");
        DeleteTestSaves();
        return failCount == 0 ? 0 : 1;
    }

    private static List<MethodInfo> DiscoverTests()
    {
        return Assembly.GetExecutingAssembly()
            .GetTypes()
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttribute<SimTestAttribute>() != null)
            .OrderBy(method => $"{method.DeclaringType!.Name}.{method.Name}", StringComparer.Ordinal)
            .ToList();
    }

    private static string DescribeFailure(Exception exception)
    {
        Exception actual = exception is TargetInvocationException { InnerException: not null } tie
            ? tie.InnerException!
            : exception;
        string message = actual is TestFailedException
            ? actual.Message
            : $"{actual.GetType().Name}: {actual.Message}";
        // The output contract is one line per test.
        return message.ReplaceLineEndings(" ");
    }

    private static void DeleteTestSaves()
    {
        string directory = SaveService.SaveDirectory;
        if (!Directory.Exists(directory))
        {
            return;
        }
        foreach (string pattern in new[] { "test_*.json", "test_*.json.tmp" })
        {
            foreach (string file in Directory.GetFiles(directory, pattern))
            {
                File.Delete(file);
            }
        }
    }
}

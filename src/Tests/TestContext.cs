using System.Diagnostics;
using Godot;

namespace TheHaunt.Tests;

/// <summary>Per-run helper handed to every [SimTest] method: assertions plus frame-based waiting.</summary>
public sealed class TestContext
{
    /// <summary>The TestRunner node — use it to add/remove test scene instances.</summary>
    public Node Host { get; }

    public SceneTree Tree { get; }

    public TestContext(Node host, SceneTree tree)
    {
        Host = host;
        Tree = tree;
    }

    public void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new TestFailedException(message);
        }
    }

    public void AssertEqual<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new TestFailedException($"{label}: expected '{expected}' but got '{actual}'");
        }
    }

    public async Task WaitFrames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await Tree.ToSignal(Tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    /// <summary>Polls once per process frame; returns false if the timeout elapses first.</summary>
    public async Task<bool> WaitUntil(Func<bool> condition, double timeoutSeconds = 5.0)
    {
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            if (condition())
            {
                return true;
            }
            if (stopwatch.Elapsed.TotalSeconds >= timeoutSeconds)
            {
                return false;
            }
            await Tree.ToSignal(Tree, SceneTree.SignalName.ProcessFrame);
        }
    }
}

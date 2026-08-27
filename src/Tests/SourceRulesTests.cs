using Godot;

namespace TheHaunt.Tests;

/// <summary>
/// Rules about the SOURCE TREE rather than about behaviour — the ones CLAUDE.md states as
/// standing architecture and that nothing else can catch, because breaking them compiles,
/// runs and passes every other test in this suite.
///
/// Reflection cannot see these: a `using` directive leaves no trace in metadata, and a
/// [Tool] attribute in the wrong folder is perfectly valid C#. So these read the files.
/// System.IO over a globalized res:// path rather than DirAccess, because .cs files are
/// project sources rather than imported resources — and because a test that silently found
/// zero files would pass forever, every count below is asserted non-zero first.
/// </summary>
public static class SourceRulesTests
{
    [SimTest]
    public static void Source_CoreHasNoGodotDependency(TestContext t)
    {
        // CLAUDE.md calls src/Core "PURE C# (no `using Godot`, test-enforced)". Until this
        // test that claim was half true: Save_NoGodotTypesInDtos checks the save DTOs'
        // property TYPES, which is a narrower thing than the whole layer's independence.
        // The rule is what makes the model testable without a scene tree, and it is one
        // stray `using` away from being lost.
        string[] files = SourceFiles("res://src/Core");
        t.Assert(files.Length > 20, $"found the Core sources to check ({files.Length} files)");

        List<string> offenders = files
            .Where(path => File.ReadAllText(path).Contains("using Godot", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .Select(name => name ?? "?")
            .ToList();

        t.AssertEqual("", string.Join(", ", offenders),
            "no file under src/Core may take a dependency on Godot");
    }

    [SimTest]
    public static void Source_EditorHintsStayOutOfTheGameLayers(TestContext t)
    {
        // The map editor works by hand-instantiating the four autoloads and letting map code
        // run in the editor COMPLETELY UNMODIFIED — that is the whole reason maps could stay
        // C# instead of migrating to .tscn. The first time somebody "fixes" an editor
        // misbehaviour with an Engine.IsEditorHint() guard in src/World, that property is
        // gone and the divergence between what the editor shows and what the game runs
        // starts. Editor concerns live in src/EditorTools and addons only.
        string[] layers = { "src/World", "src/Systems", "src/Player", "src/UI", "src/Story", "src/Core" };
        var offenders = new List<string>();
        int scanned = 0;

        foreach (string layer in layers)
        {
            foreach (string path in SourceFiles($"res://{layer}"))
            {
                scanned++;
                string text = File.ReadAllText(path);
                // Strip comments' worth of false positives cheaply: only a real call or a
                // real attribute matters, and both of those are code, not prose.
                if (text.Contains("Engine.IsEditorHint()", StringComparison.Ordinal)
                    || text.Contains("[Tool]", StringComparison.Ordinal))
                {
                    offenders.Add($"{layer}/{Path.GetFileName(path)}");
                }
            }
        }

        t.Assert(scanned > 40, $"found the game-layer sources to check ({scanned} files)");
        t.AssertEqual("", string.Join(", ", offenders),
            "Engine.IsEditorHint() and [Tool] belong in src/EditorTools and addons, nowhere else");
    }

    private static string[] SourceFiles(string resPath)
    {
        string root = ProjectSettings.GlobalizePath(resPath);
        return Directory.Exists(root)
            ? Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            : Array.Empty<string>();
    }
}

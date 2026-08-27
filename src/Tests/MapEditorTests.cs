using Godot;
using TheHaunt.Core;
using TheHaunt.EditorTools;
using TheHaunt.Systems;
using TheHaunt.World;

namespace TheHaunt.Tests;

/// <summary>
/// The two pieces of the placement editor that can be wrong without anyone noticing.
///
/// The mapper itself is an EditorPlugin and cannot run here — this is a game process,
/// where <c>Engine.IsEditorHint()</c> is false and MapStage frees itself on sight. But
/// the parts a dragged tile actually depends on are pure: how much ground a record covers
/// (<see cref="PlacementFootprint"/>), which record a click grabs
/// (<see cref="PlacementHit"/>), and the reserved set the overlay draws. A wrong footprint
/// is invisible until someone grabs the wrong tree and moves it; a wrong reserved set is
/// invisible until someone drops a boulder on a cell that turns out to be a doorway.
/// </summary>
public static class MapEditorTests
{
    /// <summary>
    /// The tool's one data-loss path, pinned. A recipe file that will not parse leaves the
    /// stage holding an EMPTY fallback recipe; the map then falls back to its own code
    /// defaults, so the viewport looks completely normal; and the dirty check compares the
    /// fallback against an unreadable file and says "dirty", which lights the Save button.
    /// Pressing it used to replace an authored map with an empty placement list.
    ///
    /// MapStage is a [Tool] node but an ordinary C# object here — never added to the tree,
    /// so the editor guard in _Ready never runs and never frees it, and nothing rebuilds.
    /// </summary>
    [SimTest]
    public static void Editor_SaveRefusesToOverwriteAnUnreadableRecipe(TestContext t)
    {
        const string probeMap = "test_unreadable_probe";
        const string broken = "{ this is not json";
        string path = MapRecipeFile.PathFor(probeMap);

        WriteRaw(path, broken);
        try
        {
            var stage = new MapStage { StageMapId = probeMap };

            t.Assert(stage.RecipeUnreadable, "a malformed file reads as unreadable");
            t.Assert(stage.IsDirty, "and as dirty — which is exactly why the button used to light up");
            t.Assert(!stage.CanSave, "but saving is refused, so the dock can grey the button");

            stage.SaveRecipe();

            t.AssertEqual(broken, ReadRaw(path), "the file on disk is untouched, byte for byte");
            t.Assert(stage.LastError.Contains(path),
                "and the refusal names the file to go and fix");

            stage.Free();
        }
        finally
        {
            DirAccess.RemoveAbsolute(path);
        }
    }

    private static void WriteRaw(string path, string text)
    {
        DirAccess.MakeDirRecursiveAbsolute(MapRecipeFile.Folder);
        using Godot.FileAccess file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write)
            ?? throw new InvalidOperationException($"Could not write the probe file at '{path}'.");
        file.StoreString(text);
    }

    private static string ReadRaw(string path)
    {
        using Godot.FileAccess file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read)
            ?? throw new InvalidOperationException($"Probe file vanished from '{path}'.");
        return file.GetAsText();
    }

    [SimTest]
    public static void Editor_FootprintsComeFromTheArtNotTheFile(TestContext t)
    {
        // A tree is 48x64 — three tiles wide, four tall — and its record names the LEFT
        // COLUMN of its BASE ROW, so the footprint climbs off the cell rather than hanging
        // below it. Getting this upside down is the single easiest bug in the tool, and it
        // would look exactly like a tree that cannot be clicked.
        var tree = new MapPlacement(PlacementKinds.Prop, FarmBuildings.TreeLeafyId, 2, 12);
        t.AssertEqual(new Rect2I(2, 9, 3, 4), PlacementFootprint.Of(tree),
            "a tree rises off its base row");
        t.AssertEqual(12, PlacementFootprint.Area(tree), "and claims twelve cells");
        t.Assert(PlacementFootprint.Of(tree).HasPoint(new Vector2I(2, 12)),
            "the record's own cell is inside its footprint");

        t.AssertEqual(new Rect2I(12, 2, 3, 4),
            PlacementFootprint.Of(new MapPlacement(PlacementKinds.Prop, FarmBuildings.TreeBareId, 12, 5)),
            "the bare tree measures the same, off its own rect");

        // Two tiles wide because BinClosed is 32px wide — never because a field said so.
        t.AssertEqual(new Rect2I(10, 8, 2, 1),
            PlacementFootprint.Of(new MapPlacement(PlacementKinds.ShippingBin, FarmBuildings.BinId, 10, 8)),
            "the bin's span is its art's");

        // An exit has no art to measure, so here the file IS the authority.
        var road = new MapPlacement(PlacementKinds.Exit, MapIds.Town, 38, 14);
        road.SetInt(PlacementFields.Width, 2);
        road.SetInt(PlacementFields.Height, 2);
        t.AssertEqual(new Rect2I(38, 14, 2, 2), PlacementFootprint.Of(road),
            "an exit's trigger is w by h");
        t.AssertEqual(new Rect2I(38, 14, 1, 1),
            PlacementFootprint.Of(new MapPlacement(PlacementKinds.Exit, MapIds.Town, 38, 14)),
            "and one tile when the file does not say");

        // One tile for the kinds that are one tile, and — the case that matters — one tile
        // rather than a throw for an id this build cannot resolve. The map fails loudly on
        // a bad id at BUILD time; the overlay must not also fall over drawing it.
        t.AssertEqual(new Rect2I(7, 8, 1, 1),
            PlacementFootprint.Of(new MapPlacement(PlacementKinds.Spawn, "house_door", 7, 8)),
            "a spawn marker is one cell");
        t.AssertEqual(new Rect2I(4, 4, 1, 1),
            PlacementFootprint.Of(new MapPlacement(PlacementKinds.Prop, "no_such_tree", 4, 4)),
            "an unresolvable prop id still draws, as one cell");
    }

    [SimTest]
    public static void Editor_ClickGrabsThePlacementYouAimedAt(TestContext t)
    {
        var recipe = new MapRecipe(MapIds.Farm);
        MapPlacement tree = recipe.Add(PlacementKinds.Prop, FarmBuildings.TreeLeafyId, 2, 12);
        MapPlacement boulder = recipe.Add(PlacementKinds.Scatter, FarmTiles.RockLargeId, 3, 10);
        MapPlacement bin = recipe.Add(PlacementKinds.ShippingBin, FarmBuildings.BinId, 10, 8);
        int treeIndex = 0, boulderIndex = 1, binIndex = 2;

        t.AssertEqual(treeIndex, PlacementHit.At(recipe, tree.Cell),
            "the tree's own cell grabs the tree");
        t.AssertEqual(treeIndex, PlacementHit.At(recipe, new Vector2I(4, 9)),
            "and so does the far corner of its canopy");
        t.AssertEqual(treeIndex, PlacementHit.At(recipe, new Vector2I(3, 12)),
            "and its trunk column");

        // The whole reason hits are ranked: the boulder sits INSIDE the canopy, and a
        // first-match search would make it permanently unreachable.
        t.AssertEqual(boulderIndex, PlacementHit.At(recipe, boulder.Cell),
            "a boulder under the canopy still wins its own cell");

        t.AssertEqual(binIndex, PlacementHit.At(recipe, new Vector2I(11, 8)),
            "the bin is grabbable by its second column, which is art and not a cell in the file");
        t.AssertEqual(-1, PlacementHit.At(recipe, new Vector2I(20, 20)),
            "open pasture grabs nothing, and the click belongs to the editor");

        // Two records stacked on one cell — a door and its spawn marker are a tile apart
        // by convention, not by rule. The later record is the one drawn on top.
        MapPlacement under = recipe.Add(PlacementKinds.Sign, "Sign", 12, 8);
        MapPlacement over = recipe.Add(PlacementKinds.Spawn, "beside_the_sign", 12, 8);
        t.AssertEqual(recipe.Placements.Count - 1, PlacementHit.At(recipe, over.Cell),
            "the topmost record wins a shared cell");
        t.Assert(under.Cell == over.Cell, "the two really are stacked");
    }

    [SimTest]
    public static async Task Farm_ReservedTilesAreTheCellsTheHoeRefuses(TestContext t)
    {
        // The base is empty, and that is the contract: a map with nothing held back has
        // nothing to draw. Never entered the tree, so no WorldSim registration to undo.
        var bare = new MapRoot();
        t.AssertEqual(0, bare.ReservedTiles().Count, "MapRoot reserves nothing by default");
        bare.Free();

        SaveService.Instance.NewGame();
        var map = new TestMap { MapId = MapIds.Farm };
        t.Host.AddChild(map);
        await t.WaitFrames(1);
        try
        {
            IReadOnlyCollection<Vector2I> reserved = map.ReservedTiles();
            t.Assert(reserved.Count > 0, "the farm holds cells back");

            // The invariant worth pinning: the set is exactly what IsTillable consults,
            // so nothing can be in it and still take a hoe. If someone stops filling the
            // set, this is what says so.
            foreach (Vector2I cell in reserved)
            {
                t.Assert(!map.IsTillable(cell.X, cell.Y),
                    $"reserved {cell} refuses the hoe");
            }

            // The three reasons a cell is reserved, one example each — and all three are
            // walkable ground that looks perfectly plantable, which is why the overlay
            // exists at all.
            t.Assert(reserved.Contains(new Vector2I(9, 26)),
                "the pen's gateway is reserved (a shape's one soft cell)");
            t.Assert(reserved.Contains(new Vector2I(38, 15)),
                "the road corridor is reserved (generative terrain, not a placement)");
            t.Assert(reserved.Contains(new Vector2I(7, 7)),
                "the farmhouse doorway is reserved (its blocker belongs to the Door node)");

            // Row 27 is the clear band the rest of the suite hoes across; if it ever ends
            // up reserved, half of FarmArtTests goes with it.
            t.Assert(!reserved.Contains(new Vector2I(20, 27)) && map.IsTillable(20, 27),
                "open pasture is not reserved");
        }
        finally
        {
            map.Free();
            await t.WaitFrames(1);
            SaveService.Instance.NewGame();
        }
    }
}

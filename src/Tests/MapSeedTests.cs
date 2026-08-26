using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;
using TheHaunt.World;
using FileAccess = Godot.FileAccess;

namespace TheHaunt.Tests;

/// <summary>
/// Holds a shipped map recipe to the code seed it was exported from, and proves the map
/// really reads it.
///
/// A map's placements now live in two places at once — the C# literals they shipped as,
/// and data/maps/(mapId).json — for exactly as long as the literals remain the fallback
/// for a missing file. Two copies of the same truth drift, so this is the same tripwire
/// as Save_MigratedKitMatchesNewGame: not a test of behaviour, a test that two things
/// still say the same thing, whose failure is an instruction rather than a bug report.
/// </summary>
public static class MapSeedTests
{
    [SimTest]
    public static void Farm_ShippedRecipeMatchesTheCodeSeed(TestContext t)
    {
        string path = MapRecipeFile.PathFor(MapIds.Farm);
        t.Assert(FileAccess.FileExists(path), $"{path} ships with the game");

        using FileAccess? file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        t.Assert(file != null, "and it opens");
        string shipped = file!.GetAsText();

        // Byte-for-byte against the exporter's own output, not against a re-parse: the
        // point is that the file on disk is what MapRecipeSeeds would write today, so a
        // canonicalising comparison would hide the very thing being watched.
        t.AssertEqual(MapRecipeSeeds.For(MapIds.Farm).ToJson(), shipped,
            $"{path} still matches TestMap.DefaultRecipe(). If this fails, DECIDE which " +
            "one is right: a code change to the defaults means re-running " +
            "MapRecipeSeeds.Export; a deliberate edit to the file means the farm has " +
            "left its seed behind, and the seed (with this guard) should go with it. " +
            "Never quietly re-export over a hand-placed map.");

        // The header check ReadFrom does is the other half of "the file is the farm's":
        // a copy that never got its map renamed would build the wrong map's furniture.
        MapRecipe parsed = MapRecipeFile.ReadFrom(path, MapIds.Farm);
        t.AssertEqual(MapIds.Farm, parsed.MapId, "the file names the map it builds");
        t.Assert(parsed.Placements.All(placement => placement.IsKnown),
            "every record in it is a kind this build knows how to place");
    }

    [SimTest]
    public static async Task Farm_BuildsFromItsShippedRecipe(TestContext t)
    {
        // Without this, the farm passing every geometry test would prove only that the
        // FALLBACK is faithful — the file could be ignored entirely and nothing would
        // notice. RecipeSource is how the map says which of the two it read.
        SaveService.Instance.NewGame();
        string path = MapRecipeFile.PathFor(MapIds.Farm);
        MapRecipe recipe = MapRecipeFile.ReadFrom(path, MapIds.Farm);

        var map = new TestMap { MapId = MapIds.Farm };
        t.Host.AddChild(map);
        await t.WaitFrames(1);
        try
        {
            t.AssertEqual(path, map.RecipeSource, "the farm built itself from the shipped file");
            t.Assert(map.RecipeSource != TestMap.CodeDefaults, "and not from the code seed");

            // Every placement in the file reached the scene, resolved. Counts rather than
            // coordinates on purpose — Farm_GeometryMatchesTheArt already pins the
            // coordinates, and pinning them twice would mean editing two files to move
            // one tree.
            foreach (MapPlacement spawn in recipe.OfKind(PlacementKinds.Spawn))
            {
                t.Assert(map.GetNodeOrNull<Marker2D>($"Spawns/{spawn.Id}") != null,
                    $"spawn '{spawn.Id}' is a marker travel can ask for");
            }
            foreach (MapPlacement sign in recipe.OfKind(PlacementKinds.Sign))
            {
                var post = map.GetNodeOrNull<Sign>($"Interactables/{sign.Id}");
                t.Assert(post != null, $"sign '{sign.Id}' is in the scene under its own name");
                t.AssertEqual(sign.Text(PlacementFields.Text), post!.Message,
                    $"sign '{sign.Id}' carries the copy the file gives it");
            }

            var doors = new List<Door>();
            var exits = new List<MapExit>();
            Collect(map, doors, exits);
            t.AssertEqual(recipe.OfKind(PlacementKinds.Door).Count(), doors.Count,
                "every door in the file is a door in the scene");
            foreach (MapPlacement door in recipe.OfKind(PlacementKinds.Door))
            {
                t.Assert(
                    doors.Any(node => node.TargetMapId == door.Id
                        && node.TargetSpawnId == door.Text(PlacementFields.Spawn, "default")
                        && node.Position == new Vector2(
                            door.X * MapRoot.TileSize + 8, door.Y * MapRoot.TileSize + 8)),
                    $"the door to '{door.Id}' leads where the file says, from the cell it says");
            }

            t.AssertEqual(recipe.OfKind(PlacementKinds.Exit).Count(), exits.Count,
                "and the road exit came from the file too");
            t.AssertEqual(MapIds.Town, exits[0].TargetMapId, "leading east to town");
        }
        finally
        {
            map.Free();
            await t.WaitFrames(1);
            SaveService.Instance.NewGame();
        }
    }

    private static void Collect(Node node, List<Door> doors, List<MapExit> exits)
    {
        if (node is Door door)
            doors.Add(door);
        if (node is MapExit exit)
            exits.Add(exit);
        foreach (Node child in node.GetChildren())
            Collect(child, doors, exits);
    }
}

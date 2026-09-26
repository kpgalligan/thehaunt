using TheHaunt.Core;
using TheHaunt.World;

namespace TheHaunt.Tests;

/// <summary>
/// The recipe format's contract, which is a TEXT contract as much as a data one. A map
/// file is meant to be read, hand-edited and merged like source, so "the same recipe
/// serialises to the same bytes" is not a nicety here — it is the difference between a
/// one-line diff when a prop moves and a whole-file churn every time anyone saves.
///
/// The other half is the preserve rule: a record this build does not understand comes
/// back out exactly as it went in. Without it, opening a newer branch's map in an older
/// build and pressing save silently deletes the newer branch's work — the same failure
/// the save system's unknown-item rule exists to prevent.
/// </summary>
public static class MapRecipeTests
{
    [SimTest]
    public static void MapRecipe_CanonicalTextIsByteStable(TestContext t)
    {
        MapRecipe recipe = Sample();

        string once = recipe.ToJson();
        t.AssertEqual(once, recipe.ToJson(), "serialising the same recipe twice");

        // The layout rules the diff legibility rests on.
        t.Assert(!once.Contains('\r'), "line endings are \\n on every platform");
        t.Assert(once.EndsWith("\n", StringComparison.Ordinal), "the file ends in a newline");
        string[] lines = once.Split('\n');
        int records = lines.Count(line => line.TrimStart().StartsWith("{\"kind\"", StringComparison.Ordinal));
        t.AssertEqual(recipe.Placements.Count, records, "one placement per line, no exceptions");

        // The deliberate off-grid exception survives as an exception: written only when
        // it is one, so a reader can see which placements are nudged on purpose.
        t.Assert(once.Contains("\"dy\": -8", StringComparison.Ordinal), "the bed's nudge is in the text");
        t.AssertEqual(1, lines.Count(line => line.Contains("\"dy\":", StringComparison.Ordinal)),
            "and only the nudged placement carries one");
    }

    [SimTest]
    public static void MapRecipe_OrderIsDeterministicWhateverTheInsertionOrder(TestContext t)
    {
        // Same placements, opposite insertion order, and every tier of the tie-break
        // exercised: two kinds on one cell, two ids on one cell and kind, and two records
        // differing only in a field.
        var forwards = new MapRecipe(MapIds.Town);
        forwards.Add(PlacementKinds.Prop, "bench_a", 22, 19);
        forwards.Add(PlacementKinds.Prop, "bench_b", 22, 19);
        forwards.Add(PlacementKinds.Scatter, "rock_large", 22, 19);
        forwards.Add(PlacementKinds.Prop, "well", 25, 19);
        forwards.Add(PlacementKinds.Spawn, "from_farm", 2, 15);
        forwards.Add(PlacementKinds.Prop, "planter", 21, 12).SetInt("shade", 1);
        forwards.Add(PlacementKinds.Prop, "planter", 21, 12).SetInt("shade", 2);

        var backwards = new MapRecipe(MapIds.Town);
        foreach (MapPlacement placement in forwards.Placements.Reverse())
        {
            MapPlacement copy = backwards.Add(placement.Kind, placement.Id, placement.X, placement.Y);
            foreach ((string key, string raw) in placement.Fields)
            {
                copy.SetRaw(key, raw);
            }
        }

        t.AssertEqual(forwards.ToJson(), backwards.ToJson(), "insertion order does not reach the file");

        // And the order is the one a reader expects: down the map, then across.
        string[] records = forwards.ToJson().Split('\n')
            .Where(line => line.TrimStart().StartsWith("{\"kind\"", StringComparison.Ordinal))
            .ToArray();
        t.AssertEqual(7, records.Length, "every placement written");
        t.Assert(records[0].Contains("\"y\": 12", StringComparison.Ordinal), "lowest y first");
        t.Assert(records[2].Contains("\"id\": \"from_farm\"", StringComparison.Ordinal),
            "then y 15 — the spawn");
        t.Assert(records[3].Contains("\"id\": \"bench_a\"", StringComparison.Ordinal),
            "then y 19, x 22, kind 'prop' before 'scatter', id 'bench_a' before 'bench_b'");
        t.Assert(records[4].Contains("\"id\": \"bench_b\"", StringComparison.Ordinal), "id breaks the kind tie");
        t.Assert(records[5].Contains("\"kind\": \"scatter\"", StringComparison.Ordinal), "kind breaks the cell tie");
    }

    // ------------------------------------------------------------------
    // Fixtures and plumbing
    // ------------------------------------------------------------------

    /// <summary>
    /// A recipe covering every part of the record: several kinds, extra fields of all
    /// three value types, and the farmhouse bed's real off-grid nudge (FarmHouseMap puts
    /// it at 3 * TileSize — half a cell above the centre of (12,3), because it is a 16x32
    /// piece standing across two cells).
    /// </summary>
    private static MapRecipe Sample()
    {
        var recipe = new MapRecipe(MapIds.FarmHouse);
        recipe.Add(PlacementKinds.Furniture, "stove", 3, 2);
        recipe.Add(PlacementKinds.Furniture, "till", 6, 4).SetBool(PlacementFields.Blocks, false);
        recipe.Add(PlacementKinds.Chest, StorageIds.FarmHouseChest, 2, 2);
        recipe.Add(PlacementKinds.Bed, "bed", 12, 3).NudgeY = -8;
        recipe.Add(PlacementKinds.Spawn, "entry", 7, 8);
        MapPlacement door = recipe.Add(PlacementKinds.Door, MapIds.Farm, 7, 9);
        door.SetText(PlacementFields.Spawn, "house_door");
        MapPlacement sign = recipe.Add(PlacementKinds.Sign, "blockade", 36, 13);
        sign.SetText(PlacementFields.Text, "The storm brought half the hillside down.");
        recipe.Add(PlacementKinds.Exit, MapIds.Town, 39, 15).SetInt(PlacementFields.Width, 2);
        return recipe;
    }
}

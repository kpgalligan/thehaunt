using Godot;
using TheHaunt.Core;
using TheHaunt.World;
using FileAccess = Godot.FileAccess;

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
    private const string Folder = "user://test_recipes/";

    [SimTest]
    public static void MapRecipe_CanonicalTextIsByteStable(TestContext t)
    {
        MapRecipe recipe = Sample();

        string once = recipe.ToJson();
        t.AssertEqual(once, recipe.ToJson(), "serialising the same recipe twice");

        // The round trip, twice over: parse -> write must reproduce the bytes, and doing
        // it again must not drift by so much as a space.
        string reloaded = MapRecipe.Parse(once, "<memory>").ToJson();
        t.AssertEqual(once, reloaded, "text survives a parse and re-write");
        t.AssertEqual(once, MapRecipe.Parse(reloaded, "<memory>").ToJson(), "and a second cycle");

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

        MapRecipe parsed = MapRecipe.Parse(once, "<memory>");
        MapPlacement bed = parsed.OfKind(PlacementKinds.Bed).Single();
        t.AssertEqual(new Vector2I(12, 3), bed.Cell, "the bed's tile");
        t.AssertEqual(new Vector2(0, -8), bed.Nudge, "the bed's sub-tile nudge, in pixels");
        t.AssertEqual("house_door", parsed.OfKind(PlacementKinds.Door).Single().Text(PlacementFields.Spawn),
            "a door's target spawn survives as a field");
        MapPlacement till = parsed.OfKind(PlacementKinds.Furniture).Single(piece => piece.Id == "till");
        t.AssertEqual(false, till.Bool(PlacementFields.Blocks, true),
            "the till's blocks:false survives as a field");
        t.AssertEqual(true, parsed.OfKind(PlacementKinds.Furniture)
            .Single(piece => piece.Id == "stove").Bool(PlacementFields.Blocks, true),
            "and a piece with no 'blocks' field falls back to blocking");
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

    [SimTest]
    public static void MapRecipe_UnknownRecordsRoundTripVerbatim(TestContext t)
    {
        // A file from a branch that has a kind this build has never heard of, plus an
        // unknown field on a kind it does know. Written in canonical form, so "survives"
        // can be asserted as "byte-identical" rather than merely "semantically equal".
        string original = Text(
            "{",
            "  \"version\": 1,",
            "  \"map\": \"town\",",
            "  \"placements\": [",
            "    {\"kind\": \"weather_vane\", \"id\": \"cupola_vane\", \"x\": 23, \"y\": 4, " +
            "\"creaks\": true, \"pitch\": 3, \"whisper\": \"it turns against the wind\"},",
            "    {\"kind\": \"prop\", \"id\": \"bench_a\", \"x\": 22, \"y\": 19, \"weathering\": 2}",
            "  ]",
            "}");

        MapRecipe recipe = MapRecipe.Parse(original, "<memory>");
        t.AssertEqual(2, recipe.Placements.Count, "the unknown record was kept, not dropped");

        MapPlacement vane = recipe.Placements.Single(p => p.Kind == "weather_vane");
        t.Assert(!vane.IsKnown, "this build cannot build a weather vane");
        t.AssertEqual(new Vector2I(23, 4), vane.Cell, "an unknown kind still has a readable cell");
        t.AssertEqual(true, vane.Bool("creaks"), "its bool field");
        t.AssertEqual(3, vane.Int("pitch"), "its number field");
        t.AssertEqual("it turns against the wind", vane.Text("whisper"), "its string field");
        t.AssertEqual(2, recipe.Placements.Single(p => p.IsKnown).Int("weathering"),
            "an unknown field on a KNOWN kind is kept too");

        t.AssertEqual(original, recipe.ToJson(), "load then save is byte-identical");
        t.AssertEqual(original, MapRecipe.Parse(recipe.ToJson(), "<memory>").ToJson(), "and stays so");
    }

    [SimTest]
    public static void MapRecipe_FileRoundTripsAndAMissingOneIsEmpty(TestContext t)
    {
        // The farm's file keeps the id's oddity: MapIds.Farm is literally "test_farm",
        // and the rename waits for the first editor-authored map and its own migration.
        t.AssertEqual("res://data/maps/test_farm.json", MapRecipeFile.PathFor(MapIds.Farm),
            "the farm's recipe path");

        // A map with no recipe yet is not an error — it builds from its code defaults,
        // which is every map today.
        MapRecipe missing = MapRecipeFile.Load("no_such_map");
        t.AssertEqual(0, missing.Placements.Count, "a missing recipe is empty, not a crash");
        t.AssertEqual("no_such_map", missing.MapId, "and still knows which map it is for");
        t.AssertEqual(missing.ToJson(), MapRecipe.Parse(missing.ToJson(), "<memory>").ToJson(),
            "an empty recipe is still valid canonical text");

        string path = Folder + "round_trip.json";
        try
        {
            MapRecipe recipe = Sample();
            MapRecipeFile.WriteTo(recipe, path);
            MapRecipe read = MapRecipeFile.ReadFrom(path, recipe.MapId);
            t.AssertEqual(recipe.ToJson(), read.ToJson(), "what came off disk is what went on it");
            t.AssertEqual(recipe.Placements.Count, read.Placements.Count, "every placement made the trip");

            // A file whose header disagrees with its name is a copy someone never
            // finished renaming, and it would build the wrong map's furniture.
            t.AssertEqual(true, Throws(t, () => MapRecipeFile.ReadFrom(path, MapIds.Town), path),
                "a recipe filed under the wrong map is refused, by path");
        }
        finally
        {
            Delete(path);
        }
    }

    [SimTest]
    public static void MapRecipe_MalformedFilesFailWithTheirPath(TestContext t)
    {
        var broken = new (string Label, string Body)[]
        {
            ("not json at all", "{ this was never JSON"),
            ("root is an array", "[]"),
            ("no map header", "{\"version\": 1, \"placements\": []}"),
            ("no placements array", "{\"version\": 1, \"map\": \"town\"}"),
            ("a record that is not an object", "{\"map\": \"town\", \"placements\": [7]}"),
            ("a record with no kind",
                "{\"map\": \"town\", \"placements\": [{\"id\": \"well\", \"x\": 1, \"y\": 2}]}"),
            ("a record with no tile",
                "{\"map\": \"town\", \"placements\": [{\"kind\": \"prop\", \"id\": \"well\", \"y\": 2}]}"),
            ("a fractional tile",
                "{\"map\": \"town\", \"placements\": [{\"kind\": \"prop\", \"id\": \"w\", \"x\": 1.5, \"y\": 2}]}"),
            // Structure in a field would be data that cannot be held to one line, which
            // is the property the whole format is built on.
            ("a structured field",
                "{\"map\": \"town\", \"placements\": [{\"kind\": \"prop\", \"id\": \"w\", \"x\": 1, \"y\": 2, " +
                "\"tags\": [\"a\"]}]}"),
            ("a repeated field",
                "{\"map\": \"town\", \"placements\": [{\"kind\": \"prop\", \"id\": \"w\", \"x\": 1, \"y\": 2, " +
                "\"x\": 3}]}"),
            // A future BREAKING format: read it wrong and a map builds wrong, silently.
            ("a version from the future", "{\"version\": 99, \"map\": \"town\", \"placements\": []}"),
        };

        string path = Folder + "malformed.json";
        try
        {
            foreach ((string label, string body) in broken)
            {
                Write(path, body);
                t.AssertEqual(true, Throws(t, () => MapRecipeFile.ReadFrom(path), path),
                    $"{label}: fails loudly, naming the file");
            }
        }
        finally
        {
            Delete(path);
        }

        // The one file error that is NOT loud, for contrast.
        t.AssertEqual(0, MapRecipeFile.Load("no_such_map").Placements.Count, "a missing file stays silent");
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

    private static string Text(params string[] lines) => string.Join("\n", lines) + "\n";

    /// <summary>True if the call threw a <see cref="MapRecipeException"/> whose message names the path.</summary>
    private static bool Throws(TestContext t, Action call, string path)
    {
        try
        {
            call();
            return false;
        }
        catch (MapRecipeException e)
        {
            t.AssertEqual(path, e.FilePath, "the exception carries the path");
            t.Assert(e.Message.Contains(path, StringComparison.Ordinal),
                $"the message names the file: {e.Message}");
            return true;
        }
    }

    private static void Write(string path, string body)
    {
        DirAccess.MakeDirRecursiveAbsolute(Folder);
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file.StoreString(body);
        file.Close();
    }

    /// <summary>
    /// Removes the file and then the folder — the folder attempt fails harmlessly while
    /// another test's file is still in it, and the last one out takes the light. Tests
    /// leave nothing behind in user://, the same rule TestRunner's save sweep follows.
    /// </summary>
    private static void Delete(string path)
    {
        if (FileAccess.FileExists(path))
        {
            DirAccess.RemoveAbsolute(path);
        }
        DirAccess.RemoveAbsolute(Folder);
    }
}

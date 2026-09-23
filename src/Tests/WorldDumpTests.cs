using System.Text.Json;
using TheHaunt.Core;
using TheHaunt.World;

namespace TheHaunt.Tests;

/// <summary>
/// The world dump (<see cref="WorldDump"/>, <c>--dump-world</c>) is what the intro
/// flyover's generator stitches the 3D town from, so a hole in it is a hole in the
/// town: every exterior must export with a grid that matches its size, and every road
/// mouth must lead to an exported map that has a mouth leading back — on the opposite
/// edge, near the spawn it arrives on — or the stitcher has nothing to join. The road
/// wrap (west_entry's west mouth -> east_entry's east side, and back) satisfies the
/// same rule, which is exactly why it reads as one continuous road. Determinism is
/// pinned too: a dump that changed run to run would churn every downstream build.
/// </summary>
public static class WorldDumpTests
{
    [SimTest]
    public static async Task World_DumpCoversEveryExteriorAndEveryRoadJoins(TestContext t)
    {
        string first = WorldDump.Render(t.Host);
        string second = WorldDump.Render(t.Host);
        await t.WaitFrames(1);
        t.Assert(first == second, "two renders are byte-identical");
        t.Assert(!first.Contains('\r'), "\\n line endings only");

        using JsonDocument doc = JsonDocument.Parse(first);
        JsonElement root = doc.RootElement;
        t.AssertEqual(WorldDump.Version, root.GetProperty("version").GetInt32(), "schema version");
        t.AssertEqual(MapRoot.TileSize, root.GetProperty("tilePx").GetInt32(), "tile size");

        var maps = root.GetProperty("maps").EnumerateArray().ToList();
        var ids = maps.Select(m => m.GetProperty("id").GetString()!).ToList();
        t.AssertEqual(string.Join(",", WorldDump.ExteriorIds()), string.Join(",", ids),
            "every exterior, in registry order");
        foreach (string expected in new[]
            { MapIds.Farm, MapIds.Town, MapIds.WestEntry, MapIds.Billies, MapIds.Fork,
              MapIds.EastFork, MapIds.EastEntry, MapIds.DriveIn })
        {
            t.Assert(ids.Contains(expected), $"'{expected}' is exported");
        }
        t.Assert(ids.All(id => !MapIds.IsInterior(id)), "no interior is exported");

        var byId = maps.ToDictionary(m => m.GetProperty("id").GetString()!);
        foreach (JsonElement map in maps)
        {
            string id = map.GetProperty("id").GetString()!;
            int width = map.GetProperty("width").GetInt32();
            int height = map.GetProperty("height").GetInt32();
            var legend = map.GetProperty("legend").EnumerateObject().Select(p => p.Name[0]).ToHashSet();
            foreach (string grid in new[] { "surfaces", "blocked" })
            {
                var rows = map.GetProperty(grid).EnumerateArray().Select(r => r.GetString()!).ToList();
                t.AssertEqual(height, rows.Count, $"{id} {grid}: one row per tile row");
                t.Assert(rows.All(r => r.Length == width), $"{id} {grid}: every row is {width} wide");
            }
            t.Assert(map.GetProperty("surfaces").EnumerateArray().All(r => r.GetString()!.All(legend.Contains)),
                $"{id}: every surface char is in its legend");
            t.Assert(map.GetProperty("spawns").GetArrayLength() > 0, $"{id} has spawns");

            var exits = map.GetProperty("exits").EnumerateArray().ToList();
            t.Assert(exits.Count > 0, $"{id} has at least one road mouth");
            foreach (JsonElement exit in exits)
            {
                string to = exit.GetProperty("to").GetString()!;
                string edge = exit.GetProperty("edge").GetString()!;
                string label = $"{id} {exit.GetProperty("id").GetString()} -> {to}";
                t.Assert(byId.ContainsKey(to), $"{label}: destination is an exported map");
                t.Assert(exit.GetProperty("spawnX").ValueKind == JsonValueKind.Number,
                    $"{label}: arrival spawn '{exit.GetProperty("spawn").GetString()}' exists there");
                int sx = exit.GetProperty("spawnX").GetInt32(), sy = exit.GetProperty("spawnY").GetInt32();

                // The reverse mouth: the destination's exit back here, on the opposite
                // edge, within four tiles of the arrival marker (MapRoot.GetArrival's
                // pairing radius) — the wrap pair included.
                bool joined = byId[to].GetProperty("exits").EnumerateArray().Any(back =>
                    back.GetProperty("to").GetString() == id
                    && back.GetProperty("edge").GetString() == Opposite(edge)
                    && EdgeDistance(back, sx, sy) <= 4);
                t.Assert(joined, $"{label}: the destination has a mouth back on the {Opposite(edge)} edge");
            }
        }
    }

    private static string Opposite(string edge) => edge switch
    {
        "N" => "S",
        "S" => "N",
        "E" => "W",
        _ => "E",
    };

    private static int EdgeDistance(JsonElement rect, int x, int y)
    {
        int x0 = rect.GetProperty("x").GetInt32(), y0 = rect.GetProperty("y").GetInt32();
        int x1 = x0 + rect.GetProperty("w").GetInt32() - 1, y1 = y0 + rect.GetProperty("h").GetInt32() - 1;
        int dx = x < x0 ? x0 - x : x > x1 ? x - x1 : 0;
        int dy = y < y0 ? y0 - y : y > y1 ? y - y1 : 0;
        return Math.Max(dx, dy);
    }
}

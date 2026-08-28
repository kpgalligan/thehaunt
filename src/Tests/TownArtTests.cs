using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;
using TheHaunt.World;

namespace TheHaunt.Tests;

/// <summary>
/// Guards the wiring between the town art (docs/designs/design_handoff_town_art) and the
/// geometry the player actually runs into. The art is drawn as sprites over a
/// collision-only tile layer, so "looks right" and "walks right" are two different
/// things and only the second one is testable here.
/// </summary>
public static class TownArtTests
{
    [SimTest]
    public static void Terrain_WalkableMatchesCollision(TestContext t)
    {
        TileSet tileSet = TownTerrain.Get();
        var source = (TileSetAtlasSource)tileSet.GetSource(0);
        t.Assert(source.GetTilesCount() > 50, "the handoff's 55 tiles plus the blocker are registered");

        int blocking = 0;
        for (int i = 0; i < source.GetTilesCount(); i++)
        {
            Vector2I coords = source.GetTileId(i);
            TileData data = source.GetTileData(coords, 0);
            bool walkable = data.GetCustomData(TownTerrain.WalkableData).AsBool();
            bool hasCollision = data.GetCollisionPolygonsCount(0) > 0;
            t.Assert(walkable != hasCollision,
                $"tile {coords}: walkable is derived from collision, never hand-listed");
            if (hasCollision)
                blocking++;
        }
        // The woods set (2 + 1 + 3 masses, 4 corners) plus the collision-only blocker.
        t.AssertEqual(11, blocking, "blocking tile count");

        TileData blocker = source.GetTileData(TerrainTiles.Blocker, 0);
        t.Assert(!blocker.GetCustomData(TownTerrain.WalkableData).AsBool(),
            "the transparent blocker cell is registered and unwalkable");
    }

    [SimTest]
    public static void Terrain_DirtEdgeAndKerbCoverEveryConfiguration(TestContext t)
    {
        var seen = new HashSet<Vector2I>();
        for (int mask = 0; mask < 16; mask++)
        {
            Vector2I tile = TerrainTiles.DirtEdge(
                (mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0, (mask & 8) != 0);
            t.AssertEqual(1, tile.Y, $"grass mask {mask}: dirt set lives on row 1");
            t.Assert(seen.Add(tile), $"grass mask {mask}: maps to a distinct column ({tile.X})");
        }
        t.AssertEqual(new Vector2I(9, 1), TerrainTiles.DirtEdge(false, false, false, false),
            "no grass on any side is the fully-surrounded centre tile");
        t.AssertEqual(new Vector2I(0, 1), TerrainTiles.DirtEdge(true, true, true, true),
            "grass on every side is the isolated patch");

        t.Assert(TerrainTiles.Kerb(false, false, false, false) == null,
            "a cobble cell surrounded by cobble takes no kerb");
        t.AssertEqual(new Vector2I(3, 2), TerrainTiles.Kerb(true, false, false, false)!.Value,
            "kerb_n is named for the side that is NOT cobble");
        t.AssertEqual(new Vector2I(8, 2), TerrainTiles.Kerb(false, true, true, false)!.Value,
            "kerb_se");

        t.Assert(TerrainTiles.DirtInnerCorner(false, true, false, false) is { } inner
            && inner == new Vector2I(12, 2), "one grass diagonal picks its inner corner");
        t.Assert(TerrainTiles.DirtInnerCorner(true, true, false, false) == null,
            "two grass diagonals are not representable — caller falls back to plain dirt");
    }

    [SimTest]
    public static async Task Town_GeometryMatchesTheArt(TestContext t)
    {
        SaveService.Instance.NewGame();
        var map = new TownMap();
        t.Host.AddChild(map);
        await t.WaitFrames(1);

        try
        {
            // The two door cells are the only openings in the facades, unchanged from
            // the procedural placeholder the art replaced.
            t.Assert(!map.IsStandable(new Vector2I(23, 11)), "hall door cell carries the Door blocker");
            t.Assert(!map.IsStandable(new Vector2I(11, 11)), "store door cell carries the Door blocker");
            t.Assert(map.IsStandable(new Vector2I(23, 12)), "hall door approach is walkable");
            t.Assert(map.IsStandable(new Vector2I(11, 12)), "store door approach is walkable");

            foreach (Vector2I wall in new[] { new Vector2I(20, 6), new Vector2I(27, 11), new Vector2I(8, 8) })
                t.Assert(!map.IsStandable(wall), $"building footprint {wall} blocks");

            // The map limit is woods, and it opens only at the two road mouths.
            foreach (Vector2I edge in new[] { new Vector2I(0, 0), new Vector2I(47, 29), new Vector2I(24, 0) })
                t.Assert(!map.IsStandable(edge), $"woods border {edge} blocks");
            t.Assert(map.IsStandable(new Vector2I(0, 14)) && map.IsStandable(new Vector2I(0, 15)),
                "the west road mouth stays open for the fork exit");
            t.Assert(map.IsStandable(new Vector2I(47, 14)) && map.IsStandable(new Vector2I(47, 15)),
                "the east road mouth stays open for the east fork exit");

            // Plaza dressing blocks, but never a tile the intro stages an NPC on.
            t.Assert(!map.IsStandable(new Vector2I(25, 19)), "the well blocks");
            t.Assert(!map.IsStandable(new Vector2I(22, 19)), "a bench blocks");
            foreach (Vector2I staging in new[]
                     { new Vector2I(24, 19), new Vector2I(30, 13), new Vector2I(31, 16), new Vector2I(33, 13) })
            {
                t.Assert(map.IsStandable(staging), $"NPC staging tile {staging} is still clear");
            }

            // Spawn markers land somewhere the player can actually stand.
            foreach (string spawn in new[] { "from_fork", "from_east_fork", "from_hall", "from_store" })
            {
                Vector2 position = map.GetSpawn(spawn);
                var tile = new Vector2I(
                    Mathf.FloorToInt(position.X / MapRoot.TileSize),
                    Mathf.FloorToInt((position.Y + 6) / MapRoot.TileSize));
                t.Assert(map.IsStandable(tile), $"spawn '{spawn}' at {tile} is standable");
            }

            t.Assert(!map.IsInterior, "the town exterior takes the day/night tint");
        }
        finally
        {
            map.Free();
            await t.WaitFrames(1);
        }
    }

    [SimTest]
    public static void DayNight_KeysRunFromNoonToTheClampHour(TestContext t)
    {
        // Minute-of-day 0 is 6:00 AM, so 300 = 11:00 AM and 1020 = 11:00 PM.
        Color day = DayNight.Modulate(300);
        t.Assert(day.R > 0.99f && day.G > 0.99f && day.B > 0.99f,
            "art is authored at the midday values — the day key is a no-op multiply");

        static float Luminance(int minute)
        {
            Color key = DayNight.Modulate(minute);
            return 0.2126f * key.R + 0.7152f * key.G + 0.0722f * key.B;
        }

        float previous = 1f;
        foreach (int minute in new[] { 720, 840, 1020, 1140 })
        {
            float luminance = Luminance(minute);
            t.Assert(luminance < previous, $"minute {minute} is darker than the key before it");
            previous = luminance;
        }
        t.Assert(previous < 0.45f, "the 1:59 AM clamp hour is navigable by lantern only");
        t.AssertEqual(previous, Luminance(1199), "the last key holds to the clock's clamp");

        // Dawn is authored as an overlay; a multiply cannot brighten, so it is applied
        // luminance-preserving instead of dimming the reference state.
        Color dawn = DayNight.Modulate(0);
        t.Assert(dawn.R > dawn.B, "dawn is a warm cast");
        t.Assert(dawn.R > 0.99f && dawn.G > 0.9f, "dawn does not darken the town");

        t.AssertEqual(0f, DayNight.LightLevel(300), "lanterns are out at midday");
        t.Assert(DayNight.LightLevel(720) > 0f, "lanterns light at dusk");
        t.AssertEqual(1f, DayNight.LightLevel(1020), "full lantern light at 11 PM");
        t.AssertEqual(1f, DayNight.LightLevel(1199), "and through the clamp hour");
    }

    [SimTest]
    public static void CastSheets_MatchTheHandoffGrid(TestContext t)
    {
        // Jane replaces the old sheet in place: same 96x96, 6x3 grid of 16x32 cells.
        Image jane = GD.Load<Texture2D>(CharacterSprites.SheetPath).GetImage();
        t.AssertEqual(96, jane.GetWidth(), "character.png width");
        t.AssertEqual(96, jane.GetHeight(), "character.png height");

        // Every NPC names a sheet that exists and a 96px block inside it, and the
        // block actually holds a drawn character (an empty block means the atlas
        // order and the def order drifted apart).
        foreach (NpcDef def in NpcDefs.All.Values)
        {
            var sheet = GD.Load<Texture2D>(def.SpriteSheet);
            t.Assert(sheet != null, $"'{def.Id}': sheet '{def.SpriteSheet}' loads");
            Image image = sheet!.GetImage();
            t.AssertEqual(96, image.GetHeight(), $"'{def.Id}': sheet height");
            t.AssertEqual(0, image.GetWidth() % CharacterSprites.BlockWidth,
                $"'{def.Id}': sheet width is whole blocks");
            t.Assert(def.SpriteBlock >= 0
                && (def.SpriteBlock + 1) * CharacterSprites.BlockWidth <= image.GetWidth(),
                $"'{def.Id}': block {def.SpriteBlock} inside the atlas");

            int drawn = 0;
            for (int y = 0; y < 96; y++)
            {
                for (int x = 0; x < CharacterSprites.BlockWidth; x++)
                {
                    if (image.GetPixel(def.SpriteBlock * CharacterSprites.BlockWidth + x, y).A > 0f)
                        drawn++;
                }
            }
            t.Assert(drawn > 500, $"'{def.Id}': block {def.SpriteBlock} holds a character ({drawn}px)");
        }

        // Dread accents stay off people in Act I (cast handoff rule 5): plum and
        // bile-green appear on no sheet. The first character to wear one is the
        // reveal — a repaint that spends them early spends the whole effect.
        var reserved = new[] { new Color("6b4560"), new Color("7d8f4a") };
        var sheets = new List<string> { CharacterSprites.SheetPath };
        sheets.AddRange(NpcDefs.All.Values.Select(d => d.SpriteSheet).Distinct());
        foreach (string path in sheets)
        {
            Image image = GD.Load<Texture2D>(path).GetImage();
            for (int y = 0; y < image.GetHeight(); y++)
            {
                for (int x = 0; x < image.GetWidth(); x++)
                {
                    Color pixel = image.GetPixel(x, y);
                    if (pixel.A == 0f)
                        continue;
                    foreach (Color accent in reserved)
                    {
                        t.Assert(!pixel.IsEqualApprox(accent),
                            $"{path} pixel {x},{y} wears a reserved dread accent");
                    }
                }
            }
        }

        // Right is a mirror of left: every sheet holds down/left/up only, and a
        // packed atlas offsets by whole 96px blocks.
        t.AssertEqual(1, CharacterSprites.Row(2), "facing 2 reuses the left row");
        t.Assert(CharacterSprites.FlipH(2) && !CharacterSprites.FlipH(1), "facing 2 is the flip");
        t.AssertEqual(new Rect2(2 * 16, 2 * 32, 16, 32), CharacterSprites.Region(0, 3, 2),
            "facing up, first walk frame, block 0");
        t.AssertEqual(new Rect2(96 + 2 * 16, 32, 16, 32), CharacterSprites.Region(1, 1, 2),
            "block 1 offsets the whole grid by 96px");
    }
}

using System.Text;
using Godot;
using TheHaunt.Core;
using FileAccess = Godot.FileAccess;

namespace TheHaunt.World;

/// <summary>
/// The dev-only exporter behind <c>--seed-tiled (mapId)</c>: turns a map's C# defaults
/// into its first Tiled file, plus the Tiled support files beside it. Every map is a
/// Tiled map with one: the exteriors (<see cref="ExteriorMap.CodeSeed"/>), the farm
/// (<see cref="TestMap.CodeSeed"/>) and the interiors (<see cref="InteriorMap.CodeSeed"/>).
///
/// What it writes, and when:
/// - every palette (<see cref="TiledPalette.All"/>: its <c>.tsx</c> + its PNG) EVERY run —
///   all ten files are pure derivations of the palettes' Names, never hand-edited, so
///   re-running after appending a name refreshes them;
/// - <see cref="ProjectPath"/> only where missing (delete it to regenerate);
/// - the map's <c>.tmx</c> only where missing. An existing <c>.tmx</c> is the map, and it
///   is NEVER overwritten — it is left byte-untouched and the run carries on.
/// </summary>
public static class TiledSeeds
{
    public const string ProjectPath = "res://data/maps/tiled/thehaunt.tiled-project";

    /// <summary>Whether this map has C# defaults to seed a Tiled file from: it is
    /// registered, and it is an <see cref="ExteriorMap"/>, the farm
    /// (<see cref="TestMap"/>) or an <see cref="InteriorMap"/> — the Tiled maps.</summary>
    public static bool Has(string mapId)
    {
        if (!MapRegistry.Contains(mapId))
            return false;
        MapRoot map = MapRegistry.Create(mapId);
        try
        {
            return map is ExteriorMap or TestMap or InteriorMap;
        }
        finally
        {
            map.Free();
        }
    }

    /// <summary>The map's code seed as a Tiled map (<see cref="ExteriorMap.CodeSeed"/>,
    /// <see cref="TestMap.CodeSeed"/> or <see cref="InteriorMap.CodeSeed"/>), built on a
    /// map that never enters the tree and is freed after. Throws for a map that is not a
    /// registered Tiled map.</summary>
    public static TiledMap For(string mapId)
    {
        if (MapRegistry.Contains(mapId))
        {
            MapRoot map = MapRegistry.Create(mapId);
            try
            {
                if (map is ExteriorMap exterior)
                    return exterior.CodeSeed();
                if (map is TestMap farm)
                    return farm.CodeSeed();
                if (map is InteriorMap interior)
                    return interior.CodeSeed();
            }
            finally
            {
                map.Free();
            }
        }
        throw new ArgumentException(
            $"No Tiled seed for map '{mapId}' — only the registered exteriors (ExteriorMap), the farm (TestMap) and the interiors (InteriorMap) build from Tiled.",
            nameof(mapId));
    }

    /// <summary>
    /// A Tiled 1.11+ project over data/maps: one object class per placement kind, so the
    /// Class dropdown offers the kinds and each carries its fields with their defaults.
    /// </summary>
    public static string ProjectJson()
    {
        var text = new StringBuilder();
        text.Append("{\n");
        text.Append("    \"automappingRulesFile\": \"\",\n");
        text.Append("    \"commands\": [\n    ],\n");
        text.Append("    \"compatibilityVersion\": 1100,\n");
        text.Append("    \"extensionsPath\": \"extensions\",\n");
        text.Append("    \"folders\": [\n        \"..\"\n    ],\n");
        text.Append("    \"properties\": [\n    ],\n");
        text.Append("    \"propertyTypes\": [\n");
        for (int i = 0; i < PlacementKinds.All.Count; i++)
        {
            string kind = PlacementKinds.All[i];
            text.Append("        {\n");
            text.Append("            \"color\": \"#ffa0a0a4\",\n");
            text.Append("            \"drawFill\": true,\n");
            text.Append("            \"id\": ").Append(MapRecipe.Number(i + 1)).Append(",\n");
            string[] members = kind switch
            {
                PlacementKinds.Door or PlacementKinds.Exit =>
                    new[] { Member(PlacementFields.Spawn, "string", "\"default\"") },
                PlacementKinds.Sign => new[]
                {
                    Member(PlacementFields.Board, "bool", "true"),
                    Member(PlacementFields.Text, "string", "\"\""),
                },
                PlacementKinds.Furniture => new[] { Member(PlacementFields.Blocks, "bool", "true") },
                PlacementKinds.Chest => new[] { Member(PlacementFields.Art, "string", "\"\"") },
                _ => Array.Empty<string>(),
            };
            if (members.Length == 0)
            {
                text.Append("            \"members\": [\n            ],\n");
            }
            else
            {
                text.Append("            \"members\": [\n");
                for (int m = 0; m < members.Length; m++)
                    text.Append(members[m]).Append(m == members.Length - 1 ? "\n" : ",\n");
                text.Append("            ],\n");
            }
            text.Append("            \"name\": ").Append(MapRecipe.Quote(kind)).Append(",\n");
            text.Append("            \"type\": \"class\",\n");
            text.Append("            \"useAs\": [\n                \"object\"\n            ]\n");
            text.Append(i == PlacementKinds.All.Count - 1 ? "        }\n" : "        },\n");
        }
        text.Append("    ]\n");
        text.Append("}\n");
        return text.ToString();
    }

    private static string Member(string name, string type, string valueJson) =>
        "                {\n" +
        $"                    \"name\": {MapRecipe.Quote(name)},\n" +
        $"                    \"type\": {MapRecipe.Quote(type)},\n" +
        $"                    \"value\": {valueJson}\n" +
        "                }";

    /// <summary>ALWAYS (re)writes every palette's .tsx and .png (<see cref="TiledPalette.All"/>)
    /// — derived from each one's Names.</summary>
    public static void WritePalette()
    {
        foreach (TiledPalette palette in TiledPalette.All)
        {
            TiledMapFile.WriteText(palette.TilesetPath, palette.ToTsx());
            SavePng(palette.BuildSwatch(), palette.ImagePath);
        }
    }

    private static void SavePng(Image image, string path)
    {
        Error error = image.SavePng(path);
        if (error != Error.Ok)
        {
            throw new MapRecipeException(path, $"could not be written ({error}).");
        }
    }

    /// <summary>
    /// Seeds a map's Tiled files; returns the res:// paths actually written. An unknown
    /// map throws before anything is written. Never overwrites a .tmx or the project.
    /// </summary>
    public static IReadOnlyList<string> Export(string mapId)
    {
        TiledMap map = For(mapId);
        var written = new List<string>();

        WritePalette();
        foreach (TiledPalette palette in TiledPalette.All)
        {
            written.Add(palette.TilesetPath);
            written.Add(palette.ImagePath);
        }

        if (!FileAccess.FileExists(ProjectPath))
        {
            TiledMapFile.WriteText(ProjectPath, ProjectJson());
            written.Add(ProjectPath);
        }

        string tmxPath = TiledMapFile.PathFor(mapId);
        if (!FileAccess.FileExists(tmxPath))
        {
            TiledMapFile.WriteText(tmxPath, map.ToTmx());
            written.Add(tmxPath);
        }
        return written;
    }
}

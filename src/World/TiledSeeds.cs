using System.Text;
using Godot;
using TheHaunt.Core;
using FileAccess = Godot.FileAccess;

namespace TheHaunt.World;

/// <summary>
/// The dev-only exporter behind <c>--seed-tiled (mapId)</c>: turns a map's C# defaults
/// into its first Tiled file, plus the Tiled support files beside it. The Tiled
/// counterpart of <see cref="MapRecipeSeeds"/>.
///
/// What it writes, and when:
/// - the surface palette (<see cref="TiledSurfaces.TilesetPath"/> + its PNG) and the
///   obstacle palette (<see cref="TiledObstacles.TilesetPath"/> + its PNG) EVERY run —
///   all four are pure derivations of the palettes' Names, never hand-edited, so
///   re-running after appending a surface or obstacle refreshes them;
/// - <see cref="ProjectPath"/> only where missing (delete it to regenerate);
/// - the map's <c>.tmx</c> only where missing. An existing <c>.tmx</c> is the map, and it
///   is NEVER overwritten — it is left byte-untouched and the run carries on.
/// </summary>
public static class TiledSeeds
{
    public const string ProjectPath = "res://data/maps/tiled/thehaunt.tiled-project";

    /// <summary>Whether this map has C# defaults to seed a Tiled file from.</summary>
    public static bool Has(string mapId) => mapId == MapIds.Town;

    public static TiledMap For(string mapId) => mapId switch
    {
        MapIds.Town => TownMap.DefaultTiledMap(),
        _ => throw new ArgumentException(
            $"No Tiled seed for map '{mapId}' — only the town builds from Tiled. " +
            "Add a case here pointing at the map's DefaultTiledMap.",
            nameof(mapId)),
    };

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
                PlacementKinds.Sign => new[] { Member(PlacementFields.Text, "string", "\"\"") },
                PlacementKinds.Furniture => new[] { Member(PlacementFields.Blocks, "bool", "true") },
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

    /// <summary>ALWAYS (re)writes both palettes' .tsx and .png — derived from
    /// TiledSurfaces.Names and TiledObstacles.Names.</summary>
    public static void WritePalette()
    {
        TiledMapFile.WriteText(TiledSurfaces.TilesetPath, TiledSurfaces.ToTsx());
        SavePng(TiledSurfaces.BuildSwatch(), TiledSurfaces.ImagePath);
        TiledMapFile.WriteText(TiledObstacles.TilesetPath, TiledObstacles.ToTsx());
        SavePng(TiledObstacles.BuildSwatch(), TiledObstacles.ImagePath);
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
        written.Add(TiledSurfaces.TilesetPath);
        written.Add(TiledSurfaces.ImagePath);
        written.Add(TiledObstacles.TilesetPath);
        written.Add(TiledObstacles.ImagePath);

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

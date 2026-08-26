using System.Globalization;
using System.Text;
using System.Text.Json;

namespace TheHaunt.World;

/// <summary>
/// The placements one map is built FROM, and the canonical text they are stored as.
/// Maps stay C# build functions — a recipe is a build function's INPUT, never a
/// replacement for it. Terrain painting stays generative and has no representation here
/// at all; what moves into data is the things a person would otherwise drag: props,
/// scatter, spawn markers, doors, exits, signs, furniture and the interactables.
///
/// A recipe is CONTENT, not save state. It belongs beside ItemDefs and CropDefs: read at
/// map build time, edited by hand or by the editor tool, and NEVER written by the running
/// game. Nothing here may reach GameData, nothing here is versioned by SaveMigrations,
/// and a change to a recipe is a content change — it needs no migration, only a rebuild.
///
/// The text format is hand-written for one reason: legible diffs. Json.Stringify and
/// System.Text.Json's indented writer both explode a short record across many lines,
/// which would make a map file as unmergeable as the base64 tile data in a .tscn — and
/// being mergeable is most of why this is JSON and not a scene. So: one placement per
/// line, sorted by y then x then kind, fields in a fixed order, "\n" endings on every
/// platform. Serialising the same recipe twice is byte-identical, and so is a
/// load/save cycle over a file this writer produced.
/// </summary>
public sealed class MapRecipe
{
    /// <summary>
    /// Bumped only by a BREAKING format change — one that would make an older build
    /// misread a newer file rather than merely ignore part of it. Additive change (a new
    /// kind, a new field) does not bump it: unknown records and unknown fields already
    /// survive, which is the whole point of the preserve rule.
    /// </summary>
    public const int CurrentVersion = 1;

    private const string VersionKey = "version";
    private const string MapKey = "map";
    private const string PlacementsKey = "placements";

    private readonly List<MapPlacement> _placements = new();

    public MapRecipe(string mapId)
    {
        MapId = mapId;
    }

    /// <summary>
    /// The map this recipe builds. NOTE the farm's is literally "test_farm"
    /// (<c>MapIds.Farm</c>): the rename is deferred to the first editor-authored map and
    /// its own save migration, so the file is <c>data/maps/test_farm.json</c> and will be
    /// renamed by that same migration. Deliberate oddity, not a typo.
    /// </summary>
    public string MapId { get; }

    /// <summary>
    /// In INSERTION order, which is not the file's order — the canonical text sorts a
    /// copy at write time. Builders must not depend on the order of this list; anything
    /// that needs a draw order gets it from the Y-sort, as every map already does.
    /// </summary>
    public IReadOnlyList<MapPlacement> Placements => _placements;

    public MapPlacement Add(MapPlacement placement)
    {
        _placements.Add(placement);
        return placement;
    }

    /// <summary>Convenience for the common record — a kind, an id and a cell.</summary>
    public MapPlacement Add(string kind, string id, int x, int y) =>
        Add(new MapPlacement(kind, id, x, y));

    public bool Remove(MapPlacement placement) => _placements.Remove(placement);

    /// <summary>Every placement of one kind, in the canonical (y, x) order the builder wants.</summary>
    public IEnumerable<MapPlacement> OfKind(string kind) =>
        Sorted().Where(placement => placement.Kind == kind);

    /// <summary>
    /// The canonical text. Deterministic: same recipe, same bytes, whatever order the
    /// placements were added in and whatever machine it runs on.
    /// </summary>
    public string ToJson()
    {
        // "\n" and not Environment.NewLine: a content file whose bytes depend on the OS
        // that last wrote it is a file that shows a whole-file diff on every checkout.
        var text = new StringBuilder();
        text.Append("{\n");
        text.Append("  \"").Append(VersionKey).Append("\": ").Append(Number(CurrentVersion)).Append(",\n");
        text.Append("  \"").Append(MapKey).Append("\": ").Append(Quote(MapId)).Append(",\n");

        List<MapPlacement> ordered = Sorted();
        if (ordered.Count == 0)
        {
            text.Append("  \"").Append(PlacementsKey).Append("\": []\n");
        }
        else
        {
            text.Append("  \"").Append(PlacementsKey).Append("\": [\n");
            for (int i = 0; i < ordered.Count; i++)
            {
                text.Append("    ").Append(Line(ordered[i]));
                text.Append(i == ordered.Count - 1 ? "\n" : ",\n");
            }
            text.Append("  ]\n");
        }

        text.Append("}\n");
        return text.ToString();
    }

    /// <summary>
    /// Reads the canonical text. Throws <see cref="MapRecipeException"/> naming
    /// <paramref name="sourcePath"/> for anything it cannot read as a recipe — a recipe
    /// is content the build depends on, so a broken one must be loud rather than quietly
    /// half-loaded. (A MISSING file is a different thing entirely and is not an error:
    /// see <see cref="MapRecipeFile.Load"/>.)
    /// </summary>
    /// <param name="sourcePath">Named in every error. A file path, or a label for text that never was a file.</param>
    public static MapRecipe Parse(string json, string sourcePath)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new MapRecipeException(sourcePath, $"is not valid JSON: {e.Message}");
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new MapRecipeException(sourcePath, "must hold a JSON object at its root.");
            }

            // Absent version = version 1: the field is insurance against a future break,
            // not a thing every hand-written file has to remember.
            int version = CurrentVersion;
            if (root.TryGetProperty(VersionKey, out JsonElement versionValue)
                && (versionValue.ValueKind != JsonValueKind.Number || !versionValue.TryGetInt32(out version)))
            {
                throw new MapRecipeException(sourcePath, $"has a '{VersionKey}' that is not a whole number.");
            }
            if (version > CurrentVersion)
            {
                throw new MapRecipeException(sourcePath,
                    $"is format version {version}, but this build reads up to {CurrentVersion}.");
            }

            if (!root.TryGetProperty(MapKey, out JsonElement mapValue)
                || mapValue.ValueKind != JsonValueKind.String
                || mapValue.GetString() is not { Length: > 0 } mapId)
            {
                throw new MapRecipeException(sourcePath, $"has no '{MapKey}' naming the map it builds.");
            }

            if (!root.TryGetProperty(PlacementsKey, out JsonElement placements)
                || placements.ValueKind != JsonValueKind.Array)
            {
                throw new MapRecipeException(sourcePath, $"has no '{PlacementsKey}' array.");
            }

            var recipe = new MapRecipe(mapId);
            int index = 0;
            foreach (JsonElement element in placements.EnumerateArray())
            {
                recipe.Add(ParsePlacement(element, index++, sourcePath));
            }
            return recipe;
        }
    }

    // ------------------------------------------------------------------
    // The canonical writer's rules — all of them, in one place so they cannot drift
    // ------------------------------------------------------------------

    /// <summary>One placement, one line: the guaranteed fields in a fixed order, then the extras by key.</summary>
    internal static string Line(MapPlacement placement)
    {
        var text = new StringBuilder();
        text.Append('{').Append(Quote(MapPlacement.KindKey)).Append(": ").Append(Quote(placement.Kind));
        text.Append(", ").Append(Quote(MapPlacement.IdKey)).Append(": ").Append(Quote(placement.Id));
        text.Append(", ").Append(Quote(MapPlacement.XKey)).Append(": ").Append(Number(placement.X));
        text.Append(", ").Append(Quote(MapPlacement.YKey)).Append(": ").Append(Number(placement.Y));
        // The nudge is written only when it is one, so the exception stays visible in the
        // file: a reader can see at a glance which placements are off-grid on purpose.
        if (placement.NudgeX != 0)
        {
            text.Append(", ").Append(Quote(MapPlacement.NudgeXKey)).Append(": ").Append(Number(placement.NudgeX));
        }
        if (placement.NudgeY != 0)
        {
            text.Append(", ").Append(Quote(MapPlacement.NudgeYKey)).Append(": ").Append(Number(placement.NudgeY));
        }
        // Raw, verbatim, in ordinal key order: an unknown field is re-emitted exactly as
        // it arrived, so nothing this build does not understand can be reformatted into
        // something that means something else.
        foreach ((string key, string raw) in placement.Fields)
        {
            text.Append(", ").Append(Quote(key)).Append(": ").Append(raw);
        }
        return text.Append('}').ToString();
    }

    /// <summary>A JSON string literal. Hand-rolled so non-ASCII stays readable instead of becoming \\uXXXX.</summary>
    internal static string Quote(string value)
    {
        var text = new StringBuilder(value.Length + 2);
        text.Append('"');
        foreach (char c in value)
        {
            switch (c)
            {
                case '"': text.Append("\\\""); break;
                case '\\': text.Append("\\\\"); break;
                case '\n': text.Append("\\n"); break;
                case '\r': text.Append("\\r"); break;
                case '\t': text.Append("\\t"); break;
                default:
                    if (c < 0x20)
                    {
                        text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        text.Append(c);
                    }
                    break;
            }
        }
        return text.Append('"').ToString();
    }

    /// <summary>InvariantCulture, always: a localised minus sign in a content file is a lovely bug.</summary>
    internal static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------
    // Ordering
    // ------------------------------------------------------------------

    /// <summary>
    /// Reading order — down the map, then across, then by kind — so a placement's line
    /// sits where a reader looking at the map would expect it, and a moved prop is a
    /// one-line diff. Id and then the whole rendered line break the remaining ties, which
    /// makes the order TOTAL: two recipes holding the same placements serialise
    /// identically no matter how either one was assembled.
    /// </summary>
    private List<MapPlacement> Sorted()
    {
        var ordered = new List<MapPlacement>(_placements);
        ordered.Sort(static (a, b) =>
        {
            int order = a.Y.CompareTo(b.Y);
            if (order != 0) return order;
            order = a.X.CompareTo(b.X);
            if (order != 0) return order;
            order = string.CompareOrdinal(a.Kind, b.Kind);
            if (order != 0) return order;
            order = string.CompareOrdinal(a.Id, b.Id);
            return order != 0 ? order : string.CompareOrdinal(Line(a), Line(b));
        });
        return ordered;
    }

    // ------------------------------------------------------------------
    // Parsing one record
    // ------------------------------------------------------------------

    private static MapPlacement ParsePlacement(JsonElement element, int index, string sourcePath)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new MapRecipeException(sourcePath, $"placement {index} is not a JSON object.");
        }

        string? kind = null, id = null;
        int? x = null, y = null;
        int nudgeX = 0, nudgeY = 0;
        var extras = new List<(string Key, string Raw)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (JsonProperty property in element.EnumerateObject())
        {
            // Duplicate keys are legal JSON and a silent last-one-wins everywhere else.
            // In a file a person edits by hand, they are a mistake worth naming.
            if (!seen.Add(property.Name))
            {
                throw new MapRecipeException(sourcePath,
                    $"placement {index} repeats the field '{property.Name}'.");
            }

            switch (property.Name)
            {
                case MapPlacement.KindKey:
                    kind = ReadString(property, index, sourcePath);
                    break;
                case MapPlacement.IdKey:
                    id = ReadString(property, index, sourcePath);
                    break;
                case MapPlacement.XKey:
                    x = ReadInt(property, index, sourcePath);
                    break;
                case MapPlacement.YKey:
                    y = ReadInt(property, index, sourcePath);
                    break;
                case MapPlacement.NudgeXKey:
                    nudgeX = ReadInt(property, index, sourcePath);
                    break;
                case MapPlacement.NudgeYKey:
                    nudgeY = ReadInt(property, index, sourcePath);
                    break;
                default:
                    // Unknown field on any kind, known or not: kept exactly as written.
                    // Scalars only — the check is here rather than in SetRaw so the error
                    // can name the file, the record and the key.
                    if (property.Value.ValueKind
                        is not (JsonValueKind.String or JsonValueKind.Number
                        or JsonValueKind.True or JsonValueKind.False))
                    {
                        throw new MapRecipeException(sourcePath,
                            $"placement {index} field '{property.Name}' is {property.Value.ValueKind}; " +
                            "recipe fields hold strings, numbers and bools only.");
                    }
                    extras.Add((property.Name, property.Value.GetRawText()));
                    break;
            }
        }

        if (kind is not { Length: > 0 })
        {
            throw new MapRecipeException(sourcePath, $"placement {index} has no '{MapPlacement.KindKey}'.");
        }
        if (id is null)
        {
            throw new MapRecipeException(sourcePath, $"placement {index} has no '{MapPlacement.IdKey}'.");
        }
        if (x is null || y is null)
        {
            throw new MapRecipeException(sourcePath,
                $"placement {index} has no tile coordinate ('{MapPlacement.XKey}' and '{MapPlacement.YKey}').");
        }

        var placement = new MapPlacement(kind, id, x.Value, y.Value)
        {
            NudgeX = nudgeX,
            NudgeY = nudgeY,
        };
        foreach ((string key, string raw) in extras)
        {
            placement.SetRaw(key, raw);
        }
        return placement;
    }

    private static string ReadString(JsonProperty property, int index, string sourcePath) =>
        property.Value.ValueKind == JsonValueKind.String
            ? property.Value.GetString()!
            : throw new MapRecipeException(sourcePath,
                $"placement {index} field '{property.Name}' must be a string.");

    private static int ReadInt(JsonProperty property, int index, string sourcePath) =>
        property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out int value)
            ? value
            : throw new MapRecipeException(sourcePath,
                $"placement {index} field '{property.Name}' must be a whole number.");
}

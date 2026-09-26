using System.Globalization;
using System.Text;

namespace TheHaunt.World;

/// <summary>
/// The placements one map is built FROM, in memory: read from a Tiled map's placement
/// layer (<see cref="TiledMap"/>), or built by a map's code seed. Maps stay C# build
/// functions — a recipe is a build function's INPUT, never a replacement for it. What
/// lives here is the things a person would otherwise drag: props, scatter, spawn
/// markers, doors, exits, signs, furniture and the interactables.
///
/// A recipe is CONTENT, not save state: read at map build time and NEVER written by the
/// running game. Nothing here may reach GameData, nothing here is versioned by
/// SaveMigrations, and a change to one is a content change — no migration, only a rebuild.
///
/// <see cref="ToJson"/> is the CANONICAL TEXT the drift guards compare: one placement per
/// line, sorted by y then x then kind, fields in a fixed order, "\n" endings on every
/// platform. Serialising the same recipe twice is byte-identical, whatever order the
/// placements were added in.
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
    /// The map this recipe builds.
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
}

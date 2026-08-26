using System.Text.Json;
using Godot;

namespace TheHaunt.World;

/// <summary>
/// One placed thing in a <see cref="MapRecipe"/>: a kind, an art/target id, and a TILE
/// coordinate. Never an atlas coordinate — <c>id</c> is a NAME, so the builder still
/// resolves it through TerrainTiles/FarmTiles.ForAct and the Act II/III variant swap
/// survives. Never a pixel position — <c>x,y</c> is a CELL, so the builder still decides
/// whether the thing anchors on its base row (Prop.Anchor, for anything drawn in
/// elevation) or on its tile centre (the Area2D interactables). Baking either one in
/// would quietly kill the mechanism that reads it.
///
/// The record's SHAPE is closed and its VOCABULARY is open. Four fields are guaranteed
/// (kind, id, x, y), two more are the optional nudge, and everything else is a scalar
/// carried verbatim — including on a kind this build has never heard of. Values are
/// strings, numbers and bools only: an object or array value would be data with a
/// structure of its own, and a structure cannot be held to one line per placement, which
/// is the property that makes these files diff and merge like source.
/// </summary>
public sealed class MapPlacement
{
    public const string KindKey = "kind";
    public const string IdKey = "id";
    public const string XKey = "x";
    public const string YKey = "y";
    public const string NudgeXKey = "dx";
    public const string NudgeYKey = "dy";

    /// <summary>The six keys the record owns; an extra field may never shadow one.</summary>
    public static readonly IReadOnlyList<string> ReservedKeys =
        new[] { KindKey, IdKey, XKey, YKey, NudgeXKey, NudgeYKey };

    // Ordinal-sorted so the canonical writer's field order depends on the keys alone and
    // not on the order a file happened to list them in, or a locale's idea of alphabet.
    private readonly SortedDictionary<string, string> _fields = new(StringComparer.Ordinal);

    public MapPlacement(string kind, string id, int x, int y)
    {
        if (string.IsNullOrEmpty(kind))
        {
            throw new ArgumentException("A placement needs a kind.", nameof(kind));
        }
        Kind = kind;
        Id = id;
        X = x;
        Y = y;
    }

    /// <summary>One of <see cref="PlacementKinds"/>, or a kind only a newer build knows.</summary>
    public string Kind { get; set; }

    /// <summary>What to place: an art name, a tile name, a target map id, a storage id — per kind.</summary>
    public string Id { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    /// <summary>
    /// Sub-tile nudge in PIXELS, added to whatever pixel position the kind's builder
    /// derives from the cell. For DELIBERATE exceptions only — the farmhouse bed sits at
    /// <c>3 * TileSize</c> rather than a tile centre because it is a 16x32 piece standing
    /// across two cells — and not as a general placement mechanism: a prop that needs a
    /// nudge to look right is usually a prop on the wrong tile.
    ///
    /// Integers, not floats. A fractional offset in a pixel-art game is a bug (it lands
    /// the sprite between texels), and integers keep the canonical text byte-stable
    /// without anyone having to reason about float formatting.
    /// </summary>
    public int NudgeX { get; set; }

    public int NudgeY { get; set; }

    public Vector2I Cell => new(X, Y);

    public Vector2 Nudge => new(NudgeX, NudgeY);

    /// <summary>
    /// Extra fields, key -> RAW JSON text of the value (a string value keeps its quotes
    /// and its escapes). Raw, because that is what makes an unknown record survive a
    /// load/save cycle byte for byte: nothing is decoded, so nothing can be re-encoded
    /// differently. Enumerates in ordinal key order — the writer's order.
    /// </summary>
    public IReadOnlyDictionary<string, string> Fields => _fields;

    /// <summary>Sets a field from raw JSON text. Scalars only; a reserved key is a bug, not a field.</summary>
    public void SetRaw(string key, string rawJson)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("A placement field needs a key.", nameof(key));
        }
        if (ReservedKeys.Contains(key))
        {
            throw new ArgumentException(
                $"'{key}' is one of the placement's own fields; set the property instead.", nameof(key));
        }
        if (!IsScalar(rawJson))
        {
            throw new ArgumentException(
                $"Field '{key}' must be a string, a number or a bool, not '{rawJson}'.", nameof(rawJson));
        }
        _fields[key] = rawJson;
    }

    public void SetText(string key, string value) => SetRaw(key, MapRecipe.Quote(value));

    public void SetInt(string key, int value) => SetRaw(key, MapRecipe.Number(value));

    public void SetBool(string key, bool value) => SetRaw(key, value ? "true" : "false");

    public bool RemoveField(string key) => _fields.Remove(key);

    /// <summary>The field's value if it is a string; <paramref name="fallback"/> if it is absent OR another type.</summary>
    public string Text(string key, string fallback = "")
    {
        JsonElement? value = Read(key);
        return value is { ValueKind: JsonValueKind.String } text ? text.GetString()! : fallback;
    }

    public int Int(string key, int fallback = 0)
    {
        JsonElement? value = Read(key);
        return value is { ValueKind: JsonValueKind.Number } number && number.TryGetInt32(out int result)
            ? result
            : fallback;
    }

    public bool Bool(string key, bool fallback = false)
    {
        JsonElement? value = Read(key);
        return value?.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => fallback,
        };
    }

    /// <summary>True when this build knows how to BUILD this record (see PlacementKinds.Contains).</summary>
    public bool IsKnown => PlacementKinds.Contains(Kind);

    public override string ToString() => MapRecipe.Line(this);

    private JsonElement? Read(string key)
    {
        if (!_fields.TryGetValue(key, out string? raw))
        {
            return null;
        }
        // Raw text is re-parsed per read rather than decoded once on load: a recipe holds
        // dozens of records and is read at map build time, so the cost is nothing next to
        // the guarantee that the stored text is the only copy and cannot drift from it.
        using var value = JsonDocument.Parse(raw);
        return value.RootElement.Clone();   // the element dies with the document
    }

    private static bool IsScalar(string rawJson)
    {
        try
        {
            using var value = JsonDocument.Parse(rawJson);
            return value.RootElement.ValueKind
                is JsonValueKind.String or JsonValueKind.Number
                or JsonValueKind.True or JsonValueKind.False;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The crop atlas (assets/sprites/farm/crops.png): one ROW per CropDef in
/// <see cref="CropDefs.All"/> order, one COLUMN per stage, addressed by
/// (row, <see cref="CropDef.StageForDay"/>) — farm handoff §3.
///
/// Cells are 16x32 with texture_origin (0,-8), so a crop stands on its tile and
/// overhangs the row above it: beans climb and cauliflower spreads instead of every
/// crop being a shrub that fits in a square. That is the handoff's one deliberate
/// change to the phase-2 spec.
///
/// The atlas is fixed at 5 columns x 4 rows. A crop id the sheet has no row for, or a
/// stage count it has no column for, paints nothing rather than throwing — same
/// tolerance the model has for unknown ids from a save file.
/// </summary>
public static class CropTiles
{
    public const string TileSetPath = "res://assets/sprites/farm/thehaunt_crops.tres";

    /// <summary>Row per crop id. Both the atlas and the lookup read this one mapping.</summary>
    public static IReadOnlyDictionary<string, int> RowByCropId { get; } = BuildRows();

    private static TileSet? _cached;

    public static TileSet Get() => _cached ??= GD.Load<TileSet>(TileSetPath)
        ?? throw new InvalidOperationException($"Crop TileSet missing at '{TileSetPath}'.");

    /// <summary>
    /// Atlas cell for a planted tile, or null when the sheet cannot draw it. Column 4 is
    /// the mature column: <see cref="CropDef.StageForDay"/> returns StageDays.Length once
    /// the crop is grown, and every shipped crop has four stages.
    /// </summary>
    public static Vector2I? Cell(string cropId, int growthDay)
    {
        if (CropDefs.TryGet(cropId) is not { } def || !RowByCropId.TryGetValue(cropId, out int row))
            return null;

        var coords = new Vector2I(def.StageForDay(growthDay), row);
        var source = (TileSetAtlasSource)Get().GetSource(0);
        return source.HasTile(coords) ? coords : null;
    }

    private static Dictionary<string, int> BuildRows()
    {
        var rows = new Dictionary<string, int>();
        foreach (string id in CropDefs.All.Keys)
            rows[id] = rows.Count;
        return rows;
    }
}

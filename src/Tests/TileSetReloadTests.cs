using System.Reflection;
using System.Text;
using Godot;
using TheHaunt.World;

namespace TheHaunt.Tests;

/// <summary>
/// The three shipped-art TileSets are assembled at RUNTIME — the walkable custom data,
/// the collision-only blocker cell, the farm's merged town atlas and the interiors'
/// transparent blocker source exist in no .tres on disk. Each builder runs once, gated by
/// an ordinary C# static.
///
/// That pairing is the hazard these tests pin. The .tres comes back from Godot's
/// process-wide resource cache, which does NOT clear when the C# static does: a `dotnet
/// build` while the Godot editor is open reloads the assembly, resets the gate, and
/// re-enters Build against a set that is already fully built. Before the builders were
/// made idempotent that meant the farm and every interior threw "already owns source 1"
/// and the town silently grew a second custom data layer, taking `walkable` — and with it
/// <see cref="MapRoot.IsStandable"/> — down with it.
///
/// Nulling the gate by reflection is the honest simulation: it is exactly what the
/// runtime does to those fields on a reload, minus the new assembly.
/// </summary>
public static class TileSetReloadTests
{
    [SimTest]
    public static void TileSets_RebuildCleanlyAfterAnAssemblyReload(TestContext t)
    {
        var builders = new (string Name, Type Owner, Func<TileSet> Get, Func<TileSet, bool> HasBlocker)[]
        {
            ("TownTerrain", typeof(TownTerrain), TownTerrain.Get,
                set => Atlas(set, 0).HasTile(TerrainTiles.Blocker)),
            ("FarmTerrain", typeof(FarmTerrain), FarmTerrain.Get,
                set => Atlas(set, FarmTerrain.TownSource).HasTile(TerrainTiles.Blocker)),
            ("InteriorTerrain", typeof(InteriorTerrain), InteriorTerrain.Get,
                set => Atlas(set, InteriorTerrain.BlockerSource).HasTile(InteriorTerrain.Blocker)),
        };

        foreach ((string name, Type owner, Func<TileSet> get, Func<TileSet, bool> hasBlocker) in builders)
        {
            TileSet first = get();
            string shape = Describe(first);
            int unwalkable = UnwalkableCount(first);
            t.Assert(hasBlocker(first), $"{name}: the blocker cell is there to begin with");

            ResetGate(owner);

            // The failure this guards was a THROW out of Build, so reaching the next line
            // at all is half the assertion.
            TileSet second = get();

            t.AssertEqual(shape, Describe(second), $"{name}: source and layer shape is stable");
            t.AssertEqual(unwalkable, UnwalkableCount(second), $"{name}: derived walkable data is stable");
            t.Assert(hasBlocker(second), $"{name}: the blocker cell survived the rebuild");
        }
    }

    private static TileSetAtlasSource Atlas(TileSet set, int sourceId) =>
        (TileSetAtlasSource)set.GetSource(sourceId);

    /// <summary>Nulls a builder's private cache, the way an assembly reload would.</summary>
    private static void ResetGate(Type owner)
    {
        FieldInfo field = owner.GetField("_cached", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{owner.Name} has no _cached static to reset.");
        field.SetValue(null, null);
    }

    /// <summary>
    /// Sources, their ids and tile counts, and the custom-data-layer count — every shape a
    /// re-entrant Build was observed to corrupt.
    /// </summary>
    private static string Describe(TileSet set)
    {
        var text = new StringBuilder();
        text.Append($"sources={set.GetSourceCount()} customData={set.GetCustomDataLayersCount()}");
        for (int i = 0; i < set.GetSourceCount(); i++)
        {
            int id = set.GetSourceId(i);
            text.Append($" [{id}:{Atlas(set, id).GetTilesCount()}]");
        }
        return text.ToString();
    }

    /// <summary>
    /// Counts tiles the derivation calls unwalkable. Reads by NAME, so a duplicated custom
    /// data layer fails here rather than silently returning the wrong layer's value.
    /// </summary>
    private static int UnwalkableCount(TileSet set)
    {
        int count = 0;
        for (int i = 0; i < set.GetSourceCount(); i++)
        {
            TileSetAtlasSource source = Atlas(set, set.GetSourceId(i));
            for (int j = 0; j < source.GetTilesCount(); j++)
            {
                TileData data = source.GetTileData(source.GetTileId(j), 0);
                if (!data.GetCustomData(TileSetTools.WalkableData).AsBool())
                    count++;
            }
        }
        return count;
    }
}

using Godot;
using TheHaunt.Content;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The fork, 40x30 tiles: the crossroads west of the town centre. East-west road on
/// rows 14-15 (Billie's to the west, town to the east), the farm road running north
/// out of the frame, and a southbound stub that is chained off — it leads to something
/// later, and the chain says so without saying what (src/Content/Places). No
/// buildings; this frame is all road.
/// </summary>
public partial class ForkMap : ExteriorMap
{
    private const int Width = 40;
    private const int Height = 30;

    protected override int MapWidth => Width;
    protected override int MapHeight => Height;

    // The north-south road's columns. North runs out of the frame to the farm; south
    // dead-ends into the trees behind the chain.
    private const int CrossLeft = 19, CrossRight = 20;
    private const int SouthStubEnd = 28;

    // The chain spans exactly the road gap in the southern treeline — trees seal the
    // rest of the row, so it cannot be strolled around.
    private const int ChainLeft = 19, ChainRight = 20, ChainRow = 24;

    public override void _EnterTree()
    {
        if (MapId.Length == 0)
            MapId = MapIds.Fork;
        base._EnterTree();
    }

    protected override void BuildDefaultSurfaces()
    {
        ResetSurfaces();

        for (int x = 0; x < Width; x++)
        {
            Set(x, RoadTop, Surface.Road);
            Set(x, RoadBottom, Surface.Road);
        }

        // The farm road, open through the north border.
        Fill(CrossLeft, 0, CrossRight, RoadTop - 1, Surface.Dirt);

        // Forest across the whole south of the frame, pierced only by the road stub —
        // so the chain closes the one gap instead of standing in open grass with
        // strollable ends. The stub dead-ends against the border woods.
        Fill(1, ChainRow, Width - 2, Height - 2, Surface.Woods);
        Fill(CrossLeft, RoadBottom + 1, CrossRight, SouthStubEnd, Surface.Dirt);
    }

    /// <summary>The fork's placements as the C# literals describe them: the seed
    /// <c>data/maps/fork.tmx</c> was written from, and the fallback when it is missing.
    /// The kerb cuts where the unsealed farm road and the chained south stub cross
    /// derive from their dirt.</summary>
    protected override MapRecipe BuildDefaultRecipe()
    {
        var recipe = new MapRecipe(MapIds.Fork);

        recipe.Add(PlacementKinds.Prop, "south_chain", ChainLeft, ChainRow);

        // >= 1 tile clear of each road-mouth exit area (spawn-clearance rule).
        recipe.Add(PlacementKinds.Spawn, "default", 24, 15);
        recipe.Add(PlacementKinds.Spawn, "from_billies", 2, 15);
        recipe.Add(PlacementKinds.Spawn, "from_town", 37, 15);
        recipe.Add(PlacementKinds.Spawn, "from_farm", 19, 2);

        // Copy lives with the place (src/Content/Places/Fork.cs).
        recipe.Add(PlacementKinds.Sign, "FingerPost", 22, 13);
        recipe.Add(PlacementKinds.Sign, "SouthChainSign", 18, ChainRow - 1);

        foreach ((string target, string spawn, int x, int y, int w, int h) in new[]
        {
            (MapIds.Billies, "from_fork", 0, RoadTop, 1, 2),
            (MapIds.Town, "from_fork", Width - 1, RoadTop, 1, 2),
            (MapIds.Farm, "road", CrossLeft, 0, 2, 1),
        })
        {
            MapPlacement exit = recipe.Add(PlacementKinds.Exit, target, x, y);
            exit.SetText(PlacementFields.Spawn, spawn);
            exit.SetInt(PlacementFields.Width, w);
            exit.SetInt(PlacementFields.Height, h);
        }

        return recipe;
    }

    protected override string? SignTextFor(string signId) => Fork.SignTextFor(signId);

    protected override IReadOnlyDictionary<string, ExteriorProp> PropCatalog() =>
        new Dictionary<string, ExteriorProp>(StringComparer.Ordinal)
        {
            ["south_chain"] = new(p => new RoadBarrier
            {
                Name = "SouthChain",
                TilesWide = ChainRight - ChainLeft + 1,
                Position = Prop.Anchor(p.X, p.Y, ChainRight - ChainLeft + 1),
            }, ExteriorProp.Rows(ChainRight - ChainLeft + 1, 1)),
        };
}

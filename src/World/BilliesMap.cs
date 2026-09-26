using Godot;
using TheHaunt.Content;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// Billie's, 40x30 tiles: the dive bar between the west entry and the fork, and — in
/// the same frame, south of the road — the pit: a covered hole nobody talks about,
/// chained off behind a warning sign (src/Content/Places). The bar has no art or
/// interior yet, so it ships as a <see cref="PlaceholderBuilding"/>; the pit is a
/// <see cref="PitCover"/> behind a <see cref="RoadBarrier"/>, all of it blocked.
/// </summary>
public partial class BilliesMap : ExteriorMap
{
    private const int Width = 40;
    private const int Height = 30;

    protected override int MapWidth => Width;
    protected override int MapHeight => Height;

    private const int BarLeft = 14, BarTop = 8, BarRight = 21, BarBottom = 11;

    // The bar's door cell, on the face's bottom row under the drawn doorway (the
    // placeholder draws its door centred, straddling x17/x18).
    private const int BarDoorX = 17;

    // The pit's cover, and the chain strung across its approach from the road.
    private const int PitLeft = 26, PitTop = 20, PitRight = 28, PitBottom = 21;
    private const int ChainLeft = 25, ChainRight = 29, ChainRow = 19;

    private static readonly Vector2I Light = new(13, 13);  // cobra head in the verge

    public override void _EnterTree()
    {
        if (MapId.Length == 0)
            MapId = MapIds.Billies;
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

        Fill(BarLeft, BarTop, BarRight, BarBottom + 1, Surface.Gravel);

        // The door path down to the road, two tiles wide under the drawn double door.
        Fill(BarDoorX, BarBottom + 1, BarDoorX + 1, 13, Surface.Dirt);

        // Bare ground around the pit — grass does not grow back over that.
        Fill(PitLeft - 1, ChainRow, PitRight + 1, PitBottom + 1, Surface.Dirt);
    }

    /// <summary>Billie's placements as the C# literals describe them: the seed
    /// <c>data/maps/billies.tmx</c> was written from, and the fallback when it is missing.
    /// The kerb cut where the bar's two-tile door path crosses derives from its dirt.</summary>
    protected override MapRecipe BuildDefaultRecipe()
    {
        var recipe = new MapRecipe(MapIds.Billies);

        recipe.Add(PlacementKinds.Prop, "bar", BarLeft, BarBottom);
        recipe.Add(PlacementKinds.Prop, "pit", PitLeft, PitBottom);
        recipe.Add(PlacementKinds.Prop, "pit_chain", ChainLeft, ChainRow);
        recipe.Add(PlacementKinds.Prop, TownProps.StreetLightId, Light.X, Light.Y);

        // >= 1 tile clear of each road-mouth exit area (spawn-clearance rule).
        recipe.Add(PlacementKinds.Spawn, "default", 20, 15);
        recipe.Add(PlacementKinds.Spawn, "from_west_entry", 2, 15);
        recipe.Add(PlacementKinds.Spawn, "from_fork", 37, 15);
        recipe.Add(PlacementKinds.Spawn, "from_bar", BarDoorX, BarBottom + 1);

        // Copy lives with the place (src/Content/Places/Billies.cs).
        recipe.Add(PlacementKinds.Sign, "BarSign", 15, 12);
        recipe.Add(PlacementKinds.Sign, "PitSign", 24, ChainRow);

        // The doorway is drawn into the placeholder face, so the Door node
        // contributes its blocker and its prompt only.
        recipe.Add(PlacementKinds.Door, MapIds.BilliesBar, BarDoorX, BarBottom)
            .SetText(PlacementFields.Spawn, "entry");

        foreach ((string target, int x) in new[] { (MapIds.WestEntry, 0), (MapIds.Fork, Width - 1) })
        {
            MapPlacement exit = recipe.Add(PlacementKinds.Exit, target, x, RoadTop);
            exit.SetText(PlacementFields.Spawn, "from_billies");
            exit.SetInt(PlacementFields.Width, 1);
            exit.SetInt(PlacementFields.Height, 2);
        }

        return recipe;
    }

    protected override string? SignTextFor(string signId) => Billies.SignTextFor(signId);

    protected override IReadOnlyDictionary<string, ExteriorProp> PropCatalog() =>
        new Dictionary<string, ExteriorProp>(StringComparer.Ordinal)
        {
            // The bar's door cell gaps: the Door node carries its blocker.
            ["bar"] = new(BuildBar, ExteriorProp.Rows(BarRight - BarLeft + 1, BarBottom - BarTop + 1)),
            ["pit"] = new(p => new PitCover
            {
                Name = "Pit",
                Position = Prop.Anchor(p.X, p.Y, PitCover.TilesWide),
            }, ExteriorProp.Rows(PitCover.TilesWide, PitCover.TilesTall)),
            ["pit_chain"] = new(p => new RoadBarrier
            {
                Name = "PitChain",
                TilesWide = ChainRight - ChainLeft + 1,
                Position = Prop.Anchor(p.X, p.Y, ChainRight - ChainLeft + 1),
            }, ExteriorProp.Rows(ChainRight - ChainLeft + 1, 1)),
            // The cobra head in the verge.
            [TownProps.StreetLightId] = new(p => new StreetLight { Position = Prop.Anchor(p.X, p.Y) },
                ExteriorProp.Rows(1, 1)),
        };

    private static Node2D BuildBar(MapPlacement p)
    {
        var bar = new PlaceholderBuilding
        {
            Name = "Bar",
            TilesWide = BarRight - BarLeft + 1,
            FootprintRows = BarBottom - BarTop + 1,
            Wall = new Color("6b5a45"),
            Position = Prop.Anchor(p.X, p.Y, BarRight - BarLeft + 1),
        };
        // The hanging-bracket mount (motel handoff §3): bars get the plaque on an
        // iron arm, one bulb over it, readable side-on down the road. It says BAR and
        // nothing else — a dive doesn't advertise its name, and everyone who matters
        // already knows whose it is.
        bar.AddChild(new BracketSign
        {
            Text = "BAR",
            Position = new Vector2(64, -60),
        });
        return bar;
    }
}

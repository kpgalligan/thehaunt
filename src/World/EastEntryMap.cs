using Godot;
using TheHaunt.Content;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// The east entry, 48x30 tiles: the police station and the hardware store north of
/// the road, the hair salon across it (src/Content/Places). The hardware store
/// starts closed — its owner is in the hospital, though the sign says only that it
/// is closed. The
/// east mouth is the other road out of town, and for a resident it wraps to the west
/// entry's west mouth (<see cref="RoadWrap"/>). All three buildings ship as
/// <see cref="PlaceholderBuilding"/>s until they have art.
/// </summary>
public partial class EastEntryMap : ExteriorMap
{
    private const int Width = 48;
    private const int Height = 30;

    protected override int MapWidth => Width;
    protected override int MapHeight => Height;

    private const int PoliceLeft = 9, PoliceTop = 8, PoliceRight = 16, PoliceBottom = 11;
    private const int HardwareLeft = 22, HardwareTop = 9, HardwareRight = 28, HardwareBottom = 11;
    private const int SalonLeft = 32, SalonTop = 18, SalonRight = 36, SalonBottom = 20;

    // The salon's door cell, on the face's bottom row under the drawn doorway.
    private const int SalonDoorX = 34;

    private static readonly Vector2I Light = new(17, 13);  // cobra head in the verge

    public override void _EnterTree()
    {
        if (MapId.Length == 0)
            MapId = MapIds.EastEntry;
        base._EnterTree();
    }

    protected override void BuildDefaultSurfaces()
    {
        ResetSurfaces();

        // The road, open at both mouths: west toward the east fork, east out of town.
        for (int x = 0; x < Width; x++)
        {
            Set(x, RoadTop, Surface.Road);
            Set(x, RoadBottom, Surface.Road);
        }

        Fill(PoliceLeft, PoliceTop, PoliceRight, PoliceBottom + 1, Surface.Gravel);
        Fill(HardwareLeft, HardwareTop, HardwareRight, HardwareBottom + 1, Surface.Gravel);
        Fill(SalonLeft, SalonTop, SalonRight, SalonBottom + 1, Surface.Gravel);
    }

    /// <summary>The east entry's placements as the C# literals describe them: the seed
    /// <c>data/maps/east_entry.tmx</c> was written from, and the fallback when it is missing.</summary>
    protected override MapRecipe BuildDefaultRecipe()
    {
        var recipe = new MapRecipe(MapIds.EastEntry);

        recipe.Add(PlacementKinds.Prop, "police_station", PoliceLeft, PoliceBottom);
        recipe.Add(PlacementKinds.Prop, "hardware_store", HardwareLeft, HardwareBottom);
        recipe.Add(PlacementKinds.Prop, "salon", SalonLeft, SalonBottom);
        recipe.Add(PlacementKinds.Prop, TownProps.StreetLightId, Light.X, Light.Y);

        // >= 1 tile clear of each road-mouth exit area (spawn-clearance rule). The
        // wrap marker is where a resident who left west finds themselves arriving.
        recipe.Add(PlacementKinds.Spawn, "default", 24, 15);
        recipe.Add(PlacementKinds.Spawn, "from_east_fork", 2, 15);
        recipe.Add(PlacementKinds.Spawn, RoadWrap.ArrivalSpawn, 45, 15);
        recipe.Add(PlacementKinds.Spawn, "from_salon", SalonDoorX, SalonBottom + 1);

        // Copy lives with the place (src/Content/Places/EastEntry.cs). The salon's
        // board stands south of the footprint (a sign north of a south-of-road
        // building lands inside its drawn face and Y-sorts invisible), west of the
        // doorway so the door approach stays clear.
        recipe.Add(PlacementKinds.Sign, "PoliceSign", 10, 12);
        recipe.Add(PlacementKinds.Sign, "HardwareSign", 23, 12);
        recipe.Add(PlacementKinds.Sign, "SalonSign", 32, 21);

        // The doorway is drawn into the placeholder face, so the Door node
        // contributes its blocker and its prompt only.
        recipe.Add(PlacementKinds.Door, MapIds.Salon, SalonDoorX, SalonBottom)
            .SetText(PlacementFields.Spawn, "entry");

        // West toward the east fork; east, the road out. It goes exactly where the
        // story says it goes.
        foreach ((string target, string spawn, int x) in new[]
        {
            (MapIds.EastFork, "from_east_entry", 0),
            (RoadWrap.PastTheEastEdgeMap, RoadWrap.ArrivalSpawn, Width - 1),
        })
        {
            MapPlacement exit = recipe.Add(PlacementKinds.Exit, target, x, RoadTop);
            exit.SetText(PlacementFields.Spawn, spawn);
            exit.SetInt(PlacementFields.Width, 1);
            exit.SetInt(PlacementFields.Height, 2);
        }

        // Kerb cut at the salon's frontage across the road.
        MapPlacement cut = recipe.Add(PlacementKinds.KerbCut, "salon_frontage", SalonDoorX - 1, RoadBottom);
        cut.SetInt(PlacementFields.Width, 2);
        cut.SetInt(PlacementFields.Height, 1);

        return recipe;
    }

    protected override string? SignTextFor(string signId) => EastEntry.SignTextFor(signId);

    // Sign mounts per the motel handoff §3: the wall band is the civic mount, so the
    // police station wears one lit from below; the hardware store's stays dark —
    // nobody is paying its bill. The salon is a store, so it takes the window mount,
    // and its OPEN neon runs on Sam's hours exactly.
    protected override IReadOnlyDictionary<string, ExteriorProp> PropCatalog() =>
        new Dictionary<string, ExteriorProp>(StringComparer.Ordinal)
        {
            ["police_station"] = new(BuildPolice,
                ExteriorProp.Rows(PoliceRight - PoliceLeft + 1, PoliceBottom - PoliceTop + 1)),
            ["hardware_store"] = new(BuildHardware,
                ExteriorProp.Rows(HardwareRight - HardwareLeft + 1, HardwareBottom - HardwareTop + 1)),
            // The salon's door cell gaps: the Door node carries its blocker.
            ["salon"] = new(BuildSalon,
                ExteriorProp.Rows(SalonRight - SalonLeft + 1, SalonBottom - SalonTop + 1)),
            // The cobra head in the verge.
            [TownProps.StreetLightId] = new(p => new StreetLight { Position = Prop.Anchor(p.X, p.Y) },
                ExteriorProp.Rows(1, 1)),
        };

    private static Node2D BuildPolice(MapPlacement p)
    {
        var police = new PlaceholderBuilding
        {
            Name = "PoliceStation",
            TilesWide = PoliceRight - PoliceLeft + 1,
            FootprintRows = PoliceBottom - PoliceTop + 1,
            Wall = new Color("7a8290"),
            Position = Prop.Anchor(p.X, p.Y, PoliceRight - PoliceLeft + 1),
        };
        police.AddChild(new WallBandSign { Text = "POLICE", Position = new Vector2(0, -55) });
        return police;
    }

    private static Node2D BuildHardware(MapPlacement p)
    {
        var hardware = new PlaceholderBuilding
        {
            Name = "HardwareStore",
            TilesWide = HardwareRight - HardwareLeft + 1,
            FootprintRows = HardwareBottom - HardwareTop + 1,
            Wall = new Color("8a7a5a"),
            Position = Prop.Anchor(p.X, p.Y, HardwareRight - HardwareLeft + 1),
        };
        hardware.AddChild(new WallBandSign
        {
            Text = "HARDWARE",
            LitAtNight = false,
            Position = new Vector2(0, -57),
        });
        return hardware;
    }

    private static Node2D BuildSalon(MapPlacement p)
    {
        var salon = new PlaceholderBuilding
        {
            Name = "Salon",
            TilesWide = SalonRight - SalonLeft + 1,
            FootprintRows = SalonBottom - SalonTop + 1,
            Wall = new Color("9a8a8a"),
            Position = Prop.Anchor(p.X, p.Y, SalonRight - SalonLeft + 1),
        };
        salon.AddChild(new WallBandSign { Text = "SALON", Position = new Vector2(0, -57) });
        salon.AddChild(new NeonWordSign
        {
            Word = "OPEN",
            OnAt = ShopHours.IsOpen,
            Position = new Vector2(-16, -39),
        });
        return salon;
    }
}

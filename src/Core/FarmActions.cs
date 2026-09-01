namespace TheHaunt.Core;

public enum ActionOutcome
{
    NoEffect,
    InvalidTarget,
    NotEnoughStamina,
    InventoryFull,
    Tilled,
    Planted,
    Watered,
    Harvested,
    Cleared,
    Struck,      // an obstacle took a hit and still stands
    Felled,      // final hit on an obstacle that leaves something behind (tree -> stump)
    Broken,      // final hit on an obstacle that clears the cell (stump, rock)
}

// All farming TileRecord mutations live here. Tile states:
// ABSENT (no record) → TILLED (Kind == "tilled", CropId null) → PLANTED (CropId set).
// Per-action contract: validate target → check stamina (refuse with NO mutation) →
// mutate → deduct. Stamina is charged only on effective use.
public static class FarmActions
{
    public static ActionOutcome UseSelected(GameData data, string mapId, int x, int y, long today, bool terrainTillable)
    {
        MapState map = data.GetMap(mapId);
        TileRecord? tile = map.GetTile(x, y);
        PlayerData player = data.Player;

        // 1. Mature crop → harvest, regardless of the selected item; stamina cost 0.
        if (tile?.CropId is string cropId
            && CropDefs.TryGet(cropId) is CropDef crop
            && tile.GrowthDay >= crop.TotalDays)
        {
            if (!player.Inventory.HasRoomFor(crop.HarvestItemId, crop.HarvestCount))
            {
                return ActionOutcome.InventoryFull;   // tile bit-identical
            }
            player.Inventory.Add(crop.HarvestItemId, crop.HarvestCount);
            if (crop.RegrowDays > 0)
            {
                tile.GrowthDay = crop.TotalDays - crop.RegrowDays;
            }
            else
            {
                tile.CropId = null;
                tile.GrowthDay = 0;
            }
            return ActionOutcome.Harvested;
        }

        ItemStackRecord? selected = player.Inventory.Selected;
        ItemDef? def = selected is null ? null : ItemDefs.TryGet(selected.ItemId);
        if (def is null)
        {
            return ActionOutcome.NoEffect;   // empty hand or unknown item id
        }

        // 2. Obstacles (trees, stumps, rocks). The cell's occupant owns the whole
        // interaction: the matching tool works it, and everything else — including
        // the hoe, whatever the view said about the terrain — is refused here, so
        // no model path can ever till or plant under a standing obstacle.
        if (map.GetObject(x, y) is PlacedObjectRecord obstacle)
        {
            ObstacleDef? obstacleDef = ObstacleDefs.TryGet(obstacle.ObjectId);
            if (obstacleDef is null || def.Tool != obstacleDef.Tool)
            {
                return ActionOutcome.NoEffect;   // unknown object (preserved) or wrong tool
            }
            if (player.Stamina < def.StaminaCost)
            {
                return ActionOutcome.NotEnoughStamina;
            }
            if (obstacle.HitsTaken + 1 < obstacleDef.Hits)
            {
                obstacle.HitsTaken++;
                player.Stamina -= def.StaminaCost;
                return ActionOutcome.Struck;
            }
            // The final hit grants the yield, all-or-nothing (harvest precedent):
            // a full inventory refuses the swing with the model bit-identical.
            if (!player.Inventory.HasRoomFor(obstacleDef.YieldItemId, obstacleDef.YieldCount))
            {
                return ActionOutcome.InventoryFull;
            }
            player.Inventory.Add(obstacleDef.YieldItemId, obstacleDef.YieldCount);
            player.Stamina -= def.StaminaCost;
            if (obstacleDef.BecomesId is string becomesId)
            {
                obstacle.ObjectId = becomesId;
                obstacle.HitsTaken = 0;
                return ActionOutcome.Felled;
            }
            map.RemoveObject(x, y);
            return ActionOutcome.Broken;
        }

        // 3. Tools.
        if (def.Tool is ToolKind tool)
        {
            switch (tool)
            {
                case ToolKind.Hoe:
                    if (tile is not null)
                    {
                        return ActionOutcome.NoEffect;
                    }
                    if (!terrainTillable)
                    {
                        return ActionOutcome.InvalidTarget;   // off-map or non-tillable terrain
                    }
                    if (player.Stamina < def.StaminaCost)
                    {
                        return ActionOutcome.NotEnoughStamina;
                    }
                    map.SetTile(new TileRecord { X = x, Y = y, Kind = "tilled", LastWateredDay = -1 });
                    player.Stamina -= def.StaminaCost;
                    return ActionOutcome.Tilled;

                case ToolKind.WateringCan:
                    // Any TILLED or PLANTED tile. Idempotent; pre-watering empty soil
                    // counts for tonight (planting preserves LastWateredDay).
                    if (tile is null || (tile.Kind != "tilled" && tile.CropId is null))
                    {
                        return ActionOutcome.NoEffect;
                    }
                    if (player.Stamina < def.StaminaCost)
                    {
                        return ActionOutcome.NotEnoughStamina;
                    }
                    tile.LastWateredDay = today;
                    player.Stamina -= def.StaminaCost;
                    return ActionOutcome.Watered;

                case ToolKind.Scythe:
                    // PLANTED only; a mature crop never reaches here (harvest priority).
                    // Unknown crop ids are preserved, not destroyed — same rule as items.
                    if (tile?.CropId is null || CropDefs.TryGet(tile.CropId) is null)
                    {
                        return ActionOutcome.NoEffect;
                    }
                    if (player.Stamina < def.StaminaCost)
                    {
                        return ActionOutcome.NotEnoughStamina;
                    }
                    tile.CropId = null;
                    tile.GrowthDay = 0;   // Kind stays "tilled"; no yield
                    player.Stamina -= def.StaminaCost;
                    return ActionOutcome.Cleared;
            }
            return ActionOutcome.NoEffect;
        }

        // 4. Seeds: TILLED with no crop only.
        if (def.PlantsCropId is string plantsCropId)
        {
            if (tile is null || tile.Kind != "tilled" || tile.CropId is not null)
            {
                return ActionOutcome.NoEffect;
            }
            if (player.Stamina < def.StaminaCost)
            {
                return ActionOutcome.NotEnoughStamina;
            }
            if (!player.Inventory.TryConsumeSelected(1))
            {
                return ActionOutcome.NoEffect;
            }
            tile.CropId = plantsCropId;
            tile.GrowthDay = 0;
            // LastWateredDay PRESERVED — watering is the only writer.
            player.Stamina -= def.StaminaCost;
            return ActionOutcome.Planted;
        }

        return ActionOutcome.NoEffect;
    }
}

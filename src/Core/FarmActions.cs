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

        // 2. Tools.
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

        // 3. Seeds: TILLED with no crop only.
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

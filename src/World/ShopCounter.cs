using Godot;
using TheHaunt.Content;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// Shop interaction strip along the store counter. No sprite of its own — the
/// counter tiles are the visual — and the owning map supplies the
/// CollisionShape2D covering them (MapExit precedent). Interacting during shop
/// hours opens the shop session on the bus; outside hours the prompt says so
/// and the press is a no-op. Doors are never locked — hours gate the counter,
/// not the building.
/// </summary>
public partial class ShopCounter : Area2D, IInteractable
{
    /// <summary>The catalog Interact opens (the shop_counter placement's id).</summary>
    public string CatalogId { get; init; } = ShopCatalog.GeneralStore;

    // The closed line lives with the place (src/Content/Places/Town.cs).
    public string PromptText =>
        ShopHours.IsOpen(Clock.Instance.Now.MinuteOfDay) ? "Shop" : Town.CounterClosedLine;

    public bool CanInteract(Node2D interactor) =>
        GameState.Instance.PlayerHasControl;

    public void Interact(Node2D interactor)
    {
        if (ShopHours.IsOpen(Clock.Instance.Now.MinuteOfDay))
            WorldSim.Instance.OpenShop(CatalogId);
    }

    public override void _Ready()
    {
        CollisionLayer = 2;
        CollisionMask = 0;
        Monitorable = true;
    }
}

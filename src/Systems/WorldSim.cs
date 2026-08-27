using Godot;
using TheHaunt.Core;
using TheHaunt.World;

namespace TheHaunt.Systems;

/// <summary>
/// Autoload #4 — the single gameplay-mutation bus. Every Phase-2 model write flows
/// through it, and UI subscribes to ITS events, never to events on Core objects
/// (SaveService swaps <see cref="SaveService.Current"/> wholesale on load).
/// </summary>
public partial class WorldSim : Node
{
    public static WorldSim Instance { get; private set; } = null!;

    /// <summary>Slots OR selection changed.</summary>
    public event Action? InventoryChanged;

    /// <summary>(current, max).</summary>
    public event Action<int, int>? StaminaChanged;

    public event Action<long>? MoneyChanged;

    /// <summary>Storage id whose slots mutated — fired once per mutating transfer.</summary>
    public event Action<string>? StorageChanged;

    public event Action<OvernightReport>? OvernightCompleted;

    /// <summary>(flagId, dayStamped) — fired only on NEW sets, never on re-sets.</summary>
    public event Action<string, long>? StoryFlagSet;

    /// <summary>(mapId, spawnId) — Main subscribes once and owns the fade/swap flow.</summary>
    // (mapId, spawnId, arrivalOffset) — the offset is the travelling body's position
    // relative to the exit zone it walked into, so the arrival can keep the player's
    // place across the seam (MapRoot.GetArrival). Zero for door/scripted travel.
    public event Action<string, string, Vector2>? TravelRequested;

    /// <summary>Mounted, dismounted, or the parked record moved.</summary>
    public event Action? ScooterChanged;

    public event Action<DialogueSession>? DialogueStarted;

    /// <summary>After any non-finishing state change (advance or choice).</summary>
    public event Action<DialogueSession>? DialogueAdvanced;

    /// <summary>Def id; fired AFTER flags applied + phase restored.</summary>
    public event Action<string>? DialogueFinished;

    /// <summary>Storage id — the chest UI shows on this.</summary>
    public event Action<string>? StorageOpened;

    public event Action? StorageClosed;

    /// <summary>Catalog id — the shop UI shows on this.</summary>
    public event Action<string>? ShopOpened;

    public event Action? ShopClosed;

    public DialogueSession? ActiveDialogue { get; private set; }

    /// <summary>Storage id of the open chest session; null when none.</summary>
    public string? OpenStorageId { get; private set; }

    /// <summary>Catalog id of the open shop session; null when none.</summary>
    public string? OpenShopId { get; private set; }

    private readonly List<MapRoot> _maps = new();
    private OvernightReport _pendingReport;

    // Whether the running dialogue took the phase to Dialogue itself; a beat-started
    // session (Cutscene) leaves the phase alone in both directions — the beat owns it.
    private bool _dialogueFromPlaying;

    private static readonly IReadOnlyList<(NpcDef Def, NpcPlacement Placement)> NoNpcs =
        Array.Empty<(NpcDef, NpcPlacement)>();

    public override void _EnterTree()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        Clock.Instance.DayEnded += OnDayEnded;
        Clock.Instance.DayStarted += OnDayStarted;
        Clock.Instance.TenMinuteTicked += OnTenMinuteTicked;
        // Autoload order GameState -> Clock -> SaveService -> WorldSim makes this safe.
        SaveService.Instance.AfterLoad += OnAfterLoad;
    }

    public override void _ExitTree()
    {
        Clock.Instance.DayEnded -= OnDayEnded;
        Clock.Instance.DayStarted -= OnDayStarted;
        Clock.Instance.TenMinuteTicked -= OnTenMinuteTicked;
        SaveService.Instance.AfterLoad -= OnAfterLoad;
    }

    public void RegisterMap(MapRoot map)
    {
        if (!_maps.Contains(map))
        {
            _maps.Add(map);
        }
    }

    public void UnregisterMap(MapRoot map)
    {
        _maps.Remove(map);
    }

    public void SelectSlot(int slot)
    {
        InventoryData inventory = SaveService.Instance.Current.Player.Inventory;
        inventory.SelectedSlot = Math.Clamp(slot, 0, InventoryData.Capacity - 1);
        InventoryChanged?.Invoke();
    }

    public ActionOutcome UseSelectedItem(Vector2I tile)
    {
        GameData data = SaveService.Instance.Current;
        string mapId = data.Player.MapId;
        MapRoot? map = FindRegisteredMap(mapId);
        if (map == null)
        {
            return ActionOutcome.InvalidTarget;
        }

        bool terrainTillable = map.IsTillable(tile.X, tile.Y);
        ActionOutcome outcome = FarmActions.UseSelected(
            data, mapId, tile.X, tile.Y,
            today: Clock.Instance.Now.DayIndex,
            terrainTillable: terrainTillable);

        switch (outcome)
        {
            case ActionOutcome.Tilled:
            case ActionOutcome.Watered:
            case ActionOutcome.Cleared:
            case ActionOutcome.Planted:
            case ActionOutcome.Harvested:
                map.RefreshTile(tile.X, tile.Y, data.GetMap(mapId).GetTile(tile.X, tile.Y));
                if (outcome is ActionOutcome.Planted or ActionOutcome.Harvested)
                {
                    InventoryChanged?.Invoke();
                }
                StaminaChanged?.Invoke(data.Player.Stamina, data.Player.MaxStamina);
                break;
        }

        // Story trigger observed at the bus — FarmActions stays story-free. Only-if-absent
        // makes every planting after the first a no-op.
        if (outcome == ActionOutcome.Planted)
        {
            SetStoryFlag(StoryKeys.FirstPlanting);
        }

        return outcome;
    }

    public bool DepositSelectedToBin()
    {
        GameData data = SaveService.Instance.Current;
        InventoryData inventory = data.Player.Inventory;
        ItemStackRecord? stack = inventory.Selected;
        if (stack == null)
        {
            return false;
        }

        // Unknown ids resolve to no def → treated as unsellable and kept in the
        // inventory (there is no withdraw in v2; refusing is the non-destructive path).
        ItemDef? def = ItemDefs.TryGet(stack.ItemId);
        if (def == null || def.SellPrice <= 0)
        {
            return false;
        }

        ItemStackRecord? existing = data.ShippingBin.FirstOrDefault(s => s.ItemId == stack.ItemId);
        if (existing != null)
        {
            // Merge in long: an int wrap here would mint a negative count the overnight
            // sale turns into negative money. Refusal is the non-destructive path.
            long merged = (long)existing.Count + stack.Count;
            if (merged > int.MaxValue)
            {
                return false;
            }
            existing.Count = (int)merged;
        }
        else
        {
            data.ShippingBin.Add(stack);
        }

        inventory.Slots[inventory.SelectedSlot] = null;
        InventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Moves the WHOLE stack in <paramref name="inventorySlot"/> into the storage,
    /// partial-on-overflow. Loss/dupe-proof ordering (spec §1.5): the source slot is
    /// vacated BEFORE the add, and the remainder (if any) goes back into that
    /// just-vacated slot — it cannot collide with the add. Phase-free model op like
    /// <see cref="DepositSelectedToBin"/>; the UI owns gating. False = nothing moved
    /// (empty slot, or destination full) — no events fire.
    /// </summary>
    public bool TransferToStorage(string storageId, int inventorySlot)
    {
        GameData data = SaveService.Instance.Current;
        InventoryData inventory = data.Player.Inventory;
        ItemStackRecord? stack = inventory.SlotAt(inventorySlot);
        if (stack == null)
        {
            return false;
        }

        StorageData storage = data.GetStorage(storageId);
        inventory.Slots[inventorySlot] = null;
        int overflow = StackOps.Add(storage.Slots, stack.ItemId, stack.Count);
        if (overflow == stack.Count)
        {
            // Nothing moved — restore the original stack object; NO events.
            inventory.Slots[inventorySlot] = stack;
            return false;
        }
        if (overflow > 0)
        {
            inventory.Slots[inventorySlot] =
                new ItemStackRecord { ItemId = stack.ItemId, Count = overflow };
        }

        StorageChanged?.Invoke(storageId);
        InventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Mirror of <see cref="TransferToStorage"/>: storage slot → inventory. Unknown
    /// item ids transfer normally at max stack 1 — never destroyed.
    /// </summary>
    public bool TransferToInventory(string storageId, int storageSlot)
    {
        GameData data = SaveService.Instance.Current;
        StorageData storage = data.GetStorage(storageId);
        if (storageSlot < 0 || storageSlot >= storage.Slots.Count)
        {
            return false;
        }
        ItemStackRecord? stack = storage.Slots[storageSlot];
        if (stack == null)
        {
            return false;
        }

        InventoryData inventory = data.Player.Inventory;
        storage.Slots[storageSlot] = null;
        int overflow = StackOps.Add(inventory.Slots, stack.ItemId, stack.Count);
        if (overflow == stack.Count)
        {
            storage.Slots[storageSlot] = stack;
            return false;
        }
        if (overflow > 0)
        {
            storage.Slots[storageSlot] =
                new ItemStackRecord { ItemId = stack.ItemId, Count = overflow };
        }

        StorageChanged?.Invoke(storageId);
        InventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Stamps <paramref name="flagId"/> with today's index (only-if-absent). On a NEW set:
    /// repaint every registered map, resync NPCs, fire <see cref="StoryFlagSet"/>.
    /// </summary>
    public bool SetStoryFlag(string flagId)
    {
        GameData data = SaveService.Instance.Current;
        long day = Clock.Instance.Now.DayIndex;
        if (!data.TrySetFlag(flagId, day))
        {
            return false;
        }

        foreach (MapRoot map in _maps)
        {
            if (!map.IsQueuedForDeletion())
            {
                map.ApplyState(data.GetMap(map.MapId));
            }
        }
        SyncNpcsNow();
        StoryFlagSet?.Invoke(flagId, day);
        return true;
    }

    /// <summary>Gate: player control + known map. True means <see cref="TravelRequested"/> fired.
    /// <paramref name="arrivalOffset"/> is where the body stood relative to the exit zone
    /// it entered (MapExit passes it; doors and scripted travel leave it zero).</summary>
    public bool RequestTravel(string mapId, string spawnId, Vector2 arrivalOffset = default)
    {
        if (!GameState.Instance.PlayerHasControl || !MapRegistry.Contains(mapId))
        {
            return false;
        }
        TravelRequested?.Invoke(mapId, spawnId, arrivalOffset);
        return true;
    }

    /// <summary>The model half of Main's travel flow: move the player record, restage NPCs.</summary>
    public void CompleteTravel(string mapId)
    {
        SaveService.Instance.Current.Player.MapId = mapId;
        SyncNpcsNow();
        SyncScooterNow();
    }

    public bool ScooterMounted => SaveService.Instance.Current.Scooter.Mounted;

    /// <summary>
    /// Mounts the parked scooter (handoff §Interactions: no ceremony — the caller
    /// swaps nothing but position; the texture and speed follow the model). Gate:
    /// player control, not already mounted, and the scooter is on the player's map.
    /// True means the parked world object is gone and the player is riding.
    /// </summary>
    public bool MountScooter()
    {
        GameData data = SaveService.Instance.Current;
        if (!GameState.Instance.PlayerHasControl || data.Scooter.Mounted
            || data.Scooter.MapId != data.Player.MapId)
        {
            return false;
        }
        data.Scooter.Mounted = true;
        SyncScooterNow();
        ScooterChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Parks the ridden scooter on <paramref name="tile"/> of the player's current
    /// map — the tile the rider is standing on (handoff: dismount spawns it there).
    /// Safe no-op when not mounted.
    /// </summary>
    public bool DismountScooter(Vector2I tile, int facing) =>
        ParkScooterAt(SaveService.Instance.Current.Player.MapId, tile, facing);

    /// <summary>
    /// The interior rule's entry point (handoff: never ridden indoors): Main parks the
    /// scooter at the door the rider just walked through — on the OUTSIDE map — before
    /// the interior loads. Refused when not mounted: only the rider parks it.
    /// </summary>
    public bool ParkScooterAt(string mapId, Vector2I tile, int facing)
    {
        GameData data = SaveService.Instance.Current;
        if (!data.Scooter.Mounted)
        {
            return false;
        }
        data.Scooter.MapId = mapId;
        data.Scooter.TileX = tile.X;
        data.Scooter.TileY = tile.Y;
        data.Scooter.Facing = Math.Clamp(facing, 0, 3);
        data.Scooter.Mounted = false;
        SyncScooterNow();
        ScooterChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Pushes the scooter record to every live registered map: the map it is parked
    /// on shows the view, every other map (and every map while mounted) shows none.
    /// </summary>
    public void SyncScooterNow()
    {
        ScooterData scooter = SaveService.Instance.Current.Scooter;
        foreach (MapRoot map in _maps)
        {
            if (!map.IsQueuedForDeletion())
            {
                map.SyncScooter(
                    !scooter.Mounted && scooter.MapId == map.MapId ? scooter : null);
            }
        }
    }

    public bool IsMapActive(string mapId) => FindRegisteredMap(mapId) != null;

    public bool StartDialogue(string dialogueId)
    {
        // The ActiveDialogue guard covers Cutscene, where the phase alone can't refuse a
        // second start (a clobbered session would silently drop its accumulated flags).
        if (!GameState.Instance.CanStartDialogue || ActiveDialogue != null)
        {
            return false;
        }
        DialogueDef? def = DialogueDefs.TryGet(dialogueId);
        if (def == null)
        {
            GD.PushError($"Unknown dialogue id '{dialogueId}'.");
            return false;
        }

        _dialogueFromPlaying = GameState.Instance.PlayerHasControl;
        ActiveDialogue = new DialogueSession(def);
        if (_dialogueFromPlaying)
        {
            GameState.Instance.TransitionTo(GameState.Phase.Dialogue);
        }
        DialogueStarted?.Invoke(ActiveDialogue);
        return true;
    }

    public bool StartNpcDialogue(string roleId)
    {
        string? dialogueId = DialogueSelector.ForNpc(
            roleId, SaveService.Instance.Current, Clock.Instance.Now);
        return dialogueId != null && StartDialogue(dialogueId);
    }

    /// <summary>Safe no-op when no session is running or the session waits on a choice.</summary>
    public void AdvanceDialogue()
    {
        if (ActiveDialogue == null || ActiveDialogue.AtChoices)
        {
            return;
        }
        if (!ActiveDialogue.Advance())
        {
            return;
        }
        if (ActiveDialogue.Finished)
        {
            FinishDialogue();
        }
        else
        {
            DialogueAdvanced?.Invoke(ActiveDialogue);
        }
    }

    public void ChooseDialogueOption(int index)
    {
        if (ActiveDialogue == null || !ActiveDialogue.AtChoices)
        {
            return;
        }
        if (!ActiveDialogue.Choose(index))
        {
            return;
        }
        if (ActiveDialogue.Finished)
        {
            FinishDialogue();
        }
        else
        {
            DialogueAdvanced?.Invoke(ActiveDialogue);
        }
    }

    // Finish sequence (spec §3.5), in order: flags land through SetStoryFlag (repaint +
    // NPC resync + StoryFlagSet per new flag), the session dies, the phase restores iff
    // this bus took it, THEN listeners hear the def id — so DialogueFinished handlers
    // always observe the post-dialogue world.
    private void FinishDialogue()
    {
        DialogueSession session = ActiveDialogue!;
        foreach (string flagId in session.FlagsRaised)
        {
            SetStoryFlag(flagId);
        }
        ActiveDialogue = null;
        if (_dialogueFromPlaying)
        {
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        }
        DialogueFinished?.Invoke(session.Def.Id);
    }

    // Menu sessions (chest / shop) mirror the dialogue session: this bus owns which
    // session is open and the phase moves with it; the UIs subscribe to Opened/Closed
    // and never drive the phase themselves. Both sessions only ever open from Playing
    // (PlayerHasControl gate), so Close always restores Playing — no from-phase flag
    // needed. FinishDialogue discipline throughout: id nulled + phase restored, THEN
    // listeners hear the event.

    /// <summary>
    /// Gate: player control + no session of either kind open. True means the phase
    /// moved to Menu and <see cref="StorageOpened"/> fired.
    /// </summary>
    public bool OpenStorage(string storageId)
    {
        if (!GameState.Instance.PlayerHasControl || OpenStorageId != null || OpenShopId != null)
        {
            return false;
        }
        OpenStorageId = storageId;
        GameState.Instance.TransitionTo(GameState.Phase.Menu);
        StorageOpened?.Invoke(storageId);
        return true;
    }

    /// <summary>Safe no-op when no chest session is open.</summary>
    public void CloseStorage()
    {
        if (OpenStorageId == null)
        {
            return;
        }
        OpenStorageId = null;
        GameState.Instance.TransitionTo(GameState.Phase.Playing);
        StorageClosed?.Invoke();
    }

    /// <summary>
    /// Same shape as <see cref="OpenStorage"/>; additionally refuses catalog ids
    /// <see cref="ShopCatalog"/> does not know.
    /// </summary>
    public bool OpenShop(string catalogId)
    {
        if (!GameState.Instance.PlayerHasControl || OpenStorageId != null || OpenShopId != null)
        {
            return false;
        }
        if (ShopCatalog.TryGet(catalogId) == null)
        {
            return false;
        }
        OpenShopId = catalogId;
        GameState.Instance.TransitionTo(GameState.Phase.Menu);
        ShopOpened?.Invoke(catalogId);
        return true;
    }

    /// <summary>Safe no-op when no shop session is open.</summary>
    public void CloseShop()
    {
        if (OpenShopId == null)
        {
            return;
        }
        OpenShopId = null;
        GameState.Instance.TransitionTo(GameState.Phase.Playing);
        ShopClosed?.Invoke();
    }

    /// <summary>
    /// Buys <paramref name="count"/> of <paramref name="itemId"/> from the open shop's
    /// catalog, all-or-nothing. Integrity ordering (spec §3.2): every check lands
    /// strictly BEFORE any mutation — a failed buy touches neither money nor inventory
    /// and fires no events. On Ok: MoneyChanged THEN InventoryChanged.
    /// </summary>
    public BuyResult BuyItem(string itemId, int count)
    {
        // Malformed count is refused before any arithmetic (a negative count would
        // otherwise credit money through the debit below).
        if (count <= 0)
        {
            return BuyResult.UnknownItem;
        }

        // (1) The item must be on the OPEN shop's catalog — no session, no sale.
        int price = -1;
        IReadOnlyList<ShopEntry>? catalog =
            OpenShopId == null ? null : ShopCatalog.TryGet(OpenShopId);
        if (catalog != null)
        {
            foreach (ShopEntry entry in catalog)
            {
                if (entry.ItemId == itemId)
                {
                    price = entry.BuyPrice;
                    break;
                }
            }
        }
        if (price < 0)
        {
            return BuyResult.UnknownItem;
        }

        GameData data = SaveService.Instance.Current;
        PlayerData player = data.Player;

        // (2)-(4) Remaining checks, still mutation-free.
        long cost = (long)price * count;
        if (player.Money < cost)
        {
            return BuyResult.InsufficientFunds;
        }
        if (!player.Inventory.HasRoomFor(itemId, count))
        {
            return BuyResult.NoRoom;
        }

        // (5)-(7) Mutate, then events in the frozen order.
        player.Money -= cost;
        int overflow = player.Inventory.Add(itemId, count);
        if (overflow != 0)
        {
            // HasRoomFor passed above — nonzero overflow means the stack algebra drifted.
            GD.PushError($"BuyItem overflow {overflow} for '{itemId}' x{count} after HasRoomFor passed.");
        }
        MoneyChanged?.Invoke(player.Money);
        InventoryChanged?.Invoke();
        return BuyResult.Ok;
    }

    /// <summary>
    /// Re-derives NPC staging from (StoryFlags, Clock.Now) and pushes it to every live
    /// registered map. Maps with no scheduled NPC get an empty list — that is how
    /// departures despawn.
    /// </summary>
    public void SyncNpcsNow()
    {
        GameData data = SaveService.Instance.Current;
        GameTime now = Clock.Instance.Now;

        Dictionary<string, List<(NpcDef Def, NpcPlacement Placement)>> byMap = new();
        foreach (NpcDef def in NpcDefs.All.Values)
        {
            NpcPlacement? resolved = NpcSchedules.Resolve(def, data, now);
            if (resolved is { } placement)
            {
                if (!byMap.TryGetValue(placement.MapId, out var list))
                {
                    list = new();
                    byMap[placement.MapId] = list;
                }
                list.Add((def, placement));
            }
        }

        foreach (MapRoot map in _maps)
        {
            if (!map.IsQueuedForDeletion())
            {
                map.SyncNpcs(byMap.TryGetValue(map.MapId, out var entries) ? entries : NoNpcs);
            }
        }
    }

    // The overnight sim is split across the two day events: the model mutation runs on
    // DayEnded (its payload carries the day being closed — the day watering happened),
    // but the repaint and UI events wait for DayStarted, because the wet/dry soil visual
    // is a function of Clock.Now — which still reads the ENDING day during DayEnded.
    // Both fire synchronously inside AdvanceToDayStart, before Main's autosave.
    private void OnDayEnded(GameTime endedDay)
    {
        _pendingReport = OvernightSim.Run(SaveService.Instance.Current, endedDay.DayIndex);
    }

    // Committed ordering (spec §1.4, risk R3): 1 dawn flags, 2 repaint, 3 UI events,
    // 4 NPC sync, 5 StoryFlagSet — all before Main's autosave. Violating it is a bug.
    private void OnDayStarted(GameTime newDay)
    {
        GameData data = SaveService.Instance.Current;

        // 1) Dawn story rules write directly — a SetStoryFlag call here would repaint
        //    per flag; the batch defers repaint/sync/events to the steps below.
        List<string> newFlags = new();
        foreach (string flagId in IntroRules.FlagsToSetOnDayStarted(data, newDay.DayIndex))
        {
            if (data.TrySetFlag(flagId, newDay.DayIndex))
            {
                newFlags.Add(flagId);
            }
        }

        // 2) Full repaint per registered map: wet overlays flip everywhere, stages
        //    advance, the road blockade toggles.
        foreach (MapRoot map in _maps)
        {
            if (!map.IsQueuedForDeletion())
            {
                map.ApplyState(data.GetMap(map.MapId));
            }
        }

        // 3) UI events.
        OvernightCompleted?.Invoke(_pendingReport);
        MoneyChanged?.Invoke(data.Player.Money);
        StaminaChanged?.Invoke(data.Player.Stamina, data.Player.MaxStamina);
        InventoryChanged?.Invoke();

        // 4) AdvanceToDayStart fires no ten-minute ticks — dawn staging would otherwise
        //    be stale until 6:10. The scooter restages too: OvernightSim just parked
        //    it back home.
        SyncNpcsNow();
        SyncScooterNow();

        // 5) Listeners (StoryDirector) see a fully repainted, restaged world.
        foreach (string flagId in newFlags)
        {
            StoryFlagSet?.Invoke(flagId, newDay.DayIndex);
        }
    }

    // NPC staging is a pure function of (flags, time) — re-derive on every coarse tick.
    private void OnTenMinuteTicked(GameTime time) => SyncNpcsNow();

    // A load can land mid-session: dialogue is atomic (complete or replay), so the
    // session is discarded WITHOUT applying its flags; staging re-derives from the new
    // graph. StoryDirector and DialogueUi handle their own AfterLoad cleanup.
    private void OnAfterLoad()
    {
        // A Playing-started session took the phase to Dialogue; discarding it must
        // also give the phase back, or the load strands a frozen, uncontrollable
        // game (beat-started sessions leave the exit to StoryDirector's finally).
        if (ActiveDialogue != null && _dialogueFromPlaying)
        {
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        }
        ActiveDialogue = null;

        // Same strand for the menu sessions: a load can land while a chest or shop is
        // up (both always Menu-from-Playing), so the discarded session must give the
        // phase back and tell its UI to hide. Flag-based — never a phase comparison.
        if (OpenStorageId != null || OpenShopId != null)
        {
            bool storageWasOpen = OpenStorageId != null;
            bool shopWasOpen = OpenShopId != null;
            OpenStorageId = null;
            OpenShopId = null;
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
            if (storageWasOpen)
            {
                StorageClosed?.Invoke();
            }
            if (shopWasOpen)
            {
                ShopClosed?.Invoke();
            }
        }

        SyncNpcsNow();
        SyncScooterNow();
    }

    private MapRoot? FindRegisteredMap(string mapId)
    {
        foreach (MapRoot map in _maps)
        {
            // A QueueFree'd map stays registered until its deferred _ExitTree runs at end
            // of frame; during a same-frame map swap the dying map must not win.
            if (map.MapId == mapId && !map.IsQueuedForDeletion())
            {
                return map;
            }
        }
        return null;
    }
}

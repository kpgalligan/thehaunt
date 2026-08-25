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

    public event Action<OvernightReport>? OvernightCompleted;

    /// <summary>(flagId, dayStamped) — fired only on NEW sets, never on re-sets.</summary>
    public event Action<string, long>? StoryFlagSet;

    /// <summary>(mapId, spawnId) — Main subscribes once and owns the fade/swap flow.</summary>
    public event Action<string, string>? TravelRequested;

    public event Action<DialogueSession>? DialogueStarted;

    /// <summary>After any non-finishing state change (advance or choice).</summary>
    public event Action<DialogueSession>? DialogueAdvanced;

    /// <summary>Def id; fired AFTER flags applied + phase restored.</summary>
    public event Action<string>? DialogueFinished;

    public DialogueSession? ActiveDialogue { get; private set; }

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
            existing.Count += stack.Count;
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

    /// <summary>Gate: player control + known map. True means <see cref="TravelRequested"/> fired.</summary>
    public bool RequestTravel(string mapId, string spawnId)
    {
        if (!GameState.Instance.PlayerHasControl || !MapRegistry.Contains(mapId))
        {
            return false;
        }
        TravelRequested?.Invoke(mapId, spawnId);
        return true;
    }

    /// <summary>The model half of Main's travel flow: move the player record, restage NPCs.</summary>
    public void CompleteTravel(string mapId)
    {
        SaveService.Instance.Current.Player.MapId = mapId;
        SyncNpcsNow();
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
        //    be stale until 6:10.
        SyncNpcsNow();

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
        SyncNpcsNow();
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

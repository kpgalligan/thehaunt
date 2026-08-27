using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;
using TheHaunt.World;

namespace TheHaunt.EditorTools;

/// <summary>
/// A live preview of any shipped map, in the Godot editor viewport. Maps are C# build
/// functions rather than scenes, so the editor has never been able to show one and the
/// whole visual feedback loop was booting the game for a --screenshot. This closes it:
/// pick a map, pick a time, look at it.
///
/// It renders the same nodes the game renders, from the same registry, and owns none of
/// them past the next rebuild — it is a preview, never a source of truth. Autoloads do
/// not run in the editor (every Instance is null), so the stage hand-builds a stub of
/// the four in project.godot's own order; that stub is the entire reason src/World needs
/// no editor branch anywhere, and _Ready's guard below is the only one in src/.
///
/// The one thing it DOES own is the working recipe: the single mutable copy of the map's
/// placements, which the addons/haunt_mapper plugin drags records around inside and which
/// the preview is built from. Rebuild never writes to disk; only SaveRecipe does.
/// </summary>
[Tool]
public partial class MapStage : Node2D
{
    /// <summary>
    /// The slot <see cref="SaveService.NewGame"/> lands in. Editor-only and set before
    /// every build, so no amount of scrubbing can reach the save the user is playing.
    /// </summary>
    private const string EditorSlot = "editor_stage";

    // 18:00. A day here runs 6:00 -> 2:00 AM, so minute-of-day 720 is DayNight's dusk
    // key: the first one where the lantern level is non-zero (LightKeys, 0.55) and the
    // tint has left white. Defaulting to a daylight hour would open on a preview where
    // every lantern is off and the tint is identity — the lighting invisible.
    private const int DuskMinute = 720;

    // GlowLight and LampPost read the clock in _Process instead of subscribing to it, so
    // a re-time needs the map subtree processing for a frame or two before it can go back
    // to costing nothing. Three is slack, not a measurement.
    private const int SettleFrames = 3;

    /// <summary>
    /// Metadata key prefix the unsaved working recipe is parked under, one entry per map.
    /// Node metadata and not a C# field, because the field is the thing an assembly
    /// reload takes away: every `dotnet build` with the editor open swaps the managed
    /// object out from under the still-living Node, and a placement dragged five seconds
    /// ago must not be a casualty of a build. Metadata is engine-side and survives it.
    /// Removed the moment the working copy matches disk, so a clean stage carries none.
    /// </summary>
    private const string RecipeMetaPrefix = "haunt_recipe_";

    private string _stageMapId = MapIds.Farm;
    private int _minuteOfDay = DuskMinute;
    private int _settleFrames;

    // Parsed cache of the metadata above, plus the map it belongs to — the pair is the
    // invalidation rule, since StageMapId can change under a cache that is still valid
    // JSON for the map it was read for.
    private MapRecipe? _recipe;
    private string _recipeMapId = "";
    private int _recipeStamp;
    // Keyed by metadata name so a scene save cannot lose the maps it is not showing.
    // Plain C# state, and deliberately so: it lives only between PreSave and PostSave,
    // which is inside one call frame's worth of editor work, not across a reload.
    private readonly Dictionary<StringName, string> _preSaveStash = new();

    // Set by every path that tries to READ the recipe file. The distinction it carries is
    // the difference between "this map has no recipe yet" (fine, an empty recipe is the
    // honest answer) and "this map's recipe is there but unparseable" (never fine to
    // write over). See SaveRecipe.
    private bool _diskUnreadable;

    /// <summary>Which map to render — any id <see cref="MapRegistry"/> knows.</summary>
    [Export]
    public string StageMapId
    {
        get => _stageMapId;
        set
        {
            // Deliberately NOT short-circuited when the value is unchanged. An assembly
            // reload restores every exported property by setting it, and that restore is
            // the stage's only notice that the preview in front of it is now a corpse —
            // the nodes survive, but their C# state (a map's reserved-tile set, a layer
            // reference) came back empty with the new managed instances. Rebuilding on a
            // set-to-the-same-value is how the preview comes back correct after a
            // `dotnet build`; measured, it was the difference between 39 reserved tiles
            // and 0. A needless five-millisecond rebuild is not worth avoiding here.
            _stageMapId = value;
            if (CanRestage)
            {
                Rebuild();
            }
        }
    }

    /// <summary>
    /// Minute of day, 0 = 6:00 AM. Re-times in place — geometry does not depend on the
    /// clock, so scrubbing never pays for a rebuild.
    ///
    /// The range stops at 1190, not at a wall-clock 1430: a day is
    /// <see cref="GameTime.MinutesPerDay"/> = 1200 minutes from 6 AM, and stepping the
    /// clock refuses to cross a day boundary (ClockModel.AdvanceMinutes returns at
    /// AtEndOfDay, 1:59 AM), so every mark past 1190 would be dead slider travel.
    /// </summary>
    [Export(PropertyHint.Range, "0,1190,10")]
    public int MinuteOfDay
    {
        get => _minuteOfDay;
        set
        {
            // Idempotent for the same reason StageMapId is — and here it also stops a
            // slider that has not moved from replaying up to 1190 minutes of tick
            // handlers, which is what a re-time costs.
            if (_minuteOfDay == value)
            {
                return;
            }
            _minuteOfDay = value;
            if (CanRestage)
            {
                ApplyTime();
            }
        }
    }

    /// <summary>
    /// The live map, for a plugin to hit-test against; null until a build succeeds.
    /// Walked out of the children rather than cached, for the same reason
    /// <see cref="Teardown"/> sweeps by type: an assembly reload nulls managed fields
    /// without freeing the nodes they pointed at.
    /// </summary>
    public MapRoot? Map
    {
        get
        {
            foreach (Node child in GetChildren())
            {
                if (child is MapRoot map && !map.IsQueuedForDeletion())
                {
                    return map;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Empty when the last build succeeded. Held as well as pushed, because the Output
    /// panel is not where someone dragging a prop is looking — a plugin can put the text
    /// on the stage itself.
    /// </summary>
    public string LastError { get; private set; } = "";

    /// <summary>
    /// Empty unless this map's recipe FILE is unreadable — a hand-broken JSON, a header
    /// naming another map. Separate from <see cref="LastError"/> on purpose: a build that
    /// succeeded off an empty recipe looks perfectly fine in the viewport, and the only
    /// thing that can tell anyone their file is broken is a line of text in the dock.
    /// </summary>
    public string RecipeError { get; private set; } = "";

    /// <summary>
    /// A one-off note for the dock to show — currently only the scene-save warning, which
    /// is the trap this whole editor is most likely to spring: Ctrl+S over the stage
    /// scene saves the .tscn, and someone who has just dragged half a map will read that
    /// as having saved the map. Cleared by the next save or revert.
    /// </summary>
    public string Notice { get; private set; } = "";

    // Exports arrive before _Ready during scene instantiation, and the in-game instance
    // frees itself the moment it readies; neither is a time to build a preview.
    private bool CanRestage => IsNodeReady() && !IsQueuedForDeletion();

    // ------------------------------------------------------------------
    // The working recipe — the single mutable copy of a map's placements
    // ------------------------------------------------------------------

    /// <summary>
    /// The placements this stage is previewing, live and mutable. THE single copy: the
    /// preview builds from it (<see cref="MapRoot.RecipeOverride"/>), the plugin drags
    /// records around inside it, and disk is only ever touched by <see cref="SaveRecipe"/>
    /// and <see cref="RevertRecipe"/>. <see cref="Rebuild"/> never writes.
    ///
    /// Read on demand rather than built once, so an assembly reload — which nulls the
    /// cache but leaves the metadata behind it — restores the same recipe rather than
    /// half of one.
    ///
    /// Mutate the records it hands back and then call <see cref="CommitRecipeEdit"/>;
    /// nothing here watches for changes.
    /// </summary>
    public MapRecipe Recipe
    {
        get
        {
            if (_recipe != null && _recipeMapId == StageMapId)
            {
                return _recipe;
            }
            _recipe = ReadWorkingRecipe(StageMapId);
            _recipeMapId = StageMapId;
            _recipeStamp++;
            return _recipe;
        }
    }

    /// <summary>
    /// Bumped every time <see cref="Recipe"/> becomes a DIFFERENT object — a map switch,
    /// a revert, an undo. A plugin holding a selection as an index into
    /// <c>Recipe.Placements</c> compares this before trusting it; the index survives a
    /// drag (which mutates in place) and not a re-parse (which does not).
    /// </summary>
    public int RecipeStamp => _recipeStamp;

    /// <summary>Where <see cref="SaveRecipe"/> writes and <see cref="RevertRecipe"/> reads.</summary>
    public string RecipePath => MapRecipeFile.PathFor(StageMapId);

    /// <summary>
    /// Whether this map has a recipe file at all. False means its placements are still
    /// C# literals and nothing in the viewport is draggable — which is worth SAYING,
    /// because a map that ignores its (empty) recipe and an editor that has lost its
    /// recipe look identical from the outside.
    /// </summary>
    public bool RecipeExists => Godot.FileAccess.FileExists(RecipePath);

    /// <summary>Whether the working copy has drifted from the file. The canonical text is the comparison — same recipe, same bytes.</summary>
    public bool IsDirty => Recipe.ToJson() != DiskJson();

    /// <summary>
    /// True when the recipe FILE exists but cannot be parsed. Reads the file, so it also
    /// refreshes <see cref="RecipeError"/>.
    ///
    /// A missing file is NOT unreadable: a map with no recipe yet legitimately loads as
    /// an empty one. This is specifically "there is something there and we could not
    /// understand it", which is the one state where the working copy is a fallback nobody
    /// wrote and must never be written back.
    /// </summary>
    public bool RecipeUnreadable
    {
        get
        {
            DiskJson();
            return _diskUnreadable;
        }
    }

    /// <summary>
    /// Whether Save would do something useful and safe — what a dock should gate its
    /// button on. The authority is the check inside <see cref="SaveRecipe"/>; this is the
    /// same question asked early enough to grey a button out.
    /// </summary>
    public bool CanSave => !RecipeUnreadable && IsDirty;

    /// <summary>The working copy's canonical text. The unit an undo action snapshots.</summary>
    public string GetRecipeJson() => Recipe.ToJson();

    /// <summary>
    /// Replaces the working copy wholesale — the undo/redo target. A whole-recipe
    /// snapshot rather than a per-field edit because a recipe is a couple of kilobytes
    /// and a snapshot cannot desync from what it is describing; a stroke's worth of undo
    /// costs less than the map it rebuilds.
    /// </summary>
    public void SetRecipeJson(string json)
    {
        MapRecipe parsed;
        try
        {
            parsed = MapRecipe.Parse(json, MapRoot.EditedRecipe);
        }
        catch (Exception e)
        {
            // An undo step that cannot be parsed is a bug in whoever wrote it, not a
            // reason to leave the stage holding a half-applied recipe.
            LastError = e.Message;
            GD.PushError($"MapStage could not apply a recipe snapshot: {e}");
            return;
        }

        // An undo can outlive the map it was recorded on: the action lives in the scene's
        // history, and the scene is this stage whatever it happens to be showing. Follow
        // the snapshot back to its map rather than writing farm placements into the town.
        // The field, not the property — the setter would rebuild, and so does this.
        _stageMapId = parsed.MapId;
        _recipe = parsed;
        _recipeMapId = parsed.MapId;
        _recipeStamp++;
        StoreWorkingRecipe(parsed);
        if (CanRestage)
        {
            Rebuild();
        }
    }

    /// <summary>
    /// "I have mutated the records <see cref="Recipe"/> handed me." Writes the working
    /// copy through to the reload-proof metadata and rebuilds the preview. Called once
    /// per changed tile during a drag — a rebuild of the largest map is a few
    /// milliseconds, which is what makes drag-to-place honest rather than a wireframe
    /// that lies until you let go.
    /// </summary>
    public void CommitRecipeEdit()
    {
        StoreWorkingRecipe(Recipe);
        Rebuild();
    }

    /// <summary>
    /// Writes the working copy to <see cref="RecipePath"/> through the canonical writer,
    /// then drops the metadata — clean is the absence of an override, not a flag saying
    /// so. The only path in this tool that touches disk.
    /// </summary>
    public void SaveRecipe()
    {
        // The one data-loss path this tool has, and it is completely silent without this
        // guard. When the file fails to parse, ReadWorkingRecipe falls back to an EMPTY
        // recipe; the map then falls back to its code defaults, so the viewport looks
        // exactly right; and DiskJson returns "", so the dirty check says "dirty" and the
        // Save button lights up. Pressing it would replace a map somebody authored with
        // "placements": []. Refuse, and name the file to fix.
        DiskJson();   // refresh _diskUnreadable / RecipeError against the file as it is NOW
        if (_diskUnreadable)
        {
            LastError = $"Refusing to save over '{RecipePath}': it could not be read ({RecipeError}). "
                + "Fix or delete that file, then press Revert.";
            GD.PushError($"MapStage: {LastError}");
            return;
        }

        try
        {
            MapRecipeFile.WriteTo(Recipe, RecipePath);
            RemoveWorkingRecipe(StageMapId);
            LastError = "";
            RecipeError = "";
            Notice = "";
        }
        catch (Exception e)
        {
            LastError = e.Message;
            GD.PushError($"MapStage could not save '{RecipePath}': {e}");
        }
    }

    /// <summary>Throws the working copy away and re-reads the file. The undo of last resort.</summary>
    public void RevertRecipe()
    {
        RemoveWorkingRecipe(StageMapId);
        _recipe = null;          // forces the getter back to disk
        _recipeMapId = "";
        Notice = "";
        if (CanRestage)
        {
            Rebuild();
        }
    }

    // The file as canonical text, or "" when it cannot be read — which is never equal to
    // a working copy, so a broken file always reads as dirty rather than as agreement.
    private string DiskJson()
    {
        try
        {
            RecipeError = "";
            _diskUnreadable = false;
            return MapRecipeFile.Load(StageMapId).ToJson();
        }
        catch (Exception e)
        {
            RecipeError = e.Message;
            _diskUnreadable = true;
            return "";
        }
    }

    private MapRecipe ReadWorkingRecipe(string mapId)
    {
        var key = new StringName(RecipeMetaPrefix + mapId);
        if (HasMeta(key))
        {
            try
            {
                return MapRecipe.Parse(GetMeta(key).AsString(), MapRoot.EditedRecipe);
            }
            catch (Exception e)
            {
                // Unreachable short of someone hand-editing the .tscn: the only writer is
                // the canonical one. Drop it rather than wedge the stage on it.
                GD.PushWarning($"MapStage discarded an unreadable working recipe: {e.Message}");
                RemoveMeta(key);
            }
        }

        try
        {
            RecipeError = "";
            _diskUnreadable = false;
            return MapRecipeFile.Load(mapId);
        }
        catch (Exception e)
        {
            // A broken FILE must not stop the stage building — the preview falls back to
            // the map's own code defaults and the dock says why. The flag is what stops
            // that fallback being mistaken for something someone authored; see SaveRecipe.
            RecipeError = e.Message;
            _diskUnreadable = true;
            return new MapRecipe(mapId);
        }
    }

    private void StoreWorkingRecipe(MapRecipe recipe)
    {
        string json = recipe.ToJson();
        if (json == DiskJson())
        {
            RemoveWorkingRecipe(recipe.MapId);
            return;
        }
        SetMeta(new StringName(RecipeMetaPrefix + recipe.MapId), json);
    }

    private void RemoveWorkingRecipe(string mapId)
    {
        var key = new StringName(RecipeMetaPrefix + mapId);
        if (HasMeta(key))
        {
            RemoveMeta(key);
        }
    }

    public override void _Notification(int what)
    {
        // Metadata is serialised into a .tscn, and an unsaved working recipe has no
        // business in one — the scene is a two-line node skeleton and should stay that
        // way. Stashed across the write rather than dropped, or Ctrl+S would silently
        // cost someone their drag.
        if (what == NotificationEditorPreSave)
        {
            // EVERY map's working recipe, not just the staged one. Switching maps with a
            // drag unsaved leaves that map's metadata behind by design (it is how the drag
            // survives coming back), so keying this on StageMapId would serialise every
            // OTHER map's unsaved work into the scene file — and stay silent about it,
            // because the warning below is keyed on the same lookup.
            _preSaveStash.Clear();
            foreach (StringName key in GetMetaList())
            {
                if (((string)key).StartsWith(RecipeMetaPrefix, StringComparison.Ordinal))
                {
                    _preSaveStash[key] = GetMeta(key).AsString();
                }
            }

            foreach (StringName key in _preSaveStash.Keys)
            {
                RemoveMeta(key);
            }

            if (_preSaveStash.Count > 0)
            {
                // The trap: the editor's "saved" feedback is about the SCENE. Say so
                // where someone who just pressed Ctrl+S is looking, and again in Output.
                string what_ = _preSaveStash.Count == 1
                    ? $"'{RecipePath}' still has"
                    : $"{_preSaveStash.Count} map recipes still have";
                Notice = $"Ctrl+S saved the scene — {what_} unsaved placements.";
                GD.PushWarning($"MapStage: the scene was saved, the map recipe was NOT. {Notice}");
            }
        }
        else if (what == NotificationEditorPostSave && _preSaveStash.Count > 0)
        {
            foreach ((StringName key, string json) in _preSaveStash)
            {
                SetMeta(key, json);
            }
            _preSaveStash.Clear();
        }
    }

    public override void _Ready()
    {
        // The ONLY editor branch in src/. Everything under src/World renders in the
        // editor unmodified — this scene is what has no business in a running game.
        if (!Engine.IsEditorHint())
        {
            QueueFree();
            return;
        }

        // scenes/Main.tscn's World node, mirrored: MapRoot enables its own Y-sort, but it
        // only merges upward if the parent sorts too.
        YSortEnabled = true;
        Rebuild();
    }

    /// <summary>
    /// Rebuilds the preview from scratch — sim stub, tint, map. Public so an editor
    /// plugin can call it after a placement edit; a full rebuild of the largest map
    /// measures a few milliseconds, which is comfortably rebuild-per-drag.
    /// </summary>
    public void Rebuild()
    {
        LastError = "";
        try
        {
            // The stub is built BEFORE the teardown, and the order is the bug it prevents.
            // An assembly reload clears every Instance static while leaving the nodes
            // standing, so tearing down first means the old map's and tint's _ExitTree
            // reach through a null WorldSim.Instance / Clock.Instance on the way out —
            // four NullReferenceExceptions in the Output panel on every `dotnet build`,
            // and they escape through the engine's own call bridge where FreeSafely's
            // catch cannot see them. Building first means those statics always point at
            // something: the dying nodes unsubscribe from the NEW singletons, which every
            // one of those teardowns treats as a no-op (a Remove of an absent map, a -=
            // for a handler that was never added), and are then freed.

            // project.godot's autoload order, and it is load-bearing: WorldSim._EnterTree
            // reads Clock.Instance and SaveService.Instance, and each Instance is set in
            // its own _EnterTree — i.e. at the AddChild call above it.
            var stub = new Node { Name = "SimStub" };
            AddChild(stub);
            Disown(stub);
            stub.AddChild(new GameState { Name = nameof(GameState) });
            stub.AddChild(new Clock { Name = nameof(Clock) });
            stub.AddChild(new SaveService { Name = nameof(SaveService) });
            stub.AddChild(new WorldSim { Name = nameof(WorldSim) });

            Teardown(keep: stub);

            // Before NewGame, never after: the stage must not be able to name the real slot.
            SaveService.DefaultSlot = EditorSlot;
            SaveService.Instance.NewGame();

            // A phase with the clock and the player frozen, or Clock._Process free-runs
            // the preview's time while the editor sits idle. Never Paused: that phase is
            // the one that sets GetTree().Paused, and GetTree() here is the EDITOR's tree.
            GameState.Instance.TransitionTo(GameState.Phase.Cutscene);
            if (GameState.Instance.ClockRuns || GameState.Instance.PlayerHasControl)
            {
                throw new InvalidOperationException(
                    "Stage phase still runs the clock or the player; pick one that freezes both.");
            }

            if (!MapRegistry.Contains(StageMapId))
            {
                throw new ArgumentException(
                    $"Unknown map id '{StageMapId}'. Known ids: {string.Join(", ", MapIds.All)}.");
            }

            // scenes/Main.tscn's World/Lighting node, which belongs to no map: without one
            // the preview renders untinted and every lantern's glow punches through nothing.
            var tint = new DayNightTint { Name = "Lighting" };
            AddChild(tint);
            Disown(tint);

            // Main.LoadMap step for step (src/Main.cs:64-75). The last two lines are the
            // ones easily dropped: SetMap picks the interior/exterior key, and without
            // CompleteTravel no NpcView is ever staged.
            MapRoot map = MapRegistry.Create(StageMapId);
            // BEFORE AddChild, because the map reads its placements in _Ready and _Ready
            // runs on the way into the tree. This is what makes the preview show the drag
            // that is not on disk yet — the stage's working copy IS the map's recipe.
            map.RecipeOverride = Recipe;
            AddChild(map);
            Disown(map);
            map.ApplyState(SaveService.Instance.Current.GetMap(map.MapId));
            tint.SetMap(map);
            WorldSim.Instance.CompleteTravel(map.MapId);

            // Idle cost: GlowLight._Process and LampPost._Process poll the clock every
            // frame, twelve of them in town alone, and a still preview has no business
            // burning that in an editor that is otherwise doing nothing. Both apply
            // themselves in _Ready, so the build is already correct; ApplyTime is what
            // hands the subtree its frames back.
            map.ProcessMode = ProcessModeEnum.Disabled;

            // Godot's managed->native call bridge SWALLOWS an exception thrown out of
            // _Ready, so the catch around this method never sees a map that died halfway
            // through building itself — it just returns a node with nothing in it and the
            // dock cheerfully reports success over an empty viewport. Assert the
            // post-condition instead: every shipped map paints a Ground layer.
            if (map.Ground is not { } ground || ground.GetUsedRect().Size == Vector2I.Zero)
            {
                LastError = $"'{StageMapId}' finished building with no Ground layer — an exception "
                    + "was almost certainly thrown out of _Ready. Check the Output panel.";
                GD.PushError($"MapStage: {LastError}");
                return;
            }

            ApplyTime();
        }
        catch (Exception e)
        {
            LastError = e.Message;
            GD.PushError($"MapStage rebuild failed for '{StageMapId}': {e}");
        }
    }

    public override void _Process(double delta)
    {
        if (_settleFrames <= 0 || --_settleFrames > 0)
        {
            return;
        }
        if (Map is { } map)
        {
            map.ProcessMode = ProcessModeEnum.Disabled;
        }
    }

    /// <summary>
    /// Re-times the preview in place. The clock is STEPPED, never set: Clock.SetTime
    /// fires no events at all, so a set would leave the tint, the store facade and the
    /// NPC staging on the old time while the lanterns — which poll — quietly disagreed
    /// with them. Stepping from the start of the day is also how a scrub goes backwards;
    /// the clock itself only ever moves forward.
    /// </summary>
    private void ApplyTime()
    {
        MapRoot? map = Map;
        if (map == null)
        {
            return;   // nothing built (or the build failed); Rebuild applies the time itself
        }

        try
        {
            Clock.Instance.SetTime(GameTime.StartOfDay(Clock.Instance.Now.DayIndex));
            Clock.Instance.AdvanceMinutes(MinuteOfDay);

            // A scrub to 0 steps nothing and so fires nothing, which would leave the tint
            // showing the previous scrub's colour. SetMap re-applies it unconditionally.
            foreach (Node child in GetChildren())
            {
                if (child is DayNightTint tint)
                {
                    tint.SetMap(map);
                }
            }

            map.ProcessMode = ProcessModeEnum.Inherit;
            _settleFrames = SettleFrames;
        }
        catch (Exception e)
        {
            LastError = e.Message;
            GD.PushError($"MapStage re-time failed: {e}");
        }
    }

    /// <summary>
    /// Frees whatever the last build left behind, found BY TYPE — never by reading
    /// fields. Every `dotnet build` with the editor open reloads the assembly, which
    /// clears statics and managed fields without freeing the nodes they pointed at, and
    /// a second live WorldSim under this node would silently double-fire every
    /// ApplyState. Free, not QueueFree, so the build afterwards is not racing a deletion
    /// scheduled for the end of the frame.
    /// </summary>
    /// <param name="keep">The stub built for THIS rebuild, which is not debris.</param>
    private void Teardown(Node keep)
    {
        // Two passes, and the order is the bug they prevent: MapRoot._ExitTree calls
        // WorldSim.Instance.UnregisterMap and DayNightTint._ExitTree unsubscribes from
        // Clock.Instance, so a stub holding both has to outlive both.
        foreach (Node child in GetChildren())
        {
            if (child is MapRoot or CanvasModulate)
            {
                FreeSafely(child);
            }
        }
        foreach (Node child in GetChildren())
        {
            if (child != keep && IsSimStub(child))
            {
                FreeSafely(child);
            }
        }
    }

    // The stub is identified by what it carries rather than by its name, so a leftover
    // from a renamed or half-built stage is still swept.
    private static bool IsSimStub(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is GameState or Clock or SaveService or WorldSim)
            {
                return true;
            }
        }
        return false;
    }

    // Debris from before an assembly reload can throw its way out of _ExitTree (the
    // statics its teardown reaches for are gone). It must not be able to block a rebuild.
    private static void FreeSafely(Node node)
    {
        try
        {
            node.Free();
        }
        catch (Exception e)
        {
            GD.PushWarning($"MapStage teardown could not free a leftover node: {e.Message}");
        }
    }

    // Godot serializes only nodes whose Owner is the scene root, so everything the stage
    // builds is explicitly disowned and none of it can end up inside MapStage.tscn.
    // Deeper nodes are safe already — nothing under a MapRoot sets an owner.
    private static void Disown(Node node) => node.Owner = null;
}

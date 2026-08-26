#if TOOLS
using Godot;
using TheHaunt.Core;
using TheHaunt.EditorTools;

namespace TheHaunt.Addons.HauntMapper;

/// <summary>
/// The mapper's dock: what map, what hour, which overlays, what is selected, and — the
/// part that matters — whether the recipe on disk still agrees with what is on screen.
///
/// It holds NO reference to the stage. <see cref="Refresh"/> is handed one for the length
/// of a call and reads it there, because a Control that caches an engine object is a
/// Control holding a dangling pointer the first time someone runs `dotnet build` with the
/// editor open. It also decides nothing: the plugin reads the widgets and acts.
///
/// EVERYTHING HERE IS BUILT TO SURVIVE AN ASSEMBLY RELOAD, and both rules cost something,
/// so both are worth stating:
///
///   No lambdas on a Godot signal. A `+=` lambda captures `this` in a compiler-generated
///   closure, and Godot's reload serialises that closure and restores it by setting its
///   fields through reflection — against a node whose C# type has not been rebuilt yet.
///   It throws ("Object of type 'Godot.VBoxContainer' cannot be converted to
///   'MapperDock'") and the connection is simply gone: a checkbox that no longer does
///   anything, with no error at the time you click it. Method groups take a different
///   path in the deserialiser and come back.
///
///   No widget cached in a plain C# field either — those fields are nulled by the reload
///   while the nodes they pointed at go on living. Each accessor below re-finds its node
///   by NAME (engine-side, so it survives) and only then caches.
///
/// The same reasoning is why the dock's outward state is READ rather than pushed: a plain
/// C# event is not serialised at all, so a plugin subscribed to one across a reload is a
/// plugin quietly talking to nobody.
/// </summary>
public partial class MapperDock : VBoxContainer
{
    /// <summary>The dock's node name. The plugin re-finds it by this after a reload.</summary>
    public const string DockName = "HauntMapperDock";

    /// <summary>What <see cref="TakeRequest"/> returns when Save was pressed.</summary>
    public const string SaveRequest = "save";

    /// <summary>What <see cref="TakeRequest"/> returns when Revert was pressed.</summary>
    public const string RevertRequest = "revert";

    // A button press is an EDGE, and an edge cannot be polled off a widget. It is latched
    // into node metadata rather than a field for the reason everything else here is:
    // metadata is engine-side, so a press does not evaporate if the assembly reloads in
    // the quarter-second before the plugin reads it.
    private const string RequestMeta = "haunt_mapper_request";

    private const string MapPickerName = "MapPicker";
    private const string TimeSliderName = "TimeSlider";
    private const string ClockName = "ClockLabel";
    private const string SelectionName = "SelectionLabel";
    private const string HoverName = "HoverLabel";
    private const string RecipeName = "RecipeLabel";
    private const string DirtyName = "DirtyLabel";
    private const string StatusName = "StatusLabel";
    private const string SaveName = "SaveButton";
    private const string RevertName = "RevertButton";

    // Toggle node name -> the layer it stands for. Named nodes rather than a list of
    // (CheckBox, flag) pairs so the overlay set is derived from ENGINE state: after a
    // reload the boxes still show what the user ticked, and Layers still agrees with them.
    private static readonly (string Node, string Text, OverlayLayers Layer)[] Toggles =
    {
        ("ToggleGrid", "Grid", OverlayLayers.Grid),
        ("TogglePlacements", "Placements", OverlayLayers.Placements),
        ("ToggleReserved", "Reserved", OverlayLayers.Reserved),
        ("ToggleBlockers", "Blockers", OverlayLayers.Blockers),
        ("ToggleNpcSlots", "NPC slots", OverlayLayers.NpcSlots),
    };

    private OptionButton? _maps;
    private HSlider? _time;
    private Label? _clock;
    private Label? _selection;
    private Label? _hover;
    private Label? _recipe;
    private Label? _dirty;
    private Label? _status;
    private Button? _save;
    private Button? _revert;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(DockWidth, 0f);
        AddThemeConstantOverride("separation", 4);

        // A reload does not re-run _Ready — the node stays in the tree and only its
        // managed half is replaced. This is here for the case where something else
        // re-readies it, because two of every widget is worse than none.
        if (GetChildCount() == 0)
        {
            BuildControls();
        }
        Refresh(null, "", null);
    }

    // ------------------------------------------------------------------
    // What the plugin reads
    // ------------------------------------------------------------------

    /// <summary>The map the dropdown is on, or "" before it has been built.</summary>
    public string SelectedMapId
    {
        get
        {
            OptionButton? maps = MapPicker;
            int index = maps?.Selected ?? -1;
            return index >= 0 && index < MapIds.All.Count ? MapIds.All[index] : "";
        }
    }

    /// <summary>Minute of day the slider is on, 0 = 6:00 AM.</summary>
    public int SelectedMinute => (int)(TimeSlider?.Value ?? 0);

    /// <summary>Which overlays are ticked, read straight off the boxes.</summary>
    public OverlayLayers Layers
    {
        get
        {
            var layers = OverlayLayers.None;
            foreach ((string node, _, OverlayLayers layer) in Toggles)
            {
                if (Find<CheckBox>(node) is { ButtonPressed: true })
                {
                    layers |= layer;
                }
            }
            return layers;
        }
    }

    /// <summary>The pending button press, consumed on read. "" when there is none.</summary>
    public string TakeRequest()
    {
        var key = new StringName(RequestMeta);
        if (!HasMeta(key))
        {
            return "";
        }
        string request = GetMeta(key).AsString();
        RemoveMeta(key);
        return request;
    }

    // ------------------------------------------------------------------
    // What the plugin writes
    // ------------------------------------------------------------------

    /// <summary>
    /// Redraws every readout from the stage. Called on a throttle by the plugin rather
    /// than from a change signal, so the dock is also right about the things nothing
    /// tells it — a recipe edited on disk, an error raised by a rebuild it did not ask for.
    /// </summary>
    public void Refresh(MapStage? stage, string selection, Vector2I? hover)
    {
        if (StatusLabel == null)
        {
            return;   // in the dock but not readied yet
        }

        bool live = stage != null && GodotObject.IsInstanceValid(stage);
        Enable(MapPicker, live);
        if (TimeSlider is { } slider)
        {
            slider.Editable = live;
        }

        if (!live)
        {
            Set(SelectionLabel, "Selection:  —");
            Set(HoverLabel, "Tile:  —");
            Set(RecipeLabel, "Recipe:  —");
            SetDirty(false, "");
            Enable(SaveButton, false);
            Enable(RevertButton, false);
            SetStatus("Select a MapStage node to edit a map.", Warn);
            return;
        }

        SelectMap(stage!.StageMapId);
        if (TimeSlider is { } time)
        {
            time.SetValueNoSignal(stage.MinuteOfDay);
        }
        Set(ClockLabel, new GameTime(stage.MinuteOfDay).ToClockString());

        Set(SelectionLabel, $"Selection:  {(selection.Length > 0 ? selection : "—")}");
        Set(HoverLabel, hover is { } cell ? $"Tile:  ({cell.X}, {cell.Y})" : "Tile:  —");
        Set(RecipeLabel, $"Recipe:  {stage.RecipePath}");

        // Unreadable is asked FIRST and separately from dirty, because the two look
        // identical from here and mean opposite things. A file that will not parse leaves
        // the working copy as an empty fallback, which reads as "dirty" — and saving that
        // would overwrite the map. SaveRecipe refuses outright; greying the button is how
        // that refusal stops being a surprise.
        bool unreadable = stage.RecipeUnreadable;
        bool dirty = stage.IsDirty;

        SetDirty(dirty && !unreadable, unreadable
            ? "UNREADABLE  —  fix the file on disk, then Revert"
            : dirty
                ? "UNSAVED  —  the map on disk is behind this one"
                : "saved  —  disk matches the preview");
        Enable(SaveButton, dirty && !unreadable);
        // Revert stays live: re-reading is exactly how someone recovers once they have
        // repaired the file by hand.
        Enable(RevertButton, dirty);

        SetStatus(StatusFor(stage, dirty), StatusColorFor(stage, dirty));
    }

    // Priority is worst-first: an exception beats a warning beats a note. One line only —
    // three stacked messages is a log, and nobody reads a log in a dock.
    private static string StatusFor(MapStage stage, bool dirty)
    {
        if (stage.LastError.Length > 0)
        {
            return $"BUILD FAILED: {stage.LastError}";
        }
        if (stage.RecipeError.Length > 0)
        {
            return $"RECIPE UNREADABLE: {stage.RecipeError}";
        }
        if (stage.Notice.Length > 0)
        {
            return stage.Notice;
        }
        if (!stage.RecipeExists)
        {
            return "No recipe file: this map's placements are still C# literals, so there "
                + "is nothing here to drag. Seed it with MapRecipeSeeds first.";
        }
        return dirty
            ? "Drag ends commit an undo step. Save writes the recipe; Ctrl+S does not."
            : "Drag a placement to move it. Nothing is written until you press Save.";
    }

    private static Color StatusColorFor(MapStage stage, bool dirty) =>
        stage.LastError.Length > 0 || stage.RecipeError.Length > 0 ? Bad
        : stage.Notice.Length > 0 || !stage.RecipeExists || dirty ? Warn
        : Dim;

    // ------------------------------------------------------------------
    // Handlers — method groups, every one. See the class comment.
    // ------------------------------------------------------------------

    private void OnSavePressed() => SetMeta(new StringName(RequestMeta), SaveRequest);

    private void OnRevertPressed() => SetMeta(new StringName(RequestMeta), RevertRequest);

    // Only the label: the value itself is polled off the slider, so a lost connection
    // costs a quarter-second of stale text and nothing else.
    private void OnTimeChanged(double value) =>
        Set(ClockLabel, new GameTime((int)value).ToClockString());

    // ------------------------------------------------------------------
    // Construction
    // ------------------------------------------------------------------

    private void BuildControls()
    {
        AddChild(Heading("HAUNT MAPPER"));

        var maps = new OptionButton
        {
            Name = MapPickerName,
            TooltipText = "Which map the stage previews.",
        };
        foreach (string mapId in MapIds.All)
        {
            maps.AddItem(mapId);
        }
        AddChild(Row("Map", maps));

        // The slider stops where the day does. A day here runs 6:00 AM -> 2:00 AM and the
        // clock refuses to cross the boundary, so anything past 1190 is dead travel that
        // silently clamps — see MapStage.MinuteOfDay.
        var time = new HSlider
        {
            Name = TimeSliderName,
            MinValue = 0,
            MaxValue = 1190,
            Step = 10,
            Value = 720,
            TooltipText = "Time of day. Re-times the preview in place; it does not rebuild.",
        };
        time.ValueChanged += OnTimeChanged;
        var clockRow = new HBoxContainer();
        time.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        clockRow.AddChild(time);
        clockRow.AddChild(Value(ClockName, "6:00 PM"));
        AddChild(Row("Time", clockRow));

        AddChild(new HSeparator());
        AddChild(Heading("Overlays"));
        var grid = new GridContainer { Columns = 2 };
        foreach ((string node, string text, OverlayLayers layer) in Toggles)
        {
            var box = new CheckBox
            {
                Name = node,
                Text = text,
                ButtonPressed = OverlayLayers.Default.HasFlag(layer),
            };
            grid.AddChild(box);
        }
        AddChild(grid);

        AddChild(new HSeparator());
        AddChild(Value(SelectionName, "Selection:  —"));
        AddChild(Value(HoverName, "Tile:  —"));

        AddChild(new HSeparator());
        AddChild(Note(RecipeName, "Recipe:  —"));
        AddChild(Value(DirtyName, ""));

        var buttons = new HBoxContainer();
        var save = new Button
        {
            Name = SaveName,
            Text = "Save recipe",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "Writes the placements to data/maps/<map>.json through the canonical writer.",
        };
        save.Pressed += OnSavePressed;
        var revert = new Button
        {
            Name = RevertName,
            Text = "Revert",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "Throws the unsaved placements away and re-reads the file.",
        };
        revert.Pressed += OnRevertPressed;
        buttons.AddChild(save);
        buttons.AddChild(revert);
        AddChild(buttons);

        // Both of these are things someone finds out the hard way otherwise, so they are
        // printed rather than left to a tooltip nobody hovers.
        AddChild(Note("CtrlSNote", "Ctrl+S saves the SCENE, not the map. Placements are "
            + "only written by Save recipe above."));
        AddChild(Note("CloseNote", "Unsaved placements survive a `dotnet build` assembly "
            + "reload, but NOT closing the editor. Save before you quit."));

        AddChild(new HSeparator());
        AddChild(Note(StatusName, ""));
    }

    private void SelectMap(string mapId)
    {
        int index = MapIds.All.ToList().IndexOf(mapId);
        // A stage pointed at a map id nothing knows leaves the dropdown alone rather than
        // lying about which map is on screen; the build error says the rest.
        if (index >= 0 && MapPicker is { } maps && maps.Selected != index)
        {
            maps.Select(index);
        }
    }

    private void SetDirty(bool dirty, string text)
    {
        if (DirtyLabel is not { } label)
        {
            return;
        }
        label.Text = text.Length > 0 ? (dirty ? $"●  {text}" : $"○  {text}") : "";
        label.AddThemeColorOverride("font_color", dirty ? Warn : Dim);
    }

    private void SetStatus(string text, Color color)
    {
        if (StatusLabel is not { } label)
        {
            return;
        }
        label.Text = text;
        label.AddThemeColorOverride("font_color", color);
    }

    private static void Set(Label? label, string text)
    {
        if (label != null)
        {
            label.Text = text;
        }
    }

    private static void Enable(BaseButton? button, bool enabled)
    {
        if (button != null)
        {
            button.Disabled = !enabled;
        }
    }

    // ------------------------------------------------------------------
    // Widget lookup: by name first, cached second
    // ------------------------------------------------------------------

    private OptionButton? MapPicker => Find<OptionButton>(MapPickerName, ref _maps);
    private HSlider? TimeSlider => Find<HSlider>(TimeSliderName, ref _time);
    private Label? ClockLabel => Find<Label>(ClockName, ref _clock);
    private Label? SelectionLabel => Find<Label>(SelectionName, ref _selection);
    private Label? HoverLabel => Find<Label>(HoverName, ref _hover);
    private Label? RecipeLabel => Find<Label>(RecipeName, ref _recipe);
    private Label? DirtyLabel => Find<Label>(DirtyName, ref _dirty);
    private Label? StatusLabel => Find<Label>(StatusName, ref _status);
    private Button? SaveButton => Find<Button>(SaveName, ref _save);
    private Button? RevertButton => Find<Button>(RevertName, ref _revert);

    // The cache is an optimisation; the NAME is the truth. A reload nulls the field and
    // leaves the node, so the next read simply finds it again.
    private T? Find<T>(string name, ref T? cached) where T : Node
    {
        if (cached != null && GodotObject.IsInstanceValid(cached))
        {
            return cached;
        }
        cached = Find<T>(name);
        return cached;
    }

    // owned: false is load-bearing — these nodes were made in code and have no owner, so
    // the default owned-only search finds precisely nothing.
    private T? Find<T>(string name) where T : Node =>
        FindChild(name, recursive: true, owned: false) as T;

    private static Label Heading(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.55f));
        return label;
    }

    private static Label Value(string name, string text) => new() { Name = name, Text = text };

    // Wrapping labels carry an explicit minimum width. Without one a Label with autowrap
    // reports a minimum width of about one character and then computes its minimum HEIGHT
    // at that width — measured at 2285px for a two-line note, which inside a
    // ScrollContainer becomes a dock you scroll for a minute to reach the bottom of.
    private static Label Note(string name, string text)
    {
        var label = new Label
        {
            Name = name,
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(DockWidth - 16f, 0f),
        };
        label.AddThemeColorOverride("font_color", Dim);
        return label;
    }

    private static HBoxContainer Row(string label, Control control)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(44f, 0f) });
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(control);
        return row;
    }

    private const float DockWidth = 240f;

    private static readonly Color Dim = new(0.72f, 0.72f, 0.76f);
    private static readonly Color Warn = new(1f, 0.78f, 0.35f);
    private static readonly Color Bad = new(1f, 0.45f, 0.4f);
}
#endif

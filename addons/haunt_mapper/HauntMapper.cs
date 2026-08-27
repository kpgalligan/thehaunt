#if TOOLS
using Godot;
using TheHaunt.EditorTools;
using TheHaunt.World;

namespace TheHaunt.Addons.HauntMapper;

/// <summary>
/// The placement editor. Drag a tree in the Godot viewport, press Save, run the game,
/// and the tree is where you put it.
///
/// Every map in this game is a C# build function, and it stays one — what moved into data
/// is the function's INPUT (data/maps/(map).json), so terrain painting, the Act II/III
/// variant swap and the scatter hash all keep working. This plugin edits that input, and
/// nothing else: it moves records inside a <see cref="MapStage"/>'s working recipe and
/// asks the stage to rebuild, which takes a few milliseconds, which is why a drag can
/// show you the real map rather than an outline that lies until you let go.
///
/// Ownership, in one line each:
///   MapStage      owns the working recipe and the preview. The only thing that writes disk.
///   MapperDock    owns the widgets, holds no engine object, decides nothing.
///   this          owns canvas input, the selection, and the undo actions.
///
/// RELOAD SAFETY is the whole reason it is shaped this way. Every `dotnet build` with the
/// editor open swaps the managed objects out from under the still-living Nodes: managed
/// fields go null, statics reset, and anything cached across the boundary is a dangling
/// reference waiting to be dereferenced. So nothing here is built in a constructor,
/// nothing is cached across a frame that a reload could sit in the middle of, and the
/// stage is re-validated (<see cref="Stage"/>) on every single use. The unsaved recipe
/// itself lives in node METADATA, which is engine-side and survives what C# fields do not.
/// </summary>
[Tool]
public partial class HauntMapper : EditorPlugin
{
    /// <summary>How often the dock re-reads the stage. Poll, not push — see <see cref="_Process"/>.</summary>
    private const double RefreshSeconds = 0.25;

    private MapperDock? _dock;

    // Whether the widgets have been shown the current stage yet. Until they have, the
    // dock's state is the LAST stage's and must not be pushed onto this one — selecting a
    // stage would silently re-point it at whatever map the dropdown was left on.
    private bool _adopted;

    // The stage this plugin is editing, as handed over by _Edit. Re-validated on use
    // rather than trusted: _Edit is not called when the node it named is freed.
    private MapStage? _stage;

    // Selection is an INDEX into Recipe.Placements plus the stamp of the recipe it
    // indexed. A drag mutates records in place so the index survives it; an undo re-parses
    // the whole recipe, so the stamp moves and the selection is correctly dropped rather
    // than silently pointing at whatever record inherited the slot.
    private int _selected = -1;
    private int _selectedStamp = -1;

    private int _dragIndex = -1;
    private string _dragBefore = "";
    private Vector2I _dragGrabTile;
    private Vector2I _dragOriginCell;

    private Vector2I? _hover;
    private double _sinceRefresh;

    // What Refresh last wrote into the dock's widgets. See ApplyDock.
    private string _shownMapId = "";
    private int _shownMinute = -1;

    // Last overlay set applied, so a ticked box repaints the viewport exactly once.
    private OverlayLayers _layers = OverlayLayers.Default;

    public override void _EnterTree()
    {
        // Built here, not in a field initialiser or a constructor: a reload runs the
        // constructor of the new managed object while the engine-side plugin is mid-swap,
        // and a dock added from there is a dock the matching _ExitTree never sees.
        var dock = new MapperDock { Name = MapperDock.DockName };

        // A ScrollContainer between the two because this dock says a lot on purpose —
        // what is selected, what is dirty, and the two warnings that are otherwise
        // learned the hard way. A dock slot is short, and silently clipping the line that
        // says "Ctrl+S did not save your map" would be a bad joke.
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        dock.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(dock);

        var host = new EditorDock
        {
            Title = "Haunt Mapper",
            DefaultSlot = EditorDock.DockSlot.RightUl,
        };
        host.AddChild(scroll);
        AddDock(host);
        _dock = dock;
    }

    public override void _ExitTree()
    {
        // Found rather than remembered, because _ExitTree also runs when the plugin is
        // disabled AFTER an assembly reload has nulled the field — and a dock that is
        // never removed comes back doubled the next time the plugin is enabled.
        if (Host() is { } host)
        {
            // Remove THEN free, both of them: one removed but not freed is a leaked
            // subtree with no owner left to free it.
            RemoveDock(host);
            host.QueueFree();
        }
        _dock = null;
        _stage = null;
        _selected = -1;
        _dragIndex = -1;
    }

    // ------------------------------------------------------------------
    // Which object this plugin speaks for
    // ------------------------------------------------------------------

    /// <summary>
    /// The gate on everything below: canvas input and the viewport overlays are only ever
    /// offered to a plugin that handles the CURRENTLY EDITED object. Answering anything
    /// but a MapStage would take the mouse away from whoever is editing a real scene.
    /// </summary>
    public override bool _Handles(GodotObject @object) => @object is MapStage;

    public override void _Edit(GodotObject @object)
    {
        _stage = @object as MapStage;
        _adopted = false;
        ClearSelection();
        CancelDrag();
        _hover = null;
        _sinceRefresh = RefreshSeconds;   // refresh on the next frame, not in 250 ms
        UpdateOverlays();
    }

    public override void _MakeVisible(bool visible)
    {
        if (!visible)
        {
            // The 2D screen went away mid-drag (a tab switch, a scene change). Leave the
            // recipe where the drag put it and just stop tracking the mouse — a half-
            // finished drag that keeps listening is how a placement ends up under a
            // click aimed at something else entirely.
            CancelDrag();
            _hover = null;
        }
    }

    // ------------------------------------------------------------------
    // Dragging
    // ------------------------------------------------------------------

    public override bool _ForwardCanvasGuiInput(InputEvent @event)
    {
        MapStage? stage = Stage();
        if (stage == null)
        {
            return false;
        }

        // Screen -> stage-local. The stage's own transform is in there because a MapStage
        // is a Node2D like any other and may have been moved; the viewport transform is
        // the editor's pan and zoom.
        Transform2D toStage =
            (stage.GetViewportTransform() * stage.GetGlobalTransform()).AffineInverse();

        switch (@event)
        {
            case InputEventMouseMotion motion:
                return OnMouseMoved(stage, TileAt(toStage * motion.Position));

            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                Vector2I tile = TileAt(toStage * button.Position);
                return button.Pressed ? OnPressed(stage, tile) : OnReleased(stage);

            // Escape during a drag puts the record back where it started. Cheap to
            // provide, and the alternative — undo, after the fact — is a step someone has
            // to think about while the map is already wrong.
            case InputEventKey { Pressed: true, Keycode: Key.Escape } when _dragIndex >= 0:
                string before = _dragBefore;
                CancelDrag();
                stage.SetRecipeJson(before);
                ClearSelection();
                UpdateOverlays();
                return true;
        }
        return false;
    }

    private bool OnMouseMoved(MapStage stage, Vector2I tile)
    {
        bool moved = _hover != tile;
        _hover = tile;

        if (_dragIndex < 0)
        {
            if (moved)
            {
                UpdateOverlays();   // the hovered-cell outline follows the mouse
            }
            return false;           // plain hover is the editor's, not ours
        }

        if (Dragged(stage) is not { } placement)
        {
            CancelDrag();
            return false;
        }

        // Delta from the grab point, not the cursor's tile: grabbing a tree by its canopy
        // and having it teleport so the cursor lands on its anchor is the single most
        // disorienting thing a tile editor can do.
        Vector2I want = _dragOriginCell + (tile - _dragGrabTile);

        // Clamp to the painted map. Dragging past the edge otherwise walks a record out to
        // a coordinate nothing paints, where it vanishes from the viewport with no error
        // and no way to grab it back — the placement is still in the recipe, just
        // unreachable. Sliding along the boundary is the behaviour every tile editor has.
        if (stage.Map?.Ground is { } ground)
        {
            Rect2I used = ground.GetUsedRect();
            want = new Vector2I(
                Math.Clamp(want.X, used.Position.X, used.End.X - 1),
                Math.Clamp(want.Y, used.Position.Y, used.End.Y - 1));
        }

        if (placement.Cell != want)
        {
            placement.X = want.X;
            placement.Y = want.Y;
            stage.CommitRecipeEdit();
            UpdateOverlays();
        }
        return true;   // consumed, or the editor rubber-band-selects underneath the drag
    }

    private bool OnPressed(MapStage stage, Vector2I tile)
    {
        int hit = PlacementHit.At(stage.Recipe, tile);
        if (hit < 0)
        {
            ClearSelection();
            UpdateOverlays();
            return false;   // nothing of ours here: let the editor have the click
        }

        _selected = hit;
        _selectedStamp = stage.RecipeStamp;
        _dragIndex = hit;
        // The whole recipe, before the stroke. A snapshot per stroke rather than a
        // per-field edit: a recipe is a couple of kilobytes, and a snapshot cannot desync
        // from the thing it describes the way a replayed delta can.
        _dragBefore = stage.GetRecipeJson();
        _dragGrabTile = tile;
        _dragOriginCell = stage.Recipe.Placements[hit].Cell;
        UpdateOverlays();
        return true;
    }

    private bool OnReleased(MapStage stage)
    {
        if (Dragged(stage) is not { } placement)
        {
            CancelDrag();
            return false;
        }

        string after = stage.GetRecipeJson();
        string before = _dragBefore;
        string label = $"Move {placement.Kind} '{placement.Id}'";
        CancelDrag();

        if (after != before)
        {
            // CommitAction(false) — do NOT run the do-method now. The drag already applied
            // it live, tile by tile; executing it again would rebuild the map for nothing
            // and, worse, replace the recipe object and drop the selection the user is
            // still looking at.
            EditorUndoRedoManager undo = GetUndoRedo();
            undo.CreateAction(label);
            undo.AddDoMethod(stage, MapStage.MethodName.SetRecipeJson, after);
            undo.AddUndoMethod(stage, MapStage.MethodName.SetRecipeJson, before);
            undo.CommitAction(execute: false);
        }
        UpdateOverlays();
        return true;
    }

    private static Vector2I TileAt(Vector2 stageLocal) => new(
        Mathf.FloorToInt(stageLocal.X / MapRoot.TileSize),
        Mathf.FloorToInt(stageLocal.Y / MapRoot.TileSize));

    // ------------------------------------------------------------------
    // Overlays
    // ------------------------------------------------------------------

    public override void _ForwardCanvasDrawOverViewport(Control viewportControl)
    {
        MapStage? stage = Stage();
        if (stage == null)
        {
            return;
        }
        Transform2D toCanvas = stage.GetViewportTransform() * stage.GetGlobalTransform();
        MapperOverlay.Draw(viewportControl, toCanvas, stage, _layers, SelectedIndex(stage), _hover);
    }

    // ------------------------------------------------------------------
    // The dock
    // ------------------------------------------------------------------

    /// <summary>
    /// The dock loop, and it POLLS rather than subscribing — which looks lazy and is not.
    /// A plain C# event is not serialised across an assembly reload, so a plugin that
    /// subscribed to its dock before a `dotnet build` is a plugin talking to nobody
    /// afterwards, with no error to say so; and the dirty flag depends on a FILE, which
    /// raises no event at all when it changes underneath the editor.
    ///
    /// So: read the widgets every frame (cheap, no disk), apply the differences, and
    /// repaint the readouts four times a second (one two-kilobyte file read).
    /// </summary>
    public override void _Process(double delta)
    {
        MapperDock? dock = Dock();
        if (dock == null)
        {
            return;
        }

        MapStage? stage = Stage();
        // Refresh first on a newly selected stage: until the widgets have been shown it,
        // they still hold the LAST stage's map, and applying that would re-point this one.
        if (stage != null && !_adopted)
        {
            _adopted = true;
            _sinceRefresh = 0;
            dock.Refresh(stage, Describe(stage), _hover);
            RememberShown(dock);
            _layers = dock.Layers;
            return;
        }
        if (stage != null)
        {
            ApplyDock(dock, stage);
        }

        _sinceRefresh += delta;
        if (_sinceRefresh < RefreshSeconds)
        {
            return;
        }
        _sinceRefresh = 0;
        dock.Refresh(stage, Describe(stage), _hover);
        RememberShown(dock);
    }

    // The baseline ApplyDock compares against: whatever Refresh just put in the widgets.
    // Anything that differs from this afterwards was typed or dragged by a person.
    private void RememberShown(MapperDock dock)
    {
        _shownMapId = dock.SelectedMapId;
        _shownMinute = dock.SelectedMinute;
    }

    // Every difference between what the dock says and what the stage is — but measured
    // against what Refresh last SHOWED, not against the stage as it is now.
    //
    // The difference matters because this runs every frame and Refresh runs four times a
    // second. Comparing against the stage would mean that for up to 250 ms the dock's
    // widgets hold a stale value that differs from a freshly-inspector-edited stage, and
    // this method would dutifully push the stale value back — so the dock silently wins
    // every argument and MapStage's two [Export]s cannot be driven from the inspector at
    // all. Comparing against the last shown value asks the right question instead: did a
    // HUMAN move this widget since we filled it in?
    private void ApplyDock(MapperDock dock, MapStage stage)
    {
        string mapId = dock.SelectedMapId;
        if (mapId.Length > 0 && mapId != _shownMapId && mapId != stage.StageMapId)
        {
            _shownMapId = mapId;
            ClearSelection();
            CancelDrag();
            stage.StageMapId = mapId;             // the setter rebuilds
            stage.NotifyPropertyListChanged();    // the inspector must not disagree with the dock
            UpdateOverlays();
        }

        int minute = dock.SelectedMinute;
        if (minute != _shownMinute && minute != stage.MinuteOfDay)
        {
            _shownMinute = minute;
            stage.MinuteOfDay = minute;           // the setter re-times in place, no rebuild
            stage.NotifyPropertyListChanged();
        }

        OverlayLayers layers = dock.Layers;
        if (layers != _layers)
        {
            _layers = layers;
            UpdateOverlays();
        }

        switch (dock.TakeRequest())
        {
            case MapperDock.SaveRequest:
                stage.SaveRecipe();
                // res:// just changed under the editor's own feet. Without this the
                // FileSystem dock — and anyone with the recipe open in the built-in text
                // editor — is looking at the version from before the drag.
                EditorInterface.Singleton.GetResourceFilesystem().Scan();
                UpdateOverlays();
                break;

            case MapperDock.RevertRequest:
                ClearSelection();
                CancelDrag();
                stage.RevertRecipe();
                UpdateOverlays();
                break;
        }
    }

    private string Describe(MapStage? stage)
    {
        if (stage == null || SelectedIndex(stage) is var index && index < 0)
        {
            return "";
        }
        MapPlacement placement = stage.Recipe.Placements[index];
        string nudge = placement.NudgeX != 0 || placement.NudgeY != 0
            ? $"  +({placement.NudgeX}, {placement.NudgeY})px"
            : "";
        return $"{placement.Kind} '{placement.Id}'  @ ({placement.X}, {placement.Y}){nudge}";
    }

    // ------------------------------------------------------------------
    // State that must never be trusted without checking
    // ------------------------------------------------------------------

    /// <summary>
    /// The edited stage, or null. IsInstanceValid because _Edit is not called when the
    /// node it named is deleted, and because a reload leaves this field pointing at a
    /// managed wrapper whose engine object may already be gone.
    /// </summary>
    private MapStage? Stage()
    {
        if (_stage != null && GodotObject.IsInstanceValid(_stage))
        {
            return _stage;
        }

        // An assembly reload empties the field WITHOUT re-running _Edit, so without this
        // every `dotnet build` would leave the plugin deaf until the node was clicked
        // again — the exact "needs a restart per C# change" that makes a tool like this
        // not worth opening. The editor's selection is the same question _Edit answers,
        // asked of the engine, which did not forget.
        foreach (Node node in EditorInterface.Singleton.GetSelection().GetSelectedNodes())
        {
            if (node is MapStage stage)
            {
                _stage = stage;
                _adopted = false;   // show it to the widgets before reading them back
                return stage;
            }
        }
        return null;
    }

    /// <summary>
    /// The dock, re-found by NAME when the field has been emptied. An assembly reload
    /// does not re-run _EnterTree — the plugin's managed half is replaced under a dock
    /// that never moved — so a field is a cache and the editor's own tree is the truth.
    /// </summary>
    private MapperDock? Dock()
    {
        if (_dock != null && GodotObject.IsInstanceValid(_dock))
        {
            return _dock;
        }
        _dock = FindByName(EditorInterface.Singleton.GetBaseControl(), MapperDock.DockName)
            as MapperDock;
        return _dock;
    }

    // The EditorDock the dock lives inside — what AddDock/RemoveDock deal in. Walked up
    // from the dock rather than remembered, for the same reason.
    private EditorDock? Host()
    {
        for (Node? at = Dock(); at != null; at = at.GetParent())
        {
            if (at is EditorDock host)
            {
                return host;
            }
        }
        return null;
    }

    private static Node? FindByName(Node node, string name)
    {
        if (node.Name == name)
        {
            return node;
        }
        foreach (Node child in node.GetChildren())
        {
            if (FindByName(child, name) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    /// <summary>
    /// The record being dragged, or null if the index no longer addresses one. It always
    /// should — a drag consumes its own input and nothing else edits the list mid-stroke —
    /// but an index into a list that has been rebuilt underneath it is an
    /// IndexOutOfRangeException thrown out of a mouse handler, which in the editor means
    /// a stack trace instead of a placement.
    /// </summary>
    private MapPlacement? Dragged(MapStage stage) =>
        _dragIndex >= 0 && _dragIndex < stage.Recipe.Placements.Count
            ? stage.Recipe.Placements[_dragIndex]
            : null;

    // -1 unless the selection still indexes the recipe it was taken from.
    private int SelectedIndex(MapStage stage) =>
        _selected >= 0 && _selectedStamp == stage.RecipeStamp
            && _selected < stage.Recipe.Placements.Count
            ? _selected
            : -1;

    private void ClearSelection()
    {
        _selected = -1;
        _selectedStamp = -1;
    }

    private void CancelDrag()
    {
        _dragIndex = -1;
        _dragBefore = "";
    }
}
#endif

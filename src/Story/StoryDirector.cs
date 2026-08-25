using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.Story;

/// <summary>
/// Story orchestration: a plain Node child of Main, NOT an autoload — it dies with Main,
/// so headless tests that never boot Main never evaluate beats. It never writes the model:
/// flags flow through <see cref="WorldSim.SetStoryFlag"/> and dialogue terminal SetsFlag.
/// Every trigger source funnels through a deferred check — GameState.StateChanged
/// dispatches synchronously inside TransitionTo (handlers that transition would nest),
/// and ClockModel.Accumulate can tick a few more minutes after a synchronous phase
/// change; CallDeferred absorbs both.
/// </summary>
public partial class StoryDirector : Node
{
    private bool _beatRunning;
    private TaskCompletionSource? _dialogueDone;
    private int _loadEpoch; // bumped per AfterLoad; a beat spanning a load must die

    public override void _Ready()
    {
        GameState.Instance.StateChanged += OnStateChanged;
        Clock.Instance.TenMinuteTicked += OnTenMinuteTicked;
        SaveService.Instance.AfterLoad += OnAfterLoad;
        WorldSim.Instance.StoryFlagSet += OnStoryFlagSet;
        WorldSim.Instance.DialogueFinished += OnDialogueFinished;
    }

    public override void _ExitTree()
    {
        GameState.Instance.StateChanged -= OnStateChanged;
        Clock.Instance.TenMinuteTicked -= OnTenMinuteTicked;
        SaveService.Instance.AfterLoad -= OnAfterLoad;
        WorldSim.Instance.StoryFlagSet -= OnStoryFlagSet;
        WorldSim.Instance.DialogueFinished -= OnDialogueFinished;
    }

    private void OnStateChanged(GameState.Phase from, GameState.Phase to) => ScheduleCheck();

    private void OnTenMinuteTicked(GameTime time) => ScheduleCheck();

    private void OnStoryFlagSet(string flagId, long dayStamped) => ScheduleCheck();

    private void OnDialogueFinished(string defId) => _dialogueDone?.TrySetResult();

    // Load mid-beat (test harness): abort cleanly. Cancelling the awaited task lands
    // RunBeat in its catch; no flags are set — the loaded save re-derives any pending
    // beat on the next deferred check and replays it from the top.
    private void OnAfterLoad()
    {
        _loadEpoch++;
        _dialogueDone?.TrySetCanceled();
        _beatRunning = false;
        ScheduleCheck();
    }

    private void ScheduleCheck() => CallDeferred(nameof(CheckTriggers));

    private void CheckTriggers()
    {
        if (_beatRunning || WorldSim.Instance.ActiveDialogue != null) return;
        if (!GameState.Instance.PlayerHasControl) return;
        string mapId = SaveService.Instance.Current.Player.MapId;
        if (!WorldSim.Instance.IsMapActive(mapId)) return;
        StoryBeatId? beat = IntroRules.PendingBeat(SaveService.Instance.Current, Clock.Instance.Now, mapId);
        if (beat == StoryBeatId.CrewArrival)  _ = RunBeat("intro_crew_arrival");
        if (beat == StoryBeatId.TownMeeting)  _ = RunBeat("intro_town_meeting");
    }

    private async Task RunBeat(string dialogueId)
    {
        _beatRunning = true;
        int epoch = _loadEpoch;
        GameState.Instance.TransitionTo(GameState.Phase.Cutscene);   // clock + player frozen; tree NOT paused
        try
        {
            WorldSim.Instance.SyncNpcsNow();                          // staging certain (crew at road / mayor at podium)
            // TCS exists BEFORE the staging await so an AfterLoad abort can cancel it.
            _dialogueDone = new TaskCompletionSource();
            await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);   // one beat of static staging
            if (epoch != _loadEpoch)
                return;   // a load landed during staging: this beat belongs to a discarded world
            WorldSim.Instance.StartDialogue(dialogueId);              // legal from Cutscene via CanStartDialogue
            await _dialogueDone.Task;                                 // completed by the DialogueFinished handler
            // completion flag applied by the session's terminal SetsFlag through SetStoryFlag —
            // already repainted + resynced (crew departs).
        }
        catch (TaskCanceledException)
        {
            // AfterLoad aborted the beat — the documented replay-from-autosave path,
            // not a failure; the finally below restores play.
        }
        catch (Exception e)
        {
            GD.PushError($"Beat '{dialogueId}' failed: {e}");
        }
        finally
        {
            _beatRunning = false;
            GameState.Instance.TransitionTo(GameState.Phase.Playing);
        }
        // The finally's transition triggers another deferred check — idempotent (the
        // completion flag blocks PendingBeat).
    }
}

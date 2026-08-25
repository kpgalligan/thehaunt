using Godot;

namespace TheHaunt.Systems;

/// <summary>
/// Autoload that owns the top-level game phase. Consumers gate behavior through the
/// derived queries (<see cref="ClockRuns"/>, <see cref="PlayerHasControl"/>) rather than
/// comparing <see cref="Current"/> directly.
/// </summary>
public partial class GameState : Node
{
    public enum Phase { Playing, Paused, Dialogue, Cutscene, Sleeping }

    public static GameState Instance { get; private set; } = null!;

    public Phase Current { get; private set; } = Phase.Playing;

    /// <summary>(from, to) — fired after the transition has fully applied.</summary>
    public event Action<Phase, Phase>? StateChanged;

    public bool ClockRuns => Current == Phase.Playing;

    public bool PlayerHasControl => Current == Phase.Playing;

    /// <summary>
    /// Dialogue may start from free play (NPC talk) or from inside a Cutscene beat
    /// (the beat keeps owning the phase exit) — never from Paused/Dialogue/Sleeping.
    /// </summary>
    public bool CanStartDialogue => Current is Phase.Playing or Phase.Cutscene;

    public override void _EnterTree()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
    }

    public void TransitionTo(Phase next)
    {
        if (next == Current)
        {
            return;
        }

        Phase from = Current;
        Current = next;

        // Tree pause is used EXCLUSIVELY for the Paused phase. Dialogue/Cutscene/Sleeping
        // never touch it — the clock stops via ClockRuns and the player via PlayerHasControl.
        GetTree().Paused = next == Phase.Paused;

        StateChanged?.Invoke(from, next);
    }
}

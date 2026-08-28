namespace TheHaunt.Core;

/// <summary>One <see cref="WorkAnimation.Advance"/> step's events.</summary>
/// <param name="Impact">The loop entered the impact frame this step — the caller
/// applies the tile mutation now, not at press time and not at loop end.</param>
/// <param name="Finished">The loop ended this step (cancelled or ran out).</param>
public readonly record struct WorkTick(bool Impact, bool Finished);

/// <summary>
/// The four-frame tool work loop (tools-animations handoff): windup, strike,
/// impact, recover. Pure timing state — no engine types — so the contract that
/// sells the hit (the long impact hold, the mutation firing exactly on entry to
/// frame 2) is testable without a scene tree. The owner constructs one, calls
/// <see cref="Start"/> on the press, and drives <see cref="Advance"/> every
/// physics tick with the live held/moving inputs.
///
/// A press COMMITS one full cycle: a tap is one completed action — Kevin amended
/// the handoff here, dropping its release-before-impact cancel. Holding repeats
/// the loop; a held direction ends it at the cycle boundary; only
/// <see cref="Cancel"/> (the owner losing control mid-swing) drops a cycle.
/// </summary>
public sealed class WorkAnimation
{
    // 90/90/140/90 ms — the impact frame holds longest; that hold is what sells
    // the hit (handoff timing; the review page's even rate is NOT the game feel).
    public static readonly IReadOnlyList<float> FrameSeconds = new[] { 0.09f, 0.09f, 0.14f, 0.09f };
    public const int FrameCount = 4;
    public const int ImpactFrame = 2;

    public bool Active { get; private set; }
    public int Frame { get; private set; }

    private float _elapsed;

    public void Start()
    {
        Active = true;
        Frame = 0;
        _elapsed = 0f;
    }

    public void Cancel()
    {
        Active = false;
        Frame = 0;
        _elapsed = 0f;
    }

    public WorkTick Advance(float delta, bool held, bool moving)
    {
        if (!Active)
        {
            return default;
        }

        bool impact = false;
        _elapsed += delta;
        while (_elapsed >= FrameSeconds[Frame])
        {
            _elapsed -= FrameSeconds[Frame];
            if (Frame < FrameCount - 1)
            {
                Frame++;
                if (Frame == ImpactFrame)
                {
                    impact = true;
                }
            }
            else if (held && !moving)
            {
                Frame = 0; // still held: the loop repeats while the action is held
            }
            else
            {
                Cancel();
                return new WorkTick(impact, Finished: true);
            }
        }
        return new WorkTick(impact, Finished: false);
    }
}

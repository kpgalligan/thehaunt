using Godot;
using TheHaunt.Core;

namespace TheHaunt.Systems;

/// <summary>
/// Autoload that drives the pure <see cref="ClockModel"/> from the frame loop and
/// re-exposes its events 1:1 for node-side consumers.
/// </summary>
public partial class Clock : Node
{
    public static Clock Instance { get; private set; } = null!;

    public ClockModel Model { get; } = new();

    public GameTime Now => Model.Now;

    // Forwarded 1:1 from Model. The Model shares this node's lifetime.
    public event Action<GameTime>? MinuteTicked;
    public event Action<GameTime>? TenMinuteTicked;
    public event Action<GameTime>? HourTicked;
    public event Action<GameTime>? DayEnded;
    public event Action<GameTime>? DayStarted;

    public override void _EnterTree()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;

        Model.MinuteTicked += OnMinuteTicked;
        Model.TenMinuteTicked += OnTenMinuteTicked;
        Model.HourTicked += OnHourTicked;
        Model.DayEnded += OnDayEnded;
        Model.DayStarted += OnDayStarted;
    }

    public override void _ExitTree()
    {
        Model.MinuteTicked -= OnMinuteTicked;
        Model.TenMinuteTicked -= OnTenMinuteTicked;
        Model.HourTicked -= OnHourTicked;
        Model.DayEnded -= OnDayEnded;
        Model.DayStarted -= OnDayStarted;
    }

    public override void _Process(double delta)
    {
        if (GameState.Instance.ClockRuns)
        {
            Model.Accumulate(delta);
        }
    }

    public void AdvanceMinutes(int minutes) => Model.AdvanceMinutes(minutes);

    public void AdvanceToDayStart() => Model.AdvanceToDayStart();

    public void SetTime(GameTime time) => Model.SetTime(time);

    private void OnMinuteTicked(GameTime time) => MinuteTicked?.Invoke(time);
    private void OnTenMinuteTicked(GameTime time) => TenMinuteTicked?.Invoke(time);
    private void OnHourTicked(GameTime time) => HourTicked?.Invoke(time);
    private void OnDayEnded(GameTime time) => DayEnded?.Invoke(time);
    private void OnDayStarted(GameTime time) => DayStarted?.Invoke(time);
}

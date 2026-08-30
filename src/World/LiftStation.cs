using Godot;
using TheHaunt.Core;
using TheHaunt.Systems;

namespace TheHaunt.World;

/// <summary>
/// The work surface of one garage lift: an interact area over the lift's three
/// cells. With no car on the lift there is no prompt at all (CanInteract false);
/// with a job in progress E is one work press through the bus
/// (<see cref="WorldSim.WorkOnGarageJob"/> — stamina and progress are the model's
/// business), and a finished car answers with a line instead of more work. An
/// out-of-stamina press also answers with a line — a mashed E that silently does
/// nothing reads as a bug, not a limit (the locked-Door rule).
/// </summary>
public partial class LiftStation : Area2D, IInteractable
{
    private const double LineSeconds = 3.0;

    [Export] public int Lift { get; set; }

    public string PromptText =>
        JobHere() is { Completed: true } ? "Read" : "Work";

    public bool CanInteract(Node2D interactor) =>
        GameState.Instance.PlayerHasControl && !IsQueuedForDeletion() && JobHere() != null;

    public void Interact(Node2D interactor)
    {
        GarageJobRecord? job = JobHere();
        if (job == null)
        {
            return;
        }
        if (job.Completed)
        {
            ShowLine("Done. The owner picks it up tomorrow.");   // [KEVIN] canon restatement
            return;
        }
        if (WorldSim.Instance.WorkOnGarageJob(Lift) == GarageWorkResult.NotEnoughStamina)
        {
            ShowLine("Too tired.");   // [KEVIN]
        }
    }

    private GarageJobRecord? JobHere() =>
        GarageOpsRules.JobAt(SaveService.Instance.Current, Lift);

    public override void _Ready()
    {
        CollisionLayer = 2;
        CollisionMask = 0;
        Monitorable = true;
        AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(48, 16) },
        });
        // No blocker: the map Block()s the lift cells on the Obstacles layer.
    }

    private Label? _line;
    private int _lineToken;   // invalidates stale hide timers, like Sign's

    private void ShowLine(string text)
    {
        _line ??= BuildLine();
        _line.Text = text;
        _line.ResetSize();
        Vector2 scaled = _line.Size * _line.Scale;
        _line.Position = new Vector2(-scaled.X / 2f, -34f - scaled.Y / 2f);
        _line.Show();

        int token = ++_lineToken;
        GetTree().CreateTimer(LineSeconds, processAlways: false).Timeout += () =>
        {
            if (IsInstanceValid(_line) && token == _lineToken)
            {
                _line.Hide();
            }
        };
    }

    private Label BuildLine()
    {
        var label = new Label
        {
            Visible = false,
            Scale = new Vector2(0.5f, 0.5f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 10,   // above the Y-sorted car sprites (Door's line rule)
        };
        AddChild(label);
        return label;
    }
}

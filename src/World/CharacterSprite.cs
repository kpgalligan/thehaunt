using Godot;
using TheHaunt.Core;

namespace TheHaunt.World;

/// <summary>
/// Shared animated character view for the player and NPCs. Owns nothing durable:
/// facing and movement are pushed in by the owner every frame, and the node draws
/// the matching cell of <see cref="CharacterSprites"/>.
///
/// Placed at the character's origin (a tile centre); the 16x32 cell is offset so its
/// bottom row sits on the bottom edge of that tile — one tile of floor, one tile of
/// overhang (art bible §01). The bob on the walk cycle is baked into the frames, so
/// nothing here moves the sprite.
/// </summary>
public partial class CharacterSprite : Node2D
{
    private const float IdleSecondsPerFrame = 1f / 1.5f;   // 1.5fps breath
    private const float WalkSecondsPerFrame = 1f / 8f;     // 8fps contact/pass

    /// <summary>Lifts the 16x32 cell so its feet land on the bottom of the origin tile.</summary>
    public static readonly Vector2 CellOffset = new(0, -CharacterSprites.CellHeight / 2 + 8);

    private Sprite2D? _sprite;
    private Color _tunic = new("4a6ab0");
    private int _facing;
    private bool _moving;
    private bool _riding;
    private float _elapsed;
    private int _frame;

    /// <summary>Set before the node enters the tree; changing it later reloads the sheet.</summary>
    public Color Tunic
    {
        get => _tunic;
        set
        {
            _tunic = value;
            if (_sprite != null)
                _sprite.Texture = CurrentSheet();
        }
    }

    public override void _Ready()
    {
        _sprite = new Sprite2D
        {
            Texture = CurrentSheet(),
            RegionEnabled = true,
            Offset = CellOffset,
        };
        AddChild(_sprite);
        ApplyFrame();
    }

    public override void _Process(double delta)
    {
        // Riding + still holds one frame: the six riding columns are a WHEEL cycle
        // (scooter handoff — the hub spoke advances 60° per column), and a spinning
        // wheel under a stationary scooter is wrong in a way the idle breath is not.
        if (_riding && !_moving)
        {
            ApplyFrame();
            return;
        }

        float secondsPerFrame = _riding
            ? WalkSecondsPerFrame / ScooterRules.SpeedMultiplier
            : _moving ? WalkSecondsPerFrame : IdleSecondsPerFrame;
        int frameCount = _riding ? CharacterSprites.RiderFrames
            : _moving ? CharacterSprites.WalkFrames : CharacterSprites.IdleFrames;

        _elapsed += (float)delta;
        while (_elapsed >= secondsPerFrame)
        {
            _elapsed -= secondsPerFrame;
            _frame = (_frame + 1) % frameCount;
        }
        ApplyFrame();
    }

    public void SetFacing(int facing)
    {
        facing = Math.Clamp(facing, 0, 3);
        if (facing == _facing)
            return;
        _facing = facing;
        ApplyFrame();
    }

    /// <summary>Walk cycle while true, idle breath while false. Restarts on change.</summary>
    public void SetMoving(bool moving)
    {
        if (moving == _moving)
            return;
        _moving = moving;
        _elapsed = 0f;
        _frame = 0;
        ApplyFrame();
    }

    /// <summary>
    /// Texture-swaps between the walk and riding sheets (scooter handoff: mounting
    /// has no ceremony — this and the speed change are the whole mount). Riding runs
    /// the six-column wheel cycle at 2x the walk rate while moving, and holds
    /// column 0 while still.
    /// </summary>
    public void SetRiding(bool riding)
    {
        if (riding == _riding)
            return;
        _riding = riding;
        _elapsed = 0f;
        _frame = 0;
        if (_sprite != null)
            _sprite.Texture = CurrentSheet();
        ApplyFrame();
    }

    private void ApplyFrame()
    {
        if (_sprite == null)
            return;
        // Riding columns ARE the cycle (0-5); on the walk sheet the cycle frames sit
        // after the two idle columns.
        int column = _riding ? (_moving ? _frame : 0)
            : _moving ? CharacterSprites.IdleFrames + _frame : _frame;
        _sprite.RegionRect = CharacterSprites.Region(_facing, column);
        _sprite.FlipH = _riding
            ? CharacterSprites.RiderFlipH(_facing) : CharacterSprites.FlipH(_facing);
    }

    private Texture2D CurrentSheet() =>
        _riding ? CharacterSprites.RiderSheet(_tunic) : CharacterSprites.Sheet(_tunic);
}

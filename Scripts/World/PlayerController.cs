using DevilGate.Scripts.World;
using Godot;

namespace DevilGate.Scripts.World;

/// <summary>
/// Click-to-move character. Uses NavigationAgent2D for paths; movement is
/// continuous (no turns, no movement rolls). One pending interaction at a
/// time: walking to an approach point fires its action on arrival.
/// </summary>
public partial class PlayerController : CharacterBody2D
{
    public bool TrackPosition { get; set; } = true;
    public const float Speed = 175f;

    private NavigationAgent2D _agent = null!;
    private PlayerToken _token = null!;
    private Label _nameLabel = null!;

    private Action? _pendingAction;
    private Vector2 _lastPosition;
    private float _stuckTimer;

    public override void _Ready()
    {
        _agent = GetNode<NavigationAgent2D>("NavigationAgent2D");
        _token = GetNode<PlayerToken>("Token");
        _nameLabel = GetNode<Label>("NameLabel");
        _agent.PathDesiredDistance = 4f;
        _agent.TargetDesiredDistance = 5f;
        _agent.Radius = 10f;
        _agent.SimplifyPath = false;
        _lastPosition = GlobalPosition;
    }

    public void Configure(string displayName, Color color)
    {
        _token.Modulate = color;
        _nameLabel.Text = displayName;
        _nameLabel.Modulate = color.Lightened(0.35f);
    }

    /// <summary>Plain move command; replaces any pending interaction.</summary>
    public void MoveTo(Vector2 target)
    {
        _pendingAction = null;
        _agent.TargetPosition = target;
    }

    /// <summary>Walk to an approach point, then run the action once on arrival.</summary>
    public void MoveToAndAct(Vector2 approachPoint, Action onArrive)
    {
        _pendingAction = onArrive;
        _agent.TargetPosition = approachPoint;
    }

    /// <summary>Cancel pending interaction/movement and stop in place.</summary>
    public void CancelPending()
    {
        _pendingAction = null;
        Stop();
    }

    /// <summary>Stop moving immediately (also used before opening modals).</summary>
    public void Stop()
    {
        _pendingAction = null;
        _agent.TargetPosition = GlobalPosition;
        Velocity = Vector2.Zero;
    }

    /// <summary>Write the current position back into the run state (for saves).</summary>
    public void WritePositionToRun()
    {
        if (!TrackPosition) return;
        var run = GameSession.Instance?.CurrentRun;
        if (run == null) return;
        run.PositionX = GlobalPosition.X;
        run.PositionY = GlobalPosition.Y;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_agent.IsNavigationFinished())
        {
            Velocity = Vector2.Zero;
            MoveAndSlide();
            if (_pendingAction != null)
            {
                var distToTarget = GlobalPosition.DistanceTo(_agent.TargetPosition);
                var action = _pendingAction;
                _pendingAction = null;
                if (distToTarget <= SafehouseLayout.InteractionRange * 1.6f)
                {
                    action.Invoke();
                }
            }
            if (Velocity == Vector2.Zero)
            {
                WritePositionToRun();
            }
            return;
        }

        var next = _agent.GetNextPathPosition();
        var to = next - GlobalPosition;
        if (to.LengthSquared() > 1f)
        {
            Velocity = to.Normalized() * Speed;
        }
        else
        {
            Velocity = Vector2.Zero;
        }
        MoveAndSlide();

        // Stuck watchdog: if we barely moved for a while, give up the path.
        if (GlobalPosition.DistanceTo(_lastPosition) < 0.6f)
        {
            _stuckTimer += (float)delta;
            if (_stuckTimer > 0.9f)
            {
                _stuckTimer = 0f;
                _pendingAction = null;
                _agent.TargetPosition = GlobalPosition;
            }
        }
        else
        {
            _stuckTimer = 0f;
        }
        _lastPosition = GlobalPosition;
    }

    public override void _ExitTree()
    {
        WritePositionToRun();
    }
}

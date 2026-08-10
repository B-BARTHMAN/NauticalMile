using Godot;
using NauticalMile.Player.MotionController.Airborn;

namespace NauticalMile.Player.MotionController.Grounded;

public partial class WalkingState : MotionState
{
    [Export] float _walkSpeed = 4.5f;
    [Export] float _acceleration = 50f;
    [Export] float _jumpForce = 6f;

    public override void Enter()
    {
    }

    public override void Exit()
    {
    }

    public override void Update(float delta)
    {
        var input = Input.GetVector(
            "left",
            "right",
            "backward",
            "forward"
        );

        var forward = -Body.GlobalTransform.Basis.Z;
        var right = Body.GlobalTransform.Basis.X;

        var moveDirection =
            (right * input.X) +
            (forward * input.Y);

        Body.Velocity = Body.Velocity.MoveToward(
            moveDirection * _walkSpeed,
            _acceleration * delta
        );

        // Instant jump
        if (Input.IsActionJustPressed("jump"))
        {
            Body.Velocity = new Vector3(
                Body.Velocity.X,
                _jumpForce,
                Body.Velocity.Z
            );

            ChangeState<JumpingState>();
            return;
        }

        if (!Input.IsActionPressed("move"))
        {
            ChangeState<IdleState>();
            return;
        }

        Body.MoveAndSlide();

        if (!Body.IsOnFloor())
        {
            ChangeState<FallingState>();
            return;
        }
    }
}
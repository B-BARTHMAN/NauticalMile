using Godot;
using NauticalMile.Player.MotionController.Grounded;

namespace NauticalMile.Player.MotionController.Airborn;

public partial class JumpingState : MotionState
{
    [Export] float _gravity = 20f;
    [Export] float _airControl = 20f;
    [Export] float _airSpeed = 4.5f;

    public override void Enter()
    {
    }

    public override void Exit()
    {
    }

    public override void Update(float delta)
    {
        // Gravity
        Body.Velocity += Vector3.Down * _gravity * delta;

        // Air movement
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

        var targetHorizontalVelocity =
            moveDirection * _airSpeed;

        var horizontalVelocity = new Vector3(
            Body.Velocity.X,
            0f,
            Body.Velocity.Z
        );

        horizontalVelocity = horizontalVelocity.MoveToward(
            targetHorizontalVelocity,
            _airControl * delta
        );

        Body.Velocity = new Vector3(
            horizontalVelocity.X,
            Body.Velocity.Y,
            horizontalVelocity.Z
        );

        Body.MoveAndSlide();

        if (Body.IsOnFloor())
        {
            ChangeState<IdleState>();
            return;
        }

        // Once we're descending, we're effectively falling.
        if (Body.Velocity.Y <= 0f)
        {
            ChangeState<FallingState>();
            return;
        }
    }
}
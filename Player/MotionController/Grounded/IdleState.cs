using System.Diagnostics;
using Godot;

namespace NauticalMile.Player.MotionController.Grounded;

public partial class IdleState : MotionState
{
    [Export]
    float _deceleration = 15;

    public override void Enter()
    {
    }

    public override void Exit()
    {
    }

    public override void Update(float delta)
    {
        if (Input.IsActionPressed("jump") &&
            Body.Velocity.LengthSquared() < 0.01f)
        {
            ChangeState<JumpChargeState>();
            return;
        }
        if (Input.IsActionPressed("move"))
        {
            ChangeState<WalkingState>();
            return;
        }

        Body.Velocity = Body.Velocity.MoveToward(Vector3.Zero, _deceleration * delta);
        Body.MoveAndSlide();
    }
}

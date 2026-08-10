using Godot;
using NauticalMile.Player.MotionController.Grounded;

namespace NauticalMile.Player.MotionController.Airborn;

public partial class FallingState : MotionState
{
    [Export] float _gravity = 20f;

    public override void Enter()
    {
    }

    public override void Exit()
    {
    }

    public override void Update(float delta)
    {
        Body.Velocity += Vector3.Down * _gravity * delta;

        Body.MoveAndSlide();

        if (Body.IsOnFloor())
        {
            ChangeState<IdleState>();
        }
    }
}
using System.Diagnostics;
using Godot;
using NauticalMile.Player.MotionController.Airborn;

namespace NauticalMile.Player.MotionController.Grounded;

public partial class JumpChargeState : MotionState
{
    [Export] float _maxChargeTime = 1.0f;
    [Export] float _minJump = 4f;
    [Export] float _maxJump = 10f;

    float _charge = 0;

    public override void Enter()
    {
        _charge = 0f;
    }

    public override void Exit()
    {
        float jump = Mathf.Lerp(_minJump, _maxJump, _charge / _maxChargeTime);
        Body.Velocity = new Vector3(
            0f,
            jump,
            0f
        );
    }

    public override void Update(float delta)
    {
        _charge += delta;
        _charge = Mathf.Min(_charge, _maxChargeTime);

        if (Input.IsActionPressed("jump")) return;

        ChangeState<JumpingState>();
    }
}

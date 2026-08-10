using System.Diagnostics;
using Godot;
namespace NauticalMile.Player.MotionController;

public abstract partial class MotionState : Node
{
    protected CharacterBody3D Body { get; private set; }
    private MotionController Controller { get; set; }

    public void Initialize(MotionController controller, CharacterBody3D body)
    {
        Controller = controller;
        Body = body;
    }

    public void OnEnter()
    {
        Debug.WriteLine(GetType().Name);
        Enter();
    }
    public abstract void Enter();
    public abstract void Exit();
    public abstract void Update(float delta);

    protected void ChangeState<TState>()
		where TState : MotionState =>
		Controller.ChangeState<TState>();
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;

namespace NauticalMile.Player.MotionController;

public partial class MotionController : Node
{
	[Export]
	public CharacterBody3D Body;

	[Export]
	private MotionState _initialState = default!;

	private MotionState State { get; set; }

	private readonly Dictionary<Type, MotionState> _states = [];

	public override void _Ready()
	{
		foreach (var state in GetChildren().OfType<MotionState>())
		{
			state.Initialize(this, Body);
			_states.Add(state.GetType(), state);
		}

		ChangeState(_initialState.GetType());
	}

	public override void _Process(double delta)
	{
		State?.Update((float)delta);
	}

	private void ChangeState(Type stateType)
	{
		Debug.Assert(_states.ContainsKey(stateType));
		State?.Exit();
		State = _states[stateType];
		State.OnEnter();
	}

	public void ChangeState<TState>()
		where TState : MotionState =>
		ChangeState(typeof(TState));
}
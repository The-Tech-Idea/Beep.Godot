using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Finite state machine component. Blind — attach to any entity for state-driven behavior.
    /// States defined as string keys. Transitions emit signals.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class StateMachineComponent : GameplayComponent
    {
        [Export] public string InitialState { get; set; } = "idle";

        [Signal] public delegate void StateChangedEventHandler(string from, string to);
        [Signal] public delegate void StateEnteredEventHandler(string state);
        [Signal] public delegate void StateExitedEventHandler(string state);

        public string CurrentState { get; private set; } = "";
        private readonly Dictionary<string, float> _stateTimers = new();

        public override void _Ready()
        {
            base._Ready();
            ChangeState(InitialState);
        }

        public override void _Process(double delta)
        {
            if (!IsActive) return;
            if (_stateTimers.ContainsKey(CurrentState))
                _stateTimers[CurrentState] += (float)delta;
        }

        public void ChangeState(string newState)
        {
            if (newState == CurrentState || !IsActive) return;
            EmitSignal(SignalName.StateExited, CurrentState);
            string old = CurrentState;
            CurrentState = newState;
            _stateTimers[newState] = 0f;
            EmitSignal(SignalName.StateChanged, old, newState);
            EmitSignal(SignalName.StateEntered, newState);
        }

        public float TimeInState(string state) =>
            _stateTimers.TryGetValue(state, out float t) ? t : 0f;
    }
}

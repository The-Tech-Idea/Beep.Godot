using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Finite state machine component wrapping BeepStateMachine with ECS lifecycle.
    /// Attach to any entity for callback-driven state behavior. States support
    /// OnEnter, OnUpdate, OnExit callbacks and trigger-based transitions.
    ///
    /// Example:
    /// var fsm = GetNode&lt;StateMachineComponent&gt;("FSM");
    /// fsm.AddState("idle", onEnter: () => sprite.Play("idle"));
    /// fsm.AddState("run", onEnter: () => sprite.Play("run"), onUpdate: () => MovePlayer());
    /// fsm.AddTransition("idle", "run", trigger: "move");
    /// fsm.Start("idle");
    /// // Later: fsm.Trigger("move");
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class StateMachineComponent : GameplayComponent, ISaveable
    {
        [Export] public string InitialState { get; set; } = "idle";

        /// <summary>Include this state machine in saves. Off by default: GameStateData holds
        /// one slot per feature, so every state machine in the scene saving would collide.</summary>
        [Export] public bool ParticipatesInSave { get; set; } = false;

        [Signal] public delegate void StateChangedEventHandler(string from, string to);
        [Signal] public delegate void StateEnteredEventHandler(string state);
        [Signal] public delegate void StateExitedEventHandler(string state);

        public string CurrentState => _fsm?.CurrentState ?? "";
        public string PreviousState => _fsm?.PreviousState ?? "";

        private GameBuilder.BeepStateMachine? _fsm;
        private readonly Dictionary<string, float> _stateTimers = new();
        private bool _initialized;

        public override void _Ready()
        {
            base._Ready();
            if (ParticipatesInSave) AddToGroup(SaveableHelper.Group);
            _fsm = new GameBuilder.BeepStateMachine();
            _initialized = false;
        }

        public override void _Process(double delta)
        {
            if (!IsActive || _fsm == null) return;

            // Track time in current state.
            if (!_stateTimers.ContainsKey(CurrentState))
                _stateTimers[CurrentState] = 0f;
            _stateTimers[CurrentState] += (float)delta;

            // Call the state's OnUpdate callback each frame.
            _fsm.Update();
        }

        public override void _ExitTree()
        {
            _fsm = null;
            base._ExitTree();
        }

        /// <summary>Define a state with optional callbacks.</summary>
        public GameBuilder.BeepStateMachine.State AddState(
            string name,
            Action? onEnter = null,
            Action? onUpdate = null,
            Action? onExit = null)
        {
            if (_fsm == null) return new GameBuilder.BeepStateMachine.State { Name = name };
            var state = _fsm.AddState(name, onEnter, onUpdate, onExit);
            return state;
        }

        /// <summary>Define a transition between two states, optionally triggered by a string key.</summary>
        public void AddTransition(string from, string to, string? trigger = null)
        {
            if (_fsm == null) return;
            _fsm.AddTransition(from, to, trigger);
        }

        /// <summary>Start the state machine in the given state.</summary>
        public void Start(string stateName)
        {
            if (_fsm == null || !IsActive) return;
            _fsm.Start(stateName);
            _stateTimers.Clear();
            _stateTimers[stateName] = 0f;
            _initialized = true;
            EmitSignal(SignalName.StateEntered, stateName);
        }

        /// <summary>Transition to a new state directly (without a trigger).</summary>
        public void ChangeState(string newState)
        {
            if (_fsm == null || !IsActive || newState == CurrentState) return;
            string old = CurrentState;
            _fsm.Transition(newState);
            _stateTimers[newState] = 0f;
            EmitSignal(SignalName.StateExited, old);
            EmitSignal(SignalName.StateChanged, old, newState);
            EmitSignal(SignalName.StateEntered, newState);
        }

        /// <summary>Fire a trigger to transition based on configured transitions.</summary>
        public void Trigger(string trigger)
        {
            if (_fsm == null || !IsActive) return;
            string old = CurrentState;
            _fsm.Trigger(trigger);
            if (CurrentState != old)
            {
                _stateTimers[CurrentState] = 0f;
                EmitSignal(SignalName.StateExited, old);
                EmitSignal(SignalName.StateChanged, old, CurrentState);
                EmitSignal(SignalName.StateEntered, CurrentState);
            }
        }

        /// <summary>Get how long (in seconds) the machine has been in a given state.</summary>
        public float TimeInState(string state) =>
            _stateTimers.TryGetValue(state, out float t) ? t : 0f;

        /// <summary>Get current state time.</summary>
        public float CurrentStateTime => TimeInState(CurrentState);

        // ── ISaveable Implementation ──
        public void Save(GameBuilder.GameStateData state)
        {
            if (string.IsNullOrEmpty(CurrentState)) return;
            state.GameData["state_machine_current"] = CurrentState;
            state.GameData["state_machine_previous"] = PreviousState ?? "";
            if (_stateTimers.TryGetValue(CurrentState, out var time))
                state.GameData["state_machine_time"] = time;
        }

        public void Load(GameBuilder.GameStateData state)
        {
            if (_fsm == null) return;

            // Restore state machine to the saved state.
            if (!state.GameData.TryGetValue("state_machine_current", out var currentObj)) return;

            var current = currentObj.AsString();
            if (string.IsNullOrEmpty(current)) return;

            // Only restore if we're initialized and the state exists.
            if (!_initialized) Start(InitialState);
            ChangeState(current);

            // Restore the timer for this state.
            if (state.GameData.TryGetValue("state_machine_time", out var timeObj))
                _stateTimers[current] = (float)timeObj;
        }
    }
}

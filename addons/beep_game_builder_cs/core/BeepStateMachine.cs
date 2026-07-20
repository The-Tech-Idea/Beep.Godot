using Godot;
using System;
using System.Collections.Generic;

namespace Beep.GameBuilder;

/// <summary>Simple finite state machine for UI states. Add states with enter/update/exit callbacks.</summary>
public class BeepStateMachine
{
    private Dictionary<string, State> _states = new();
    private State _current;
    private string _previousState;

    public string CurrentState => _current?.Name;
    public string PreviousState => _previousState;

    public class State
    {
        public string Name;
        public Action OnEnter, OnUpdate, OnExit;
        public Dictionary<string, string> Transitions = new();
    }

    public State AddState(string name, Action onEnter = null, Action onUpdate = null, Action onExit = null)
    {
        var s = new State { Name = name, OnEnter = onEnter, OnUpdate = onUpdate, OnExit = onExit };
        _states[name] = s;
        return s;
    }

    public void AddTransition(string from, string to, string trigger = null)
    {
        if (_states.TryGetValue(from, out var s))
            s.Transitions[trigger ?? to] = to;
    }

    public void Start(string name)
    {
        _previousState = null;
        _current = _states.GetValueOrDefault(name);
        if (_current == null)
            GD.PushWarning($"[BeepStateMachine] Start('{name}') — no such state registered; the machine has no current state.");
        _current?.OnEnter?.Invoke();
    }

    public void Transition(string to)
    {
        if (_current == null || !_states.ContainsKey(to))
        {
            GD.PushWarning($"[BeepStateMachine] Transition to '{to}' ignored — {(_current == null ? "no current state (call Start first)" : $"'{to}' is not a registered state")}.");
            return;
        }
        _previousState = _current.Name;
        _current.OnExit?.Invoke();
        _current = _states[to];
        _current.OnEnter?.Invoke();
    }

    public void Trigger(string trigger)
    {
        if (_current != null && _current.Transitions.TryGetValue(trigger, out var to))
            Transition(to);
    }

    public void Update()
    {
        _current?.OnUpdate?.Invoke();
    }
}

/// <summary>Lightweight pub/sub event bus for decoupled UI communication.</summary>
public static class BeepEventBus
{
    private static Dictionary<string, List<Action<object>>> _listeners = new();
    private static Dictionary<string, List<Action>> _simpleListeners = new();

    public static void Subscribe(string eventName, Action<object> callback)
    {
        if (!_listeners.ContainsKey(eventName)) _listeners[eventName] = new();
        _listeners[eventName].Add(callback);
    }

    public static void Subscribe(string eventName, Action callback)
    {
        if (!_simpleListeners.ContainsKey(eventName)) _simpleListeners[eventName] = new();
        _simpleListeners[eventName].Add(callback);
    }

    public static void Unsubscribe(string eventName, Action<object> callback)
    {
        if (_listeners.TryGetValue(eventName, out var list)) list.Remove(callback);
    }

    public static void Unsubscribe(string eventName, Action callback)
    {
        if (_simpleListeners.TryGetValue(eventName, out var list)) list.Remove(callback);
    }

    public static void Emit(string eventName, object data = null)
    {
        // Snapshot before dispatch: a listener that Subscribes/Unsubscribes while handling the event
        // would otherwise mutate the list mid-iteration and throw.
        if (_listeners.TryGetValue(eventName, out var list))
            foreach (var cb in list.ToArray()) cb?.Invoke(data);
        if (_simpleListeners.TryGetValue(eventName, out var slist))
            foreach (var cb in slist.ToArray()) cb?.Invoke();
    }

    public static void Clear() { _listeners.Clear(); _simpleListeners.Clear(); }
}

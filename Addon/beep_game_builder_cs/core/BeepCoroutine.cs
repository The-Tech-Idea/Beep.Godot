using Godot;
using System;
using System.Collections.Generic;

/// <summary>Coroutine-like delayed execution. Schedule actions to run after time or on next frame.</summary>
public static class BeepCoroutine
{
    private static List<Job> _jobs = new();
    private static Node _host;
    private static bool _initialized;

    private class Job
    {
        public float Timer;
        public float Delay;
        public Action Action;
        public bool Repeat;
        public string Id;
    }

    public static void Init(Node host)
    {
        if (_initialized) return;
        _host = host;
        _initialized = true;
    }

    public static void Process(double delta)
    {
        for (int i = _jobs.Count - 1; i >= 0; i--)
        {
            var j = _jobs[i];
            j.Timer -= (float)delta;
            if (j.Timer <= 0)
            {
                j.Action?.Invoke();
                if (j.Repeat) j.Timer = j.Delay;
                else _jobs.RemoveAt(i);
            }
        }
    }

    /// <summary>Run action after delay seconds.</summary>
    public static void Run(float delay, Action action, string id = null)
    {
        _jobs.Add(new Job { Timer = delay, Delay = delay, Action = action, Id = id });
    }

    /// <summary>Run action on next frame.</summary>
    public static void NextFrame(Action action) => Run(0, action);

    /// <summary>Run action repeatedly every interval seconds until cancelled.</summary>
    public static string Repeat(float interval, Action action, string id = null)
    {
        id ??= Guid.NewGuid().ToString();
        _jobs.Add(new Job { Timer = interval, Delay = interval, Action = action, Repeat = true, Id = id });
        return id;
    }

    public static void Cancel(string id) => _jobs.RemoveAll(j => j.Id == id);

    /// <summary>Wait for a Godot signal, then run action. Usage: BeepCoroutine.WaitSignal(timer, "timeout", () => Done());</summary>
    public static async void WaitSignal(GodotObject source, string signal, Action then = null)
    {
        await _host.ToSignal(source, signal);
        then?.Invoke();
    }
}

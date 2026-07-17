using System;
using System.Collections.Generic;
using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Per-scene coroutine/scheduler host. Schedules delayed or repeating callbacks
    /// without needing a global static. Attach to any scene; call Delay/Repeat from
    /// any sibling component. Replaces static BeepCoroutine with an instance-based
    /// component that integrates with ECS lifecycle and supports job cancellation.
    ///
    /// Example:
    /// var coro = GetNode&lt;CoroutineHostComponent&gt;("Coroutines");
    /// coro.Delay(2f, () => DoSomething());                 // After 2 seconds
    /// var jobId = coro.Repeat(0.5f, () => UpdateHUD());    // Every 0.5 seconds
    /// coro.Cancel(jobId);                                  // Stop the job
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class CoroutineHostComponent : UIComponent, ISaveable
    {
        [Signal] public delegate void JobStartedEventHandler(string jobId);
        [Signal] public delegate void JobCompletedEventHandler(string jobId);

        private struct Job
        {
            public string Id;
            public double Timer;
            public double Interval;  // 0 = one-shot, > 0 = repeating
            public Action Callback;
            public bool OneShot => Interval == 0;
        }

        private readonly List<Job> _jobs = new();

        public override void _Process(double delta)
        {
            if (!IsActive) return;

            for (int i = _jobs.Count - 1; i >= 0; i--)
            {
                var job = _jobs[i];
                job.Timer -= delta;

                if (job.Timer <= 0)
                {
                    job.Callback?.Invoke();
                    if (job.OneShot)
                    {
                        EmitSignal(SignalName.JobCompleted, job.Id);
                        _jobs.RemoveAt(i);
                    }
                    else
                    {
                        // Repeating job: reset timer
                        job.Timer = job.Interval;
                        _jobs[i] = job;
                    }
                }
                else
                {
                    _jobs[i] = job;
                }
            }
        }

        public override void _ExitTree()
        {
            _jobs.Clear();
            base._ExitTree();
        }

        /// <summary>Schedule a one-shot callback after a delay (seconds).</summary>
        public string Delay(double seconds, Action callback, string jobId = null)
        {
            jobId ??= Guid.NewGuid().ToString();
            var job = new Job
            {
                Id = jobId,
                Timer = seconds,
                Interval = 0,  // One-shot
                Callback = callback
            };
            _jobs.Add(job);
            EmitSignal(SignalName.JobStarted, jobId);
            return jobId;
        }

        /// <summary>Schedule a callback to run on the next frame.</summary>
        public string NextFrame(Action callback, string jobId = null)
        {
            return Delay(0, callback, jobId);
        }

        /// <summary>Schedule a repeating callback at the given interval (seconds).
        /// Returns the job ID; use Cancel(jobId) to stop it.</summary>
        public string Repeat(double interval, Action callback, string jobId = null)
        {
            jobId ??= Guid.NewGuid().ToString();
            var job = new Job
            {
                Id = jobId,
                Timer = interval,
                Interval = interval,  // Repeating
                Callback = callback
            };
            _jobs.Add(job);
            EmitSignal(SignalName.JobStarted, jobId);
            return jobId;
        }

        /// <summary>Cancel a scheduled job by ID.</summary>
        public void Cancel(string jobId)
        {
            var idx = _jobs.FindIndex(j => j.Id == jobId);
            if (idx >= 0)
            {
                EmitSignal(SignalName.JobCompleted, jobId);
                _jobs.RemoveAt(idx);
            }
        }

        /// <summary>Cancel all scheduled jobs.</summary>
        public void CancelAll()
        {
            foreach (var job in _jobs)
                EmitSignal(SignalName.JobCompleted, job.Id);
            _jobs.Clear();
        }

        /// <summary>Get the number of active jobs.</summary>
        public int ActiveJobCount => _jobs.Count;

        /// <summary>Check if a job is active.</summary>
        public bool IsJobActive(string jobId) => _jobs.Exists(j => j.Id == jobId);

        /// <summary>Wait for a signal, then run a callback. Usage: coro.WaitSignal(timer, "timeout", () => Done()).</summary>
        public async void WaitSignal(GodotObject source, StringName signal, Action then = null)
        {
            await ToSignal(source, signal);
            then?.Invoke();
        }

        // ── ISaveable Implementation ──
        // Note: Coroutines are transient and not persisted on save/load.
        // On load, all scheduled jobs are cleared (game resumes from save point).
        public void Save(GameBuilder.GameStateData state)
        {
            // Don't persist jobs — they're transient/runtime tasks
            // (tween animations, effect delays, etc.)
        }

        public void Load(GameBuilder.GameStateData state)
        {
            // Clear all jobs on load — resume from the save point in a clean state
            CancelAll();
        }
    }
}

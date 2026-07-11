using System.Collections.Generic;
using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Per-scene coroutine/scheduler host. Provides Delay(seconds, callback) and
    /// Repeat(interval, callback) without needing a global static. Attach to any
    /// node in the scene; call Delay/Repeat from any sibling component.
    /// Replaces the static BeepCoroutine class.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class CoroutineHostComponent : UIComponent
    {
        private struct Job
        {
            public double Timer;
            public double Interval; // 0 = one-shot
            public System.Action Callback;
        }

        private readonly List<Job> _jobs = new();

        /// <summary>Schedule a one-shot callback after a delay.</summary>
        public void Delay(double seconds, System.Action callback)
        {
            _jobs.Add(new Job { Timer = seconds, Interval = 0, Callback = callback });
        }

        /// <summary>Schedule a repeating callback at the given interval (seconds).</summary>
        public void Repeat(double interval, System.Action callback)
        {
            _jobs.Add(new Job { Timer = interval, Interval = interval, Callback = callback });
        }

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
                    if (job.Interval > 0)
                    {
                        job.Timer = job.Interval;
                        _jobs[i] = job;
                    }
                    else
                        _jobs.RemoveAt(i);
                }
                else
                    _jobs[i] = job;
            }
        }
    }
}

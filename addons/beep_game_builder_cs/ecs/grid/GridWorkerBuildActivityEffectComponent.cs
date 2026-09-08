using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// The worker-side half of the construction-in-progress effect family:
    /// while a sibling IWorker is actively working a job whose kind is in
    /// ActiveJobKinds, periodically triggers a sibling ParticleComponent
    /// burst and/or SpriteEffectComponent play - hammering sparks, dust, a
    /// welding flash, whatever the unit's own effect scenes are. Neither
    /// effect component is required; wire either, both, or neither.
    ///
    /// Deliberately does not assume a human: the sibling worker is found by
    /// the IWorker contract (or, for a GDScript worker, by the same
    /// duck-typed shape GridWorkerPorts reads for the port contracts) - a
    /// crane, a robot, or a drone works exactly like GridWorkerComponent
    /// here, differing only in which effect scenes are wired. ActiveJobKinds
    /// defaults to "build" but is not hardcoded to it - the same component
    /// works for a gather/mine "in progress" effect too.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridWorkerBuildActivityEffectComponent : GameplayComponent
    {
        [Export] public NodePath JobQueuePath { get; set; } = new("");

        /// <summary>Job kinds that trigger the effect while the sibling
        /// worker is working one; empty means any kind.</summary>
        [Export] public Godot.Collections.Array<string> ActiveJobKinds { get; set; } = new() { "build" };

        /// <summary>Seconds between bursts while working continuously.</summary>
        [Export(PropertyHint.Range, "0.05,10,0.05")] public float BurstIntervalSeconds { get; set; } = 1f;

        private GridJobQueueComponent? _jobs;
        private Node? _worker;
        private ParticleComponent? _particle;
        private SpriteEffectComponent? _sprite;
        private bool _warnedAutoQueueFree;
        private bool _wasActive;
        private float _burstTimer;

        public float EffectiveBurstInterval => Mathf.Max(0.05f, float.IsFinite(BurstIntervalSeconds) ? BurstIntervalSeconds : 1f);

        /// <summary>Whether the effect is currently considered active - for tests/tools.</summary>
        public bool IsEffectActive { get; private set; }

        public override void _Ready()
        {
            base._Ready();
            ResolveReferences();
            SetProcess(!Engine.IsEditorHint());
            UpdateConfigurationWarnings();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (_particle == null && _sprite == null && IsInsideTree())
                return new[] { "No sibling ParticleComponent or SpriteEffectComponent found - this component has nothing to trigger. Add one beside it." };
            return System.Array.Empty<string>();
        }

        public override void _Process(double delta)
        {
            if (!IsActive || Engine.IsEditorHint())
                return;

            Tick(delta);
        }

        public void Tick(double delta)
        {
            ResolveReferences();
            bool active = _worker != null && GridWorkerPorts.IsWorking(_worker) && JobKindMatches();

            if (active && !_wasActive)
            {
                _burstTimer = 0f;
                TriggerBurst();
            }
            else if (active)
            {
                float step = double.IsFinite(delta) && delta > 0.0 ? (float)delta : 0f;
                _burstTimer -= step;
                if (_burstTimer <= 0f)
                {
                    _burstTimer = EffectiveBurstInterval;
                    TriggerBurst();
                }
            }
            else if (!active && _wasActive)
            {
                _particle?.Stop();
            }

            _wasActive = active;
            IsEffectActive = active;
        }

        private void TriggerBurst()
        {
            _particle?.Burst();
            _sprite?.Play();
        }

        private bool JobKindMatches()
        {
            if (ActiveJobKinds.Count == 0)
                return true;
            if (_worker == null || _jobs == null)
                return false;

            string jobId = GridWorkerPorts.CurrentJobId(_worker);
            if (string.IsNullOrEmpty(jobId))
                return false;

            string kind = _jobs.GetJobKind(jobId);
            foreach (string allowed in ActiveJobKinds)
            {
                if (string.Equals(allowed?.Trim(), kind, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private void ResolveReferences()
        {
            Resolve(JobQueuePath, ref _jobs);

            if (_worker == null || !GodotObject.IsInstanceValid(_worker))
                _worker = FindWorkerSibling();

            if (_particle == null || !GodotObject.IsInstanceValid(_particle))
            {
                _particle = GetSiblingComponent<ParticleComponent>();
                // This effect bursts REPEATEDLY for as long as the worker
                // works. ParticleComponent's AutoQueueFree (its default)
                // frees the emitter when the first burst finishes, after
                // which every later Burst() is a silent no-op - the effect
                // fires exactly once and nothing says why. The export is
                // the developer's; it is not overridden here, but it is
                // named, once.
                if (_particle != null && _particle.AutoQueueFree && !_warnedAutoQueueFree)
                {
                    _warnedAutoQueueFree = true;
                    GD.PushWarning($"[{Name}] Sibling ParticleComponent '{_particle.Name}' has AutoQueueFree on - its emitter is freed after the first burst finishes, so this activity effect will burst once and then never again. Turn AutoQueueFree off for a repeating effect.");
                }
            }

            if (_sprite == null || !GodotObject.IsInstanceValid(_sprite))
                _sprite = GetSiblingComponent<SpriteEffectComponent>();
        }

        private Node? FindWorkerSibling()
        {
            Node? parent = GetParent();
            if (parent == null)
                return null;

            foreach (Node child in parent.GetChildren())
            {
                if (child == this)
                    continue;
                if (child is IWorker || GridWorkerPorts.AnswersWorkerShape(child))
                    return child;
            }
            return null;
        }
    }
}

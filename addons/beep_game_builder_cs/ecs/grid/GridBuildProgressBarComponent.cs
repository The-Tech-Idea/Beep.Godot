using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// A floating progress bar over each structure currently under
    /// construction. Attach beside GridBuildSiteComponent; listens to its
    /// ALREADY-emitted BuildSiteCreated/Completed/Cancelled signals to track
    /// which placed nodes are active, and reads GetJobProgress01 off the job
    /// queue - never the worker - so it stays correct for ANY IWorker
    /// implementation that reports progress, not just GridWorkerComponent.
    ///
    /// This component draws nothing and sizes nothing. The bar is an
    /// AUTHORED scene - ProgressBarScene, any scene whose root is a Godot
    /// Range (a plain ProgressBar, the kit's KitMeter, a TextureProgressBar);
    /// the shipped default is templates/scenes/grid_build_progress_bar.tscn,
    /// a KitMeter - and all this component does is instantiate it, place it
    /// over the site, and set its Value. How it looks is the scene's job.
    ///
    /// The bar is an OVERLAY, parented under this component - not under the
    /// placed node - and positioned in world space from the placed node's
    /// GlobalPosition. Parenting it under the structure put it inside the
    /// subtree the construction-stage visuals treat as "the building's art",
    /// so a reveal effect clipped the bar to nothing; an overlay must never
    /// live where an effect on the building can reach it. It also does not
    /// inherit the structure's scale or rotation, which a readout should not.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridBuildProgressBarComponent : Node
    {
        private const string DefaultProgressBarScenePath = "res://addons/beep_game_builder_cs/templates/scenes/grid_build_progress_bar.tscn";

        [Export] public NodePath BuildSitePath { get; set; } = new("");
        [Export] public NodePath JobQueuePath { get; set; } = new("");
        [Export] public bool AutoConnect { get; set; } = true;

        /// <summary>The bar scene; its root must be a Range. Unset uses the
        /// shipped default.</summary>
        [Export] public PackedScene? ProgressBarScene { get; set; }

        /// <summary>Where the bar's centre sits relative to the placed
        /// node's origin, in world pixels.</summary>
        [Export] public Vector2 BarOffset { get; set; } = new(0, -20);

        /// <summary>
        /// The bar's modulate while its job is queued with no worker holding
        /// it. An unstaffed site must read differently from a slow one
        /// (Frostpunk's 0/10 ring, ONI's "no worker", Banished's status
        /// icons): the player's problem is labour, not time. White while a
        /// worker holds the job.
        /// </summary>
        [Export] public Color StalledModulate { get; set; } = new(0.55f, 0.55f, 0.55f, 1f);

        /// <summary>How often the bar values refresh. A build takes real
        /// seconds; it does not need frame-rate updates.</summary>
        [Export(PropertyHint.Range, "0.05,5,0.05")] public float RefreshIntervalSeconds { get; set; } = 0.25f;

        private GridBuildSiteComponent? _buildSite;
        private GridJobQueueComponent? _jobs;
        private bool _connected;
        private PackedScene? _resolvedScene;
        private readonly Dictionary<string, (Godot.Range Bar, Node2D Placed)> _bars = new();
        private float _refreshAccumulator;

        public float EffectiveRefreshInterval => Mathf.Max(0.05f, float.IsFinite(RefreshIntervalSeconds) ? RefreshIntervalSeconds : 0.25f);

        public override void _Ready()
        {
            ResolveReferences();
            if (AutoConnect && !Engine.IsEditorHint())
                ConnectSystems();
            SetProcess(!Engine.IsEditorHint());
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            DisconnectSystems();
            foreach ((Godot.Range bar, _) in _bars.Values)
                if (GodotObject.IsInstanceValid(bar))
                    bar.QueueFree();
            _bars.Clear();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (BuildSitePath.IsEmpty)
                return new[] { "BuildSitePath should point to a GridBuildSiteComponent." };
            return System.Array.Empty<string>();
        }

        public void ConnectSystems()
        {
            ResolveReferences();
            if (_buildSite == null || _connected)
                return;

            _buildSite.BuildSiteCreated += OnBuildSiteCreated;
            _buildSite.BuildSiteCompleted += OnBuildSiteEnded;
            _buildSite.BuildSiteCancelled += OnBuildSiteCancelled;
            _connected = true;
        }

        public void DisconnectSystems()
        {
            if (_buildSite != null && GodotObject.IsInstanceValid(_buildSite) && _connected)
            {
                _buildSite.BuildSiteCreated -= OnBuildSiteCreated;
                _buildSite.BuildSiteCompleted -= OnBuildSiteEnded;
                _buildSite.BuildSiteCancelled -= OnBuildSiteCancelled;
            }
            _connected = false;
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint())
                return;

            float step = double.IsFinite(delta) && delta > 0.0 ? (float)delta : 0f;
            _refreshAccumulator += step;
            if (_refreshAccumulator < EffectiveRefreshInterval)
                return;

            _refreshAccumulator = 0f;
            RefreshBars();
        }

        /// <summary>Number of bars currently tracked - for tests/tools.</summary>
        public int ActiveBarCount => _bars.Count;

        /// <summary>The live progress (0..1) a bar is currently showing, or -1 if untracked - for tests/tools.</summary>
        public float ProgressForJob(string jobId)
        {
            if (!_bars.TryGetValue(jobId, out (Godot.Range Bar, Node2D Placed) entry) || !GodotObject.IsInstanceValid(entry.Bar))
                return -1f;
            double span = entry.Bar.MaxValue - entry.Bar.MinValue;
            return span > 0.0 ? (float)((entry.Bar.Value - entry.Bar.MinValue) / span) : 0f;
        }

        /// <summary>The bar node for a job, or null - for tests/tools.</summary>
        public Godot.Range? BarForJob(string jobId)
            => _bars.TryGetValue(jobId, out (Godot.Range Bar, Node2D Placed) entry) && GodotObject.IsInstanceValid(entry.Bar) ? entry.Bar : null;

        /// <summary>Whether a job's bar currently shows the stalled look - for tests/tools.</summary>
        public bool IsStalled(string jobId)
            => BarForJob(jobId) is { } bar && bar.Modulate == StalledModulate;

        private void RefreshBars()
        {
            ResolveReferences();
            if (_jobs == null)
                return;

            foreach ((string jobId, (Godot.Range bar, Node2D placed)) in _bars)
            {
                if (!GodotObject.IsInstanceValid(bar))
                    continue;
                PlaceBar(bar, placed);
                float progress = _jobs.GetJobProgress01(jobId);
                bar.Value = Mathf.Lerp(bar.MinValue, bar.MaxValue, progress);
                bool stalled = _jobs.GetJobState(jobId) == GridJobQueueComponent.GridJobState.Queued;
                bar.Modulate = stalled ? StalledModulate : Colors.White;
            }
        }

        private void PlaceBar(Godot.Range bar, Node2D placed)
        {
            if (!GodotObject.IsInstanceValid(placed) || !placed.IsInsideTree())
                return;
            bar.GlobalPosition = placed.GlobalPosition + BarOffset - bar.Size / 2f;
        }

        private void OnBuildSiteCreated(string buildId, string jobId, Node2D placed, int x, int y)
        {
            if (_bars.ContainsKey(jobId) || !GodotObject.IsInstanceValid(placed))
                return;

            PackedScene? scene = ResolveScene();
            if (scene == null)
                return;

            // InstantiateOrNull (not Instantiate<T>) so a scene whose root is
            // not a Range warns instead of throwing mid-signal.
            Godot.Range? bar = scene.InstantiateOrNull<Godot.Range>();
            if (bar == null)
            {
                GD.PushWarning($"[{Name}] ProgressBarScene's root is not a Range (ProgressBar, KitMeter, TextureProgressBar...) - no bar will show for build '{buildId}'.");
                return;
            }

            bar.Name = $"BuildProgressBar_{jobId}";
            // A floating readout must never take the click meant for the
            // world under it.
            bar.MouseFilter = Control.MouseFilterEnum.Ignore;
            bar.Value = bar.MinValue;
            AddChild(bar);
            PlaceBar(bar, placed);
            _bars[jobId] = (bar, placed);
        }

        private void OnBuildSiteEnded(string buildId, string jobId, Node2D placed, int x, int y)
            => RemoveBar(jobId);

        private void OnBuildSiteCancelled(string buildId, string jobId, int x, int y)
            => RemoveBar(jobId);

        private void RemoveBar(string jobId)
        {
            if (!_bars.Remove(jobId, out (Godot.Range Bar, Node2D Placed) entry))
                return;
            if (GodotObject.IsInstanceValid(entry.Bar))
                entry.Bar.QueueFree();
        }

        private PackedScene? ResolveScene()
        {
            if (ProgressBarScene != null)
                return ProgressBarScene;
            if (_resolvedScene != null)
                return _resolvedScene;

            if (!ResourceLoader.Exists(DefaultProgressBarScenePath))
            {
                GD.PushWarning($"[{Name}] No ProgressBarScene set and the shipped default ('{DefaultProgressBarScenePath}') could not load - no progress bars will show.");
                return null;
            }

            _resolvedScene = ResourceLoader.Load<PackedScene>(DefaultProgressBarScenePath);
            return _resolvedScene;
        }

        private void ResolveReferences()
        {
            // A freed build site took its signal connections with it; the flag
            // must drop before Resolve picks a replacement up, or
            // ConnectSystems would believe the new instance is already wired.
            if (_buildSite != null && !GodotObject.IsInstanceValid(_buildSite))
            {
                _buildSite = null;
                _connected = false;
            }
            EntityComponent.Resolve(this, BuildSitePath, ref _buildSite);

            EntityComponent.Resolve(this, JobQueuePath, ref _jobs);
        }
    }
}

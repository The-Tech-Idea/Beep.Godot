using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Drives a structure's construction-site visuals from the build site
    /// and the job queue. Attach beside GridBuildSiteComponent; for every
    /// build with GridBuildDefinition.ConstructionStages it instantiates
    /// those scenes and tells them the facts of the site through
    /// IConstructionVisual - footprint and cell size, which stage they are,
    /// the site's STATE (pending materials, queued with no worker, working,
    /// complete), the materials on site, progress, and each hit of work.
    /// The scenes render all of that; this component draws nothing.
    ///
    /// A site exists from the moment the build is placed, not from the
    /// moment its job starts: the delivery beat every simulated-site game
    /// has (Settlers' stacks, Banished's piles, Timberborn's deliveries)
    /// happens while the build is still waiting for RequiredMaterials, so
    /// the visual is created on BuildSiteAwaitingMaterials as well as on
    /// BuildSiteCreated, and keyed by the placed node.
    ///
    /// Stage scenes live under a site root that is a child of THIS
    /// component, positioned from the placed node - not under the placed
    /// node. GridBuildSiteComponent tints or hides the placed node while it
    /// builds; stages under it would inherit that (a translucent yellow
    /// site is the "fading in" look this whole family exists to replace).
    /// The site root's z is the footprint's FRONT edge in the same absolute
    /// domain units use (their own y), so a worker on the approach cell in
    /// front of the site draws in front of the rising wall and one behind
    /// it draws behind.
    ///
    /// On completion the placed node's finished art is shown by the site
    /// component at once (a hard swap, as every real game does) and the
    /// stage scenes are kept for TeardownSeconds with SiteState "complete",
    /// so they can take their scaffold down over it, then freed.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridBuildStageVisualComponent : Node
    {
        [Export] public NodePath BuildSitePath { get; set; } = new("");
        [Export] public NodePath BuildCatalogPath { get; set; } = new("");
        [Export] public NodePath JobQueuePath { get; set; } = new("");
        /// <summary>The GridProjectionComponent whose tile size is the
        /// CellSize told to every stage scene.</summary>
        [Export] public NodePath GridPath { get; set; } = new("");
        [Export] public bool AutoConnect { get; set; } = true;

        /// <summary>How often the site re-reads state and progress.</summary>
        [Export(PropertyHint.Range, "0.05,5,0.05")] public float RefreshIntervalSeconds { get; set; } = 0.25f;

        /// <summary>How long stage scenes stay after completion to tear
        /// their site down (scaffold coming down over the finished art).</summary>
        [Export(PropertyHint.Range, "0,10,0.1")] public float TeardownSeconds { get; set; } = 1.0f;

        /// <summary>Seconds between WorkPulse calls while progress is
        /// actually advancing - the cadence of the work, whatever does it.</summary>
        [Export(PropertyHint.Range, "0.1,5,0.05")] public float WorkPulseIntervalSeconds { get; set; } = 0.6f;

        private GridBuildSiteComponent? _buildSite;
        private GridBuildCatalogComponent? _catalog;
        private GridJobQueueComponent? _jobs;
        private GridProjectionComponent? _grid;
        private bool _connected;
        private bool _warnedNoGrid;
        private readonly Dictionary<Node2D, SiteVisual> _sitesByPlaced = new();
        private readonly Dictionary<string, SiteVisual> _sitesByJob = new();
        private float _refreshAccumulator;

        public float EffectiveRefreshInterval => Mathf.Max(0.05f, float.IsFinite(RefreshIntervalSeconds) ? RefreshIntervalSeconds : 0.25f);
        public float EffectiveTeardownSeconds => Mathf.Max(0f, float.IsFinite(TeardownSeconds) ? TeardownSeconds : 1f);
        public float EffectiveWorkPulseInterval => Mathf.Max(0.1f, float.IsFinite(WorkPulseIntervalSeconds) ? WorkPulseIntervalSeconds : 0.6f);

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
            foreach (SiteVisual site in _sitesByPlaced.Values)
                FreeSite(site);
            _sitesByPlaced.Clear();
            _sitesByJob.Clear();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (BuildSitePath.IsEmpty)
                return new[] { "BuildSitePath should point to a GridBuildSiteComponent." };
            if (BuildCatalogPath.IsEmpty)
                return new[] { "BuildCatalogPath should point to a GridBuildCatalogComponent." };
            return System.Array.Empty<string>();
        }

        public void ConnectSystems()
        {
            ResolveReferences();
            if (_buildSite == null || _connected)
                return;

            _buildSite.BuildSiteAwaitingMaterials += OnBuildSiteAwaitingMaterials;
            _buildSite.BuildSiteCreated += OnBuildSiteCreated;
            _buildSite.BuildSiteCompleted += OnBuildSiteCompleted;
            _buildSite.BuildSiteCancelled += OnBuildSiteCancelled;
            _connected = true;
        }

        public void DisconnectSystems()
        {
            if (_buildSite != null && GodotObject.IsInstanceValid(_buildSite) && _connected)
            {
                _buildSite.BuildSiteAwaitingMaterials -= OnBuildSiteAwaitingMaterials;
                _buildSite.BuildSiteCreated -= OnBuildSiteCreated;
                _buildSite.BuildSiteCompleted -= OnBuildSiteCompleted;
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

            float elapsed = _refreshAccumulator;
            _refreshAccumulator = 0f;
            RefreshSites(elapsed);
        }

        // ---- test/tool hooks --------------------------------------------------

        /// <summary>The stage index currently visible for a job, or -1 if untracked.</summary>
        public int VisibleStageIndex(string jobId)
        {
            if (!_sitesByJob.TryGetValue(jobId, out SiteVisual? site))
                return -1;
            for (int i = 0; i < site.Stages.Count; i++)
                if (GodotObject.IsInstanceValid(site.Stages[i]) && site.Stages[i].Visible)
                    return i;
            return -1;
        }

        /// <summary>A job's instantiated stage node by index, or null.</summary>
        public Node2D? StageForJob(string jobId, int index)
            => _sitesByJob.TryGetValue(jobId, out SiteVisual? site)
               && index >= 0 && index < site.Stages.Count && GodotObject.IsInstanceValid(site.Stages[index])
                ? site.Stages[index] : null;

        /// <summary>The site state last pushed for a job ("pending", "queued",
        /// "working", "complete"), or empty if untracked.</summary>
        public string SiteStateForJob(string jobId)
            => _sitesByJob.TryGetValue(jobId, out SiteVisual? site) ? site.State : "";

        /// <summary>A placed node's stage instances - also for a site still
        /// pending materials, which has no job id yet.</summary>
        public Godot.Collections.Array<Node2D> StagesForPlaced(Node2D placed)
        {
            var result = new Godot.Collections.Array<Node2D>();
            if (_sitesByPlaced.TryGetValue(placed, out SiteVisual? site))
                foreach (Node2D stage in site.Stages)
                    if (GodotObject.IsInstanceValid(stage))
                        result.Add(stage);
            return result;
        }

        /// <summary>The node the stage scenes are parented under for a placed
        /// node - a child of this component, never of the placed node.</summary>
        public Node2D? SiteRootForPlaced(Node2D placed)
            => _sitesByPlaced.TryGetValue(placed, out SiteVisual? site) && GodotObject.IsInstanceValid(site.Root) ? site.Root : null;

        // ---- signals -----------------------------------------------------------

        private void OnBuildSiteAwaitingMaterials(string buildId, Node2D placed, int x, int y)
            => CreateSite(buildId, "", placed, new Vector2I(x, y));

        private void OnBuildSiteCreated(string buildId, string jobId, Node2D placed, int x, int y)
        {
            if (_sitesByPlaced.TryGetValue(placed, out SiteVisual? existing))
            {
                // The pending site's job has just been created: from here
                // the stock is built in, so the scenes are told it in full
                // once more - materials are only re-read while pending.
                existing.JobId = jobId;
                _sitesByJob[jobId] = existing;
                PushMaterials(existing);
                return;
            }
            CreateSite(buildId, jobId, placed, new Vector2I(x, y));
        }

        private void OnBuildSiteCompleted(string buildId, string jobId, Node2D placed, int x, int y)
        {
            if (!_sitesByJob.TryGetValue(jobId, out SiteVisual? site))
                return;

            PushState(site, "complete");
            PushProgress(site, 1f);
            site.TeardownRemaining = EffectiveTeardownSeconds;
            if (site.TeardownRemaining <= 0f)
                RemoveSite(site);
        }

        private void OnBuildSiteCancelled(string buildId, string jobId, int x, int y)
        {
            SiteVisual? site = null;
            if (!string.IsNullOrEmpty(jobId))
                _sitesByJob.TryGetValue(jobId, out site);
            if (site == null)
            {
                var cell = new Vector2I(x, y);
                foreach (SiteVisual candidate in _sitesByPlaced.Values)
                    if (candidate.Cell == cell)
                    {
                        site = candidate;
                        break;
                    }
            }
            if (site != null)
                RemoveSite(site);
        }

        // ---- site lifecycle ----------------------------------------------------

        private void CreateSite(string buildId, string jobId, Node2D placed, Vector2I cell)
        {
            if (_sitesByPlaced.ContainsKey(placed) || !GodotObject.IsInstanceValid(placed))
                return;

            ResolveReferences();
            GridBuildDefinition? build = _catalog?.FindBuild(buildId);
            if (build == null)
                return;

            Vector2I footprint = build.EffectiveFootprint;
            Vector2 cellSize = _grid?.EffectiveTileSize ?? Vector2.Zero;
            if (_grid == null && !_warnedNoGrid)
            {
                _warnedNoGrid = true;
                GD.PushWarning($"[{Name}] No GridProjectionComponent found (set GridPath) - stage scenes will be told a zero CellSize and cannot size themselves to the site.");
            }

            var site = new SiteVisual(buildId, jobId, placed, cell, build)
            {
                Storage = EntityComponent.FindComponent<GridStorageComponent>(placed, recursive: true),
                State = string.IsNullOrEmpty(jobId) ? "pending" : "queued"
            };

            // The placed node's own scene may answer the contract too - a
            // build with no ConstructionStages that still wants to present
            // its state and progress.
            if (GridConstructionVisualPorts.AnswersConstructionVisualShape(placed))
            {
                GridConstructionVisualPorts.ConfigureSite(placed, footprint, cellSize, 0, 1);
                GridConstructionVisualPorts.SetSiteState(placed, site.State);
                GridConstructionVisualPorts.SetSiteMaterials(placed, MaterialsFor(site));
                GridConstructionVisualPorts.SetBuildProgress(placed, 0f);
            }

            if (build.ConstructionStages.Count > 0)
            {
                var root = new Node2D
                {
                    Name = $"Site_{placed.Name}",
                    ZAsRelative = false,
                    ZIndex = FrontEdgeZ(placed, footprint, cellSize)
                };
                AddChild(root);
                root.GlobalPosition = placed.GlobalPosition;
                site.Root = root;

                int count = build.ConstructionStages.Count;
                Godot.Collections.Array materials = MaterialsFor(site);
                foreach (PackedScene? stageScene in build.ConstructionStages)
                {
                    Node2D? stage = stageScene?.InstantiateOrNull<Node2D>();
                    if (stage == null)
                    {
                        if (stageScene != null)
                            GD.PushWarning($"[{Name}] A ConstructionStages entry on build '{buildId}' does not root a Node2D and was skipped.");
                        continue;
                    }
                    int index = site.Stages.Count;
                    stage.Name = $"Stage_{index}";
                    stage.Visible = index == 0;
                    if (GridConstructionVisualPorts.AnswersConstructionVisualShape(stage))
                    {
                        GridConstructionVisualPorts.ConfigureSite(stage, footprint, cellSize, index, count);
                        GridConstructionVisualPorts.SetSiteState(stage, site.State);
                        GridConstructionVisualPorts.SetSiteMaterials(stage, materials);
                        GridConstructionVisualPorts.SetBuildProgress(stage, 0f);
                    }
                    root.AddChild(stage);
                    site.Stages.Add(stage);
                }
            }

            site.StatePushed = true;
            _sitesByPlaced[placed] = site;
            if (!string.IsNullOrEmpty(jobId))
                _sitesByJob[jobId] = site;
        }

        /// <summary>
        /// The site's draw order, in the absolute-y domain units use: the y
        /// of the footprint's front edge, so anything standing in front of
        /// the site sorts over it and anything behind sorts under.
        /// </summary>
        private static int FrontEdgeZ(Node2D placed, Vector2I footprint, Vector2 cellSize)
        {
            float frontEdgeY = placed.GlobalPosition.Y + (footprint.Y - 0.5f) * cellSize.Y;
            return Mathf.Clamp(Mathf.RoundToInt(frontEdgeY), -4096, 4096);
        }

        private void RefreshSites(float elapsed)
        {
            ResolveReferences();
            if (_sitesByPlaced.Count == 0)
                return;

            var finished = new List<SiteVisual>();
            foreach (SiteVisual site in _sitesByPlaced.Values)
            {
                if (!GodotObject.IsInstanceValid(site.Placed))
                {
                    finished.Add(site);
                    continue;
                }

                if (site.Root != null && GodotObject.IsInstanceValid(site.Root))
                    site.Root.GlobalPosition = site.Placed.GlobalPosition;

                if (site.State == "complete")
                {
                    site.TeardownRemaining -= elapsed;
                    if (site.TeardownRemaining <= 0f)
                        finished.Add(site);
                    continue;
                }

                if (string.IsNullOrEmpty(site.JobId))
                {
                    PushState(site, "pending");
                    PushMaterials(site);
                    continue;
                }

                if (_jobs == null)
                    continue;

                switch (_jobs.GetJobState(site.JobId))
                {
                    case GridJobQueueComponent.GridJobState.Queued:
                        PushState(site, "queued");
                        break;
                    case GridJobQueueComponent.GridJobState.Claimed:
                        PushState(site, "working");
                        break;
                    default:
                        // Completed and Cancelled arrive as site signals;
                        // a job the queue no longer knows is one of those.
                        continue;
                }

                float progress = _jobs.GetJobProgress01(site.JobId);
                bool advanced = progress > site.LastProgress + 0.0001f;
                PushProgress(site, progress);

                if (site.State == "working" && advanced)
                {
                    site.PulseAccumulator += elapsed;
                    if (site.PulseAccumulator >= EffectiveWorkPulseInterval)
                    {
                        site.PulseAccumulator = 0f;
                        Pulse(site);
                    }
                }
                site.LastProgress = progress;
            }

            foreach (SiteVisual site in finished)
                RemoveSite(site);
        }

        private void PushState(SiteVisual site, string state)
        {
            if (site.State == state && site.StatePushed)
                return;
            site.State = state;
            site.StatePushed = true;
            if (GodotObject.IsInstanceValid(site.Placed) && GridConstructionVisualPorts.AnswersConstructionVisualShape(site.Placed))
                GridConstructionVisualPorts.SetSiteState(site.Placed, state);
            foreach (Node2D stage in site.Stages)
                if (GodotObject.IsInstanceValid(stage) && GridConstructionVisualPorts.AnswersConstructionVisualShape(stage))
                    GridConstructionVisualPorts.SetSiteState(stage, state);
        }

        private void PushMaterials(SiteVisual site)
        {
            Godot.Collections.Array materials = MaterialsFor(site);
            if (GodotObject.IsInstanceValid(site.Placed) && GridConstructionVisualPorts.AnswersConstructionVisualShape(site.Placed))
                GridConstructionVisualPorts.SetSiteMaterials(site.Placed, materials);
            foreach (Node2D stage in site.Stages)
                if (GodotObject.IsInstanceValid(stage) && GridConstructionVisualPorts.AnswersConstructionVisualShape(stage))
                    GridConstructionVisualPorts.SetSiteMaterials(stage, materials);
        }

        private void PushProgress(SiteVisual site, float progress)
        {
            if (GodotObject.IsInstanceValid(site.Placed) && GridConstructionVisualPorts.AnswersConstructionVisualShape(site.Placed))
                GridConstructionVisualPorts.SetBuildProgress(site.Placed, progress);

            int count = site.Stages.Count;
            if (count == 0)
                return;

            int visibleIndex = Mathf.Clamp((int)(progress * count), 0, count - 1);
            for (int i = 0; i < count; i++)
            {
                Node2D stage = site.Stages[i];
                if (!GodotObject.IsInstanceValid(stage))
                    continue;
                stage.Visible = i == visibleIndex;
                if (GridConstructionVisualPorts.AnswersConstructionVisualShape(stage))
                    GridConstructionVisualPorts.SetBuildProgress(stage, progress);
            }
        }

        private void Pulse(SiteVisual site)
        {
            if (GodotObject.IsInstanceValid(site.Placed) && GridConstructionVisualPorts.AnswersConstructionVisualShape(site.Placed))
                GridConstructionVisualPorts.WorkPulse(site.Placed);
            foreach (Node2D stage in site.Stages)
                if (GodotObject.IsInstanceValid(stage) && stage.Visible && GridConstructionVisualPorts.AnswersConstructionVisualShape(stage))
                    GridConstructionVisualPorts.WorkPulse(stage);
        }

        /// <summary>
        /// The physical stock on site, from RequiredMaterials - never Costs,
        /// which is wallet currency deducted at placement. While pending,
        /// delivered is what the site's storage holds; from the job's start
        /// the stock has been built in (GridBuildSiteComponent unloads it in
        /// one shot), so it reads as fully delivered and the scene shows it
        /// being used up by BuildProgress.
        /// </summary>
        private static Godot.Collections.Array MaterialsFor(SiteVisual site)
        {
            var result = new Godot.Collections.Array();
            bool pending = string.IsNullOrEmpty(site.JobId);
            foreach ((string resourceId, int amount) in GridResourceAmount.Enumerate(site.Build.RequiredMaterials))
            {
                int delivered = pending && site.Storage != null && GodotObject.IsInstanceValid(site.Storage)
                    ? Mathf.Clamp(site.Storage.Stored(resourceId), 0, amount)
                    : amount;
                result.Add(new Godot.Collections.Dictionary
                {
                    ["id"] = resourceId,
                    ["required"] = amount,
                    ["delivered"] = delivered
                });
            }
            return result;
        }

        private void RemoveSite(SiteVisual site)
        {
            _sitesByPlaced.Remove(site.Placed);
            if (!string.IsNullOrEmpty(site.JobId))
                _sitesByJob.Remove(site.JobId);
            FreeSite(site);
        }

        private static void FreeSite(SiteVisual site)
        {
            foreach (Node2D stage in site.Stages)
                if (GodotObject.IsInstanceValid(stage))
                    stage.QueueFree();
            site.Stages.Clear();
            if (site.Root != null && GodotObject.IsInstanceValid(site.Root))
                site.Root.QueueFree();
            site.Root = null;
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

            EntityComponent.Resolve(this, BuildCatalogPath, ref _catalog);
            EntityComponent.Resolve(this, JobQueuePath, ref _jobs);
            EntityComponent.Resolve(this, GridPath, ref _grid);
        }

        private sealed class SiteVisual
        {
            public SiteVisual(string buildId, string jobId, Node2D placed, Vector2I cell, GridBuildDefinition build)
            {
                BuildId = buildId;
                JobId = jobId;
                Placed = placed;
                Cell = cell;
                Build = build;
            }

            public string BuildId { get; }
            public string JobId { get; set; }
            public Node2D Placed { get; }
            public Vector2I Cell { get; }
            public GridBuildDefinition Build { get; }
            public GridStorageComponent? Storage { get; set; }
            public Node2D? Root { get; set; }
            public List<Node2D> Stages { get; } = new();
            public string State { get; set; } = "pending";
            public bool StatePushed { get; set; }
            public float LastProgress { get; set; }
            public float PulseAccumulator { get; set; }
            public float TeardownRemaining { get; set; } = -1f;
        }
    }
}

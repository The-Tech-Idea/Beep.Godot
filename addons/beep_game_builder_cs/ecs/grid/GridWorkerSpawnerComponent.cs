using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Spawns worker, truck, or NPC units from a base/building and wires them to
    /// the reusable grid navigation and job systems.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridWorkerSpawnerComponent : Node
    {
        [Signal] public delegate void UnitSpawnedEventHandler(Node unit, string workerId, int x, int y);
        [Signal] public delegate void SpawnRejectedEventHandler(string reason);

        [Export] public PackedScene? UnitScene { get; set; }
        [Export] public ActorDefinition? UnitDefinition { get; set; }
        [Export] public string OwnerId { get; set; } = "";
        [Export] public NodePath ActorRegistryPath { get; set; } = new("");
        [Export] public NodePath UnitsRootPath { get; set; } = new("");
        [Export] public NodePath GridPath { get; set; } = new("");
        [Export] public NodePath NavigationPath { get; set; } = new("");
        [Export] public NodePath JobQueuePath { get; set; } = new("");
        [Export] public NodePath CellDataPath { get; set; } = new("");
        [Export] public NodePath PlacementPath { get; set; } = new("");
        [Export] public Vector2I SpawnCell { get; set; } = Vector2I.Zero;
        /// <summary>
        /// Spawn in the active player's generated start area instead of at SpawnCell: each unit
        /// takes the nearest walkable area cell no spawned unit stands on
        /// (GridStartAreaComponent.SpawnCellsFor). Needs StartAreaPath and GridPath.
        /// </summary>
        [Export] public bool SpawnAtStartArea { get; set; }
        [Export] public NodePath StartAreaPath { get; set; } = new("");
        [Export] public string WorkerIdPrefix { get; set; } = "worker";
        [Export] public bool AutoSpawnOnReady { get; set; } = false;
        [Export(PropertyHint.Range, "0,32,1")] public int InitialWorkers { get; set; } = 1;
        [Export(PropertyHint.Range, "1,128,1")] public int MaxWorkers { get; set; } = 8;
        [Export(PropertyHint.Range, "8,256,1")] public float DefaultUnitSpeed { get; set; } = 140f;
        [Export] public bool DriveCharacterBody { get; set; } = true;
        [Export] public bool SetZIndexFromY { get; set; } = true;
        [Export] public bool TreatCellDataBlockedAsUnspawnable { get; set; } = true;
        [Export] public bool TreatBlockedTerrainKindsAsUnspawnable { get; set; } = true;
        [Export] public bool TreatPlacementOccupiedAsUnspawnable { get; set; } = true;
        [Export] public Godot.Collections.Array<string> BlockedTerrainKinds { get; set; }
            = GridTerrainRules.DefaultBlockedTerrainKinds();
        [Export] public Godot.Collections.Array<string> AllowedTerrainKinds { get; set; } = new();

        private readonly List<Node2D> _spawnedUnits = new();
        private Node? _unitsRoot;
        private GridProjectionComponent? _grid;
        private GridNavigationComponent? _navigation;
        private GridJobQueueComponent? _jobs;
        private GridCellDataComponent? _cellData;
        private GridPlacementComponent? _placement;
        private GridStartAreaComponent? _startArea;
        private int _nextWorkerNumber = 1;

        public int EffectiveMaxWorkers => Mathf.Max(1, MaxWorkers);
        public int EffectiveInitialWorkers => Mathf.Clamp(InitialWorkers, 0, EffectiveMaxWorkers);
        public float EffectiveDefaultUnitSpeed => Mathf.Max(0f, float.IsFinite(DefaultUnitSpeed) ? DefaultUnitSpeed : 140f);

        public int SpawnedCount
        {
            get
            {
                PruneFreedUnits();
                return _spawnedUnits.Count;
            }
        }

        public override void _Ready()
        {
            ResolveReferences();
            if (!Engine.IsEditorHint() && AutoSpawnOnReady)
            {
                GameStateManagerComponent.AfterWorldReady(this, () =>
                {
                    int count = EffectiveInitialWorkers;
                    for (int i = 0; i < count; i++)
                    {
                        if (SpawnAtStartArea) SpawnWorker();
                        else SpawnWorker(SpawnCell + new Vector2I(i, 0));
                    }
                });
            }

            UpdateConfigurationWarnings();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (MaxWorkers <= 0)
                return new[] { "MaxWorkers must be greater than zero." };

            if (InitialWorkers < 0)
                return new[] { "InitialWorkers cannot be negative." };

            if (SpawnAtStartArea && StartAreaPath.IsEmpty)
                return new[] { "SpawnAtStartArea needs StartAreaPath to point to a GridStartAreaComponent." };

            return Array.Empty<string>();
        }

        /// <summary>Spawns at SpawnCell, or - with SpawnAtStartArea - in the active player's start area.</summary>
        public Node2D? SpawnWorker()
        {
            if (!SpawnAtStartArea)
                return SpawnWorker(SpawnCell);

            ResolveReferences();
            PruneFreedUnits();
            if (_startArea is null || _grid is null)
                return Reject("missing_start_area_or_grid");

            // Every cell a live spawned unit stands on, so the next one does not stack on it.
            // The candidates are all of the area's walkable cells: asking for fewer could hand
            // back only cells the existing units already hold.
            var standing = new HashSet<Vector2I>();
            foreach (Node2D unit in _spawnedUnits)
                standing.Add(_grid.WorldToCell(unit.GlobalPosition));

            int start = StartIndexForOwner();
            if (start < 0)
                return Reject("owner_has_no_start");

            foreach (Vector2I cell in _startArea.SpawnCellsFor(start, int.MaxValue))
                if (!standing.Contains(cell) && SpawnBlockReason(cell) is null)
                    return SpawnWorker(cell);

            return Reject("no_start_area_spawn_cell");
        }

        /// <summary>
        /// The start THIS spawner's units belong in: its owner's, resolved through the registry's
        /// player to the faction the assignment holds. A map with a spawner per player therefore
        /// puts each player's units in their own area from one scene, where reading the start
        /// component's ActiveStartIndex would have put every player in the local player's.
        ///
        /// Falls through to ActiveStartIndex when there is no catalog, no registry or no owner -
        /// a single-player sandbox has one start and one player, and that is what it means.
        /// -1 only when the owner is a real player whose faction was assigned nothing: refusing to
        /// spawn is the honest answer, because any other area belongs to somebody else.
        /// </summary>
        private int StartIndexForOwner()
        {
            if (_startArea!.FactionCatalog is null || OwnerId.Length == 0)
                return _startArea.ActiveStartIndex;

            var registry = ActorRegistryPath.IsEmpty ? null : GetNodeOrNull<ActorRegistryComponent>(ActorRegistryPath);
            if (registry?.FindPlayer(OwnerId) is not { } player)
                return _startArea.ActiveStartIndex;

            return _startArea.StartIndexOf(player.FactionId);
        }

        public Node2D? SpawnWorker(Vector2I cell)
        {
            ResolveReferences();
            PruneFreedUnits();

            if (_grid == null || _navigation == null || _jobs == null)
                return Reject("missing_grid_navigation_or_jobs");

            if (_unitsRoot == null)
                return Reject("missing_units_root");

            if (_spawnedUnits.Count >= EffectiveMaxWorkers)
                return Reject("max_workers_reached");

            string? spawnBlockReason = SpawnBlockReason(cell);
            if (spawnBlockReason != null)
                return Reject(spawnBlockReason);

            Node2D? unit = CreateUnitNode();
            if (unit == null)
                return Reject("unit_scene_must_instantiate_node2d");

            string workerId = NextWorkerId();
            unit.Name = UniqueUnitName(workerId);
            if (ActorComponent.ForBody(unit) is { } actor)
            {
                var registry = ActorRegistryPath.IsEmpty ? null : GetNodeOrNull<ActorRegistryComponent>(ActorRegistryPath);
                if (registry is null || registry.FindPlayer(OwnerId) is null)
                {
                    unit.Free();
                    return Reject("missing_actor_registry_or_owner");
                }
                actor.ActorId = "";
                actor.OwnerId = OwnerId;
                actor.RegistryPath = registry.GetPath();
                actor.Definition = UnitDefinition ?? actor.Definition;
                if (actor.Definition is null)
                {
                    unit.Free();
                    return Reject("missing_actor_definition");
                }
            }

            GridPathFollowerComponent follower = EnsurePathFollower(unit);
            GridWorkerComponent worker = EnsureWorker(unit, workerId, follower);

            follower.GridPath = _grid.GetPath();
            follower.NavigationPath = _navigation.GetPath();
            follower.Speed = EffectiveDefaultUnitSpeed;
            follower.DriveCharacterBody = DriveCharacterBody;
            follower.SetZIndexFromY = SetZIndexFromY;

            worker.JobQueuePath = _jobs.GetPath();
            worker.GridPath = _grid.GetPath();
            worker.PathFollowerPath = new NodePath("../" + follower.Name);
            worker.WorkerId = workerId;

            Vector2 spawnPosition = _grid.CellToWorld(cell);
            unit.Position = _unitsRoot is Node2D units2D ? units2D.ToLocal(spawnPosition) : spawnPosition;
            _unitsRoot.AddChild(unit);
            if (ActorComponent.ForBody(unit) is { } registered)
                workerId = worker.WorkerId = registered.ActorId;

            _spawnedUnits.Add(unit);
            EmitSignal(SignalName.UnitSpawned, unit, workerId, cell.X, cell.Y);
            return unit;
        }

        public Godot.Collections.Array<Node> GetSpawnedUnits()
        {
            PruneFreedUnits();
            var units = new Godot.Collections.Array<Node>();
            foreach (Node2D unit in _spawnedUnits)
                units.Add(unit);
            return units;
        }

        public bool CanSpawnAt(Vector2I cell)
        {
            ResolveReferences();
            PruneFreedUnits();

            return _grid != null
                && _navigation != null
                && _jobs != null
                && _unitsRoot != null
                && _spawnedUnits.Count < EffectiveMaxWorkers
                && SpawnBlockReason(cell) == null;
        }

        private Node2D? CreateUnitNode()
        {
            var scene = UnitDefinition?.Scene ?? UnitScene;
            if (scene != null)
            {
                var instance = scene.Instantiate();
                if (instance is Node2D body2D) return body2D;
                instance.Free();
                return null;
            }

            var body = new CharacterBody2D();
            body.AddChild(new Polygon2D
            {
                Name = "Body",
                Color = new Color(0.24f, 0.58f, 0.92f, 1f),
                Polygon = new[]
                {
                    new Vector2(0, -14),
                    new Vector2(12, -5),
                    new Vector2(10, 12),
                    new Vector2(-10, 12),
                    new Vector2(-12, -5)
                }
            });
            body.AddChild(new CollisionShape2D
            {
                Name = "CollisionShape2D",
                Shape = new RectangleShape2D { Size = new Vector2(24, 24) }
            });
            return body;
        }

        private GridPathFollowerComponent EnsurePathFollower(Node2D unit)
        {
            GridPathFollowerComponent? follower = EntityComponent.FindComponent<GridPathFollowerComponent>(unit, recursive: false);
            if (follower != null)
                return follower;

            follower = new GridPathFollowerComponent { Name = "PathFollower" };
            unit.AddChild(follower);
            return follower;
        }

        private GridWorkerComponent EnsureWorker(Node2D unit, string workerId, GridPathFollowerComponent follower)
        {
            GridWorkerComponent? worker = EntityComponent.FindComponent<GridWorkerComponent>(unit, recursive: false);
            if (worker != null)
                return worker;

            worker = new GridWorkerComponent
            {
                Name = "GridWorker",
                WorkerId = workerId
            };
            unit.AddChild(worker);
            return worker;
        }

        private string NextWorkerId()
        {
            string prefix = GridIds.NodeName(WorkerIdPrefix, "worker");
            return $"{prefix}_{_nextWorkerNumber++}";
        }

        private string UniqueUnitName(string workerId)
        {
            string baseName = string.IsNullOrWhiteSpace(workerId) ? "Worker" : workerId;
            if (_unitsRoot == null || !_unitsRoot.HasNode(baseName))
                return baseName;

            int suffix = 2;
            while (_unitsRoot.HasNode($"{baseName}_{suffix}"))
                suffix++;

            return $"{baseName}_{suffix}";
        }

        private Node2D? Reject(string reason)
        {
            EmitSignal(SignalName.SpawnRejected, reason);
            return null;
        }

        private string? SpawnBlockReason(Vector2I cell)
        {
            if (_navigation != null)
            {
                if (!_navigation.IsInBounds(cell))
                    return "spawn_cell_out_of_bounds";

                if (_navigation.IsBlocked(cell))
                    return "blocked_spawn_cell";
            }

            if (_placement != null
                && TreatPlacementOccupiedAsUnspawnable
                && _placement.IsOccupied(cell))
                return "occupied_spawn_cell";

            if (_cellData == null)
                return null;

            if (TreatCellDataBlockedAsUnspawnable
                && _cellData.HasFlag(cell, GridCellDataComponent.CellFlags.Blocked))
                return "blocked_spawn_cell";

            // GridTerrainRules.Normalize also replaces spaces and dashes - the
            // private normalizer this replaced had quietly forgotten that, so
            // this component alone treated "Deep Water" and "deep_water" as
            // different kinds.
            string terrainKind = GridTerrainRules.Normalize(_cellData.GetTerrainKind(cell));
            if (!GridTerrainRules.IsAllowed(terrainKind, AllowedTerrainKinds))
                return "unspawnable_terrain";

            if (TreatBlockedTerrainKindsAsUnspawnable
                && GridTerrainRules.MatchesAny(terrainKind, BlockedTerrainKinds))
                return "unspawnable_terrain";

            return null;
        }

        private void PruneFreedUnits()
        {
            for (int i = _spawnedUnits.Count - 1; i >= 0; i--)
            {
                if (!GodotObject.IsInstanceValid(_spawnedUnits[i]))
                    _spawnedUnits.RemoveAt(i);
            }
        }

        private void ResolveReferences()
        {
            if (_unitsRoot == null || !GodotObject.IsInstanceValid(_unitsRoot))
                _unitsRoot = !UnitsRootPath.IsEmpty
                    ? GetNodeOrNull<Node>(UnitsRootPath)
                    : GetParent();

            EntityComponent.Resolve(this, GridPath, ref _grid);
            EntityComponent.Resolve(this, NavigationPath, ref _navigation);
            EntityComponent.Resolve(this, JobQueuePath, ref _jobs);
            EntityComponent.Resolve(this, CellDataPath, ref _cellData);
            EntityComponent.Resolve(this, PlacementPath, ref _placement);
            EntityComponent.Resolve(this, StartAreaPath, ref _startArea);
        }

    }
}

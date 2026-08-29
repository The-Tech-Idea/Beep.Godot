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
        [Export] public NodePath UnitsRootPath { get; set; } = new("");
        [Export] public NodePath GridPath { get; set; } = new("");
        [Export] public NodePath NavigationPath { get; set; } = new("");
        [Export] public NodePath JobQueuePath { get; set; } = new("");
        [Export] public NodePath CellDataPath { get; set; } = new("");
        [Export] public NodePath PlacementPath { get; set; } = new("");
        [Export] public Vector2I SpawnCell { get; set; } = Vector2I.Zero;
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
        [Export] public Godot.Collections.Array<string> BlockedTerrainKinds { get; set; } = new()
        {
            "water",
            "sea",
            "ocean",
            "deep_water",
            "lava"
        };
        [Export] public Godot.Collections.Array<string> AllowedTerrainKinds { get; set; } = new();

        private readonly List<Node2D> _spawnedUnits = new();
        private Node? _unitsRoot;
        private GridProjectionComponent? _grid;
        private GridNavigationComponent? _navigation;
        private GridJobQueueComponent? _jobs;
        private GridCellDataComponent? _cellData;
        private GridPlacementComponent? _placement;
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
                int count = EffectiveInitialWorkers;
                for (int i = 0; i < count; i++)
                    SpawnWorker(SpawnCell + new Vector2I(i, 0));
            }

            UpdateConfigurationWarnings();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (MaxWorkers <= 0)
                return new[] { "MaxWorkers must be greater than zero." };

            if (InitialWorkers < 0)
                return new[] { "InitialWorkers cannot be negative." };

            return Array.Empty<string>();
        }

        public Node2D? SpawnWorker()
        {
            return SpawnWorker(SpawnCell);
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
            _unitsRoot.AddChild(unit);
            unit.GlobalPosition = _grid.CellToWorld(cell);

            GridPathFollowerComponent follower = EnsurePathFollower(unit);
            GridWorkerComponent worker = EnsureWorker(unit, workerId, follower);

            follower.GridPath = follower.GetPathTo(_grid);
            follower.NavigationPath = follower.GetPathTo(_navigation);
            follower.Speed = EffectiveDefaultUnitSpeed;
            follower.DriveCharacterBody = DriveCharacterBody;
            follower.SetZIndexFromY = SetZIndexFromY;

            worker.JobQueuePath = worker.GetPathTo(_jobs);
            worker.GridPath = worker.GetPathTo(_grid);
            worker.PathFollowerPath = worker.GetPathTo(follower);
            worker.WorkerId = workerId;

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
            if (UnitScene != null)
                return UnitScene.Instantiate() as Node2D;

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
            string prefix = SafeName(WorkerIdPrefix);
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

        private static string SafeName(string value)
        {
            string safe = string.IsNullOrWhiteSpace(value) ? "worker" : value.Trim().ToLowerInvariant().Replace(' ', '_');
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                safe = safe.Replace(c, '_');
            return safe.Replace('/', '_').Replace('\\', '_');
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

            string terrainKind = NormalizeTerrainKind(_cellData.GetTerrainKind(cell));
            if (AllowedTerrainKinds.Count > 0)
            {
                bool allowed = false;
                foreach (string allowedKind in AllowedTerrainKinds)
                {
                    if (NormalizeTerrainKind(allowedKind) == terrainKind)
                    {
                        allowed = true;
                        break;
                    }
                }

                if (!allowed)
                    return "unspawnable_terrain";
            }

            if (TreatBlockedTerrainKindsAsUnspawnable)
            {
                foreach (string blockedKind in BlockedTerrainKinds)
                    if (NormalizeTerrainKind(blockedKind) == terrainKind)
                        return "unspawnable_terrain";
            }

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

            if (_grid == null || !GodotObject.IsInstanceValid(_grid))
                _grid = !GridPath.IsEmpty
                    ? GetNodeOrNull<GridProjectionComponent>(GridPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridProjectionComponent>(GetTree()?.CurrentScene) : null;

            if (_navigation == null || !GodotObject.IsInstanceValid(_navigation))
                _navigation = !NavigationPath.IsEmpty
                    ? GetNodeOrNull<GridNavigationComponent>(NavigationPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridNavigationComponent>(GetTree()?.CurrentScene) : null;

            if (_jobs == null || !GodotObject.IsInstanceValid(_jobs))
                _jobs = !JobQueuePath.IsEmpty
                    ? GetNodeOrNull<GridJobQueueComponent>(JobQueuePath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridJobQueueComponent>(GetTree()?.CurrentScene) : null;

            if (_cellData == null || !GodotObject.IsInstanceValid(_cellData))
                _cellData = !CellDataPath.IsEmpty
                    ? GetNodeOrNull<GridCellDataComponent>(CellDataPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridCellDataComponent>(GetTree()?.CurrentScene) : null;

            if (_placement == null || !GodotObject.IsInstanceValid(_placement))
                _placement = !PlacementPath.IsEmpty
                    ? GetNodeOrNull<GridPlacementComponent>(PlacementPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridPlacementComponent>(GetTree()?.CurrentScene) : null;
        }

        private static string NormalizeTerrainKind(string value)
            => string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();
    }
}

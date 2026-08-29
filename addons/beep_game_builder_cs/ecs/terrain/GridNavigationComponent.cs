using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// A* navigation over a 2D grid. Works with top-down or isometric worlds by
    /// using GridProjectionComponent only for world/cell conversion; the pathing
    /// itself stays cell-based and independent of TileMap.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridNavigationComponent : Node
    {
        public enum DiagonalPolicy
        {
            Never,
            Always,
            NoCornerCutting
        }

        [Signal] public delegate void PathFoundEventHandler(int startX, int startY, int goalX, int goalY, int length);
        [Signal] public delegate void PathFailedEventHandler(int startX, int startY, int goalX, int goalY, string reason);

        [Export] public NodePath GridPath { get; set; } = new("");
        [Export] public NodePath PlacementPath { get; set; } = new("");
        [Export] public NodePath RoadPath { get; set; } = new("");
        [Export] public NodePath CellDataPath { get; set; } = new("");
        [Export] public bool UseBounds { get; set; } = true;
        [Export] public Vector2I BoundsOrigin { get; set; } = Vector2I.Zero;
        [Export] public Vector2I BoundsSize { get; set; } = new(64, 64);
        [Export] public DiagonalPolicy Diagonals { get; set; } = DiagonalPolicy.NoCornerCutting;
        [Export] public bool TreatPlacementOccupiedAsBlocked { get; set; } = true;
        [Export] public bool TreatCellDataBlockedAsBlocked { get; set; } = true;
        [Export] public bool TreatBlockedTerrainKindsAsBlocked { get; set; } = true;
        [Export] public Godot.Collections.Array<string> BlockedTerrainKinds { get; set; } = new()
        {
            "water",
            "sea",
            "ocean",
            "deep_water",
            "lava"
        };
        [Export] public Godot.Collections.Dictionary TerrainCostMultipliers { get; set; } = new()
        {
            ["sand"] = 1.15f,
            ["desert"] = 1.25f,
            ["mud"] = 1.8f,
            ["swamp"] = 2.1f,
            ["snow"] = 1.35f,
            ["ice"] = 1.2f,
            ["rock"] = 1.4f,
            ["stone"] = 1.2f,
            ["shallow_water"] = 2.5f
        };
        [Export] public bool AllowBlockedStart { get; set; } = true;
        [Export] public bool AllowBlockedGoal { get; set; } = false;
        [Export(PropertyHint.Range, "16,200000,1")] public int MaxVisitedCells { get; set; } = 10000;

        private readonly HashSet<Vector2I> _blocked = new();
        private GridProjectionComponent? _grid;
        private GridPlacementComponent? _placement;
        private GridRoadComponent? _roads;
        private GridCellDataComponent? _cellData;

        public override void _Ready()
        {
            ResolveReferences();
            UpdateConfigurationWarnings();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (UseBounds && (BoundsSize.X <= 0 || BoundsSize.Y <= 0))
                return new[] { "BoundsSize must be greater than zero when UseBounds is enabled." };

            return System.Array.Empty<string>();
        }

        public Godot.Collections.Array<Vector2I> FindCellPath(Vector2I start, Vector2I goal)
        {
            ResolveReferences();

            if (!IsCellAllowed(start, AllowBlockedStart))
                return Fail(start, goal, "start_blocked_or_out_of_bounds");

            if (!IsCellAllowed(goal, AllowBlockedGoal))
                return Fail(start, goal, "goal_blocked_or_out_of_bounds");

            var open = new PriorityQueue<Vector2I, float>();
            var cameFrom = new Dictionary<Vector2I, Vector2I>();
            var bestCost = new Dictionary<Vector2I, float> { [start] = 0f };
            var closed = new HashSet<Vector2I>();
            open.Enqueue(start, Heuristic(start, goal));

            int visited = 0;
            while (open.Count > 0)
            {
                Vector2I current = open.Dequeue();
                if (!closed.Add(current))
                    continue;

                if (++visited > MaxVisitedCells)
                    return Fail(start, goal, "max_visited_cells");

                if (current == goal)
                    return Succeed(start, goal, Reconstruct(cameFrom, current));

                foreach (Vector2I next in Neighbors(current, goal))
                {
                    if (closed.Contains(next))
                        continue;

                    float nextCost = bestCost[current] + StepCost(current, next);
                    if (bestCost.TryGetValue(next, out float oldCost) && nextCost >= oldCost)
                        continue;

                    bestCost[next] = nextCost;
                    cameFrom[next] = current;
                    open.Enqueue(next, nextCost + Heuristic(next, goal));
                }
            }

            return Fail(start, goal, "no_path");
        }

        public Godot.Collections.Array<Vector2> FindWorldPath(Vector2 startWorld, Vector2 goalWorld)
        {
            ResolveReferences();
            var points = new Godot.Collections.Array<Vector2>();
            if (_grid == null)
                return points;

            var cells = FindCellPath(_grid.WorldToCell(startWorld), _grid.WorldToCell(goalWorld));
            foreach (Vector2I cell in cells)
                points.Add(_grid.CellToWorld(cell));
            return points;
        }

        public float TraversalCost(Vector2I from, Vector2I to)
        {
            float baseCost = BaseStepCost(from, to);
            ResolveReferences();
            float terrainCost = TerrainCostMultiplier(to);
            float roadCost = Mathf.Clamp(_roads?.GetTraversalCostMultiplier(to) ?? 1f, 0.05f, 10f);
            return baseCost * terrainCost * roadCost;
        }

        public bool IsBlocked(Vector2I cell)
        {
            if (_blocked.Contains(cell))
                return true;

            if (TreatCellDataBlockedAsBlocked || TreatBlockedTerrainKindsAsBlocked)
            {
                ResolveReferences();
                if (_cellData != null)
                {
                    if (TreatCellDataBlockedAsBlocked
                        && _cellData.HasFlag(cell, GridCellDataComponent.CellFlags.Blocked))
                        return true;

                    if (TreatBlockedTerrainKindsAsBlocked
                        && IsBlockedTerrainKind(_cellData.GetTerrainKind(cell)))
                        return true;
                }
            }

            if (TreatPlacementOccupiedAsBlocked)
            {
                ResolveReferences();
                if (_placement?.IsOccupied(cell) == true)
                    return true;
            }

            return false;
        }

        public bool IsInBounds(Vector2I cell)
        {
            if (!UseBounds)
                return true;

            return cell.X >= BoundsOrigin.X
                && cell.Y >= BoundsOrigin.Y
                && cell.X < BoundsOrigin.X + BoundsSize.X
                && cell.Y < BoundsOrigin.Y + BoundsSize.Y;
        }

        public void SetBlocked(Vector2I cell, bool blocked)
        {
            if (blocked) _blocked.Add(cell);
            else _blocked.Remove(cell);
        }

        public void ClearBlocked() => _blocked.Clear();

        public Godot.Collections.Array<Vector2I> GetBlockedCells()
        {
            var cells = new Godot.Collections.Array<Vector2I>();
            foreach (Vector2I cell in _blocked)
                cells.Add(cell);
            return cells;
        }

        private void ResolveReferences()
        {
            if (!GridPath.IsEmpty)
                _grid = GetNodeOrNull<GridProjectionComponent>(GridPath);

            if (!PlacementPath.IsEmpty)
                _placement = GetNodeOrNull<GridPlacementComponent>(PlacementPath);
            else if ((_placement == null || !GodotObject.IsInstanceValid(_placement)) && IsInsideTree())
                _placement = EntityComponent.FindComponent<GridPlacementComponent>(GetTree()?.CurrentScene);

            if (!RoadPath.IsEmpty)
                _roads = GetNodeOrNull<GridRoadComponent>(RoadPath);
            else if ((_roads == null || !GodotObject.IsInstanceValid(_roads)) && IsInsideTree())
                _roads = EntityComponent.FindComponent<GridRoadComponent>(GetTree()?.CurrentScene);

            if (!CellDataPath.IsEmpty)
                _cellData = GetNodeOrNull<GridCellDataComponent>(CellDataPath);
            else if ((_cellData == null || !GodotObject.IsInstanceValid(_cellData)) && IsInsideTree())
                _cellData = EntityComponent.FindComponent<GridCellDataComponent>(GetTree()?.CurrentScene);
        }

        private bool IsCellAllowed(Vector2I cell, bool allowBlocked)
        {
            if (!IsInBounds(cell))
                return false;

            return allowBlocked || !IsBlocked(cell);
        }

        private IEnumerable<Vector2I> Neighbors(Vector2I current, Vector2I goal)
        {
            foreach (Vector2I delta in CardinalSteps)
            {
                Vector2I next = current + delta;
                if (next == goal ? IsCellAllowed(next, AllowBlockedGoal) : IsCellAllowed(next, allowBlocked: false))
                    yield return next;
            }

            if (Diagonals == DiagonalPolicy.Never)
                yield break;

            foreach (Vector2I delta in DiagonalSteps)
            {
                Vector2I next = current + delta;
                if (!(next == goal ? IsCellAllowed(next, AllowBlockedGoal) : IsCellAllowed(next, allowBlocked: false)))
                    continue;

                if (Diagonals == DiagonalPolicy.NoCornerCutting)
                {
                    Vector2I sideA = current + new Vector2I(delta.X, 0);
                    Vector2I sideB = current + new Vector2I(0, delta.Y);
                    if (!IsCellAllowed(sideA, allowBlocked: false) || !IsCellAllowed(sideB, allowBlocked: false))
                        continue;
                }

                yield return next;
            }
        }

        private float Heuristic(Vector2I a, Vector2I b)
        {
            int dx = Mathf.Abs(a.X - b.X);
            int dy = Mathf.Abs(a.Y - b.Y);
            float minCost = Mathf.Clamp((_roads?.MinimumCostMultiplier ?? 1f) * MinimumTerrainCostMultiplier(), 0.05f, 1f);

            if (Diagonals == DiagonalPolicy.Never)
                return (dx + dy) * minCost;

            int diagonal = Mathf.Min(dx, dy);
            int straight = Mathf.Max(dx, dy) - diagonal;
            return (diagonal * 1.41421356f + straight) * minCost;
        }

        private float StepCost(Vector2I a, Vector2I b)
            => TraversalCost(a, b);

        private static float BaseStepCost(Vector2I a, Vector2I b)
            => a.X != b.X && a.Y != b.Y ? 1.41421356f : 1f;

        private float TerrainCostMultiplier(Vector2I cell)
        {
            ResolveReferences();
            if (_cellData == null)
                return 1f;

            string terrainKind = NormalizeTerrainKind(_cellData.GetTerrainKind(cell));
            if (string.IsNullOrEmpty(terrainKind) || !TerrainCostMultipliers.ContainsKey(terrainKind))
                return 1f;

            return Mathf.Clamp(GridVariantReader.Float(TerrainCostMultipliers[terrainKind], 1f), 0.05f, 10f);
        }

        private float MinimumTerrainCostMultiplier()
        {
            if (TerrainCostMultipliers.Count == 0)
                return 1f;

            float min = 1f;
            foreach (Variant value in TerrainCostMultipliers.Values)
                min = Mathf.Min(min, Mathf.Clamp(GridVariantReader.Float(value, 1f), 0.05f, 10f));

            return min;
        }

        private bool IsBlockedTerrainKind(string terrainKind)
        {
            string normalized = NormalizeTerrainKind(terrainKind);
            if (string.IsNullOrEmpty(normalized))
                return false;

            foreach (string blocked in BlockedTerrainKinds)
                if (NormalizeTerrainKind(blocked) == normalized)
                    return true;

            return false;
        }

        private static string NormalizeTerrainKind(string value)
            => string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');

        private Godot.Collections.Array<Vector2I> Reconstruct(Dictionary<Vector2I, Vector2I> cameFrom, Vector2I current)
        {
            var reversed = new List<Vector2I> { current };
            while (cameFrom.TryGetValue(current, out Vector2I previous))
            {
                current = previous;
                reversed.Add(current);
            }

            reversed.Reverse();
            var path = new Godot.Collections.Array<Vector2I>();
            foreach (Vector2I cell in reversed)
                path.Add(cell);
            return path;
        }

        private Godot.Collections.Array<Vector2I> Succeed(Vector2I start, Vector2I goal, Godot.Collections.Array<Vector2I> path)
        {
            EmitSignal(SignalName.PathFound, start.X, start.Y, goal.X, goal.Y, path.Count);
            return path;
        }

        private Godot.Collections.Array<Vector2I> Fail(Vector2I start, Vector2I goal, string reason)
        {
            EmitSignal(SignalName.PathFailed, start.X, start.Y, goal.X, goal.Y, reason);
            return new Godot.Collections.Array<Vector2I>();
        }

        private static readonly Vector2I[] CardinalSteps =
        {
            Vector2I.Right,
            Vector2I.Down,
            Vector2I.Left,
            Vector2I.Up
        };

        private static readonly Vector2I[] DiagonalSteps =
        {
            new(1, 1),
            new(-1, 1),
            new(-1, -1),
            new(1, -1)
        };
    }
}

using Godot;
using System;
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
        /// <summary>
        /// Optional bridge to the terrain engine: point this at a
        /// TerrainDataLayersComponent and terrain kinds are read from the
        /// generated map's data layers, with GridCellDataComponent as the
        /// fallback for cells the layers do not cover. Deliberately explicit -
        /// never found scene-wide - so a scene that has both systems does not
        /// silently switch its source of truth.
        /// </summary>
        [Export] public NodePath DataLayersPath { get; set; } = new("");
        [Export] public bool UseBounds { get; set; } = true;
        [Export] public Vector2I BoundsOrigin { get; set; } = Vector2I.Zero;
        [Export] public Vector2I BoundsSize { get; set; } = new(64, 64);
        [Export] public DiagonalPolicy Diagonals { get; set; } = DiagonalPolicy.NoCornerCutting;
        [Export] public bool TreatPlacementOccupiedAsBlocked { get; set; } = true;
        [Export] public bool TreatCellDataBlockedAsBlocked { get; set; } = true;
        [Export] public bool TreatBlockedTerrainKindsAsBlocked { get; set; } = true;
        // shallow_water is deliberately ABSENT here, unlike the build-side
        // components (placement, roads, spawner, scatter, tools): a unit wades
        // through the shallows at the 2.5x cost below, while nothing may be
        // BUILT in them. Two different questions, two different lists.
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
        private TerrainDataLayersComponent? _dataLayers;

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

        /// <summary>
        /// One path search's working set, resolved and normalized ONCE.
        ///
        /// The per-step callbacks used to re-resolve every collaborator through
        /// GetNodeOrNull, re-normalize every entry of BlockedTerrainKinds, and
        /// probe the Variant cost dictionary for EVERY cell the search visited -
        /// tens of thousands of native lookups and string allocations per
        /// FindCellPath. Everything a step needs is snapshotted here instead,
        /// and a cell's terrain kind is read and normalized at most once per
        /// search however many neighbours probe it.
        /// </summary>
        private sealed class Search
        {
            public GridCellDataComponent? Cells;
            public TerrainDataLayersComponent? DataLayers;
            public GridPlacementComponent? Placement;
            public GridRoadComponent? Roads;
            public HashSet<Vector2I> Blocked = new();
            public bool UseCellBlockedFlag;
            public HashSet<string>? BlockedKinds;
            public Dictionary<string, float>? Costs;
            public float MinimumStepCost = 1f;

            private readonly Dictionary<Vector2I, string> _kinds = new();

            public string KindAt(Vector2I cell)
            {
                if (_kinds.TryGetValue(cell, out string? kind))
                    return kind;

                // The generated map's data layers win when wired; cell data
                // answers for cells they do not cover (off-map, or edited).
                kind = DataLayers is null ? "" : GridTerrainRules.Normalize(DataLayers.TerrainAt(cell));
                if (kind.Length == 0 && Cells is not null)
                    kind = GridTerrainRules.Normalize(Cells.GetTerrainKind(cell));
                _kinds[cell] = kind;
                return kind;
            }

            public float CostFor(Vector2I cell)
            {
                if (Costs is null)
                    return 1f;

                return Costs.TryGetValue(KindAt(cell), out float cost) ? cost : 1f;
            }
        }

        private Search BuildSearch()
        {
            var search = new Search
            {
                Cells = _cellData,
                DataLayers = _dataLayers,
                Placement = TreatPlacementOccupiedAsBlocked ? _placement : null,
                Roads = _roads,
                Blocked = _blocked,
                UseCellBlockedFlag = TreatCellDataBlockedAsBlocked && _cellData != null,
            };

            bool hasKindSource = _cellData != null || _dataLayers != null;
            if (TreatBlockedTerrainKindsAsBlocked && hasKindSource && BlockedTerrainKinds.Count > 0)
            {
                search.BlockedKinds = new HashSet<string>(StringComparer.Ordinal);
                foreach (string kind in BlockedTerrainKinds)
                {
                    string normalized = GridTerrainRules.Normalize(kind);
                    if (normalized.Length > 0)
                        search.BlockedKinds.Add(normalized);
                }
            }

            if (hasKindSource && TerrainCostMultipliers.Count > 0)
            {
                search.Costs = new Dictionary<string, float>(StringComparer.Ordinal);
                foreach (Variant key in TerrainCostMultipliers.Keys)
                {
                    string normalized = GridTerrainRules.Normalize(key.AsString());
                    if (normalized.Length == 0)
                        continue;

                    float value = Mathf.Clamp(GridVariantReader.Float(TerrainCostMultipliers[key], 1f), 0.05f, 10f);
                    search.Costs[normalized] = value;
                }
            }

            // The admissible floor the heuristic scales by: the cheapest a step
            // could possibly be, roads and terrain combined.
            search.MinimumStepCost = Mathf.Clamp(
                (_roads?.MinimumCostMultiplier ?? 1f) * MinimumTerrainCostMultiplier(search.Costs), 0.05f, 1f);
            return search;
        }

        private static float MinimumTerrainCostMultiplier(Dictionary<string, float>? costs)
        {
            float min = 1f;
            if (costs is null)
                return min;

            foreach (float value in costs.Values)
                min = Mathf.Min(min, value);
            return min;
        }

        public Godot.Collections.Array<Vector2I> FindCellPath(Vector2I start, Vector2I goal)
        {
            ResolveReferences();
            Search search = BuildSearch();

            if (!IsCellAllowed(search, start, AllowBlockedStart))
                return Fail(start, goal, "start_blocked_or_out_of_bounds");

            if (!IsCellAllowed(search, goal, AllowBlockedGoal))
                return Fail(start, goal, "goal_blocked_or_out_of_bounds");

            var open = new PriorityQueue<Vector2I, float>();
            var cameFrom = new Dictionary<Vector2I, Vector2I>();
            var bestCost = new Dictionary<Vector2I, float> { [start] = 0f };
            var closed = new HashSet<Vector2I>();
            open.Enqueue(start, Heuristic(search, start, goal));

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

                foreach (Vector2I next in Neighbors(search, current, goal))
                {
                    if (closed.Contains(next))
                        continue;

                    float nextCost = bestCost[current] + StepCost(search, current, next);
                    if (bestCost.TryGetValue(next, out float oldCost) && nextCost >= oldCost)
                        continue;

                    bestCost[next] = nextCost;
                    cameFrom[next] = current;
                    open.Enqueue(next, nextCost + Heuristic(search, next, goal));
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

        /// <summary>One-off traversal cost between two cells, for external callers.</summary>
        public float TraversalCost(Vector2I from, Vector2I to)
        {
            ResolveReferences();
            Search search = BuildSearch();
            return StepCost(search, from, to);
        }

        /// <summary>One-off blocked query, for external callers.</summary>
        public bool IsBlocked(Vector2I cell)
        {
            ResolveReferences();
            return IsBlocked(BuildSearch(), cell);
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
            // Cached-and-valid everywhere. The explicit-path branches used to
            // re-run GetNodeOrNull on every call, which the per-step callbacks
            // then multiplied by the whole search.
            if (_grid == null || !GodotObject.IsInstanceValid(_grid))
                _grid = !GridPath.IsEmpty
                    ? GetNodeOrNull<GridProjectionComponent>(GridPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridProjectionComponent>(GetTree()?.CurrentScene) : null;

            if (_placement == null || !GodotObject.IsInstanceValid(_placement))
                _placement = !PlacementPath.IsEmpty
                    ? GetNodeOrNull<GridPlacementComponent>(PlacementPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridPlacementComponent>(GetTree()?.CurrentScene) : null;

            if (_roads == null || !GodotObject.IsInstanceValid(_roads))
                _roads = !RoadPath.IsEmpty
                    ? GetNodeOrNull<GridRoadComponent>(RoadPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridRoadComponent>(GetTree()?.CurrentScene) : null;

            if (_cellData == null || !GodotObject.IsInstanceValid(_cellData))
                _cellData = !CellDataPath.IsEmpty
                    ? GetNodeOrNull<GridCellDataComponent>(CellDataPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridCellDataComponent>(GetTree()?.CurrentScene) : null;

            // Explicit wire only, never found scene-wide - see DataLayersPath.
            if (_dataLayers == null || !GodotObject.IsInstanceValid(_dataLayers))
                _dataLayers = !DataLayersPath.IsEmpty
                    ? GetNodeOrNull<TerrainDataLayersComponent>(DataLayersPath)
                    : null;
        }

        private bool IsBlocked(Search search, Vector2I cell)
        {
            if (search.Blocked.Contains(cell))
                return true;

            if (search.Cells != null
                && search.UseCellBlockedFlag
                && search.Cells.HasFlag(cell, GridCellDataComponent.CellFlags.Blocked))
                return true;

            // Not nested under Cells: the kind can come from the terrain data
            // layers alone, in a scene with no GridCellDataComponent at all.
            if (search.BlockedKinds != null && search.BlockedKinds.Contains(search.KindAt(cell)))
                return true;

            return search.Placement?.IsOccupied(cell) == true;
        }

        private bool IsCellAllowed(Search search, Vector2I cell, bool allowBlocked)
        {
            if (!IsInBounds(cell))
                return false;

            return allowBlocked || !IsBlocked(search, cell);
        }

        private IEnumerable<Vector2I> Neighbors(Search search, Vector2I current, Vector2I goal)
        {
            foreach (Vector2I delta in CardinalSteps)
            {
                Vector2I next = current + delta;
                if (next == goal ? IsCellAllowed(search, next, AllowBlockedGoal) : IsCellAllowed(search, next, allowBlocked: false))
                    yield return next;
            }

            if (Diagonals == DiagonalPolicy.Never)
                yield break;

            foreach (Vector2I delta in DiagonalSteps)
            {
                Vector2I next = current + delta;
                if (!(next == goal ? IsCellAllowed(search, next, AllowBlockedGoal) : IsCellAllowed(search, next, allowBlocked: false)))
                    continue;

                if (Diagonals == DiagonalPolicy.NoCornerCutting)
                {
                    Vector2I sideA = current + new Vector2I(delta.X, 0);
                    Vector2I sideB = current + new Vector2I(0, delta.Y);
                    if (!IsCellAllowed(search, sideA, allowBlocked: false) || !IsCellAllowed(search, sideB, allowBlocked: false))
                        continue;
                }

                yield return next;
            }
        }

        private float Heuristic(Search search, Vector2I a, Vector2I b)
        {
            int dx = Mathf.Abs(a.X - b.X);
            int dy = Mathf.Abs(a.Y - b.Y);

            if (Diagonals == DiagonalPolicy.Never)
                return (dx + dy) * search.MinimumStepCost;

            int diagonal = Mathf.Min(dx, dy);
            int straight = Mathf.Max(dx, dy) - diagonal;
            return (diagonal * 1.41421356f + straight) * search.MinimumStepCost;
        }

        private static float StepCost(Search search, Vector2I from, Vector2I to)
        {
            float baseCost = from.X != to.X && from.Y != to.Y ? 1.41421356f : 1f;
            float roadCost = Mathf.Clamp(search.Roads?.GetTraversalCostMultiplier(to) ?? 1f, 0.05f, 10f);
            return baseCost * search.CostFor(to) * roadCost;
        }

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

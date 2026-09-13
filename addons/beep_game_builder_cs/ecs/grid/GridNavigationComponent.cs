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
        [ExportGroup("Terrain Traversal")]
        [Export] public bool RespectTerrainHeight { get; set; } = true;
        [Export(PropertyHint.Range, "0,8,1")] public int MaximumStepHeight { get; set; } = 0;
        /// <summary>Ramp direction is a cardinal Vector2I on the lower cell, pointing uphill.</summary>
        [Export] public bool AllowTerrainRamps { get; set; } = true;
        [Export(PropertyHint.Range, "16,200000,1")] public int MaxVisitedCells { get; set; } = 10000;

        private readonly HashSet<Vector2I> _blocked = new();
        private GridProjectionComponent? _grid;
        private GridPlacementComponent? _placement;
        private GridRoadComponent? _roads;
        private GridCellDataComponent? _cellData;
        private readonly Dictionary<Type, Node?> _fallbackSources = new();
        private Node? _fallbackRoot;
        private string[] _blockedKindValues = System.Array.Empty<string>();
        private HashSet<string>? _normalizedBlockedKinds;

        public override void _EnterTree()
        {
            if (Engine.IsEditorHint()) return;
            var tree = GetTree();
            // Store an object ID and method name in the native SceneTree, not a
            // managed delegate handle that becomes invalid on assembly reload.
            var callback = new Callable(this, MethodName.OnReferenceNodeChanged);
            tree.Connect(SceneTree.SignalName.NodeAdded, callback);
            tree.Connect(SceneTree.SignalName.NodeRemoved, callback);
        }

        public override void _Ready()
        {
            ResolveReferences();
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            ClearPathRequests();
            DisconnectRequestSources();
            // Resolve the live tree here: non-exported fields are lost on hot reload.
            var tree = GetTree();
            if (GodotObject.IsInstanceValid(tree))
            {
                var callback = new Callable(this, MethodName.OnReferenceNodeChanged);
                if (tree.IsConnected(SceneTree.SignalName.NodeAdded, callback))
                    tree.Disconnect(SceneTree.SignalName.NodeAdded, callback);
                if (tree.IsConnected(SceneTree.SignalName.NodeRemoved, callback))
                    tree.Disconnect(SceneTree.SignalName.NodeRemoved, callback);
            }
            _fallbackSources.Clear();
            _fallbackRoot = null;
        }

        private void OnReferenceNodeChanged(Node node)
        {
            if (node is GridProjectionComponent or GridPlacementComponent or GridRoadComponent or GridCellDataComponent)
                _fallbackSources.Clear();
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
            public Func<Vector2I, bool>? Availability;
            public bool IsAvailable(Vector2I cell) => Availability?.Invoke(cell) ?? Cells?.IsCellAvailable(cell) != false;
            public GridCellDataComponent? Cells;
            public GridPlacementComponent? Placement;
            public GridRoadComponent? Roads;
            public required HashSet<Vector2I> Blocked;
            public bool UseCellBlockedFlag;
            public HashSet<string>? BlockedKinds;
            public Dictionary<string, float>? Costs;
            public float MinimumStepCost = 1f;
            public bool RespectHeight;
            public bool AllowRamps;
            public int MaximumStep;
            private readonly Dictionary<Vector2I, int> _heights = new();

            public int HeightAt(Vector2I cell)
            {
                if (_heights.TryGetValue(cell, out int height)) return height;
                Variant value = Cells?.GetMetadata(cell, "terrain_relief") ?? default;
                height = value.VariantType == Variant.Type.Int ? Mathf.Clamp(value.AsInt32(), 0, 2) : 0;
                _heights[cell] = height;
                return height;
            }

            public bool CanCross(Vector2I from, Vector2I to)
            {
                if (!IsAvailable(from) || !IsAvailable(to)) return false;
                if (!RespectHeight || Cells is null) return true;
                int fromHeight = HeightAt(from), toHeight = HeightAt(to);
                int difference = Mathf.Abs(toHeight - fromHeight);
                if (difference <= MaximumStep) return true;
                Vector2I delta = to - from;
                if (!AllowRamps || difference != 1 || Mathf.Abs(delta.X) + Mathf.Abs(delta.Y) != 1) return false;
                Vector2I lower = fromHeight < toHeight ? from : to;
                Vector2I uphill = fromHeight < toHeight ? delta : -delta;
                Variant direction = Cells.GetMetadata(lower, "terrain_ramp_direction");
                return direction.VariantType == Variant.Type.Vector2I && direction.AsVector2I() == uphill;
            }

            private readonly Dictionary<Vector2I, string> _kinds = new();

            public string KindAt(Vector2I cell)
            {
                if (_kinds.TryGetValue(cell, out string? kind))
                    return kind;

                // The one terrain-kind rule, not a copy of it: this used to
                // re-implement GridCellRules' precedence inline, and the two
                // had to be fixed in step.
                kind = GridCellRules.TerrainKindAt(Cells, cell);
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

        private Search BuildSearch(bool includeCosts = true)
        {
            var search = new Search
            {
                Cells = _cellData,
                Placement = TreatPlacementOccupiedAsBlocked ? _placement : null,
                Roads = _roads,
                Blocked = _blocked,
                UseCellBlockedFlag = TreatCellDataBlockedAsBlocked && _cellData != null,
                RespectHeight = RespectTerrainHeight,
                AllowRamps = AllowTerrainRamps,
                MaximumStep = Mathf.Max(0, MaximumStepHeight),
            };

            bool hasKindSource = _cellData != null;
            if (TreatBlockedTerrainKindsAsBlocked && hasKindSource && BlockedTerrainKinds.Count > 0)
                search.BlockedKinds = ResolveBlockedKinds();

            if (!includeCosts) return search;

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

        private HashSet<string> ResolveBlockedKinds()
        {
            int count = BlockedTerrainKinds.Count;
            bool unchanged = _normalizedBlockedKinds is not null && _blockedKindValues.Length == count;
            for (int i = 0; unchanged && i < count; i++)
                unchanged = string.Equals(_blockedKindValues[i], BlockedTerrainKinds[i], StringComparison.Ordinal);
            if (unchanged) return _normalizedBlockedKinds!;

            var values = new string[count];
            var normalized = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < count; i++)
            {
                values[i] = BlockedTerrainKinds[i];
                string kind = GridTerrainRules.Normalize(values[i]);
                if (kind.Length > 0) normalized.Add(kind);
            }
            // Replace rather than mutate: active searches must retain their old rules
            // so the scheduler can detect a change before returning a stale route.
            _blockedKindValues = values;
            return _normalizedBlockedKinds = normalized;
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
            var search = new PathSearch(this, BuildSearch(), start, goal);
            while (!search.Complete) search.Step(4096);
            return search.Reason.Length == 0 ? Succeed(start, goal, search.Path) : Fail(start, goal, search.Reason);
        }

        public Godot.Collections.Array<Vector2> FindWorldPath(Vector2 startWorld, Vector2 goalWorld)
        {
            ResolveReferences();
            var points = new Godot.Collections.Array<Vector2>();
            var grid = _grid;
            if (grid == null || !startWorld.IsFinite() || !goalWorld.IsFinite())
                return points;

            Vector2I start = grid.WorldToCell(startWorld), goal = grid.WorldToCell(goalWorld);
            if (start == new Vector2I(int.MinValue, int.MinValue)
                || goal == new Vector2I(int.MinValue, int.MinValue)) return points;
            var cells = FindCellPath(start, goal);
            foreach (Vector2I cell in cells)
            {
                // PathFound listeners can change or remove the view synchronously.
                if (!GodotObject.IsInstanceValid(grid)) return new Godot.Collections.Array<Vector2>();
                Vector2 point = grid.CellToWorld(cell);
                if (!point.IsFinite()) return new Godot.Collections.Array<Vector2>();
                points.Add(point);
            }
            return points;
        }

        /// <summary>One-off traversal cost between two cells, for external callers.</summary>
        public float TraversalCost(Vector2I from, Vector2I to)
        {
            ResolveReferences();
            Search search = BuildSearch();
            return StepCost(search, from, to);
        }

        /// <summary>Checks a legal adjacent move, including corner and height rules.</summary>
        public bool CanTraverse(Vector2I from, Vector2I to)
        {
            ResolveReferences();
            return CanTraverse(BuildSearch(includeCosts: false), from, to);
        }

        /// <summary>Validates a supplied route with one resolved terrain working set.</summary>
        public bool CanTraversePath(Godot.Collections.Array<Vector2I> cells)
        {
            ResolveReferences();
            if (cells.Count == 0) return false;
            Search search = BuildSearch(includeCosts: false);
            if (!IsCellAllowed(search, cells[0], AllowBlockedStart)) return false;
            for (int i = 1; i < cells.Count; i++)
            {
                if (!CanTraverse(search, cells[i - 1], cells[i])) return false;
                if (i < cells.Count - 1 && !IsCellAllowed(search, cells[i], false)) return false;
            }
            return true;
        }

        private bool CanTraverse(Search search, Vector2I from, Vector2I to)
        {
            long dx = (long)to.X - from.X, dy = (long)to.Y - from.Y;
            if (Math.Abs(dx) > 1 || Math.Abs(dy) > 1 || (dx == 0 && dy == 0)) return false;
            if (!IsCellAllowed(search, from, AllowBlockedStart)) return false;
            return CanEnterNeighbor(search, from, to, AllowBlockedGoal);
        }

        /// <summary>One-off blocked query, for external callers.</summary>
        public bool IsBlocked(Vector2I cell)
        {
            ResolveReferences();
            return IsBlocked(BuildSearch(includeCosts: false), cell);
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
            if (blocked ? _blocked.Add(cell) : _blocked.Remove(cell)) _navigationRevision++;
        }

        public void ClearBlocked()
        {
            if (_blocked.Count == 0) return;
            _blocked.Clear();
            _navigationRevision++;
        }

        public Godot.Collections.Array<Vector2I> GetBlockedCells()
        {
            var cells = new Godot.Collections.Array<Vector2I>();
            foreach (Vector2I cell in _blocked)
                cells.Add(cell);
            return cells;
        }

        private void ResolveReferences()
        {
            // Resolve explicit paths once per query, not per visited cell. A valid
            // cached node may have moved, or its authored path may now name another node.
            // _grid is explicit-ONLY: an unwired GridPath means this component has no
            // projection, never "adopt whichever one the scene holds" - same policy as
            // GridPlacementComponent's, through the same one owner.
            EntityComponent.ResolveLive(this, GridPath, ref _grid, fallbackWhenEmpty: false);
            ResolveSource(PlacementPath, ref _placement);
            ResolveSource(RoadPath, ref _roads);
            ResolveSource(CellDataPath, ref _cellData);
        }

        private void ResolveSource<T>(NodePath path, ref T? cached) where T : Node
        {
            // The fresh-resolve rule is EntityComponent.ResolveLive's; this wrapper adds only the
            // repeated-fallback memo below. Delegating keeps one implementation of "re-point the
            // path and the new node is picked up next query".
            if (!path.IsEmpty) { EntityComponent.ResolveLive(this, path, ref cached); return; }
            Node? root = IsInsideTree() ? GetTree().CurrentScene : null;
            if (root != _fallbackRoot)
            {
                _fallbackSources.Clear();
                _fallbackRoot = root;
            }
            // Missing optional collaborators are cached too. Otherwise 200 moving
            // units repeatedly traverse all 1,000 actor subtrees every physics tick.
            if (_fallbackSources.TryGetValue(typeof(T), out var found)
                && (found is null || (GodotObject.IsInstanceValid(found) && found.IsInsideTree())))
            {
                cached = found as T;
                return;
            }
            cached = EntityComponent.FindComponent<T>(root);
            _fallbackSources[typeof(T)] = cached;
        }

        private bool IsBlocked(Search search, Vector2I cell)
        {
            if (!search.IsAvailable(cell)) return true;
            if (search.Blocked.Contains(cell))
                return true;

            if (search.Cells != null
                && search.UseCellBlockedFlag
                && search.Cells.HasFlag(cell, GridCellDataComponent.CellFlags.Blocked))
                return true;

            // Kind blocking and explicit cell flags are independent rules.
            if (search.BlockedKinds != null && search.BlockedKinds.Contains(search.KindAt(cell)))
                return true;

            return search.Placement?.IsOccupied(cell) == true;
        }

        private bool IsCellAllowed(Search search, Vector2I cell, bool allowBlocked)
        {
            if (!IsInBounds(cell) || !search.IsAvailable(cell))
                return false;

            return allowBlocked || !IsBlocked(search, cell);
        }

        private IEnumerable<Vector2I> Neighbors(Search search, Vector2I current, Vector2I goal)
        {
            foreach (Vector2I delta in CardinalSteps)
            {
                Vector2I next = current + delta;
                if (CanEnterNeighbor(search, current, next, next == goal && AllowBlockedGoal))
                    yield return next;
            }

            if (Diagonals == DiagonalPolicy.Never)
                yield break;

            foreach (Vector2I delta in DiagonalSteps)
            {
                Vector2I next = current + delta;
                if (CanEnterNeighbor(search, current, next, next == goal && AllowBlockedGoal))
                    yield return next;
            }
        }

        private bool CanEnterNeighbor(Search search, Vector2I current, Vector2I next, bool allowBlocked)
        {
            bool diagonal = current.X != next.X && current.Y != next.Y;
            if (diagonal && Diagonals == DiagonalPolicy.Never) return false;
            if (!IsCellAllowed(search, next, allowBlocked) || !search.CanCross(current, next)) return false;
            if (!diagonal) return true;

            Vector2I sideA = new(next.X, current.Y), sideB = new(current.X, next.Y);
            // Diagonals may not shortcut a cliff or turn across a ramp edge.
            if (!search.CanCross(current, sideA) || !search.CanCross(current, sideB)
                || !search.CanCross(sideA, next) || !search.CanCross(sideB, next)) return false;
            return Diagonals != DiagonalPolicy.NoCornerCutting
                || (IsCellAllowed(search, sideA, false) && IsCellAllowed(search, sideB, false));
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
            if (search.Cells?.IsCellAvailable(from) == false || search.Cells?.IsCellAvailable(to) == false)
                return float.PositiveInfinity;
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

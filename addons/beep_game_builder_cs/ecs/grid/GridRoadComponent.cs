using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Stores player-built roads/paths on grid cells and exposes movement-cost
    /// helpers for GridNavigationComponent. Use it for dirt paths, roads, rails,
    /// trails, or any top-down/isometric route network.
    ///
    /// Persistence is owned by GridWorldStateComponent, the same as
    /// GridCellDataComponent/GridPlacementComponent/GridNavigationComponent/
    /// GridSelectionComponent/GridJobQueueComponent - this component used to
    /// also implement ISaveable and join the save group under its own key,
    /// so a save/load wrote and read road data through two independent
    /// owners at once, in an order the save group's iteration does not
    /// guarantee, each overwriting the other's result.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridRoadComponent : Node2D
    {
        [Signal] public delegate void RoadChangedEventHandler(int x, int y, string kind, bool hasRoad);
        [Signal] public delegate void RoadsChangedEventHandler();
        [Signal] public delegate void RoadRejectedEventHandler(int x, int y, string kind, string reason);

        [Export] public NodePath GridPath { get; set; } = new("");
        [Export] public NodePath CellDataPath { get; set; } = new("");
        [Export] public string DefaultRoadKind { get; set; } = "dirt_path";
        [Export] public bool TreatCellDataBlockedAsUnroadable { get; set; } = true;
        [Export] public bool TreatBlockedTerrainKindsAsUnroadable { get; set; } = true;
        [Export] public Godot.Collections.Array<string> BlockedTerrainKinds { get; set; }
            = GridTerrainRules.DefaultBlockedTerrainKinds();
        [Export(PropertyHint.Range, "0.05,1,0.01")] public float DefaultRoadCostMultiplier { get; set; } = 0.55f;
        [Export] public bool DrawRoads { get; set; } = true;
        [Export] public Color RoadColor { get; set; } = new(0.58f, 0.43f, 0.25f, 0.7f);
        [Export] public Color OutlineColor { get; set; } = new(0.16f, 0.11f, 0.06f, 0.45f);
        [Export(PropertyHint.Range, "0.1,1,0.05")] public float RoadWidthRatio { get; set; } = 0.46f;
        [Export(PropertyHint.Range, "0,6,0.1")] public float OutlineWidth { get; set; } = 1f;

        private readonly Dictionary<Vector2I, RoadRecord> _roads = new();
        private GridProjectionComponent? _grid;
        private GridCellDataComponent? _cells;

        public float EffectiveDefaultRoadCostMultiplier => Mathf.Clamp(float.IsFinite(DefaultRoadCostMultiplier) ? DefaultRoadCostMultiplier : 0.55f, 0.05f, 1f);
        public float EffectiveRoadWidthRatio => Mathf.Clamp(float.IsFinite(RoadWidthRatio) ? RoadWidthRatio : 0.46f, 0.05f, 1f);
        public float EffectiveOutlineWidth => Mathf.Max(0f, float.IsFinite(OutlineWidth) ? OutlineWidth : 0f);

        public override void _Ready()
        {
            ResolveReferences();
            SetProcess(Engine.IsEditorHint());
            UpdateConfigurationWarnings();
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint())
                QueueRedraw();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (GridPath.IsEmpty)
                return new[] { "GridPath should point to a GridProjectionComponent." };
            if (DefaultRoadCostMultiplier <= 0f || DefaultRoadCostMultiplier > 1f)
                return new[] { "DefaultRoadCostMultiplier should be greater than 0 and at most 1." };
            return Array.Empty<string>();
        }

        public override void _Draw()
        {
            if (!DrawRoads)
                return;

            ResolveReferences();
            if (_grid == null)
                return;

            foreach (Vector2I cell in _roads.Keys)
                DrawRoadCell(cell);
        }

        public void SetRoad(Vector2I cell, bool hasRoad)
        {
            if (hasRoad)
                SetRoad(cell, DefaultRoadKind, EffectiveDefaultRoadCostMultiplier);
            else
                ClearRoad(cell);
        }

        public void SetRoad(Vector2I cell, string kind, float costMultiplier = -1f)
        {
            TrySetRoad(cell, kind, costMultiplier);
        }

        public bool TrySetRoad(Vector2I cell, string kind, float costMultiplier = -1f)
        {
            string roadKind = string.IsNullOrWhiteSpace(kind) ? DefaultRoadKind : kind.Trim();
            if (!CanBuildRoad(cell))
            {
                RejectRoad(cell, roadKind, "unroadable_terrain");
                return false;
            }

            float multiplier = costMultiplier > 0f && float.IsFinite(costMultiplier) ? costMultiplier : EffectiveDefaultRoadCostMultiplier;
            _roads[cell] = new RoadRecord(roadKind, Mathf.Clamp(multiplier, 0.05f, 1f));
            _minimumCostDirty = true;
            EmitSignal(SignalName.RoadChanged, cell.X, cell.Y, roadKind, true);
            QueueRedraw();
            return true;
        }

        public bool CanBuildRoad(Vector2I cell)
        {
            ResolveReferences();
            if (_cells == null)
                return true;

            if (TreatCellDataBlockedAsUnroadable
                && _cells.HasFlag(cell, GridCellDataComponent.CellFlags.Blocked))
                return false;

            if (!TreatBlockedTerrainKindsAsUnroadable)
                return true;

            return !GridTerrainRules.MatchesAny(
                GridTerrainRules.Normalize(_cells.GetTerrainKind(cell)), BlockedTerrainKinds);
        }

        public void ClearRoad(Vector2I cell)
        {
            if (!_roads.Remove(cell))
                return;

            _minimumCostDirty = true;
            EmitSignal(SignalName.RoadChanged, cell.X, cell.Y, "", false);
            QueueRedraw();
        }

        public void ClearRoads()
        {
            if (_roads.Count == 0)
                return;

            _roads.Clear();
            _minimumCostDirty = true;
            EmitSignal(SignalName.RoadsChanged);
            QueueRedraw();
        }

        public bool HasRoad(Vector2I cell) => _roads.ContainsKey(cell);

        public string GetRoadKind(Vector2I cell)
            => _roads.TryGetValue(cell, out RoadRecord? road) ? road.Kind : "";

        public float GetTraversalCostMultiplier(Vector2I cell)
            => _roads.TryGetValue(cell, out RoadRecord? road) ? road.CostMultiplier : 1f;

        public int RoadCount => _roads.Count;

        /// <summary>
        /// Cached and recomputed only when the road set changes: navigation
        /// reads this inside its A* heuristic, once per visited cell, and
        /// walking every road there made pathfinding cost scale with the road
        /// network instead of the search.
        /// </summary>
        public float MinimumCostMultiplier
        {
            get
            {
                if (_minimumCostDirty)
                {
                    float min = 1f;
                    foreach (RoadRecord road in _roads.Values)
                        min = Mathf.Min(min, road.CostMultiplier);
                    _minimumCostCache = Mathf.Clamp(min, 0.05f, 1f);
                    _minimumCostDirty = false;
                }
                return _minimumCostCache;
            }
        }

        private float _minimumCostCache = 1f;
        private bool _minimumCostDirty = true;

        public Godot.Collections.Array<Vector2I> GetRoadCells()
        {
            var cells = new Godot.Collections.Array<Vector2I>();
            foreach (Vector2I cell in _roads.Keys)
                cells.Add(cell);
            return cells;
        }

        public Godot.Collections.Array<Godot.Collections.Dictionary> GetRoads()
        {
            var roads = new Godot.Collections.Array<Godot.Collections.Dictionary>();
            foreach ((Vector2I cell, RoadRecord road) in _roads)
            {
                roads.Add(new Godot.Collections.Dictionary
                {
                    ["cell"] = cell,
                    ["kind"] = road.Kind,
                    ["cost_multiplier"] = road.CostMultiplier
                });
            }
            return roads;
        }

        /// <summary>
        /// Dictionary-shaped snapshot of the road set, for a caller that wants
        /// one value rather than the raw array GetRoads()/LoadRoads() work
        /// with directly. Not part of ISaveable: GridWorldStateComponent is
        /// the sole save/load owner for roads (it calls GetRoads()/LoadRoads()
        /// directly, not these) - a second, independent ISaveable on this
        /// component used to write the identical road data to a second save
        /// key, so a save/load could silently disagree with itself depending
        /// on which of the two ran last.
        /// </summary>
        public Godot.Collections.Dictionary CaptureState()
            => new()
            {
                ["version"] = 1,
                ["roads"] = GetRoads()
            };

        public void RestoreState(Godot.Collections.Dictionary state)
        {
            LoadRoads(ReadArray(state, "roads"));
        }

        public void LoadRoads(Godot.Collections.Array roads, bool clearExisting = true)
        {
            _minimumCostDirty = true;
            bool changed = false;
            if (clearExisting)
            {
                changed = _roads.Count > 0;
                _roads.Clear();
            }

            foreach (Variant value in roads)
            {
                if (value.VariantType == Variant.Type.Vector2I || value.VariantType == Variant.Type.Vector2)
                {
                    Vector2I roadCell = GridVariantReader.Vector2I(value, Vector2I.Zero);
                    if (!CanBuildRoad(roadCell))
                        continue;

                    _roads[roadCell] = new RoadRecord(DefaultRoadKind, EffectiveDefaultRoadCostMultiplier);
                    changed = true;
                    EmitSignal(SignalName.RoadChanged, roadCell.X, roadCell.Y, DefaultRoadKind, true);
                    continue;
                }

                if (!GridVariantReader.TryDictionary(value, out Godot.Collections.Dictionary dict))
                    continue;

                Vector2I cell = GridVariantReader.Vector2I(dict, "cell", new Vector2I(int.MinValue, int.MinValue));
                if (cell.X == int.MinValue || cell.Y == int.MinValue)
                    continue;
                if (!CanBuildRoad(cell))
                    continue;

                string roadKind = GridVariantReader.String(dict, "kind", DefaultRoadKind);
                float multiplier = GridVariantReader.Float(dict, "cost_multiplier", EffectiveDefaultRoadCostMultiplier);
                _roads[cell] = new RoadRecord(
                    string.IsNullOrWhiteSpace(roadKind) ? DefaultRoadKind : roadKind.Trim(),
                    Mathf.Clamp(multiplier > 0f && float.IsFinite(multiplier) ? multiplier : EffectiveDefaultRoadCostMultiplier, 0.05f, 1f));
                changed = true;
                EmitSignal(SignalName.RoadChanged, cell.X, cell.Y, _roads[cell].Kind, true);
            }

            if (changed)
                EmitSignal(SignalName.RoadsChanged);
            QueueRedraw();
        }

        /// <summary>The cells a road connects through, in the order its joins are drawn.</summary>
        private static readonly Vector2I[] Neighbours =
        {
            new(-1, 0), new(1, 0), new(0, -1), new(0, 1),
        };

        /// <summary>
        /// One road cell: its own body, and a join out to every neighbour that is also road.
        /// </summary>
        /// <remarks>
        /// <para><b>A ROAD IS A PATH, AND A PATH IS THE THING ITS CELLS MAKE TOGETHER.</b> This drew
        /// each cell as its own polygon shrunk towards its own centre — half the tile — so a run of
        /// road came out as a row of separate marks with ground between them: legible as "something
        /// is here" and not legible as a road at all. Walking the route is what the player did when
        /// they drew it; drawing it one cell at a time throws that away.</para>
        ///
        /// <para>Each join is drawn from the side two cells share to those same points pulled in
        /// towards this cell's centre, so the two cells meet in the middle of their shared edge
        /// whichever way the projection has bent it. <see cref="TerrainOverlayEdges.SharedSide"/> is
        /// the same walk a start-area border uses, because it is the same question: which edge do
        /// these two cells have in common.</para>
        ///
        /// <para>The outline is the same shapes drawn a little wider UNDERNEATH rather than a line
        /// traced around them. A traced line would run down the middle of every straight run — the
        /// exact seam this exists to remove — because two overlapping quads have no single outline
        /// to trace.</para>
        /// </remarks>
        private void DrawRoadCell(Vector2I cell)
        {
            if (_grid == null)
                return;

            Vector2[] own = CellPolygon(cell);
            if (own.Length < 3)
                return;

            Vector2 centre = ToLocal(_grid.ToGlobal(_grid.CellToWorld(cell)));
            float reach = Reach(own, centre);
            float width = EffectiveRoadWidthRatio;

            if (EffectiveOutlineWidth > 0f && reach > 0.0f)
                PaintCell(cell, own, centre, width + (EffectiveOutlineWidth / reach), OutlineColor);

            PaintCell(cell, own, centre, width, RoadColor);
        }

        /// <summary>One pass of the road in one colour: the cell's body, then a join per neighbour.</summary>
        private void PaintCell(Vector2I cell, Vector2[] own, Vector2 centre, float ratio, Color colour)
        {
            DrawColoredPolygon(Shrunk(own, centre, ratio), colour);

            foreach (Vector2I step in Neighbours)
            {
                if (!_roads.ContainsKey(cell + step))
                    continue;

                Vector2[] other = CellPolygon(cell + step);

                if (other.Length < 3
                    || !TerrainOverlayEdges.SharedSide(own, other, out Vector2 from, out Vector2 to))
                    continue;

                DrawColoredPolygon(
                    new[] { from, to, centre + ((to - centre) * ratio), centre + ((from - centre) * ratio) },
                    colour);
            }
        }

        /// <summary>A cell's own polygon in this node's space, or nothing when it has none.</summary>
        private Vector2[] CellPolygon(Vector2I cell)
        {
            if (_grid == null)
                return Array.Empty<Vector2>();

            System.Span<Vector2> corners = stackalloc Vector2[4];
            int n = _grid.CellCorners(cell, corners);

            if (n < 3)
                return Array.Empty<Vector2>();

            var points = new Vector2[n];

            for (int i = 0; i < n; i++)
                points[i] = ToLocal(_grid.ToGlobal(corners[i]));

            return points;
        }

        private static Vector2[] Shrunk(Vector2[] points, Vector2 centre, float ratio)
        {
            var shrunk = new Vector2[points.Length];

            for (int i = 0; i < points.Length; i++)
                shrunk[i] = centre + ((points[i] - centre) * ratio);

            return shrunk;
        }

        /// <summary>How far a cell's corners sit from its centre, on average — its on-screen size.</summary>
        private static float Reach(Vector2[] points, Vector2 centre)
        {
            if (points.Length == 0)
                return 0.0f;

            float total = 0.0f;

            for (int i = 0; i < points.Length; i++)
                total += points[i].DistanceTo(centre);

            return total / points.Length;
        }

        private void ResolveReferences()
        {
            EntityComponent.Resolve(this, GridPath, ref _grid);
            EntityComponent.Resolve(this, CellDataPath, ref _cells);
        }

        private static Godot.Collections.Array ReadArray(Godot.Collections.Dictionary state, string key)
            => GridVariantReader.Array(state, key);




        private void RejectRoad(Vector2I cell, string kind, string reason)
        {
            EmitSignal(SignalName.RoadRejected, cell.X, cell.Y, kind, reason);
        }

        private sealed record RoadRecord(string Kind, float CostMultiplier);
    }
}

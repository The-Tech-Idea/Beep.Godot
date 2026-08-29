using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Stores player-built roads/paths on grid cells and exposes movement-cost
    /// helpers for GridNavigationComponent. Use it for dirt paths, roads, rails,
    /// trails, or any top-down/isometric route network.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridRoadComponent : Node2D, ISaveable
    {
        [Signal] public delegate void RoadChangedEventHandler(int x, int y, string kind, bool hasRoad);
        [Signal] public delegate void RoadsChangedEventHandler();
        [Signal] public delegate void RoadRejectedEventHandler(int x, int y, string kind, string reason);

        [Export] public NodePath GridPath { get; set; } = new("");
        [Export] public NodePath CellDataPath { get; set; } = new("");
        [Export] public bool ParticipatesInSave { get; set; } = true;
        [Export] public string SaveKey { get; set; } = "grid_roads.state";
        [Export] public string DefaultRoadKind { get; set; } = "dirt_path";
        [Export] public bool TreatCellDataBlockedAsUnroadable { get; set; } = true;
        [Export] public bool TreatBlockedTerrainKindsAsUnroadable { get; set; } = true;
        [Export] public Godot.Collections.Array<string> BlockedTerrainKinds { get; set; } = new()
        {
            "water",
            "sea",
            "ocean",
            "deep_water",
            "lava"
        };
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
            if (!Engine.IsEditorHint() && ParticipatesInSave)
                AddToGroup(SaveableHelper.Group);
            SetProcess(Engine.IsEditorHint());
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            if (ParticipatesInSave)
                RemoveFromGroup(SaveableHelper.Group);
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
            if (string.IsNullOrWhiteSpace(SaveKey))
                return new[] { "SaveKey must not be empty when roads participate in saves." };
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

            string terrainKind = NormalizeTerrainKind(_cells.GetTerrainKind(cell));
            foreach (string blockedKind in BlockedTerrainKinds)
                if (NormalizeTerrainKind(blockedKind) == terrainKind)
                    return false;

            return true;
        }

        public void ClearRoad(Vector2I cell)
        {
            if (!_roads.Remove(cell))
                return;

            EmitSignal(SignalName.RoadChanged, cell.X, cell.Y, "", false);
            QueueRedraw();
        }

        public void ClearRoads()
        {
            if (_roads.Count == 0)
                return;

            _roads.Clear();
            EmitSignal(SignalName.RoadsChanged);
            QueueRedraw();
        }

        public bool HasRoad(Vector2I cell) => _roads.ContainsKey(cell);

        public string GetRoadKind(Vector2I cell)
            => _roads.TryGetValue(cell, out RoadRecord? road) ? road.Kind : "";

        public float GetTraversalCostMultiplier(Vector2I cell)
            => _roads.TryGetValue(cell, out RoadRecord? road) ? road.CostMultiplier : 1f;

        public int RoadCount => _roads.Count;

        public float MinimumCostMultiplier
        {
            get
            {
                float min = 1f;
                foreach (RoadRecord road in _roads.Values)
                    min = Mathf.Min(min, road.CostMultiplier);
                return Mathf.Clamp(min, 0.05f, 1f);
            }
        }

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

                Vector2I cell = DictVector2I(dict, "cell", new Vector2I(int.MinValue, int.MinValue));
                if (cell.X == int.MinValue || cell.Y == int.MinValue)
                    continue;
                if (!CanBuildRoad(cell))
                    continue;

                string roadKind = DictString(dict, "kind", DefaultRoadKind);
                float multiplier = DictFloat(dict, "cost_multiplier", EffectiveDefaultRoadCostMultiplier);
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

        public void Save(GameBuilder.GameStateData state)
        {
            if (!string.IsNullOrWhiteSpace(SaveKey))
                state.GameData[SaveKey] = CaptureState();
        }

        public void Load(GameBuilder.GameStateData state)
        {
            if (!string.IsNullOrWhiteSpace(SaveKey)
                && state.GameData.TryGetValue(SaveKey, out Variant value)
                && GridVariantReader.TryDictionary(value, out Godot.Collections.Dictionary saved))
            {
                RestoreState(saved);
            }
        }

        private void DrawRoadCell(Vector2I cell)
        {
            if (_grid == null)
                return;

            Vector2 center = ToLocal(_grid.ToGlobal(_grid.CellToWorld(cell)));
            Vector2[] corners = _grid.CellCorners(cell);
            var points = new Vector2[corners.Length];
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 corner = ToLocal(_grid.ToGlobal(corners[i]));
                points[i] = center + (corner - center) * EffectiveRoadWidthRatio;
            }

            DrawColoredPolygon(points, RoadColor);
            if (EffectiveOutlineWidth > 0f)
                DrawPolyline(points, OutlineColor, EffectiveOutlineWidth, true);
        }

        private void ResolveReferences()
        {
            if (_grid == null || !GodotObject.IsInstanceValid(_grid))
                _grid = !GridPath.IsEmpty
                    ? GetNodeOrNull<GridProjectionComponent>(GridPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridProjectionComponent>(GetTree()?.CurrentScene) : null;

            if (_cells == null || !GodotObject.IsInstanceValid(_cells))
                _cells = !CellDataPath.IsEmpty
                    ? GetNodeOrNull<GridCellDataComponent>(CellDataPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridCellDataComponent>(GetTree()?.CurrentScene) : null;
        }

        private static Godot.Collections.Array ReadArray(Godot.Collections.Dictionary state, string key)
            => GridVariantReader.Array(state, key);

        private static string DictString(Godot.Collections.Dictionary dict, string key, string fallback)
            => dict.ContainsKey(key) ? dict[key].AsString() : fallback;

        private static float DictFloat(Godot.Collections.Dictionary dict, string key, float fallback)
            => GridVariantReader.Float(dict, key, fallback);

        private static Vector2I DictVector2I(Godot.Collections.Dictionary dict, string key, Vector2I fallback)
            => GridVariantReader.Vector2I(dict, key, fallback);

        private void RejectRoad(Vector2I cell, string kind, string reason)
        {
            EmitSignal(SignalName.RoadRejected, cell.X, cell.Y, kind, reason);
        }

        private static string NormalizeTerrainKind(string value)
            => string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');

        private sealed record RoadRecord(string Kind, float CostMultiplier);
    }
}

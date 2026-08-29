using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Stardew-style grid tool actions for click/toolbar driven games. It applies
    /// hoe, water, plant, harvest, clear, and job-queue actions to a target cell
    /// or to the current GridSelectionComponent selection.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridToolActionComponent : Node
    {
        public enum ToolAction
        {
            Clear,
            Hoe,
            Water,
            Plant,
            Harvest,
            QueueJob,
            Road,
            RemoveRoad
        }

        [Signal] public delegate void ToolAppliedEventHandler(string action, int x, int y);
        [Signal] public delegate void ToolRejectedEventHandler(string action, int x, int y, string reason);

        [Export] public NodePath GridPath { get; set; } = new("");
        [Export] public NodePath CellDataPath { get; set; } = new("");
        [Export] public NodePath SelectionPath { get; set; } = new("");
        [Export] public NodePath JobQueuePath { get; set; } = new("");
        [Export] public NodePath RoadPath { get; set; } = new("");
        [Export] public NodePath NavigationPath { get; set; } = new("");
        [Export] public NodePath CropCatalogPath { get; set; } = new("");
        [Export] public NodePath CalendarPath { get; set; } = new("");
        [Export] public NodePath ResourceWalletPath { get; set; } = new("");
        [Export] public ToolAction CurrentAction { get; set; } = ToolAction.Hoe;
        [Export] public string RoadKind { get; set; } = "dirt_path";
        [Export(PropertyHint.Range, "0.05,1,0.01")] public float RoadCostMultiplier { get; set; } = 0.55f;
        [Export] public string CropId { get; set; } = "turnip";
        [Export(PropertyHint.Range, "0,365,1")] public int CropDaysToMature { get; set; } = 3;
        [Export] public string JobKind { get; set; } = "clear_land";
        [Export(PropertyHint.Range, "0.01,600,0.01")] public float JobWorkSeconds { get; set; } = 1.5f;
        [Export] public int JobPriority { get; set; } = 0;
        [Export] public bool ApplyToSelectionWhenPresent { get; set; } = true;
        [Export] public bool UseMouseInput { get; set; } = false;
        [Export] public bool AddHarvestYieldToWallet { get; set; } = true;
        [Export] public bool UseNavigationBounds { get; set; } = true;
        [Export] public bool RejectNavigationBlockedCellsForJobs { get; set; } = false;
        [Export] public bool TreatBlockedTerrainKindsAsUnworkable { get; set; } = true;
        [Export] public Godot.Collections.Array<string> BlockedTerrainKinds { get; set; } = new()
        {
            "water",
            "sea",
            "ocean",
            "deep_water",
            "lava"
        };
        [Export] public Godot.Collections.Array<string> AllowedTerrainKinds { get; set; } = new();

        private static readonly Vector2I InvalidCell = new(int.MinValue, int.MinValue);
        private GridProjectionComponent? _grid;
        private GridCellDataComponent? _cells;
        private GridSelectionComponent? _selection;
        private GridJobQueueComponent? _jobs;
        private GridRoadComponent? _roads;
        private GridNavigationComponent? _navigation;
        private GridCropCatalogComponent? _cropCatalog;
        private GridCalendarComponent? _calendar;
        private GridResourceWalletComponent? _resourceWallet;

        public float EffectiveRoadCostMultiplier => Mathf.Clamp(float.IsFinite(RoadCostMultiplier) ? RoadCostMultiplier : 0.55f, 0.05f, 1f);
        public int EffectiveCropDaysToMature => Mathf.Max(0, CropDaysToMature);
        public float EffectiveJobWorkSeconds => Mathf.Max(0.01f, float.IsFinite(JobWorkSeconds) ? JobWorkSeconds : 1.5f);
        public string EffectiveCropId => string.IsNullOrWhiteSpace(CropId) ? "crop" : CropId.Trim();
        public string EffectiveJobKind => string.IsNullOrWhiteSpace(JobKind) ? "work" : JobKind.Trim();
        public string EffectiveRoadKind => string.IsNullOrWhiteSpace(RoadKind) ? "dirt_path" : RoadKind.Trim();

        public override void _Ready()
        {
            ResolveReferences();
            SetProcessUnhandledInput(!Engine.IsEditorHint());
            UpdateConfigurationWarnings();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (CellDataPath.IsEmpty)
                return new[] { "CellDataPath should point to a GridCellDataComponent." };
            if (UseMouseInput && GridPath.IsEmpty)
                return new[] { "GridPath should point to a GridProjectionComponent when UseMouseInput is enabled." };
            return System.Array.Empty<string>();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!UseMouseInput || @event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouse)
                return;

            ResolveReferences();
            if (_grid == null)
            {
                Reject(CurrentAction, InvalidCell, "missing_grid");
                return;
            }

            ApplyToCell(_grid.WorldToCell(mouse.GlobalPosition), CurrentAction);
            GetViewport()?.SetInputAsHandled();
        }

        public int ApplyCurrent()
            => Apply(CurrentAction);

        public int Apply(ToolAction action)
        {
            ResolveReferences();
            if (ApplyToSelectionWhenPresent && _selection != null)
            {
                var selected = _selection.GetSelectedCells();
                if (selected.Count > 0)
                    return ApplyToCells(selected, action);
            }

            if (_selection != null && _selection.HoverCell.X != int.MinValue)
                return ApplyToCell(_selection.HoverCell, action) ? 1 : 0;

            return Fail(action, InvalidCell, "no_target_cell");
        }

        public int ApplyToCells(Godot.Collections.Array cells, ToolAction action)
        {
            int count = 0;
            foreach (Variant value in cells)
            {
                if (!TryReadCell(value, out Vector2I cell))
                    continue;

                if (ApplyToCell(cell, action))
                    count++;
            }
            return count;
        }

        public int ApplyToCells(Godot.Collections.Array<Vector2I> cells, ToolAction action)
        {
            var looseCells = new Godot.Collections.Array();
            foreach (Vector2I cell in cells)
                looseCells.Add(cell);

            return ApplyToCells(looseCells, action);
        }

        public bool ApplyToCell(Vector2I cell, ToolAction action)
        {
            ResolveReferences();
            if (cell.X == int.MinValue || cell.Y == int.MinValue)
                return Reject(action, cell, "invalid_cell");

            if (_cells == null)
                return Reject(action, cell, "missing_cell_data");

            bool applied = action switch
            {
                ToolAction.Clear => ApplyClear(cell),
                ToolAction.Hoe => ApplyHoe(cell),
                ToolAction.Water => ApplyWater(cell),
                ToolAction.Plant => ApplyPlant(cell),
                ToolAction.Harvest => ApplyHarvest(cell),
                ToolAction.QueueJob => ApplyQueueJob(cell),
                ToolAction.Road => ApplyRoad(cell),
                ToolAction.RemoveRoad => ApplyRemoveRoad(cell),
                _ => false
            };

            if (applied)
                EmitSignal(SignalName.ToolApplied, action.ToString(), cell.X, cell.Y);

            return applied;
        }

        private bool ApplyClear(Vector2I cell)
        {
            if (!CanWorkTerrain(cell))
                return Reject(ToolAction.Clear, cell, "unworkable_terrain");

            _cells!.ClearLand(cell);
            return true;
        }

        private bool ApplyHoe(Vector2I cell)
        {
            if (_cells!.HasFlag(cell, GridCellDataComponent.CellFlags.Blocked))
                return Reject(ToolAction.Hoe, cell, "blocked");

            if (!CanWorkTerrain(cell))
                return Reject(ToolAction.Hoe, cell, "unworkable_terrain");

            _cells.Till(cell);
            return true;
        }

        private bool ApplyWater(Vector2I cell)
        {
            if (!_cells!.HasFlag(cell, GridCellDataComponent.CellFlags.Tilled))
                return Reject(ToolAction.Water, cell, "not_tilled");

            _cells.Water(cell);
            return true;
        }

        private bool ApplyPlant(Vector2I cell)
        {
            if (_cells!.GetCropId(cell) != "")
                return Reject(ToolAction.Plant, cell, "already_planted");

            if (!CanWorkTerrain(cell))
                return Reject(ToolAction.Plant, cell, "unworkable_terrain");

            string cropId = EffectiveCropId;
            if (_cropCatalog != null && _calendar != null && !_cropCatalog.CanPlant(cropId, _calendar.Season))
                return Reject(ToolAction.Plant, cell, "wrong_season");

            int daysToMature = _cropCatalog?.DaysToMature(cropId, EffectiveCropDaysToMature) ?? EffectiveCropDaysToMature;
            if (!_cells.PlantCrop(cell, cropId, daysToMature))
                return Reject(ToolAction.Plant, cell, "not_tilled_or_missing_crop");

            return true;
        }

        private bool ApplyHarvest(Vector2I cell)
        {
            if (!_cells!.HasFlag(cell, GridCellDataComponent.CellFlags.HarvestReady))
                return Reject(ToolAction.Harvest, cell, "not_ready");

            string cropId = _cells.GetCropId(cell);
            if (string.IsNullOrEmpty(cropId))
                return Reject(ToolAction.Harvest, cell, "missing_crop");

            if (AddHarvestYieldToWallet && !ResourceWalletPath.IsEmpty && _resourceWallet == null)
                return Reject(ToolAction.Harvest, cell, "missing_resource_wallet");

            if (!_cells.HarvestCrop(cell))
                return Reject(ToolAction.Harvest, cell, "missing_crop");

            if (AddHarvestYieldToWallet && _resourceWallet != null)
            {
                string yieldId = _cropCatalog?.YieldItem(cropId) ?? cropId;
                int yieldCount = _cropCatalog?.YieldCount(cropId) ?? 1;
                _resourceWallet.AddAmount(yieldId, yieldCount);
            }

            return true;
        }

        private bool ApplyQueueJob(Vector2I cell)
        {
            if (_jobs == null)
                return Reject(ToolAction.QueueJob, cell, "missing_job_queue");

            string? blockReason = WorkJobBlockReason(cell);
            if (blockReason != null)
                return Reject(ToolAction.QueueJob, cell, blockReason);

            _jobs.AddJob(cell, EffectiveJobKind, EffectiveJobWorkSeconds, JobPriority);
            return true;
        }

        private bool ApplyRoad(Vector2I cell)
        {
            if (_roads == null)
                return Reject(ToolAction.Road, cell, "missing_road_component");

            return _roads.TrySetRoad(cell, EffectiveRoadKind, EffectiveRoadCostMultiplier)
                || Reject(ToolAction.Road, cell, "road_rejected");
        }

        private bool ApplyRemoveRoad(Vector2I cell)
        {
            if (_roads == null)
                return Reject(ToolAction.RemoveRoad, cell, "missing_road_component");

            _roads.ClearRoad(cell);
            return true;
        }

        private int Fail(ToolAction action, Vector2I cell, string reason)
        {
            Reject(action, cell, reason);
            return 0;
        }

        private bool Reject(ToolAction action, Vector2I cell, string reason)
        {
            EmitSignal(SignalName.ToolRejected, action.ToString(), cell.X, cell.Y, reason);
            return false;
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

            if (_selection == null || !GodotObject.IsInstanceValid(_selection))
                _selection = !SelectionPath.IsEmpty
                    ? GetNodeOrNull<GridSelectionComponent>(SelectionPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridSelectionComponent>(GetTree()?.CurrentScene) : null;

            if (_jobs == null || !GodotObject.IsInstanceValid(_jobs))
                _jobs = !JobQueuePath.IsEmpty
                    ? GetNodeOrNull<GridJobQueueComponent>(JobQueuePath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridJobQueueComponent>(GetTree()?.CurrentScene) : null;

            if (_roads == null || !GodotObject.IsInstanceValid(_roads))
                _roads = !RoadPath.IsEmpty
                    ? GetNodeOrNull<GridRoadComponent>(RoadPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridRoadComponent>(GetTree()?.CurrentScene) : null;

            if (_navigation == null || !GodotObject.IsInstanceValid(_navigation))
                _navigation = !NavigationPath.IsEmpty
                    ? GetNodeOrNull<GridNavigationComponent>(NavigationPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridNavigationComponent>(GetTree()?.CurrentScene) : null;

            if (_cropCatalog == null || !GodotObject.IsInstanceValid(_cropCatalog))
                _cropCatalog = !CropCatalogPath.IsEmpty
                    ? GetNodeOrNull<GridCropCatalogComponent>(CropCatalogPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridCropCatalogComponent>(GetTree()?.CurrentScene) : null;

            if (_calendar == null || !GodotObject.IsInstanceValid(_calendar))
                _calendar = !CalendarPath.IsEmpty
                    ? GetNodeOrNull<GridCalendarComponent>(CalendarPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridCalendarComponent>(GetTree()?.CurrentScene) : null;

            if (_resourceWallet == null || !GodotObject.IsInstanceValid(_resourceWallet))
                _resourceWallet = !ResourceWalletPath.IsEmpty
                    ? GetNodeOrNull<GridResourceWalletComponent>(ResourceWalletPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridResourceWalletComponent>(GetTree()?.CurrentScene) : null;
        }

        private bool CanWorkTerrain(Vector2I cell)
        {
            if (_navigation != null && UseNavigationBounds && !_navigation.IsInBounds(cell))
                return false;

            if (_cells == null)
                return true;

            string terrainKind = NormalizeTerrainKind(_cells.GetTerrainKind(cell));
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
                    return false;
            }

            if (TreatBlockedTerrainKindsAsUnworkable)
            {
                if (IsBlockedTerrainKind(terrainKind))
                    return false;
            }

            return true;
        }

        private string? WorkJobBlockReason(Vector2I cell)
        {
            if (_navigation != null)
            {
                if (UseNavigationBounds && !_navigation.IsInBounds(cell))
                    return "cell_out_of_bounds";

                if (RejectNavigationBlockedCellsForJobs && _navigation.IsBlocked(cell))
                    return "blocked_cell";
            }

            return CanWorkTerrain(cell) ? null : "unworkable_terrain";
        }

        private static bool TryReadCell(Variant value, out Vector2I cell)
        {
            cell = default;
            if (value.VariantType == Variant.Type.Vector2I)
            {
                cell = value.AsVector2I();
                return cell.X != int.MinValue && cell.Y != int.MinValue;
            }

            if (value.VariantType == Variant.Type.Vector2)
            {
                Vector2 point = value.AsVector2();
                if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
                    return false;

                cell = new Vector2I(Mathf.RoundToInt(point.X), Mathf.RoundToInt(point.Y));
                return cell.X != int.MinValue && cell.Y != int.MinValue;
            }

            if (value.VariantType == Variant.Type.Dictionary)
            {
                var data = value.AsGodotDictionary();
                if (data.ContainsKey("cell"))
                    return TryReadCell(data["cell"], out cell);

                Variant x = ReadVariant(data, "x", "X");
                Variant y = ReadVariant(data, "y", "Y");
                if (TryReadInt(x, out int ix) && TryReadInt(y, out int iy))
                {
                    cell = new Vector2I(ix, iy);
                    return cell.X != int.MinValue && cell.Y != int.MinValue;
                }
            }

            return false;
        }

        private static Variant ReadVariant(Godot.Collections.Dictionary data, string lower, string upper)
        {
            if (data.ContainsKey(lower)) return data[lower];
            if (data.ContainsKey(upper)) return data[upper];
            return default;
        }

        private static bool TryReadInt(Variant value, out int result)
        {
            result = 0;
            if (value.VariantType == Variant.Type.Int)
            {
                result = value.AsInt32();
                return true;
            }
            if (value.VariantType == Variant.Type.Float)
            {
                double raw = value.AsDouble();
                if (!double.IsFinite(raw))
                    return false;
                result = Mathf.RoundToInt((float)raw);
                return true;
            }
            if (value.VariantType == Variant.Type.String)
                return int.TryParse(value.AsString(), out result);
            return false;
        }

        private static string NormalizeTerrainKind(string value)
            => string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');

        private bool IsBlockedTerrainKind(string normalizedTerrainKind)
        {
            if (BlockedTerrainKinds.Count == 0)
                return normalizedTerrainKind is "water" or "sea" or "ocean" or "deep_water" or "lava";

            foreach (string blockedKind in BlockedTerrainKinds)
                if (NormalizeTerrainKind(blockedKind) == normalizedTerrainKind)
                    return true;

            return false;
        }
    }
}

using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Grid-based scene placement for top-down and isometric 2D games.
    ///
    /// Pair with <see cref="GridProjectionComponent"/>. UI can call BeginPlacement
    /// after a toolbar/build-menu selection, and this component handles preview
    /// movement, grid snapping, footprint occupancy, click-to-place, and cancel.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridPlacementComponent : Node2D
    {
        public enum PlacementState
        {
            Idle,
            Placing
        }

        [Signal] public delegate void PlacementStartedEventHandler(string id);
        [Signal] public delegate void PlacementMovedEventHandler(string id, int x, int y, bool valid);
        [Signal] public delegate void PlacementPlacedEventHandler(string id, Node2D placed, int x, int y);
        [Signal] public delegate void PlacementCancelledEventHandler(string id);
        [Signal] public delegate void PlacementRejectedEventHandler(string id, int x, int y, string reason);

        [Export] public NodePath GridPath { get; set; } = new("");
        [Export] public NodePath PlacementRootPath { get; set; } = new("");
        [Export] public NodePath ResourceWalletPath { get; set; } = new("");
        [Export] public NodePath CellDataPath { get; set; } = new("");
        /// <summary>
        /// Optional bridge to the terrain engine: when set, terrain kinds come
        /// from the TerrainDataLayersComponent's generated map, with cell data
        /// as the fallback where the layers have no tile. Explicit wire only.
        /// </summary>
        [Export] public NodePath DataLayersPath { get; set; } = new("");
        [Export] public NodePath NavigationPath { get; set; } = new("");
        [Export] public PackedScene? PlacementScene { get; set; }
        [Export] public Texture2D? PreviewTexture { get; set; }
        [Export] public string PlacementId { get; set; } = "";
        [Export] public Vector2I Footprint { get; set; } = Vector2I.One;
        [Export] public bool UseMouseInput { get; set; } = true;
        [Export] public bool ChargeCostOnConfirm { get; set; } = true;
        [Export] public bool KeepPlacingAfterConfirm { get; set; } = true;
        [Export] public bool MarkPlacedCellsOccupied { get; set; } = true;
        [Export] public bool MarkPlacedCellsBlockedInNavigation { get; set; } = true;
        [Export] public bool TreatCellDataBlockedAsUnplaceable { get; set; } = true;
        [Export] public bool TreatBlockedTerrainKindsAsUnplaceable { get; set; } = true;
        [Export] public Godot.Collections.Array<string> BlockedTerrainKinds { get; set; }
            = GridTerrainRules.DefaultBlockedTerrainKinds();
        [Export] public Godot.Collections.Array<string> AllowedTerrainKinds { get; set; } = new();
        [Export] public bool SetZIndexFromY { get; set; } = true;
        [Export] public int ZIndexOffset { get; set; } = 0;
        [Export] public Color ValidPreviewColor { get; set; } = new(0.35f, 1f, 0.55f, 0.62f);
        [Export] public Color InvalidPreviewColor { get; set; } = new(1f, 0.28f, 0.22f, 0.58f);

        public Vector2I EffectiveFootprint => new(Mathf.Max(1, Footprint.X), Mathf.Max(1, Footprint.Y));

        public PlacementState State { get; private set; } = PlacementState.Idle;
        public Vector2I CurrentCell { get; private set; } = new(int.MinValue, int.MinValue);
        public bool CurrentCellValid { get; private set; }

        private readonly HashSet<Vector2I> _occupied = new();
        private GridProjectionComponent? _grid;
        private Node? _placementRoot;
        private GridResourceWalletComponent? _resourceWallet;
        private GridCellDataComponent? _cellData;
        private TerrainDataLayersComponent? _dataLayers;
        private GridNavigationComponent? _navigation;
        private Node2D? _preview;
        private PackedScene? _activeScene;
        private string _activeId = "";
        private string _activeDisplayName = "";
        private string _activeCategory = "";
        private Godot.Collections.Array _activeCosts = new();
        private bool _activeChargeCostOnConfirm;
        private bool _pendingDefinitionPlacement;
        // Whether the ACTIVE build blocks navigation. Occupancy and navigation
        // blocking are different facts - a garden is walkable but not
        // buildable-over - and folding this into MarkPlacedCellsOccupied was
        // how every walkable build also became infinitely stackable.
        private bool _activeBlocksNavigation = true;

        public override void _Ready()
        {
            ResolveReferences();
            SetProcess(!Engine.IsEditorHint());
            SetProcessUnhandledInput(!Engine.IsEditorHint());
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree() => ClearPreview();

        public override void _Process(double delta)
        {
            if (State != PlacementState.Placing || _grid == null) return;
            MovePreviewTo(_grid.MouseCell());
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!UseMouseInput || State != PlacementState.Placing) return;

            if (@event is InputEventMouseButton { Pressed: true } mouse)
            {
                if (mouse.ButtonIndex == MouseButton.Left)
                {
                    ConfirmPlacement();
                    GetViewport().SetInputAsHandled();
                }
                else if (mouse.ButtonIndex == MouseButton.Right)
                {
                    CancelPlacement();
                    GetViewport().SetInputAsHandled();
                }
            }
            else if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
            {
                CancelPlacement();
                GetViewport().SetInputAsHandled();
            }
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (GridPath.IsEmpty)
                return new[] { "GridPath should point to a GridProjectionComponent." };

            if (Footprint.X <= 0 || Footprint.Y <= 0)
                return new[] { "Footprint must be at least 1x1." };

            return System.Array.Empty<string>();
        }

        public void BeginPlacement(PackedScene scene, string id = "")
        {
            PlacementScene = scene;
            _activeCosts = new Godot.Collections.Array();
            _activeChargeCostOnConfirm = false;
            BeginPlacement(id);
        }

        public void BeginPlacement(GridBuildDefinition definition, bool chargeCostOnConfirm = true)
        {
            if (definition == null)
            {
                GD.PushWarning($"[{Name}] BeginPlacement needs a GridBuildDefinition.");
                return;
            }

            PlacementScene = definition.Scene;
            PreviewTexture = definition.PreviewTexture;
            PlacementId = definition.BuildId;
            Footprint = definition.EffectiveFootprint;
            MarkPlacedCellsOccupied = definition.OccupiesCells;
            _activeBlocksNavigation = definition.BlocksNavigation;
            SetZIndexFromY = definition.SetZIndexFromY;
            _activeDisplayName = definition.DisplayName;
            _activeCategory = definition.Category;
            _activeCosts = definition.Costs;
            _activeChargeCostOnConfirm = chargeCostOnConfirm;
            _pendingDefinitionPlacement = true;
            BeginPlacement(definition.BuildId);
        }

        public void BeginPlacement(string id = "")
        {
            bool fromDefinition = _pendingDefinitionPlacement;
            _pendingDefinitionPlacement = false;
            if (!fromDefinition)
            {
                _activeCosts = new Godot.Collections.Array();
                _activeChargeCostOnConfirm = false;
                _activeBlocksNavigation = true;
            }

            ResolveReferences();
            if (_grid == null)
            {
                GD.PushWarning($"[{Name}] GridPlacementComponent cannot place without a GridProjectionComponent at GridPath.");
                return;
            }

            _activeScene = PlacementScene;
            _activeId = string.IsNullOrEmpty(id) ? PlacementId : id;
            if (string.IsNullOrEmpty(_activeId))
            {
                _activeCosts = new Godot.Collections.Array();
                _activeChargeCostOnConfirm = false;
            }

            if (_activeScene == null && PreviewTexture == null)
            {
                GD.PushWarning($"[{Name}] BeginPlacement needs PlacementScene or PreviewTexture.");
                return;
            }

            State = PlacementState.Placing;
            BuildPreview();
            MovePreviewTo(_grid.MouseCell(), forceSignal: true);
            EmitSignal(SignalName.PlacementStarted, _activeId);
        }

        public void CancelPlacement()
        {
            if (State == PlacementState.Idle) return;
            string id = _activeId;
            State = PlacementState.Idle;
            _activeScene = null;
            _activeId = "";
            _activeDisplayName = "";
            _activeCategory = "";
            _activeCosts = new Godot.Collections.Array();
            _activeChargeCostOnConfirm = false;
            CurrentCell = new Vector2I(int.MinValue, int.MinValue);
            CurrentCellValid = false;
            ClearPreview();
            EmitSignal(SignalName.PlacementCancelled, id);
        }

        public Node2D? ConfirmPlacement()
        {
            if (State != PlacementState.Placing || _grid == null)
                return null;

            if (!CurrentCellValid)
            {
                EmitSignal(SignalName.PlacementRejected, _activeId, CurrentCell.X, CurrentCell.Y, "occupied");
                return null;
            }

            bool chargeCost = _activeChargeCostOnConfirm && ChargeCostOnConfirm && _activeCosts.Count > 0;
            if (chargeCost)
            {
                ResolveReferences();
                if (_resourceWallet == null)
                {
                    EmitSignal(SignalName.PlacementRejected, _activeId, CurrentCell.X, CurrentCell.Y, "missing_resource_wallet");
                    return null;
                }

                if (!_resourceWallet.Spend(_activeCosts))
                {
                    EmitSignal(SignalName.PlacementRejected, _activeId, CurrentCell.X, CurrentCell.Y, "missing_resources");
                    return null;
                }
            }

            if (_activeScene == null)
            {
                EmitSignal(SignalName.PlacementRejected, _activeId, CurrentCell.X, CurrentCell.Y, "missing_scene");
                if (chargeCost)
                    _resourceWallet?.Refund(_activeCosts);
                return null;
            }

            if (_activeScene.Instantiate() is not Node2D placed)
            {
                EmitSignal(SignalName.PlacementRejected, _activeId, CurrentCell.X, CurrentCell.Y, "scene_root_not_node2d");
                if (chargeCost)
                    _resourceWallet?.Refund(_activeCosts);
                return null;
            }

            (_placementRoot ?? GetParent() ?? this).AddChild(placed);
            placed.GlobalPosition = _grid.CellToWorld(CurrentCell);
            if (SetZIndexFromY)
                placed.ZIndex = ClampZ(ZIndexOffset + Mathf.RoundToInt(placed.GlobalPosition.Y));

            // Whether the wallet was actually charged travels with the node,
            // so a later teardown (a cancelled build job) knows whether a
            // refund is owed rather than guessing.
            placed.SetMeta("grid_build_cost_charged", chargeCost);

            ConfigurePlacedObject(placed, CurrentCell);

            // Occupancy and navigation are marked SEPARATELY: a walkable
            // build still occupies its cells, and a rare stackable decoration
            // (OccupiesCells false) can still block navigation if authored so.
            if (MarkPlacedCellsOccupied)
                SetFootprintOccupied(CurrentCell, true);
            if (_activeBlocksNavigation)
                SetFootprintNavigationBlocked(CurrentCell, true);

            EmitSignal(SignalName.PlacementPlaced, _activeId, placed, CurrentCell.X, CurrentCell.Y);

            if (KeepPlacingAfterConfirm)
            {
                BuildPreview();
                MovePreviewTo(CurrentCell, forceSignal: true);
            }
            else
            {
                FinishPlacement();
            }

            return placed;
        }

        public bool MovePreviewToCell(Vector2I cell)
        {
            ResolveReferences();
            if (State != PlacementState.Placing || _grid == null)
                return false;

            MovePreviewTo(cell, forceSignal: true);
            return CurrentCellValid;
        }

        public bool CanPlace(Vector2I anchorCell)
        {
            ResolveReferences();
            foreach (Vector2I cell in FootprintCells(anchorCell))
                if (_occupied.Contains(cell) || !CanPlaceOnCellData(cell))
                    return false;
            return true;
        }

        public void SetOccupied(Vector2I cell, bool occupied)
        {
            if (occupied) _occupied.Add(cell);
            else _occupied.Remove(cell);
        }

        public bool IsOccupied(Vector2I cell) => _occupied.Contains(cell);

        public void ClearOccupied() => _occupied.Clear();

        public void SetFootprintOccupied(Vector2I anchorCell, bool occupied)
        {
            foreach (Vector2I cell in FootprintCells(anchorCell))
                SetOccupied(cell, occupied);
        }

        public Godot.Collections.Array<Vector2I> GetOccupiedCells()
        {
            var cells = new Godot.Collections.Array<Vector2I>();
            foreach (Vector2I cell in _occupied)
                cells.Add(cell);
            return cells;
        }

        private void ResolveReferences()
        {
            if (_grid == null || !GodotObject.IsInstanceValid(_grid))
                _grid = !GridPath.IsEmpty
                    ? GetNodeOrNull<GridProjectionComponent>(GridPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridProjectionComponent>(GetTree()?.CurrentScene) : null;

            if (_placementRoot == null || !GodotObject.IsInstanceValid(_placementRoot))
                _placementRoot = !PlacementRootPath.IsEmpty
                    ? GetNodeOrNull<Node>(PlacementRootPath)
                    : GetParent();

            if (_resourceWallet == null || !GodotObject.IsInstanceValid(_resourceWallet))
                _resourceWallet = !ResourceWalletPath.IsEmpty
                    ? GetNodeOrNull<GridResourceWalletComponent>(ResourceWalletPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridResourceWalletComponent>(GetTree()?.CurrentScene) : null;

            if (_cellData == null || !GodotObject.IsInstanceValid(_cellData))
                _cellData = !CellDataPath.IsEmpty
                    ? GetNodeOrNull<GridCellDataComponent>(CellDataPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridCellDataComponent>(GetTree()?.CurrentScene) : null;

            if (_navigation == null || !GodotObject.IsInstanceValid(_navigation))
                _navigation = !NavigationPath.IsEmpty
                    ? GetNodeOrNull<GridNavigationComponent>(NavigationPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridNavigationComponent>(GetTree()?.CurrentScene) : null;

            // Explicit wire only, never found scene-wide - see DataLayersPath.
            if (_dataLayers == null || !GodotObject.IsInstanceValid(_dataLayers))
                _dataLayers = !DataLayersPath.IsEmpty
                    ? GetNodeOrNull<TerrainDataLayersComponent>(DataLayersPath)
                    : null;
        }

        private string TerrainKindAt(Vector2I cell)
        {
            string kind = _dataLayers is null ? "" : GridTerrainRules.Normalize(_dataLayers.TerrainAt(cell));
            if (kind.Length == 0 && _cellData is not null)
                kind = GridTerrainRules.Normalize(_cellData.GetTerrainKind(cell));
            return kind;
        }

        private bool CanPlaceOnCellData(Vector2I cell)
        {
            if (_cellData == null && _dataLayers == null)
                return true;

            if (TreatCellDataBlockedAsUnplaceable
                && _cellData != null
                && _cellData.HasFlag(cell, GridCellDataComponent.CellFlags.Blocked))
                return false;

            string terrainKind = TerrainKindAt(cell);
            if (!GridTerrainRules.IsAllowed(terrainKind, AllowedTerrainKinds))
                return false;

            if (TreatBlockedTerrainKindsAsUnplaceable
                && GridTerrainRules.MatchesAny(terrainKind, BlockedTerrainKinds))
                return false;

            return true;
        }

        private void BuildPreview()
        {
            ClearPreview();

            if (PreviewTexture != null)
            {
                _preview = new Sprite2D
                {
                    Name = "PlacementPreview",
                    Texture = PreviewTexture,
                    Centered = true,
                    Modulate = ValidPreviewColor,
                    ZIndex = 4096
                };
            }
            else if (_activeScene?.Instantiate() is Node2D scenePreview)
            {
                _preview = scenePreview;
                _preview.Name = "PlacementPreview";
                _preview.Modulate = ValidPreviewColor;
                _preview.ProcessMode = ProcessModeEnum.Disabled;
                _preview.ZIndex = 4096;
                DisablePreviewCollision(_preview);
            }

            if (_preview != null)
                AddChild(_preview);
        }

        private void ClearPreview()
        {
            if (_preview != null && GodotObject.IsInstanceValid(_preview))
                _preview.QueueFree();
            _preview = null;
        }

        private void FinishPlacement()
        {
            State = PlacementState.Idle;
            _activeScene = null;
            _activeId = "";
            _activeDisplayName = "";
            _activeCategory = "";
            _activeCosts = new Godot.Collections.Array();
            _activeChargeCostOnConfirm = false;
            CurrentCell = new Vector2I(int.MinValue, int.MinValue);
            CurrentCellValid = false;
            ClearPreview();
        }

        private void MovePreviewTo(Vector2I cell, bool forceSignal = false)
        {
            if (_grid == null) return;

            bool valid = CanPlace(cell);
            bool changed = forceSignal || cell != CurrentCell || valid != CurrentCellValid;
            CurrentCell = cell;
            CurrentCellValid = valid;

            if (_preview != null)
            {
                _preview.GlobalPosition = _grid.CellToWorld(cell);
                _preview.Modulate = valid ? ValidPreviewColor : InvalidPreviewColor;
                if (SetZIndexFromY)
                    _preview.ZIndex = ClampZ(4000 + ZIndexOffset + Mathf.RoundToInt(_preview.GlobalPosition.Y));
            }

            if (changed)
                EmitSignal(SignalName.PlacementMoved, _activeId, cell.X, cell.Y, valid);
        }

        private void ConfigurePlacedObject(Node2D placed, Vector2I cell)
        {
            GridObjectComponent? gridObject = EntityComponent.FindComponent<GridObjectComponent>(placed, recursive: false);
            if (gridObject == null)
            {
                gridObject = new GridObjectComponent { Name = "GridObject" };
                placed.AddChild(gridObject);
            }

            string displayName = string.IsNullOrWhiteSpace(_activeDisplayName) ? _activeId : _activeDisplayName;
            // The object mirrors the marks placement makes and RESERVES them
            // itself, so freeing the placed node (demolition, a cancelled
            // build job) releases exactly those cells on exit. Before this,
            // placement marked directly and the object's reserved sets stayed
            // empty - deleting a placed building leaked its occupancy and
            // navigation blocks forever.
            gridObject.ReservePlacementFootprint = MarkPlacedCellsOccupied;
            gridObject.ReserveNavigationFootprint = _activeBlocksNavigation && MarkPlacedCellsBlockedInNavigation;
            gridObject.ReserveFootprintOnReady = true;
            gridObject.Configure(_activeId, displayName, _activeCategory, cell, Footprint, _activeBlocksNavigation);
        }

        private void SetFootprintNavigationBlocked(Vector2I anchorCell, bool blocked)
        {
            if (!MarkPlacedCellsBlockedInNavigation)
                return;

            ResolveReferences();
            if (_navigation == null)
                return;

            foreach (Vector2I cell in FootprintCells(anchorCell))
                _navigation.SetBlocked(cell, blocked);
        }

        private IEnumerable<Vector2I> FootprintCells(Vector2I anchorCell)
        {
            int width = EffectiveFootprint.X;
            int height = EffectiveFootprint.Y;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    yield return new Vector2I(anchorCell.X + x, anchorCell.Y + y);
        }

        private static void DisablePreviewCollision(Node node)
        {
            if (node is CollisionObject2D collision)
            {
                collision.CollisionLayer = 0;
                collision.CollisionMask = 0;
            }

            foreach (Node child in node.GetChildren())
                DisablePreviewCollision(child);
        }

        private static int ClampZ(int zIndex)
            => zIndex < (int)RenderingServer.CanvasItemZMin
                ? (int)RenderingServer.CanvasItemZMin
                : zIndex > (int)RenderingServer.CanvasItemZMax
                    ? (int)RenderingServer.CanvasItemZMax
                    : zIndex;
    }
}

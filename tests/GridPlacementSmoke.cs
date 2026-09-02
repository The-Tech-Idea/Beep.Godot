using Beep.ECS;
using Godot;

[GlobalClass]
public partial class GridPlacementSmoke : Node
{
    public string Failure { get; private set; } = string.Empty;

    public bool Run()
    {
        Failure = string.Empty;

        if (!VerifyTopDownProjection()) return false;
        if (!VerifyIsometricProjection()) return false;
        if (!VerifyProjectionBoundsInvalidTuning()) return false;
        if (!VerifyPlacementOccupancy()) return false;
        if (!VerifyPlacementUsesCellDataTerrain()) return false;
        if (!VerifyPlacementMarksNavigationFootprint()) return false;
        if (!VerifyGridInteractionMode()) return false;
        if (!VerifyGridInteractionModeBar()) return false;
        if (!VerifyGridInteractionStatus()) return false;
        if (!VerifyGridInteractionCursor()) return false;
        if (!VerifyGridObjectComponent()) return false;
        if (!VerifyGridObjectInspector()) return false;
        if (!VerifyGridBuildCatalogAndResources()) return false;
        if (!VerifyGridBuildSites()) return false;
        if (!VerifyGridResourceBar()) return false;
        if (!VerifyGridBuildToolbar()) return false;
        if (!VerifyNavigationAvoidsBlockedCells()) return false;
        if (!VerifyNavigationUsesPlacementOccupancy()) return false;
        if (!VerifyNavigationUsesCellDataTerrain()) return false;
        if (!VerifyGridRoads()) return false;
        if (!VerifyGridRoadsUseCellDataTerrain()) return false;
        if (!VerifyPathFollowerMovesBody()) return false;
        if (!VerifyPathFollowerBoundsInvalidTuning()) return false;
        if (!VerifyGridSelectionState()) return false;
        if (!VerifyGridCameraController()) return false;
        if (!VerifyGridJobQueueBoundsInvalidWorkSeconds()) return false;
        if (!VerifyGridJobQueueAndWorker()) return false;
        if (!VerifyGridWorkerBoundsInvalidTuning()) return false;
        if (!VerifyGridWorkerRejectsClaimedJob()) return false;
        if (!VerifyGridWorkerStatusPanel()) return false;
        if (!VerifyGridJobBoard()) return false;
        if (!VerifyGridJobEffects()) return false;
        if (!VerifyGridResourceNodes()) return false;
        if (!VerifyGridResourceScatter()) return false;
        if (!VerifyGridResourceScatterBoundsInvalidTuning()) return false;
        if (!VerifyGridProduction()) return false;
        if (!VerifyGridProductionPanel()) return false;
        if (!VerifyGridObjectiveTracker()) return false;
        if (!VerifyGridObjectivePanel()) return false;
        if (!VerifyGridObjectiveEventBinder()) return false;
        if (!VerifyGridWorkerSpawner()) return false;
        if (!VerifyGridWorkerSpawnerUsesCellDataTerrain()) return false;
        if (!VerifyGridWorkerSpawnerBoundsInvalidTuning()) return false;
        if (!VerifyGridWorkerSpawnerPanel()) return false;
        if (!VerifySelectionJobCommand()) return false;
        if (!VerifySelectionJobCommandUsesTerrainRules()) return false;
        if (!VerifySelectionJobCommandBoundsInvalidTuning()) return false;
        if (!VerifyGridCellData()) return false;
        if (!VerifyGridTerrainGenerator()) return false;
        if (!VerifyGridToolActions()) return false;
        if (!VerifyGridToolActionsBoundInvalidTuning()) return false;
        if (!VerifyGridLooseArrayInputs()) return false;
        if (!VerifyGridMalformedStateInputs()) return false;
        if (!VerifyGridToolPalette()) return false;
        if (!VerifyGridMinimap()) return false;
        if (!VerifyGridCropCatalog()) return false;
        if (!VerifyGridCellOverlay()) return false;
        if (!VerifyGridVisualHelpersBoundInvalidTuning()) return false;
        if (!VerifyGridTileMapLayerBridge()) return false;
        if (!VerifyGridCalendar()) return false;
        if (!VerifyGridCalendarHud()) return false;
        if (!VerifyGridWorldStateRoundTrip()) return false;

        return true;
    }

    private bool VerifyTopDownProjection()
    {
        var grid = new GridProjectionComponent
        {
            Projection = GridProjectionComponent.GridProjection.TopDown,
            TileSize = new Vector2(64, 32),
            Origin = new Vector2(10, 20)
        };

        Vector2I cell = new(2, 3);
        Vector2 world = grid.CellToWorld(cell);
        if (!Expect(world.IsEqualApprox(new Vector2(170, 132)), $"Top-down CellToWorld returned {world}."))
            return false;

        if (!Expect(grid.WorldToCell(world) == cell, "Top-down WorldToCell did not round-trip the cell center."))
            return false;

        if (!Expect(grid.WorldToCell(new Vector2(10, 20)) == Vector2I.Zero, "Top-down origin should be inside cell 0,0."))
            return false;

        return true;
    }

    private bool VerifyIsometricProjection()
    {
        var grid = new GridProjectionComponent
        {
            Projection = GridProjectionComponent.GridProjection.Isometric,
            TileSize = new Vector2(64, 32),
            Origin = new Vector2(100, 50)
        };

        Vector2I cell = new(3, 1);
        Vector2 world = grid.CellToWorld(cell);
        if (!Expect(world.IsEqualApprox(new Vector2(164, 114)), $"Isometric CellToWorld returned {world}."))
            return false;

        if (!Expect(grid.WorldToCell(world) == cell, "Isometric WorldToCell did not round-trip the cell center."))
            return false;

        if (!Expect(grid.WorldToCell(grid.CellToWorld(new Vector2I(-2, 5))) == new Vector2I(-2, 5),
                "Isometric WorldToCell did not round-trip a negative/positive mixed cell."))
            return false;

        return true;
    }

    private bool VerifyProjectionBoundsInvalidTuning()
    {
        var grid = new GridProjectionComponent
        {
            Projection = GridProjectionComponent.GridProjection.TopDown,
            TileSize = new Vector2(float.NaN, -32f),
            Origin = new Vector2(float.NaN, float.PositiveInfinity)
        };

        Vector2 world = grid.CellToWorld(Vector2I.Zero);
        Vector2I roundTrip = grid.WorldToCell(world);
        Vector2I invalid = grid.WorldToCell(new Vector2(float.NaN, 4f));
        bool bounded = float.IsFinite(world.X)
            && float.IsFinite(world.Y)
            && roundTrip == Vector2I.Zero
            && invalid.X == int.MinValue
            && grid.EffectiveTileSize.X >= 1f
            && grid.EffectiveTileSize.Y >= 1f;

        if (!Expect(bounded, $"GridProjection did not bound invalid tile/origin values. world={world}, roundTrip={roundTrip}, invalid={invalid}."))
            return false;

        return true;
    }

    private bool VerifyPlacementOccupancy()
    {
        var placement = new GridPlacementComponent { Footprint = new Vector2I(2, 3) };
        Vector2I anchor = new(4, 7);

        if (!Expect(placement.CanPlace(anchor), "Fresh placement grid should allow an empty footprint."))
            return false;

        placement.SetFootprintOccupied(anchor, true);
        if (!Expect(!placement.CanPlace(anchor), "Occupied anchor footprint should reject placement."))
            return false;

        if (!Expect(!placement.CanPlace(new Vector2I(5, 9)), "Overlapping footprint should reject placement."))
            return false;

        if (!Expect(placement.CanPlace(new Vector2I(6, 10)), "Non-overlapping footprint should still be placeable."))
            return false;

        placement.SetFootprintOccupied(anchor, false);
        if (!Expect(placement.CanPlace(anchor), "Cleared footprint should be placeable again."))
            return false;

        return true;
    }

    private bool VerifyPlacementUsesCellDataTerrain()
    {
        var root = new Node { Name = "GridPlacementCellDataSmokeRoot" };
        AddChild(root);

        var cells = new GridCellDataComponent
        {
            Name = "Cells",
            DefaultTerrainKind = "grass"
        };
        cells.SetTerrainKind(new Vector2I(1, 1), "water");
        cells.AddFlag(new Vector2I(2, 2), GridCellDataComponent.CellFlags.Blocked);
        cells.SetTerrainKind(new Vector2I(3, 3), "sand");
        root.AddChild(cells);

        var placement = new GridPlacementComponent
        {
            Name = "Placement",
            CellDataPath = new NodePath("../Cells"),
            Footprint = Vector2I.One
        };
        root.AddChild(placement);

        bool rejectsWater = !placement.CanPlace(new Vector2I(1, 1));
        bool rejectsBlockedFlag = !placement.CanPlace(new Vector2I(2, 2));
        bool allowsGrass = placement.CanPlace(Vector2I.Zero);
        placement.AllowedTerrainKinds.Add("sand");
        bool whitelistRejectsGrass = !placement.CanPlace(Vector2I.Zero);
        bool whitelistAllowsSand = placement.CanPlace(new Vector2I(3, 3));

        root.QueueFree();

        if (!Expect(rejectsWater && rejectsBlockedFlag && allowsGrass, "GridPlacement did not consume GridCellData blocked terrain and flags."))
            return false;

        if (!Expect(whitelistRejectsGrass && whitelistAllowsSand, "GridPlacement did not enforce AllowedTerrainKinds."))
            return false;

        return true;
    }

    private bool VerifyPlacementMarksNavigationFootprint()
    {
        var root = new Node { Name = "GridPlacementNavigationSmokeRoot" };
        AddChild(root);

        var grid = new GridProjectionComponent
        {
            Name = "Grid",
            Projection = GridProjectionComponent.GridProjection.TopDown,
            TileSize = new Vector2(16, 16)
        };
        root.AddChild(grid);

        var buildings = new Node2D { Name = "Buildings" };
        root.AddChild(buildings);

        var navigation = new GridNavigationComponent
        {
            Name = "Navigation",
            GridPath = new NodePath("../Grid"),
            BoundsSize = new Vector2I(12, 12),
            TreatPlacementOccupiedAsBlocked = false
        };
        root.AddChild(navigation);

        var placement = new GridPlacementComponent
        {
            Name = "Placement",
            GridPath = new NodePath("../Grid"),
            PlacementRootPath = new NodePath("../Buildings"),
            NavigationPath = new NodePath("../Navigation"),
            UseMouseInput = false,
            KeepPlacingAfterConfirm = false
        };
        root.AddChild(placement);

        var buildingRoot = new Node2D { Name = "BlockingBuildingSceneRoot" };
        var scene = new PackedScene();
        Error packResult = scene.Pack(buildingRoot);
        buildingRoot.Free();
        if (!Expect(packResult == Error.Ok, $"PackedScene.Pack for placement navigation smoke returned {packResult}."))
        {
            root.QueueFree();
            return false;
        }

        var blockingBuild = new GridBuildDefinition
        {
            BuildId = "blocking_shed",
            DisplayName = "Blocking Shed",
            Scene = scene,
            Footprint = new Vector2I(2, 1),
            BlocksNavigation = true
        };

        placement.BeginPlacement(blockingBuild, chargeCostOnConfirm: false);
        placement.MovePreviewToCell(new Vector2I(2, 2));
        Node2D? blockingPlaced = placement.ConfirmPlacement();

        bool blockingMarked = blockingPlaced != null
            && placement.IsOccupied(new Vector2I(2, 2))
            && placement.IsOccupied(new Vector2I(3, 2))
            && navigation.IsBlocked(new Vector2I(2, 2))
            && navigation.IsBlocked(new Vector2I(3, 2))
            && !navigation.IsBlocked(new Vector2I(4, 2));

        var decorationBuild = new GridBuildDefinition
        {
            BuildId = "decor_flag",
            DisplayName = "Decor Flag",
            Scene = scene,
            Footprint = Vector2I.One,
            BlocksNavigation = false
        };

        placement.BeginPlacement(decorationBuild, chargeCostOnConfirm: false);
        placement.MovePreviewToCell(new Vector2I(5, 5));
        Node2D? decorationPlaced = placement.ConfirmPlacement();

        bool decorationOpen = decorationPlaced != null
            && !placement.IsOccupied(new Vector2I(5, 5))
            && !navigation.IsBlocked(new Vector2I(5, 5));

        root.QueueFree();

        if (!Expect(blockingMarked, "GridPlacement did not mark every blocking build footprint cell in placement and navigation."))
            return false;

        if (!Expect(decorationOpen, "GridPlacement incorrectly blocked placement/navigation for a non-blocking build definition."))
            return false;

        return true;
    }

    private bool VerifyGridInteractionMode()
    {
        var root = new Node { Name = "GridInteractionModeSmokeRoot" };
        AddChild(root);

        var grid = new GridProjectionComponent
        {
            Name = "Grid",
            Projection = GridProjectionComponent.GridProjection.TopDown,
            TileSize = new Vector2(16, 16)
        };
        root.AddChild(grid);

        var cells = new GridCellDataComponent { Name = "Cells" };
        root.AddChild(cells);

        var selection = new GridSelectionComponent
        {
            Name = "Selection",
            GridPath = new NodePath("../Grid"),
            UseMouseInput = true,
            DrawSelection = false
        };
        root.AddChild(selection);

        var buildings = new Node2D { Name = "Buildings" };
        root.AddChild(buildings);

        var placement = new GridPlacementComponent
        {
            Name = "Placement",
            GridPath = new NodePath("../Grid"),
            PlacementRootPath = new NodePath("../Buildings"),
            UseMouseInput = true,
            KeepPlacingAfterConfirm = false
        };
        root.AddChild(placement);

        var tools = new GridToolActionComponent
        {
            Name = "Tools",
            GridPath = new NodePath("../Grid"),
            CellDataPath = new NodePath("../Cells"),
            SelectionPath = new NodePath("../Selection"),
            UseMouseInput = true,
            CurrentAction = GridToolActionComponent.ToolAction.Hoe
        };
        root.AddChild(tools);

        var coordinator = new GridInteractionModeComponent
        {
            Name = "InteractionMode",
            GridPath = new NodePath("../Grid"),
            SelectionPath = new NodePath("../Selection"),
            ToolActionPath = new NodePath("../Tools"),
            PlacementPath = new NodePath("../Placement"),
            ManageChildMouseInput = true
        };
        root.AddChild(coordinator);

        bool inputOwned = !selection.UseMouseInput && !tools.UseMouseInput && !placement.UseMouseInput;

        coordinator.SelectMode();
        bool selected = coordinator.HandlePrimaryCell(new Vector2I(2, 3))
            && selection.IsSelected(new Vector2I(2, 3));

        coordinator.ToolMode();
        bool tooled = coordinator.HandlePrimaryCell(new Vector2I(4, 5))
            && cells.HasFlag(new Vector2I(4, 5), GridCellDataComponent.CellFlags.Tilled);

        var buildingRoot = new Node2D { Name = "InteractionPlacedBuilding" };
        var scene = new PackedScene();
        Error packResult = scene.Pack(buildingRoot);
        buildingRoot.Free();
        if (!Expect(packResult == Error.Ok, $"PackedScene.Pack for interaction mode smoke returned {packResult}."))
        {
            root.QueueFree();
            return false;
        }

        placement.PlacementScene = scene;
        placement.BeginPlacement("shed");
        coordinator.BuildMode();
        bool built = coordinator.HandlePrimaryCell(new Vector2I(6, 7))
            && buildings.GetChildCount() == 1
            && placement.IsOccupied(new Vector2I(6, 7));

        coordinator.SelectMode();
        bool cancelled = !coordinator.CancelCurrentInteraction();
        root.QueueFree();

        if (!Expect(inputOwned, "GridInteractionMode did not take mouse input ownership from child grid components."))
            return false;

        if (!Expect(selected, "GridInteractionMode did not route select-mode clicks to GridSelectionComponent."))
            return false;

        if (!Expect(tooled, "GridInteractionMode did not route tool-mode clicks to GridToolActionComponent."))
            return false;

        if (!Expect(built, "GridInteractionMode did not route build-mode clicks to GridPlacementComponent."))
            return false;

        if (!Expect(cancelled, "GridInteractionMode reported a cancelled interaction when none was active."))
            return false;

        return true;
    }

    private bool VerifyGridInteractionModeBar()
    {
        var root = new Control { Name = "GridInteractionModeBarSmokeRoot" };
        AddChild(root);

        var interaction = new GridInteractionModeComponent
        {
            Name = "InteractionMode",
            UseMouseInput = false,
            ManageChildMouseInput = false,
            CurrentMode = GridInteractionModeComponent.InteractionMode.Select
        };
        root.AddChild(interaction);

        var bar = new GridInteractionModeBarComponent
        {
            Name = "ModeBar",
            InteractionModePath = new NodePath("../InteractionMode"),
            BuildInEditor = false,
            GenerateControlsWhenPathsEmpty = true,
            ShowDisabled = true
        };
        root.AddChild(bar);
        bar.RebuildBar();

        bool initial = bar.VisibleModeButtonCount() == 5
            && bar.SelectedModeName() == "Select";

        bool tool = bar.SelectMode(GridInteractionModeComponent.InteractionMode.Tool)
            && interaction.CurrentMode == GridInteractionModeComponent.InteractionMode.Tool
            && bar.SelectedModeName() == "Tool";

        interaction.SetMode(GridInteractionModeComponent.InteractionMode.Build);
        bar.RefreshSelection();
        bool synced = bar.SelectedModeName() == "Build";

        root.QueueFree();

        if (!Expect(initial, "GridInteractionModeBar did not render the expected mode buttons."))
            return false;

        if (!Expect(tool, "GridInteractionModeBar did not switch the interaction mode."))
            return false;

        if (!Expect(synced, "GridInteractionModeBar did not stay in sync with external mode changes."))
            return false;

        return true;
    }

    private bool VerifyGridInteractionStatus()
    {
        var root = new Control { Name = "GridInteractionStatusSmokeRoot" };
        AddChild(root);

        var grid = new GridProjectionComponent
        {
            Name = "Grid",
            Projection = GridProjectionComponent.GridProjection.TopDown,
            TileSize = new Vector2(16, 16)
        };
        root.AddChild(grid);

        var cells = new GridCellDataComponent { Name = "Cells" };
        root.AddChild(cells);

        var selection = new GridSelectionComponent
        {
            Name = "Selection",
            GridPath = new NodePath("../Grid"),
            UseMouseInput = false
        };
        root.AddChild(selection);

        var tools = new GridToolActionComponent
        {
            Name = "Tools",
            CellDataPath = new NodePath("../Cells"),
            CurrentAction = GridToolActionComponent.ToolAction.Water
        };
        root.AddChild(tools);

        var buildings = new Node2D { Name = "Buildings" };
        root.AddChild(buildings);
        var placement = new GridPlacementComponent
        {
            Name = "Placement",
            GridPath = new NodePath("../Grid"),
            PlacementRootPath = new NodePath("../Buildings"),
            PlacementId = "shed",
            UseMouseInput = false
        };
        root.AddChild(placement);

        var interaction = new GridInteractionModeComponent
        {
            Name = "InteractionMode",
            GridPath = new NodePath("../Grid"),
            SelectionPath = new NodePath("../Selection"),
            ToolActionPath = new NodePath("../Tools"),
            PlacementPath = new NodePath("../Placement"),
            UseMouseInput = false,
            ManageChildMouseInput = false
        };
        root.AddChild(interaction);

        var status = new GridInteractionStatusComponent
        {
            Name = "InteractionStatus",
            InteractionModePath = new NodePath("../InteractionMode"),
            SelectionPath = new NodePath("../Selection"),
            ToolActionPath = new NodePath("../Tools"),
            PlacementPath = new NodePath("../Placement"),
            BuildInEditor = false,
            GenerateControlsWhenPathsEmpty = true,
            AutoRefresh = false
        };
        root.AddChild(status);
        status.RebuildStatus();

        selection.UpdateHoverFromWorld(grid.CellToWorld(new Vector2I(1, 2)));
        bool selectText = status.StatusText().Contains("Select")
            && status.StatusText().Contains("Cell 1,2");

        interaction.ToolMode();
        bool toolText = status.StatusText().Contains("Tool")
            && status.StatusText().Contains("Water");

        var buildingRoot = new Node2D { Name = "StatusBuildingRoot" };
        var scene = new PackedScene();
        Error packResult = scene.Pack(buildingRoot);
        buildingRoot.Free();
        if (!Expect(packResult == Error.Ok, $"PackedScene.Pack for interaction status smoke returned {packResult}."))
        {
            root.QueueFree();
            return false;
        }

        placement.PlacementScene = scene;
        placement.BeginPlacement();
        placement.MovePreviewToCell(new Vector2I(3, 4));
        interaction.BuildMode();
        bool buildText = status.StatusText().Contains("Build")
            && status.StatusText().Contains("shed")
            && status.StatusText().Contains("Cell 3,4 ok");

        status.LastFeedback = "custom feedback";
        bool feedbackText = status.StatusText().Contains("custom feedback");
        root.QueueFree();

        if (!Expect(selectText, "GridInteractionStatus did not show select mode and hover cell."))
            return false;

        if (!Expect(toolText, "GridInteractionStatus did not show tool mode and current tool."))
            return false;

        if (!Expect(buildText, "GridInteractionStatus did not show build mode, build id, and placement cell validity."))
            return false;

        if (!Expect(feedbackText, "GridInteractionStatus did not show feedback text."))
            return false;

        return true;
    }

    private bool VerifyGridInteractionCursor()
    {
        var root = new Node2D { Name = "GridInteractionCursorSmokeRoot" };
        AddChild(root);

        var grid = new GridProjectionComponent
        {
            Name = "Grid",
            Projection = GridProjectionComponent.GridProjection.Isometric,
            TileSize = new Vector2(64, 32)
        };
        root.AddChild(grid);

        var selection = new GridSelectionComponent
        {
            Name = "Selection",
            GridPath = new NodePath("../Grid"),
            UseMouseInput = false
        };
        root.AddChild(selection);

        var buildings = new Node2D { Name = "Buildings" };
        root.AddChild(buildings);
        var placement = new GridPlacementComponent
        {
            Name = "Placement",
            GridPath = new NodePath("../Grid"),
            PlacementRootPath = new NodePath("../Buildings"),
            UseMouseInput = false,
            KeepPlacingAfterConfirm = false
        };
        root.AddChild(placement);

        var interaction = new GridInteractionModeComponent
        {
            Name = "InteractionMode",
            GridPath = new NodePath("../Grid"),
            SelectionPath = new NodePath("../Selection"),
            PlacementPath = new NodePath("../Placement"),
            UseMouseInput = false,
            ManageChildMouseInput = false
        };
        root.AddChild(interaction);

        var cursor = new GridInteractionCursorComponent
        {
            Name = "InteractionCursor",
            GridPath = new NodePath("../Grid"),
            InteractionModePath = new NodePath("../InteractionMode"),
            SelectionPath = new NodePath("../Selection"),
            PlacementPath = new NodePath("../Placement")
        };
        root.AddChild(cursor);

        selection.UpdateHoverFromWorld(grid.CellToWorld(new Vector2I(2, 1)));
        bool hoverCell = cursor.CurrentCell() == new Vector2I(2, 1)
            && cursor.ShouldDrawForMode()
            && cursor.CurrentOutlineColor().IsEqualApprox(cursor.SelectColor);

        interaction.ToolMode();
        bool toolColor = cursor.CurrentOutlineColor().IsEqualApprox(cursor.ToolColor);

        var buildingRoot = new Node2D { Name = "CursorBuildingRoot" };
        var scene = new PackedScene();
        Error packResult = scene.Pack(buildingRoot);
        buildingRoot.Free();
        if (!Expect(packResult == Error.Ok, $"PackedScene.Pack for interaction cursor smoke returned {packResult}."))
        {
            root.QueueFree();
            return false;
        }

        placement.PlacementScene = scene;
        placement.BeginPlacement("shed");
        placement.MovePreviewToCell(new Vector2I(4, 3));
        interaction.BuildMode();
        bool validBuild = cursor.CurrentCell() == new Vector2I(4, 3)
            && cursor.CurrentOutlineColor().IsEqualApprox(cursor.BuildValidColor);

        placement.SetFootprintOccupied(new Vector2I(5, 3), true);
        placement.MovePreviewToCell(new Vector2I(5, 3));
        bool invalidBuild = cursor.CurrentCell() == new Vector2I(5, 3)
            && cursor.CurrentOutlineColor().IsEqualApprox(cursor.BuildInvalidColor);

        interaction.DisableInteractions();
        bool hiddenDisabled = !cursor.ShouldDrawForMode();
        cursor.HideWhenDisabled = false;
        bool visibleDisabled = cursor.ShouldDrawForMode();
        root.QueueFree();

        if (!Expect(hoverCell, "GridInteractionCursor did not track the selection hover cell."))
            return false;

        if (!Expect(toolColor, "GridInteractionCursor did not use the tool-mode outline color."))
            return false;

        if (!Expect(validBuild && invalidBuild, "GridInteractionCursor did not reflect valid and invalid placement state."))
            return false;

        if (!Expect(hiddenDisabled && visibleDisabled, "GridInteractionCursor did not honor disabled-mode visibility."))
            return false;

        return true;
    }

    private bool VerifyGridObjectComponent()
    {
        var root = new Node { Name = "GridObjectSmokeRoot" };
        AddChild(root);

        var placement = new GridPlacementComponent { Name = "Placement" };
        root.AddChild(placement);

        var navigation = new GridNavigationComponent
        {
            Name = "Navigation",
            UseBounds = false,
            TreatPlacementOccupiedAsBlocked = false
        };
        root.AddChild(navigation);

        var body = new Node2D { Name = "InspectableBuilding" };
        var gridObject = new GridObjectComponent
        {
            Name = "GridObject",
            PlacementPath = new NodePath("../../Placement"),
            NavigationPath = new NodePath("../../Navigation"),
            ReserveFootprintOnReady = true
        };
        body.AddChild(gridObject);
        root.AddChild(body);

        gridObject.Configure(
            "Well House",
            "Well House",
            "Production",
            new Vector2I(4, 6),
            new Vector2I(2, 3),
            blocksNavigation: true,
            complete: false);
        gridObject.Description = "Processes early field output.";
        gridObject.SetMetadataValue("owner", "player");

        bool configured = gridObject.ObjectId == "well_house"
            && gridObject.DisplayName == "Well House"
            && gridObject.Category == "Production"
            && gridObject.Cell == new Vector2I(4, 6)
            && gridObject.Footprint == new Vector2I(2, 3)
            && !gridObject.Complete
            && placement.IsOccupied(new Vector2I(4, 6))
            && placement.IsOccupied(new Vector2I(5, 8))
            && navigation.IsBlocked(new Vector2I(4, 6))
            && navigation.IsBlocked(new Vector2I(5, 8))
            && body.GetMeta("grid_object_cell", Vector2I.Zero).AsVector2I() == new Vector2I(4, 6);

        gridObject.SetCell(new Vector2I(8, 1));
        bool movedReservation = !placement.IsOccupied(new Vector2I(4, 6))
            && !navigation.IsBlocked(new Vector2I(4, 6))
            && placement.IsOccupied(new Vector2I(8, 1))
            && navigation.IsBlocked(new Vector2I(9, 3));

        var snapshot = gridObject.CaptureState();
        gridObject.Configure("other", "Other", "Utility", Vector2I.Zero, Vector2I.One, false);
        gridObject.RestoreState(snapshot);
        bool restored = gridObject.ObjectId == "well_house"
            && gridObject.GetMetadataValue("owner").AsString() == "player"
            && gridObject.Description == "Processes early field output."
            && body.GetMeta("grid_object_complete", true).AsBool() == false
            && body.GetMeta("grid_object_description", "").AsString() == "Processes early field output."
            && placement.IsOccupied(new Vector2I(8, 1))
            && navigation.IsBlocked(new Vector2I(9, 3));

        gridObject.ReleaseFootprint();
        bool released = !placement.IsOccupied(new Vector2I(8, 1))
            && !navigation.IsBlocked(new Vector2I(9, 3));
        root.QueueFree();

        if (!Expect(configured, "GridObject did not configure exported identity/cell/footprint state."))
            return false;

        if (!Expect(movedReservation, "GridObject did not move placement/navigation footprint reservations when its cell changed."))
            return false;

        if (!Expect(restored, "GridObject did not capture and restore inspectable state."))
            return false;

        if (!Expect(released, "GridObject did not release placement/navigation footprint reservations."))
            return false;

        return true;
    }

    private bool VerifyGridObjectInspector()
    {
        var root = new Control { Name = "GridObjectInspectorSmokeRoot" };
        AddChild(root);

        var selection = new GridSelectionComponent
        {
            Name = "Selection",
            UseMouseInput = false,
            DrawSelection = false
        };
        root.AddChild(selection);

        var objects = new Node2D { Name = "Objects" };
        root.AddChild(objects);

        var building = new Node2D { Name = "Workshop" };
        objects.AddChild(building);

        var gridObject = new GridObjectComponent
        {
            Name = "GridObject",
            ObjectKind = "workshop",
            Description = "Makes useful parts."
        };
        building.AddChild(gridObject);
        gridObject.Configure(
            "Workshop",
            "Workshop",
            "",
            new Vector2I(3, 4),
            new Vector2I(2, 1),
            blocksNavigation: true,
            complete: false);
        gridObject.SetMetadataValue("status", "waiting");

        var inspector = new GridObjectInspectorComponent
        {
            Name = "ObjectInspector",
            SelectionPath = new NodePath("../Selection"),
            ObjectsRootPath = new NodePath("../Objects"),
            BuildInEditor = false,
            HideWhenEmpty = true
        };
        root.AddChild(inspector);

        var panel = new PanelContainer { Name = "Panel" };
        inspector.AddChild(panel);
        var content = new VBoxContainer { Name = "Content" };
        panel.AddChild(content);
        var title = new Label { Name = "Title" };
        var details = new Label { Name = "Details" };
        content.AddChild(title);
        content.AddChild(details);

        selection.SelectCell(new Vector2I(4, 4));
        inspector.RebuildInspector();
        bool selected = inspector.SelectedObject == gridObject
            && inspector.InspectedObjectId == "workshop"
            && inspector.TextForObject(gridObject).Contains("workshop")
            && inspector.TextForObject(gridObject).Contains("Makes useful parts.")
            && inspector.TextForObject(gridObject).Contains("Under construction")
            && inspector.TextForObject(gridObject).Contains("status: waiting")
            && inspector.VisibleLineCount() >= 5;

        selection.ClearSelection();
        inspector.RebuildInspector();
        bool cleared = inspector.SelectedObject == null && !inspector.Visible;
        root.QueueFree();

        if (!Expect(selected, "GridObjectInspector did not resolve and render the selected footprint object."))
            return false;

        if (!Expect(cleared, "GridObjectInspector did not clear when the grid selection was cleared."))
            return false;

        return true;
    }

    private bool VerifyGridBuildCatalogAndResources()
    {
        var root = new Node { Name = "GridBuildCatalogSmokeRoot" };
        AddChild(root);

        var grid = new GridProjectionComponent
        {
            Name = "Grid",
            Projection = GridProjectionComponent.GridProjection.TopDown,
            TileSize = new Vector2(10, 10)
        };
        root.AddChild(grid);

        var buildings = new Node2D { Name = "Buildings" };
        root.AddChild(buildings);

        var wallet = new GridResourceWalletComponent
        {
            Name = "Resources",
            ApplyStartingResourcesOnReady = false
        };
        wallet.SetAmount("wood", 10);
        wallet.SetAmount("stone", 2);
        wallet.SetAmount("coins", 3);
        root.AddChild(wallet);

        var placement = new GridPlacementComponent
        {
            Name = "Placement",
            GridPath = new NodePath("../Grid"),
            PlacementRootPath = new NodePath("../Buildings"),
            ResourceWalletPath = new NodePath("../Resources"),
            UseMouseInput = false,
            KeepPlacingAfterConfirm = false
        };
        root.AddChild(placement);

        var buildingRoot = new Node2D { Name = "WellHouseSceneRoot" };
        var scene = new PackedScene();
        Error packResult = scene.Pack(buildingRoot);
        buildingRoot.Free();
        if (!Expect(packResult == Error.Ok, $"PackedScene.Pack for build smoke returned {packResult}."))
        {
            root.QueueFree();
            return false;
        }

        var build = new GridBuildDefinition
        {
            BuildId = "well_house",
            DisplayName = "Well House",
            Category = "Production",
            Scene = scene,
            Footprint = new Vector2I(2, 1),
            BlocksNavigation = true
        };
        build.Costs.Add(new GridResourceAmount { ResourceId = "wood", Amount = 4 });
        build.Costs.Add(new GridResourceAmount { ResourceId = "stone", Amount = 1 });

        var expensive = new GridBuildDefinition
        {
            BuildId = "refinery",
            DisplayName = "Refinery",
            Category = "Production",
            Scene = scene
        };
        expensive.Costs.Add(new GridResourceAmount { ResourceId = "wood", Amount = 99 });

        var catalog = new GridBuildCatalogComponent
        {
            Name = "BuildCatalog",
            PlacementPath = new NodePath("../Placement"),
            ResourceWalletPath = new NodePath("../Resources")
        };
        catalog.Builds.Add(build);
        catalog.Builds.Add(expensive);
        catalog.Builds.Add(new Godot.Collections.Dictionary
        {
            ["build_id"] = "dict_shed",
            ["display_name"] = "Dictionary Shed",
            ["category"] = "Production",
            ["preview_texture"] = new PlaceholderTexture2D(),
            ["footprint"] = new Vector2I(-2, 0),
            ["build_seconds"] = float.NaN,
            ["costs"] = new Godot.Collections.Array
            {
                new Godot.Collections.Dictionary
                {
                    ["resource_id"] = "coins",
                    ["amount"] = "3"
                }
            }
        });
        root.AddChild(catalog);

        bool categoryOk = catalog.BuildIdsForCategory("Production").Count == 3;
        bool costSummaryOk = catalog.CostSummary("well_house")["wood"].AsInt32() == 4
            && catalog.CostSummary("well_house")["stone"].AsInt32() == 1;
        GridBuildDefinition? dictionaryBuild = catalog.FindBuild("dict_shed");
        bool dictionaryBuildOk = dictionaryBuild != null
            && dictionaryBuild.EffectiveFootprint == Vector2I.One
            && Mathf.IsEqualApprox(dictionaryBuild.EffectiveBuildSeconds, 0f)
            && catalog.CostSummary("dict_shed")["coins"].AsInt32() == 3
            && catalog.CanAfford("dict_shed");
        if (!dictionaryBuildOk)
        {
            Failure = $"GridBuildCatalog dictionary definition details: found={dictionaryBuild != null}, footprint={dictionaryBuild?.EffectiveFootprint.ToString() ?? "null"}, seconds={dictionaryBuild?.EffectiveBuildSeconds.ToString() ?? "null"}, cost={(catalog.CostSummary("dict_shed").ContainsKey("coins") ? catalog.CostSummary("dict_shed")["coins"].AsInt32().ToString() : "missing")}, affordable={catalog.CanAfford("dict_shed")}.";
            root.QueueFree();
            return false;
        }
        bool affordable = catalog.CanAfford("well_house") && !catalog.CanAfford("refinery");
        bool began = catalog.BeginPlacement("well_house");
        bool moved = placement.MovePreviewToCell(new Vector2I(3, 4));
        Node2D? placed = placement.ConfirmPlacement();

        bool placedOk = placed != null
            && buildings.GetChildCount() == 1
            && wallet.GetAmount("wood") == 6
            && wallet.GetAmount("stone") == 1
            && placement.IsOccupied(new Vector2I(3, 4))
            && placement.IsOccupied(new Vector2I(4, 4));
        GridObjectComponent? placedObject = placed == null ? null : EntityComponent.FindComponent<GridObjectComponent>(placed, recursive: false);
        bool objectConfigured = placedObject != null
            && placedObject.ObjectId == "well_house"
            && placedObject.DisplayName == "Well House"
            && placedObject.Category == "Production"
            && placedObject.Cell == new Vector2I(3, 4)
            && placedObject.Footprint == new Vector2I(2, 1)
            && placedObject.BlocksNavigation
            && placed != null
            && placed.GetMeta("grid_object_id", "").AsString() == "well_house";

        bool rejectedExpensive = !catalog.BeginPlacement("refinery");
        var snapshot = wallet.CaptureState();
        wallet.SetAmount("wood", 0);
        wallet.RestoreState(snapshot);
        bool walletRestored = wallet.GetAmount("wood") == 6 && wallet.GetAmount("stone") == 1;

        var authoredAmounts = new Godot.Collections.Array
        {
            new GridResourceAmount { ResourceId = "wood", Amount = 8 },
            new Godot.Collections.Dictionary
            {
                ["ResourceId"] = "coins",
                ["Amount"] = "12"
            }
        };
        wallet.LoadAmounts(authoredAmounts);
        bool untypedAmountsLoaded = wallet.GetAmount("wood") == 8 && wallet.GetAmount("coins") == 12;

        var dictionaryCostBuild = new GridBuildDefinition
        {
            BuildId = "dict_cost",
            DisplayName = "Dictionary Cost",
            Category = "Production",
            PreviewTexture = new PlaceholderTexture2D()
        };
        dictionaryCostBuild.Costs.Add(new Godot.Collections.Dictionary
        {
            ["resource_id"] = "coins",
            ["amount"] = "5"
        });
        catalog.Builds.Add(dictionaryCostBuild);
        bool dictionaryCosts = catalog.CostSummary("dict_cost")["coins"].AsInt32() == 5
            && catalog.CanAfford("dict_cost");
        root.QueueFree();

        if (!Expect(categoryOk, "GridBuildCatalog did not return builds by category."))
            return false;

        if (!Expect(costSummaryOk, "GridBuildCatalog did not summarize build costs."))
            return false;

        if (!Expect(affordable, "GridBuildCatalog affordability check failed."))
            return false;

        if (!Expect(began && moved, "GridBuildCatalog did not start a valid placement preview."))
            return false;

        if (!Expect(placedOk, "GridPlacement did not place catalog build, spend resources, and reserve footprint."))
            return false;

        if (!Expect(objectConfigured, "GridPlacement did not attach and configure GridObject metadata on the placed build."))
            return false;

        if (!Expect(rejectedExpensive, "GridBuildCatalog did not reject an unaffordable build."))
            return false;

        if (!Expect(walletRestored, "GridResourceWallet did not restore captured amounts."))
            return false;

        if (!Expect(untypedAmountsLoaded, "GridResourceWallet did not load untyped authored amount data without a typed-array cast."))
            return false;

        if (!Expect(dictionaryCosts, "GridBuildCatalog/GridResourceWallet did not read dictionary-authored build costs."))
            return false;

        return true;
    }

    private bool VerifyGridBuildSites()
    {
        var root = new Node { Name = "GridBuildSitesSmokeRoot" };
        AddChild(root);

        var placement = new GridPlacementComponent { Name = "Placement" };
        root.AddChild(placement);

        var jobs = new GridJobQueueComponent
        {
            Name = "Jobs",
            RemoveCompletedJobs = false
        };
        root.AddChild(jobs);

        var catalog = new GridBuildCatalogComponent { Name = "BuildCatalog" };
        catalog.Builds.Add(new GridBuildDefinition
        {
            BuildId = "shed",
            DisplayName = "Shed",
            BuildSeconds = 2.5f,
            JobKind = "build"
        });
        catalog.Builds.Add(new GridBuildDefinition
        {
            BuildId = "instant_path",
            DisplayName = "Instant Path",
            BuildSeconds = 0f,
            JobKind = "build"
        });
        root.AddChild(catalog);

        var buildSites = new GridBuildSiteComponent
        {
            Name = "BuildSites",
            PlacementPath = new NodePath("../Placement"),
            BuildCatalogPath = new NodePath("../BuildCatalog"),
            JobQueuePath = new NodePath("../Jobs"),
            AutoConnect = false
        };
        root.AddChild(buildSites);

        var placed = new Node2D { Name = "Shed" };
        root.AddChild(placed);
        bool registered = buildSites.RegisterPlacedBuild("shed", placed, new Vector2I(6, 7));
        string jobId = placed.GetMeta("grid_build_site_job_id", "").AsString();
        bool queued = registered
            && !string.IsNullOrEmpty(jobId)
            && jobs.GetJobKind(jobId) == "build"
            && jobs.GetJobCell(jobId) == new Vector2I(6, 7)
            && Mathf.IsEqualApprox(jobs.GetJobWorkSeconds(jobId), 2.5f)
            && buildSites.ActiveBuildSiteCount == 1
            && placed.GetMeta("grid_build_site_state", "").AsString() == "under_construction";

        jobs.CompleteJob(jobId, "worker");
        bool completed = buildSites.CompleteBuildSite(jobId)
            && placed.Visible
            && placed.GetMeta("grid_build_site_state", "").AsString() == "complete"
            && buildSites.ActiveBuildSiteCount == 0;

        var instant = new Node2D { Name = "InstantPath" };
        root.AddChild(instant);
        bool instantIgnored = !buildSites.RegisterPlacedBuild("instant_path", instant, new Vector2I(1, 1))
            && buildSites.ActiveBuildSiteCount == 0;
        root.QueueFree();

        if (!Expect(queued, "GridBuildSite did not create a build job and under-construction site for a timed build."))
            return false;

        if (!Expect(completed, "GridBuildSite did not finalize the placed node after the build job completed."))
            return false;

        if (!Expect(instantIgnored, "GridBuildSite should ignore build definitions with BuildSeconds <= 0."))
            return false;

        return true;
    }

    private bool VerifyGridResourceBar()
    {
        var root = new Control { Name = "GridResourceBarSmokeRoot" };
        AddChild(root);

        var wallet = new GridResourceWalletComponent
        {
            Name = "Resources",
            ApplyStartingResourcesOnReady = false
        };
        wallet.SetAmount("wood", 12);
        wallet.SetAmount("stone", 4);
        root.AddChild(wallet);

        var bar = new GridResourceBarComponent
        {
            Name = "ResourceBar",
            ResourceWalletPath = new NodePath("../Resources"),
            BuildInEditor = false,
            RowPath = new NodePath("Row")
        };
        root.AddChild(bar);
        var row = new HBoxContainer { Name = "Row" };
        bar.AddChild(row);
        bar.RebuildBar();

        bool initialOk = bar.VisibleResourceCount() == 2
            && bar.TextForResource("wood") == "wood: 12"
            && bar.TextForResource("stone") == "stone: 4";

        wallet.AddAmount("wood", 3);
        bar.RebuildBar();
        bool refreshOk = bar.TextForResource("wood") == "wood: 15";

        wallet.SetAmount("stone", 0);
        bar.RebuildBar();
        bool hideZeroOk = bar.VisibleResourceCount() == 1
            && bar.TextForResource("stone") == "";
        root.QueueFree();

        if (!Expect(initialOk, "GridResourceBar did not render initial wallet amounts."))
            return false;

        if (!Expect(refreshOk, "GridResourceBar did not refresh changed wallet amounts."))
            return false;

        if (!Expect(hideZeroOk, "GridResourceBar did not hide zero-amount resources."))
            return false;

        if (!VerifyGridResourceBarSceneLabels())
            return false;

        return true;
    }

    private bool VerifyGridResourceBarSceneLabels()
    {
        var root = new Control { Name = "GridResourceBarSceneLabelsSmokeRoot" };
        AddChild(root);

        var wallet = new GridResourceWalletComponent
        {
            Name = "Resources",
            ApplyStartingResourcesOnReady = false
        };
        wallet.SetAmount("wood", 8);
        wallet.SetAmount("stone", 0);
        root.AddChild(wallet);

        var bar = new GridResourceBarComponent
        {
            Name = "ResourceBar",
            ResourceWalletPath = new NodePath("../Resources"),
            BuildInEditor = false,
            BoundResourceIds = new[] { "wood", "stone" },
            BoundLabelPaths = new[] { new NodePath("Row/Wood"), new NodePath("Row/Stone") }
        };
        root.AddChild(bar);

        var row = new HBoxContainer { Name = "Row" };
        bar.AddChild(row);
        var wood = new Label { Name = "Wood" };
        var stone = new Label { Name = "Stone" };
        row.AddChild(wood);
        row.AddChild(stone);

        bar.RebuildBar();
        bool initial = bar.VisibleResourceCount() == 1
            && bar.TextForResource("wood") == "wood: 8"
            && bar.TextForResource("stone") == "stone: 0"
            && wood.Visible
            && !stone.Visible;

        wallet.SetAmount("stone", 5);
        bar.RebuildBar();
        bool updated = bar.VisibleResourceCount() == 2
            && bar.TextForResource("stone") == "stone: 5"
            && stone.Visible;

        root.QueueFree();

        if (!Expect(initial, "GridResourceBar did not bind scene-authored resource labels."))
            return false;

        if (!Expect(updated, "GridResourceBar did not refresh scene-authored resource labels."))
            return false;

        return true;
    }

    private bool VerifyGridBuildToolbar()
    {
        var root = new Control { Name = "GridBuildToolbarSmokeRoot" };
        AddChild(root);

        var grid = new GridProjectionComponent
        {
            Name = "Grid",
            Projection = GridProjectionComponent.GridProjection.TopDown,
            TileSize = new Vector2(10, 10)
        };
        root.AddChild(grid);

        var wallet = new GridResourceWalletComponent
        {
            Name = "Resources",
            ApplyStartingResourcesOnReady = false
        };
        wallet.SetAmount("wood", 5);
        root.AddChild(wallet);

        var placement = new GridPlacementComponent
        {
            Name = "Placement",
            GridPath = new NodePath("../Grid"),
            ResourceWalletPath = new NodePath("../Resources"),
            UseMouseInput = false
        };
        root.AddChild(placement);

        var buildingRoot = new Node2D { Name = "BarnSceneRoot" };
        var scene = new PackedScene();
        Error packResult = scene.Pack(buildingRoot);
        buildingRoot.Free();
        if (!Expect(packResult == Error.Ok, $"PackedScene.Pack for toolbar smoke returned {packResult}."))
        {
            root.QueueFree();
            return false;
        }

        var barn = new GridBuildDefinition
        {
            BuildId = "barn",
            DisplayName = "Barn",
            Category = "Farm",
            Scene = scene
        };
        barn.Costs.Add(new GridResourceAmount { ResourceId = "wood", Amount = 3 });

        var silo = new GridBuildDefinition
        {
            BuildId = "silo",
            DisplayName = "Silo",
            Category = "Farm",
            Scene = scene
        };
        silo.Costs.Add(new GridResourceAmount { ResourceId = "wood", Amount = 99 });

        var well = new GridBuildDefinition
        {
            BuildId = "well",
            DisplayName = "Well",
            Category = "Utility",
            Scene = scene
        };

        var catalog = new GridBuildCatalogComponent
        {
            Name = "BuildCatalog",
            PlacementPath = new NodePath("../Placement"),
            ResourceWalletPath = new NodePath("../Resources")
        };
        catalog.Builds.Add(barn);
        catalog.Builds.Add(silo);
        catalog.Builds.Add(well);
        root.AddChild(catalog);

        var interaction = new GridInteractionModeComponent
        {
            Name = "InteractionMode",
            UseMouseInput = false,
            ManageChildMouseInput = false
        };
        root.AddChild(interaction);

        var toolbar = new GridBuildToolbarComponent
        {
            Name = "BuildToolbar",
            BuildCatalogPath = new NodePath("../BuildCatalog"),
            ResourceWalletPath = new NodePath("../Resources"),
            InteractionModePath = new NodePath("../InteractionMode"),
            BuildInEditor = false,
            GenerateControlsWhenPathsEmpty = true
        };
        root.AddChild(toolbar);
        toolbar.RebuildToolbar();

        bool farmCountOk = toolbar.VisibleBuildButtonCount() == 2;
        toolbar.SelectCategory("Utility");
        bool utilityCountOk = toolbar.VisibleBuildButtonCount() == 1;
        toolbar.SelectCategory("Farm");
        bool selected = toolbar.SelectBuild("barn");
        bool placementStarted = placement.State == GridPlacementComponent.PlacementState.Placing
            && placement.PlacementId == "barn"
            && interaction.CurrentMode == GridInteractionModeComponent.InteractionMode.Build;

        wallet.SetAmount("wood", 0);
        toolbar.RefreshAffordability();
        bool rejectedUnaffordable = !toolbar.SelectBuild("silo");
        root.QueueFree();

        if (!Expect(farmCountOk, "GridBuildToolbar did not build buttons for the default category."))
            return false;

        if (!Expect(utilityCountOk, "GridBuildToolbar did not switch categories."))
            return false;

        if (!Expect(selected && placementStarted, "GridBuildToolbar did not start catalog placement."))
            return false;

        if (!Expect(rejectedUnaffordable, "GridBuildToolbar did not reject an unaffordable build selection."))
            return false;

        return true;
    }

    private bool VerifyNavigationAvoidsBlockedCells()
    {
        var nav = new GridNavigationComponent
        {
            BoundsSize = new Vector2I(5, 5),
            Diagonals = GridNavigationComponent.DiagonalPolicy.Never
        };
        nav.SetBlocked(new Vector2I(1, 0), true);

        var path = nav.FindCellPath(Vector2I.Zero, new Vector2I(2, 0));
        if (!Expect(path.Count > 0, "Navigation did not find a route around one blocked cell."))
            return false;

        if (!Expect(path[0] == Vector2I.Zero, "Navigation path should include the start cell."))
            return false;

        if (!Expect(path[path.Count - 1] == new Vector2I(2, 0), "Navigation path should end at the goal cell."))
            return false;

        foreach (Vector2I cell in path)
            if (!Expect(cell != new Vector2I(1, 0), "Navigation path walked through a blocked cell."))
                return false;

        if (!Expect(path.Count == 5, $"Expected detour path length 5, got {path.Count}."))
            return false;

        return true;
    }

    private bool VerifyNavigationUsesPlacementOccupancy()
    {
        var root = new Node();
        AddChild(root);

        var placement = new GridPlacementComponent { Name = "Placement" };
        placement.SetOccupied(new Vector2I(1, 0), true);
        root.AddChild(placement);

        var nav = new GridNavigationComponent
        {
            Name = "Navigation",
            BoundsSize = new Vector2I(4, 3),
            Diagonals = GridNavigationComponent.DiagonalPolicy.Never,
            PlacementPath = new NodePath("../Placement")
        };
        root.AddChild(nav);

        var path = nav.FindCellPath(Vector2I.Zero, new Vector2I(2, 0));
        root.QueueFree();

        if (!Expect(path.Count == 5, $"Navigation did not detour around placement occupancy; path length was {path.Count}."))
            return false;

        foreach (Vector2I cell in path)
            if (!Expect(cell != new Vector2I(1, 0), "Navigation path crossed an occupied placement cell."))
                return false;

        return true;
    }

    private bool VerifyNavigationUsesCellDataTerrain()
    {
        var root = new Node { Name = "GridNavigationCellDataSmokeRoot" };
        AddChild(root);

        var cells = new GridCellDataComponent
        {
            Name = "Cells",
            DefaultTerrainKind = "grass"
        };
        cells.SetTerrainKind(new Vector2I(1, 0), "water");
        cells.SetTerrainKind(new Vector2I(0, 1), "mud");
        cells.SetTerrainKind(new Vector2I(1, 1), "sand");
        root.AddChild(cells);

        var nav = new GridNavigationComponent
        {
            Name = "Navigation",
            BoundsSize = new Vector2I(4, 3),
            Diagonals = GridNavigationComponent.DiagonalPolicy.Never,
            CellDataPath = new NodePath("../Cells")
        };
        root.AddChild(nav);

        var path = nav.FindCellPath(Vector2I.Zero, new Vector2I(2, 0));
        bool avoidsWater = path.Count == 5 && !path.Contains(new Vector2I(1, 0));
        bool mudCost = nav.TraversalCost(Vector2I.Zero, new Vector2I(0, 1)) > nav.TraversalCost(Vector2I.Zero, new Vector2I(0, 2));

        root.QueueFree();

        if (!Expect(avoidsWater, $"Navigation did not use GridCellData terrain blocks; path length was {path.Count}."))
            return false;

        if (!Expect(mudCost, "Navigation did not apply GridCellData terrain movement costs."))
            return false;

        return true;
    }

    private bool VerifyGridRoads()
    {
        var root = new Node { Name = "GridRoadSmokeRoot" };
        AddChild(root);

        var grid = new GridProjectionComponent
        {
            Name = "Grid",
            Projection = GridProjectionComponent.GridProjection.TopDown,
            TileSize = new Vector2(10, 10)
        };
        root.AddChild(grid);

        var roads = new GridRoadComponent
        {
            Name = "Roads",
            GridPath = new NodePath("../Grid"),
            DefaultRoadCostMultiplier = 0.1f
        };
        root.AddChild(roads);

        for (int x = 0; x <= 4; x++)
            roads.SetRoad(new Vector2I(x, 1), "dirt_path", 0.1f);

        var nav = new GridNavigationComponent
        {
            Name = "Navigation",
            BoundsSize = new Vector2I(5, 3),
            Diagonals = GridNavigationComponent.DiagonalPolicy.Never,
            RoadPath = new NodePath("../Roads")
        };
        root.AddChild(nav);

        var path = nav.FindCellPath(Vector2I.Zero, new Vector2I(4, 0));
        bool prefersRoad = path.Count == 7
            && path.Contains(new Vector2I(0, 1))
            && path.Contains(new Vector2I(4, 1));
        bool roadCostOk = Mathf.IsEqualApprox(nav.TraversalCost(new Vector2I(0, 0), new Vector2I(0, 1)), 0.1f);

        var snapshot = roads.CaptureState();
        roads.ClearRoads();
        roads.RestoreState(snapshot);
        bool restored = roads.RoadCount == 5
            && roads.HasRoad(new Vector2I(2, 1))
            && Mathf.IsEqualApprox(roads.GetTraversalCostMultiplier(new Vector2I(2, 1)), 0.1f);
        root.QueueFree();

        if (!Expect(prefersRoad, $"GridNavigation did not prefer the cheaper road route; path length was {path.Count}."))
            return false;

        if (!Expect(roadCostOk, "GridNavigation did not apply GridRoad traversal cost."))
            return false;

        if (!Expect(restored, "GridRoad did not capture and restore road cells."))
            return false;

        return true;
    }

    private bool VerifyGridRoadsUseCellDataTerrain()
    {
        var root = new Node { Name = "GridRoadCellDataSmokeRoot" };
        AddChild(root);

        var cells = new GridCellDataComponent
        {
            Name = "Cells",
            DefaultTerrainKind = "grass"
        };
        cells.SetTerrainKind(new Vector2I(1, 0), "water");
        cells.AddFlag(new Vector2I(2, 0), GridCellDataComponent.CellFlags.Blocked);
        root.AddChild(cells);

        var roads = new GridRoadComponent
        {
            Name = "Roads",
            CellDataPath = new NodePath("../Cells")
        };
        root.AddChild(roads);

        bool rejectsWater = !roads.TrySetRoad(new Vector2I(1, 0), "dirt_path", 0.5f)
            && !roads.HasRoad(new Vector2I(1, 0));
        bool rejectsBlockedFlag = !roads.TrySetRoad(new Vector2I(2, 0), "dirt_path", 0.5f)
            && !roads.HasRoad(new Vector2I(2, 0));
        bool allowsGrass = roads.TrySetRoad(Vector2I.Zero, "dirt_path", 0.5f)
            && roads.HasRoad(Vector2I.Zero);

        var loaded = new Godot.Collections.Array
        {
            new Godot.Collections.Dictionary { ["cell"] = new Vector2I(1, 0), ["kind"] = "dirt_path" },
            new Godot.Collections.Dictionary { ["cell"] = new Vector2I(3, 0), ["kind"] = "dirt_path" }
        };
        roads.LoadRoads(loaded);
        bool loadSkipsUnroadable = !roads.HasRoad(new Vector2I(1, 0))
            && roads.HasRoad(new Vector2I(3, 0));

        var tools = new GridToolActionComponent
        {
            Name = "Tools",
            CellDataPath = new NodePath("../Cells"),
            RoadPath = new NodePath("../Roads")
        };
        root.AddChild(tools);

        bool toolRejectsWater = !tools.ApplyToCell(new Vector2I(1, 0), GridToolActionComponent.ToolAction.Road);

        root.QueueFree();

        if (!Expect(rejectsWater && rejectsBlockedFlag && allowsGrass, "GridRoad did not enforce GridCellData terrain rules."))
            return false;

        if (!Expect(loadSkipsUnroadable, "GridRoad.LoadRoads did not skip unroadable saved road cells."))
            return false;

        if (!Expect(toolRejectsWater, "GridToolAction road tool did not report terrain-rejected road placement."))
            return false;

        return true;
    }

    private bool VerifyPathFollowerMovesBody()
    {
        var root = new Node { Name = "GridFollowerSmokeRoot" };
        AddChild(root);

        var grid = new GridProjectionComponent
        {
            Name = "Grid",
            Projection = GridProjectionComponent.GridProjection.TopDown,
            TileSize = new Vector2(10, 10)
        };
        root.AddChild(grid);

        var nav = new GridNavigationComponent
        {
            Name = "Navigation",
            BoundsSize = new Vector2I(6, 3),
            Diagonals = GridNavigationComponent.DiagonalPolicy.Never
        };
        root.AddChild(nav);

        var body = new Node2D { Name = "Body", GlobalPosition = grid.CellToWorld(Vector2I.Zero) };
        root.AddChild(body);

        var follower = new GridPathFollowerComponent
        {
            Name = "Follower",
            GridPath = new NodePath("../../Grid"),
            NavigationPath = new NodePath("../../Navigation"),
            Speed = 100f,
            StopDistance = 0.05f,
            DriveCharacterBody = false,
            SetZIndexFromY = false
        };
        body.AddChild(follower);

        if (!Expect(follower.MoveToCell(new Vector2I(3, 0)), "GridPathFollower failed to start a valid move."))
        {
            root.QueueFree();
            return false;
        }

        for (int i = 0; i < 20 && follower.IsMoving; i++)
            follower.AdvancePath(0.05);

        Vector2 expected = grid.CellToWorld(new Vector2I(3, 0));
        bool arrived = body.GlobalPosition.DistanceTo(expected) <= 0.1f;
        root.QueueFree();

        if (!Expect(arrived, $"GridPathFollower did not move body to destination; got {body.GlobalPosition}, expected {expected}."))
            return false;

        return true;
    }

    private bool VerifyPathFollowerBoundsInvalidTuning()
    {
        var root = new Node { Name = "GridFollowerBoundsSmokeRoot" };
        AddChild(root);

        var body = new Node2D { Name = "Body", GlobalPosition = Vector2.Zero };
        root.AddChild(body);

        var follower = new GridPathFollowerComponent
        {
            Name = "Follower",
            Speed = float.NaN,
            StopDistance = -10f,
            DriveCharacterBody = false,
            SetZIndexFromY = true
        };
        body.AddChild(follower);

        var points = new Godot.Collections.Array<Vector2>
        {
            new(float.NaN, 0f),
            new(10f, 0f)
        };

        bool started = follower.SetWorldPath(points);
        follower.AdvancePath(double.NaN);
        follower.AdvancePath(0.05);
        follower.AdvancePath(0.05);
        follower.AdvancePath(0.05);

        bool bounded = started
            && !follower.IsMoving
            && body.GlobalPosition.DistanceTo(new Vector2(10f, 0f)) <= 0.1f
            && float.IsFinite(follower.EffectiveSpeed)
            && float.IsFinite(follower.EffectiveStopDistance);
        root.QueueFree();

        if (!Expect(bounded, $"GridPathFollower did not bound invalid tuning; position={body.GlobalPosition}, moving={follower.IsMoving}."))
            return false;

        return true;
    }

    private bool VerifyGridSelectionState()
    {
        var selection = new GridSelectionComponent();

        selection.SelectCell(new Vector2I(2, 3));
        if (!Expect(selection.IsSelected(new Vector2I(2, 3)), "GridSelection did not select a single cell."))
            return false;

        selection.SelectCell(new Vector2I(4, 5));
        if (!Expect(!selection.IsSelected(new Vector2I(2, 3)) && selection.IsSelected(new Vector2I(4, 5)),
                "GridSelection non-additive select should replace the previous cell."))
            return false;

        selection.ToggleCell(new Vector2I(4, 5));
        if (!Expect(!selection.IsSelected(new Vector2I(4, 5)), "GridSelection ToggleCell should remove a selected cell."))
            return false;

        selection.BeginDrag(new Vector2I(1, 1));
        selection.FinishDrag(new Vector2I(3, 2));
        var cells = selection.GetSelectedCells();
        if (!Expect(cells.Count == 6, $"GridSelection rectangle expected 6 cells, got {cells.Count}."))
            return false;

        if (!Expect(selection.IsSelected(new Vector2I(1, 1)) && selection.IsSelected(new Vector2I(3, 2)),
                "GridSelection rectangle did not include both drag corners."))
            return false;

        var reversed = GridSelectionComponent.CellsInRect(new Vector2I(3, 2), new Vector2I(1, 1));
        if (!Expect(reversed.Count == 6, $"GridSelection reversed rectangle expected 6 cells, got {reversed.Count}."))
            return false;

        selection.ClearSelection();
        if (!Expect(selection.GetSelectedCells().Count == 0, "GridSelection ClearSelection did not clear all cells."))
            return false;

        return true;
    }

    private bool VerifyGridCameraController()
    {
        var camera = new Camera2D
        {
            Name = "Camera",
            GlobalPosition = new Vector2(50, 50),
            Zoom = Vector2.One
        };
        AddChild(camera);

        var controller = new GridCameraControllerComponent
        {
            Name = "GridCameraController",
            UseBounds = true,
            KeepViewportInsideBounds = false,
            BoundsPosition = Vector2.Zero,
            BoundsSize = new Vector2(100, 100),
            MinZoom = new Vector2(0.5f, 0.5f),
            MaxZoom = new Vector2(2f, 2f),
            PositionSmoothing = 0f,
            ZoomSmoothing = 0f,
            UseKeyboardPan = false,
            UseEdgePan = false
        };
        camera.AddChild(controller);

        controller.FocusWorld(new Vector2(150, -20), immediate: true);
        if (!Expect(camera.GlobalPosition.IsEqualApprox(new Vector2(100, 0)), $"GridCameraController did not clamp focus; got {camera.GlobalPosition}."))
        {
            camera.QueueFree();
            return false;
        }

        controller.SetZoomLevel(5f, immediate: true);
        if (!Expect(camera.Zoom.IsEqualApprox(new Vector2(2, 2)), $"GridCameraController did not clamp max zoom; got {camera.Zoom}."))
        {
            camera.QueueFree();
            return false;
        }

        controller.ZoomAtWorldPoint(new Vector2(100, 0), -5f, immediate: true);
        if (!Expect(camera.Zoom.IsEqualApprox(new Vector2(0.5f, 0.5f)), $"GridCameraController did not clamp min zoom; got {camera.Zoom}."))
        {
            camera.QueueFree();
            return false;
        }

        controller.PanByWorldDelta(new Vector2(-200, 200));
        controller._Process(1);
        bool clamped = camera.GlobalPosition.X >= -0.01f
            && camera.GlobalPosition.X <= 100.01f
            && camera.GlobalPosition.Y >= -0.01f
            && camera.GlobalPosition.Y <= 100.01f;

        controller.PanSpeed = float.NaN;
        controller.PositionSmoothing = float.NaN;
        controller.ZoomSmoothing = float.PositiveInfinity;
        controller.ZoomStep = float.NaN;
        controller.BoundsPosition = new Vector2(float.NaN, float.PositiveInfinity);
        controller.BoundsSize = new Vector2(float.NaN, -4f);
        controller.MinZoom = new Vector2(float.NaN, 4f);
        controller.MaxZoom = new Vector2(0.25f, float.PositiveInfinity);
        controller.SetZoom(new Vector2(float.NaN, float.NegativeInfinity), immediate: true);
        controller.FocusWorld(new Vector2(float.NaN, float.PositiveInfinity), immediate: true);
        controller.ZoomAtWorldPoint(new Vector2(float.NaN, 20f), float.NaN, immediate: true);
        controller.PanByWorldDelta(new Vector2(float.NaN, 4f));
        controller._Process(double.NaN);
        bool invalidTuningBounded = float.IsFinite(camera.GlobalPosition.X)
            && float.IsFinite(camera.GlobalPosition.Y)
            && float.IsFinite(camera.Zoom.X)
            && float.IsFinite(camera.Zoom.Y)
            && controller.EffectivePanSpeed == 0f
            && controller.EffectiveZoomStep == 0f
            && controller.EffectivePositionSmoothing == 0f
            && controller.EffectiveZoomSmoothing == 0f
            && controller.EffectiveBoundsSize.IsEqualApprox(new Vector2(4096, 4096));
        camera.QueueFree();

        if (!Expect(clamped, $"GridCameraController pan escaped bounds; got {camera.GlobalPosition}."))
            return false;

        if (!Expect(invalidTuningBounded, $"GridCameraController let invalid tuning poison the camera; pos={camera.GlobalPosition}, zoom={camera.Zoom}."))
            return false;

        return true;
    }

    private bool VerifyGridJobQueueAndWorker()
    {
        var root = new Node { Name = "GridJobSmokeRoot" };
        AddChild(root);

        var grid = new GridProjectionComponent
        {
            Name = "Grid",
            Projection = GridProjectionComponent.GridProjection.TopDown,
            TileSize = new Vector2(10, 10)
        };
        root.AddChild(grid);

        var nav = new GridNavigationComponent
        {
            Name = "Navigation",
            BoundsSize = new Vector2I(6, 3),
            Diagonals = GridNavigationComponent.DiagonalPolicy.Never
        };
        root.AddChild(nav);

        var queue = new GridJobQueueComponent
        {
            Name = "Jobs",
            RemoveCompletedJobs = false,
            DefaultWorkSeconds = 0.1f
        };
        root.AddChild(queue);

        var body = new Node2D { Name = "Worker", GlobalPosition = grid.CellToWorld(Vector2I.Zero) };
        root.AddChild(body);

        var follower = new GridPathFollowerComponent
        {
            Name = "PathFollower",
            GridPath = new NodePath("../../Grid"),
            NavigationPath = new NodePath("../../Navigation"),
            Speed = 100f,
            StopDistance = 0.05f,
            DriveCharacterBody = false,
            SetZIndexFromY = false
        };
        body.AddChild(follower);

        var worker = new GridWorkerComponent
        {
            Name = "GridWorker",
            WorkerId = "worker_smoke",
            JobQueuePath = new NodePath("../../Jobs"),
            GridPath = new NodePath("../../Grid"),
            PathFollowerPath = new NodePath("../PathFollower"),
            AutoClaimJobs = false
        };
        body.AddChild(worker);

        string jobId = queue.AddJob(new Vector2I(2, 0), "clear_land", 0.1f, priority: 5);
        if (!Expect(queue.QueuedCount == 1, "GridJobQueue did not add one queued job."))
        {
            root.QueueFree();
            return false;
        }

        if (!Expect(worker.ClaimNextJob(), "GridWorker did not claim the queued job."))
        {
            root.QueueFree();
            return false;
        }

        if (!Expect(worker.CurrentJobId == jobId, $"GridWorker claimed {worker.CurrentJobId}, expected {jobId}."))
        {
            root.QueueFree();
            return false;
        }

        for (int i = 0; i < 20 && follower.IsMoving; i++)
        {
            follower.AdvancePath(0.05);
            worker.Tick(0.05);
        }

        for (int i = 0; i < 10 && worker.State != GridWorkerComponent.WorkerState.Idle; i++)
            worker.Tick(0.05);

        bool completed = queue.GetJobState(jobId) == GridJobQueueComponent.GridJobState.Completed;
        bool idle = worker.State == GridWorkerComponent.WorkerState.Idle && worker.CurrentJobId == "";
        bool arrived = body.GlobalPosition.DistanceTo(grid.CellToWorld(new Vector2I(2, 0))) <= 0.1f;
        root.QueueFree();

        if (!Expect(completed, "GridWorker did not mark the claimed job completed."))
            return false;

        if (!Expect(idle, "GridWorker did not return to idle after completing the job."))
            return false;

        if (!Expect(arrived, "GridWorker did not move to the job cell."))
            return false;

        return true;
    }

    private bool VerifyGridJobQueueBoundsInvalidWorkSeconds()
    {
        var queue = new GridJobQueueComponent
        {
            DefaultWorkSeconds = float.NaN,
            RemoveCompletedJobs = false
        };

        string added = queue.AddJob(new Vector2I(1, 2), "clear_land", float.NaN);
        float addedSeconds = queue.GetJobWorkSeconds(added);

        var saved = new Godot.Collections.Array
        {
            new Godot.Collections.Dictionary
            {
                ["id"] = "loaded_1",
                ["kind"] = "repair",
                ["cell"] = new Vector2I(3, 4),
                ["work_seconds"] = float.NaN
            }
        };
        queue.LoadJobs(saved);
        float loadedSeconds = queue.GetJobWorkSeconds("loaded_1");

        bool bounded = float.IsFinite(addedSeconds)
            && addedSeconds >= 0.01f
            && float.IsFinite(loadedSeconds)
            && loadedSeconds >= 0.01f;

        if (!Expect(bounded, $"GridJobQueue did not bound invalid work seconds. added={addedSeconds}, loaded={loadedSeconds}."))
            return false;

        return true;
    }

    private bool VerifyGridWorkerBoundsInvalidTuning()
    {
        var root = new Node { Name = "GridWorkerBoundsSmokeRoot" };
        AddChild(root);

        var grid = new GridProjectionComponent
        {
            Name = "Grid",
            Projection = GridProjectionComponent.GridProjection.TopDown,
            TileSize = new Vector2(10, 10)
        };
        root.AddChild(grid);

        var nav = new GridNavigationComponent
        {
            Name = "Navigation",
            BoundsSize = new Vector2I(2, 2),
            Diagonals = GridNavigationComponent.DiagonalPolicy.Never
        };
        root.AddChild(nav);

        var queue = new GridJobQueueComponent
        {
            Name = "Jobs",
            RemoveCompletedJobs = false
        };
        root.AddChild(queue);

        var body = new Node2D { Name = "Worker", GlobalPosition = grid.CellToWorld(Vector2I.Zero) };
        root.AddChild(body);

        var follower = new GridPathFollowerComponent
        {
            Name = "PathFollower",
            GridPath = new NodePath("../../Grid"),
            NavigationPath = new NodePath("../../Navigation"),
            Speed = float.NaN,
            StopDistance = float.NaN,
            DriveCharacterBody = false,
            SetZIndexFromY = false
        };
        body.AddChild(follower);

        var worker = new GridWorkerComponent
        {
            Name = "GridWorker",
            WorkerId = "bounded_worker",
            JobQueuePath = new NodePath("../../Jobs"),
            GridPath = new NodePath("../../Grid"),
            PathFollowerPath = new NodePath("../PathFollower"),
            AutoClaimJobs = false,
            ClaimIntervalSeconds = float.NaN,
            WorkSpeedMultiplier = float.NaN
        };
        body.AddChild(worker);

        string jobId = queue.AddJob(Vector2I.Zero, "clear_land", 0.01f);
        bool assigned = worker.AssignJob(jobId);
        follower.AdvancePath(double.NaN);
        worker.Tick(double.NaN);
        follower.AdvancePath(0.05);
        worker.Tick(0.05);
        worker.Tick(0.05);

        bool completed = assigned
            && queue.GetJobState(jobId) == GridJobQueueComponent.GridJobState.Completed
            && worker.State == GridWorkerComponent.WorkerState.Idle
            && worker.CurrentJobId == ""
            && float.IsFinite(worker.EffectiveClaimInterval)
            && float.IsFinite(worker.EffectiveWorkSpeed);
        root.QueueFree();

        if (!Expect(completed, "GridWorker did not recover from invalid tuning and complete a local job."))
            return false;

        return true;
    }

    private bool VerifyGridWorkerStatusPanel()
    {
        var root = new Control { Name = "GridWorkerStatusSmokeRoot" };
        AddChild(root);

        var grid = new GridProjectionComponent
        {
            Name = "Grid",
            Projection = GridProjectionComponent.GridProjection.TopDown,
            TileSize = new Vector2(10, 10)
        };
        root.AddChild(grid);

        var nav = new GridNavigationComponent
        {
            Name = "Navigation",
            BoundsSize = new Vector2I(6, 3),
            Diagonals = GridNavigationComponent.DiagonalPolicy.Never
        };
        root.AddChild(nav);

        var queue = new GridJobQueueComponent { Name = "Jobs", RemoveCompletedJobs = false };
        root.AddChild(queue);

        var units = new Node2D { Name = "Units" };
        root.AddChild(units);

        var idleBody = new Node2D { Name = "IdleWorker", GlobalPosition = grid.CellToWorld(Vector2I.Zero) };
        units.AddChild(idleBody);
        idleBody.AddChild(new GridPathFollowerComponent
        {
            Name = "PathFollower",
            GridPath = new NodePath("../../../Grid"),
            NavigationPath = new NodePath("../../../Navigation"),
            DriveCharacterBody = false
        });
        var idleWorker = new GridWorkerComponent
        {
            Name = "GridWorker",
            WorkerId = "idle_worker",
            JobQueuePath = new NodePath("../../../Jobs"),
            GridPath = new NodePath("../../../Grid"),
            PathFollowerPath = new NodePath("../PathFollower"),
            AutoClaimJobs = false
        };
        idleBody.AddChild(idleWorker);
        idleWorker.SetProcess(false);

        var activeBody = new Node2D { Name = "ActiveWorker", GlobalPosition = grid.CellToWorld(Vector2I.Zero) };
        units.AddChild(activeBody);
        var activeFollower = new GridPathFollowerComponent
        {
            Name = "PathFollower",
            GridPath = new NodePath("../../../Grid"),
            NavigationPath = new NodePath("../../../Navigation"),
            Speed = 100f,
            DriveCharacterBody = false
        };
        activeBody.AddChild(activeFollower);
        var activeWorker = new GridWorkerComponent
        {
            Name = "GridWorker",
            WorkerId = "active_worker",
            JobQueuePath = new NodePath("../../../Jobs"),
            GridPath = new NodePath("../../../Grid"),
            PathFollowerPath = new NodePath("../PathFollower"),
            AutoClaimJobs = false
        };
        activeBody.AddChild(activeWorker);
        activeWorker.SetProcess(false);

        string jobId = queue.AddJob(new Vector2I(2, 0), "clear_land", 3f);
        bool assigned = activeWorker.AssignJob(jobId);
        activeFollower.IsActive = false;
        activeWorker.IsActive = false;
        activeWorker.SetProcess(false);

        var panel = new GridWorkerStatusPanelComponent
        {
            Name = "WorkerStatus",
            UnitsRootPath = new NodePath("../Units"),
            JobQueuePath = new NodePath("../Jobs"),
            BuildInEditor = false,
            GenerateControlsWhenPathsEmpty = true,
            AutoRefresh = false
        };
        root.AddChild(panel);
        panel.RebuildPanel();

        bool claimed = assigned
            && activeWorker.CurrentJobId == jobId
            && queue.GetJobState(jobId) == GridJobQueueComponent.GridJobState.Claimed;
        bool initial = claimed
            && panel.SummaryText() == "Total 2 | Idle 1 | Active 1"
            && panel.VisibleWorkerRowCount() == 2
            && panel.TextForWorker("idle_worker").Contains("Idle")
            && panel.TextForWorker("active_worker").Contains("clear_land (2,0)");

        if (!Expect(initial,
            $"GridWorkerStatusPanel did not render idle and active workers. claimed={claimed}, state={activeWorker.State}, job='{activeWorker.CurrentJobId}', queue={queue.GetJobState(jobId)}, summary='{panel.SummaryText()}', activeRow='{panel.TextForWorker("active_worker")}', idleRow='{panel.TextForWorker("idle_worker")}'."))
        {
            root.QueueFree();
            return false;
        }

        bool cancelled = panel.CancelWorkerJob("active_worker")
            && activeWorker.State == GridWorkerComponent.WorkerState.Idle
            && activeWorker.CurrentJobId == ""
            && queue.GetJobState(jobId) == GridJobQueueComponent.GridJobState.Queued
            && panel.SummaryText() == "Total 2 | Idle 2 | Active 0";
        root.QueueFree();

        if (!Expect(cancelled, "GridWorkerStatusPanel did not cancel and release the active worker job."))
            return false;

        return true;
    }

    private bool VerifyGridWorkerRejectsClaimedJob()
    {
        var root = new Node2D { Name = "GridWorkerClaimOwnershipSmokeRoot" };
        AddChild(root);

        var grid = new GridProjectionComponent
        {
            Name = "Grid",
            Projection = GridProjectionComponent.GridProjection.TopDown,
            TileSize = new Vector2(10, 10)
        };
        root.AddChild(grid);

        var nav = new GridNavigationComponent
        {
            Name = "Navigation",
            BoundsSize = new Vector2I(6, 3),
            Diagonals = GridNavigationComponent.DiagonalPolicy.Never
        };
        root.AddChild(nav);

        var queue = new GridJobQueueComponent { Name = "Jobs", RemoveCompletedJobs = false };
        root.AddChild(queue);

        Node2D MakeWorkerBody(string bodyName, string workerId)
        {
            var body = new Node2D { Name = bodyName, GlobalPosition = grid.CellToWorld(Vector2I.Zero) };
            root.AddChild(body);
            var follower = new GridPathFollowerComponent
            {
                Name = "PathFollower",
                GridPath = new NodePath("../../Grid"),
                NavigationPath = new NodePath("../../Navigation"),
                DriveCharacterBody = false,
                SetZIndexFromY = false
            };
            body.AddChild(follower);
            body.AddChild(new GridWorkerComponent
            {
                Name = "GridWorker",
                WorkerId = workerId,
                JobQueuePath = new NodePath("../../Jobs"),
                GridPath = new NodePath("../../Grid"),
                PathFollowerPath = new NodePath("../PathFollower"),
                AutoClaimJobs = false,
                ClaimIntervalSeconds = 0f,
                WorkSpeedMultiplier = -4f
            });
            return body;
        }

        var ownerBody = MakeWorkerBody("OwnerWorker", "owner_worker");
        var thiefBody = MakeWorkerBody("ThiefWorker", "thief_worker");
        var owner = ownerBody.GetNode<GridWorkerComponent>("GridWorker");
        var thief = thiefBody.GetNode<GridWorkerComponent>("GridWorker");
        string jobId = queue.AddJob(new Vector2I(2, 0), "clear_land", 0.1f, priority: 2);

        bool ownerAssigned = owner.AssignJob(jobId);
        bool thiefRejected = !thief.AssignJob(jobId)
            && thief.CurrentJobId == ""
            && thief.State == GridWorkerComponent.WorkerState.Idle
            && queue.GetJobState(jobId) == GridJobQueueComponent.GridJobState.Claimed
            && queue.GetJobClaimedBy(jobId) == "owner_worker";

        if (!Expect(ownerAssigned && thiefRejected,
            $"GridWorker allowed a second worker to take a claimed job. ownerAssigned={ownerAssigned}, thiefJob='{thief.CurrentJobId}', thiefState={thief.State}, queueState={queue.GetJobState(jobId)}, claimedBy='{queue.GetJobClaimedBy(jobId)}'."))
        {
            root.QueueFree();
            return false;
        }

        owner.CancelCurrentJob();
        bool released = queue.GetJobState(jobId) == GridJobQueueComponent.GridJobState.Queued
            && queue.GetJobClaimedBy(jobId) == "";
        root.QueueFree();

        if (!Expect(released, "Cancelling the owning worker did not release the claimed job."))
            return false;

        return true;
    }

    private bool VerifyGridJobBoard()
    {
        var root = new Control { Name = "GridJobBoardSmokeRoot" };
        AddChild(root);

        var queue = new GridJobQueueComponent
        {
            Name = "Jobs",
            RemoveCompletedJobs = false,
            RemoveCancelledJobs = false
        };
        root.AddChild(queue);

        string clearJob = queue.AddJob(new Vector2I(2, 3), "clear_land", priority: 2);
        string buildJob = queue.AddJob(new Vector2I(4, 5), "build", priority: 5);
        queue.ClaimJob(buildJob, "worker_1");

        var board = new GridJobBoardComponent
        {
            Name = "JobBoard",
            JobQueuePath = new NodePath("../Jobs"),
            BuildInEditor = false,
            GenerateControlsWhenPathsEmpty = true,
            ShowCompletedJobs = true,
            MaxVisibleJobs = 4
        };
        root.AddChild(board);
        board.RebuildBoard();

        bool initial = board.SummaryText() == "Queued 1 | Active 1 | Done 0"
            && board.VisibleJobRowCount() == 2
            && board.TextForJob(buildJob).Contains("Claimed by worker_1")
            && board.TextForJob(clearJob).Contains("clear_land (2,3)");

        queue.CompleteJob(buildJob, "worker_1");
        board.RefreshBoard();
        bool completed = board.SummaryText() == "Queued 1 | Active 0 | Done 1"
            && board.TextForJob(buildJob).Contains("Completed");

        bool cancelled = board.CancelJob(clearJob)
            && queue.GetJobState(clearJob) == GridJobQueueComponent.GridJobState.Cancelled
            && board.SummaryText() == "Queued 0 | Active 0 | Done 1";
        root.QueueFree();

        if (!Expect(initial, "GridJobBoard did not render queued/claimed job state."))
            return false;

        if (!Expect(completed, "GridJobBoard did not refresh completed job state."))
            return false;

        if (!Expect(cancelled, "GridJobBoard did not cancel jobs through GridJobQueueComponent."))
            return false;

        return true;
    }

    private bool VerifyGridJobEffects()
    {
        var root = new Node { Name = "GridJobEffectsSmokeRoot" };
        AddChild(root);

        var cells = new GridCellDataComponent { Name = "Cells" };
        cells.AddFlag(new Vector2I(1, 1), GridCellDataComponent.CellFlags.Blocked);
        cells.Till(new Vector2I(3, 3));
        cells.PlantCrop(new Vector2I(3, 3), "turnip", 0);
        cells.AdvanceDay();
        root.AddChild(cells);

        var queue = new GridJobQueueComponent
        {
            Name = "Jobs",
            RemoveCompletedJobs = false
        };
        root.AddChild(queue);

        var wallet = new GridResourceWalletComponent
        {
            Name = "Resources",
            ApplyStartingResourcesOnReady = false
        };
        root.AddChild(wallet);

        var placement = new GridPlacementComponent { Name = "Placement" };
        root.AddChild(placement);

        var resourceRoot = new Node2D { Name = "ResourceNodes" };
        root.AddChild(resourceRoot);

        var tree = new GridResourceNodeComponent
        {
            Name = "Tree",
            ResourceWalletPath = new NodePath("../../Resources"),
            PlacementPath = new NodePath("../../Placement"),
            UseExplicitCell = true,
            Cell = new Vector2I(1, 1),
            ResourceId = "wood",
            Amount = 2,
            AmountPerGather = 1,
            MarkCellOccupiedOnReady = true
        };
        resourceRoot.AddChild(tree);

        var effects = new GridJobEffectComponent
        {
            Name = "JobEffects",
            JobQueuePath = new NodePath("../Jobs"),
            CellDataPath = new NodePath("../Cells"),
            ResourceNodesRootPath = new NodePath("../ResourceNodes"),
            AutoConnect = false,
            UseToolActionForHarvest = false
        };
        root.AddChild(effects);
        effects.ConnectQueue();

        string clearJob = queue.AddJob(new Vector2I(1, 1), "clear_land");
        queue.CompleteJob(clearJob, "worker");
        bool cleared = cells.HasFlag(new Vector2I(1, 1), GridCellDataComponent.CellFlags.Cleared)
            && !cells.HasFlag(new Vector2I(1, 1), GridCellDataComponent.CellFlags.Blocked)
            && wallet.GetAmount("wood") == 2
            && tree.IsDepleted
            && !placement.IsOccupied(new Vector2I(1, 1));

        string tillJob = queue.AddJob(new Vector2I(2, 2), "till");
        queue.CompleteJob(tillJob, "worker");
        bool tilled = cells.HasFlag(new Vector2I(2, 2), GridCellDataComponent.CellFlags.Tilled);

        string waterJob = queue.AddJob(new Vector2I(2, 2), "water");
        queue.CompleteJob(waterJob, "worker");
        bool watered = cells.HasFlag(new Vector2I(2, 2), GridCellDataComponent.CellFlags.Watered);

        string harvestJob = queue.AddJob(new Vector2I(3, 3), "harvest");
        queue.CompleteJob(harvestJob, "worker");
        bool harvested = cells.GetCropId(new Vector2I(3, 3)) == ""
            && !cells.HasFlag(new Vector2I(3, 3), GridCellDataComponent.CellFlags.Planted);

        string unknownJob = queue.AddJob(new Vector2I(4, 4), "dance");
        bool rejectedUnknown = !effects.ApplyJobEffect(unknownJob);
        root.QueueFree();

        if (!Expect(cleared, "GridJobEffect did not clear land on clear_land job completion."))
            return false;

        if (!Expect(tilled, "GridJobEffect did not till land on till job completion."))
            return false;

        if (!Expect(watered, "GridJobEffect did not water land on water job completion."))
            return false;

        if (!Expect(harvested, "GridJobEffect did not harvest crop on harvest job completion."))
            return false;

        if (!Expect(rejectedUnknown, "GridJobEffect did not reject an unknown job kind."))
            return false;

        return true;
    }

    private bool VerifyGridResourceNodes()
    {
        var root = new Node { Name = "GridResourceNodeSmokeRoot" };
        AddChild(root);

        var grid = new GridProjectionComponent
        {
            Name = "Grid",
            Projection = GridProjectionComponent.GridProjection.TopDown,
            TileSize = new Vector2(16, 16)
        };
        root.AddChild(grid);

        var wallet = new GridResourceWalletComponent
        {
            Name = "Resources",
            ApplyStartingResourcesOnReady = false
        };
        root.AddChild(wallet);

        var jobs = new GridJobQueueComponent
        {
            Name = "Jobs",
            RemoveCompletedJobs = false
        };
        root.AddChild(jobs);

        var placement = new GridPlacementComponent { Name = "Placement" };
        root.AddChild(placement);

        var nodes = new Node2D { Name = "ResourceNodes" };
        root.AddChild(nodes);

        var tree = new GridResourceNodeComponent
        {
            Name = "Tree",
            GridPath = new NodePath("../../Grid"),
            PlacementPath = new NodePath("../../Placement"),
            ResourceWalletPath = new NodePath("../../Resources"),
            JobQueuePath = new NodePath("../../Jobs"),
            UseExplicitCell = true,
            Cell = new Vector2I(5, 5),
            ResourceId = "wood",
            Amount = 2,
            AmountPerGather = 1,
            GatherJobKind = "gather",
            HideWhenDepleted = true,
            MarkCellOccupiedOnReady = true
        };
        nodes.AddChild(tree);
        bool reservedCell = placement.IsOccupied(new Vector2I(5, 5));

        var effects = new GridJobEffectComponent
        {
            Name = "JobEffects",
            JobQueuePath = new NodePath("../Jobs"),
            CellDataPath = new NodePath(""),
            ResourceNodesRootPath = new NodePath("../ResourceNodes"),
            AutoConnect = false
        };
        root.AddChild(effects);
        effects.ConnectQueue();

        string job1 = tree.QueueGatherJob();
        string duplicateJob = tree.QueueGatherJob();
        bool duplicateSuppressed = duplicateJob == job1
            && jobs.QueuedCount == 1
            && tree.ActiveGatherJobId == job1;
        jobs.CompleteJob(job1, "worker");
        bool firstGather = wallet.GetAmount("wood") == 1
            && tree.RemainingAmount == 1
            && !tree.IsDepleted
            && tree.Visible
            && tree.ActiveGatherJobId == ""
            && placement.IsOccupied(new Vector2I(5, 5));

        string job2 = jobs.AddJob(new Vector2I(5, 5), "chop");
        jobs.CompleteJob(job2, "worker");
        bool depleted = wallet.GetAmount("wood") == 2
            && tree.RemainingAmount == 0
            && tree.IsDepleted
            && !tree.Visible
            && !placement.IsOccupied(new Vector2I(5, 5));

        string missing = jobs.AddJob(new Vector2I(9, 9), "gather");
        bool rejectedMissing = !effects.ApplyJobEffect(missing);
        root.QueueFree();

        if (!Expect(!string.IsNullOrEmpty(job1), "GridResourceNode did not queue a gather job."))
            return false;

        if (!Expect(reservedCell, "GridResourceNode did not reserve its placement cell on ready."))
            return false;

        if (!Expect(duplicateSuppressed, "GridResourceNode queued duplicate gather work for the same live resource node."))
            return false;

        if (!Expect(firstGather, "GridResourceNode did not gather one resource amount into the wallet."))
            return false;

        if (!Expect(depleted, "GridResourceNode did not deplete after gathering all resources."))
            return false;

        if (!Expect(rejectedMissing, "GridJobEffect did not reject gather jobs with no resource node at the cell."))
            return false;

        return true;
    }

    private bool VerifyGridResourceScatter()
    {
        var root = new Node { Name = "GridResourceScatterSmokeRoot" };
        AddChild(root);

        var grid = new GridProjectionComponent
        {
            Name = "Grid",
            Projection = GridProjectionComponent.GridProjection.TopDown,
            TileSize = new Vector2(16, 16)
        };
        root.AddChild(grid);

        var wallet = new GridResourceWalletComponent
        {
            Name = "Resources",
            ApplyStartingResourcesOnReady = false
        };
        root.AddChild(wallet);

        var jobs = new GridJobQueueComponent { Name = "Jobs" };
        root.AddChild(jobs);

        var placement = new GridPlacementComponent { Name = "Placement" };
        placement.SetOccupied(new Vector2I(0, 0), true);
        root.AddChild(placement);

        var cells = new GridCellDataComponent
        {
            Name = "Cells",
            DefaultTerrainKind = "grass"
        };
        cells.SetTerrainKind(new Vector2I(0, 1), "water");
        root.AddChild(cells);

        var resourceRoot = new Node2D { Name = "ResourceNodes" };
        root.AddChild(resourceRoot);

        var scatter = new GridResourceScatterComponent
        {
            Name = "ResourceScatter",
            GridPath = new NodePath("../Grid"),
            ResourceRootPath = new NodePath("../ResourceNodes"),
            PlacementPath = new NodePath("../Placement"),
            CellDataPath = new NodePath("../Cells"),
            ResourceWalletPath = new NodePath("../Resources"),
            JobQueuePath = new NodePath("../Jobs"),
            Seed = 7,
            BoundsOrigin = Vector2I.Zero,
            BoundsSize = new Vector2I(2, 2),
            Density = 1f,
            MaxNodes = 4,
            ResourceId = "wood",
            MinAmount = 3,
            MaxAmount = 3,
            AmountPerGather = 1,
            AvoidOccupiedCells = true,
            MarkGeneratedCellsOccupied = true
        };
        root.AddChild(scatter);

        int generated = scatter.RebuildScatter();
        bool countOk = generated == 2 && resourceRoot.GetChildCount() == 2;
        bool occupiedAvoided = true;
        bool terrainAvoided = true;
        bool generatedReserved = true;
        var generatedCells = new Godot.Collections.Array<Vector2I>();
        bool nodeConfigured = true;
        foreach (Node child in resourceRoot.GetChildren())
        {
            if (child is not GridResourceNodeComponent resource)
            {
                nodeConfigured = false;
                continue;
            }

            occupiedAvoided &= resource.Cell != Vector2I.Zero;
            terrainAvoided &= resource.Cell != new Vector2I(0, 1);
            generatedReserved &= placement.IsOccupied(resource.Cell);
            generatedCells.Add(resource.Cell);
            nodeConfigured &= resource.ResourceId == "wood"
                && resource.Amount == 3
                && resource.UseExplicitCell
                && !resource.ResourceWalletPath.IsEmpty
                && !resource.JobQueuePath.IsEmpty;
        }

        int cleared = scatter.ClearGenerated();
        bool generatedUnreserved = true;
        foreach (Vector2I cell in generatedCells)
            generatedUnreserved &= !placement.IsOccupied(cell);
        bool clearOk = cleared == 2 && generatedUnreserved;
        root.QueueFree();

        if (!Expect(countOk, "GridResourceScatter did not generate the expected resource node count."))
            return false;

        if (!Expect(occupiedAvoided, "GridResourceScatter did not avoid occupied cells."))
            return false;

        if (!Expect(terrainAvoided, "GridResourceScatter did not avoid blocked terrain cells."))
            return false;

        if (!Expect(generatedReserved, "GridResourceScatter did not mark generated resource cells occupied."))
            return false;

        if (!Expect(nodeConfigured, "GridResourceScatter did not configure generated GridResourceNodeComponent nodes."))
            return false;

        if (!Expect(clearOk, "GridResourceScatter did not clear generated resource nodes and release occupied cells."))
            return false;

        return true;
    }

    private bool VerifyGridResourceScatterBoundsInvalidTuning()
    {
        var root = new Node { Name = "GridResourceScatterBoundsSmokeRoot" };
        AddChild(root);

        var grid = new GridProjectionComponent
        {
            Name = "Grid",
            Projection = GridProjectionComponent.GridProjection.TopDown,
            TileSize = new Vector2(16, 16)
        };
        root.AddChild(grid);

        var resourceRoot = new Node2D { Name = "ResourceNodes" };
        root.AddChild(resourceRoot);

        var scatter = new GridResourceScatterComponent
        {
            Name = "ResourceScatter",
            GridPath = new NodePath("../Grid"),
            ResourceRootPath = new NodePath("../ResourceNodes"),
            BoundsSize = new Vector2I(100000, 100000),
            Density = float.NaN,
            MaxNodes = -5,
            ResourceId = " ",
            GatherJobKind = " ",
            GatherSeconds = float.NaN,
            AmountPerGather = -8
        };
        root.AddChild(scatter);

        int empty = scatter.RebuildScatter();
        scatter.Density = 1f;
        scatter.MaxNodes = 2;
        int generated = scatter.RebuildScatter();
        bool countBounded = empty == 0
            && generated == 2
            && resourceRoot.GetChildCount() == 2
            && scatter.EffectiveBoundsSize.X == 1024
            && scatter.EffectiveBoundsSize.Y == 1024;

        bool nodeBounded = true;
        foreach (Node child in resourceRoot.GetChildren())
        {
            if (child is not GridResourceNodeComponent resource)
            {
                nodeBounded = false;
                continue;
            }

            nodeBounded &= resource.ResourceId == "resource"
                && resource.GatherJobKind == "gather"
                && resource.AmountPerGather == 1
                && float.IsFinite(resource.GatherSeconds)
                && resource.GatherSeconds >= 0.01f;
        }

        root.QueueFree();

        if (!Expect(countBounded && nodeBounded,
            $"GridResourceScatter did not bound invalid scatter tuning. empty={empty}, generated={generated}, children={resourceRoot.GetChildCount()}."))
            return false;

        return true;
    }

    private bool VerifyGridProduction()
    {
        var root = new Node { Name = "GridProductionSmokeRoot" };
        AddChild(root);

        var wallet = new GridResourceWalletComponent
        {
            Name = "Resources",
            ApplyStartingResourcesOnReady = false
        };
        wallet.SetAmount("wood", 4);
        root.AddChild(wallet);

        var recipe = new GridProductionRecipe
        {
            RecipeId = "planks",
            DisplayName = "Planks",
            DurationSeconds = 2f
        };
        recipe.Inputs.Add(new GridResourceAmount { ResourceId = "wood", Amount = 2 });
        recipe.Outputs.Add(new GridResourceAmount { ResourceId = "plank", Amount = 3 });

        var production = new GridProductionComponent
        {
            Name = "SawmillProduction",
            ResourceWalletPath = new NodePath("../Resources"),
            ActiveRecipeId = "planks",
            Loop = false
        };
        production.Recipes.Add(recipe);
        root.AddChild(production);

        bool started = production.StartProduction()
            && wallet.GetAmount("wood") == 2
            && production.State == GridProductionComponent.ProductionState.Producing
            && production.Progress01 < 0.01f;

        bool doubleStartRejected = !production.StartProduction("planks")
            && wallet.GetAmount("wood") == 2
            && production.State == GridProductionComponent.ProductionState.Producing
            && production.CurrentRecipeId == "planks";

        production.Tick(1.0);
        bool progressed = production.State == GridProductionComponent.ProductionState.Producing
            && production.Progress01 > 0.45f
            && production.Progress01 < 0.55f;

        production.Tick(1.25);
        bool completed = production.State == GridProductionComponent.ProductionState.Idle
            && wallet.GetAmount("wood") == 2
            && wallet.GetAmount("plank") == 3;

        production.Loop = true;
        bool loopStarted = production.StartProduction("planks");
        production.Tick(2.1);
        bool looped = wallet.GetAmount("wood") == 0
            && wallet.GetAmount("plank") == 6
            && production.State == GridProductionComponent.ProductionState.Idle;

        bool rejectedMissingInputs = !production.StartProduction("planks")
            && wallet.GetAmount("wood") == 0
            && wallet.GetAmount("plank") == 6;

        var dictionaryRecipe = new Godot.Collections.Dictionary
        {
            ["recipe_id"] = "dictionary_planks",
            ["display_name"] = "Dictionary Planks",
            ["duration_seconds"] = -0.5f,
            ["inputs"] = new Godot.Collections.Array
            {
                new Godot.Collections.Dictionary
                {
                    ["resource_id"] = "bark",
                    ["amount"] = "2"
                }
            },
            ["outputs"] = new Godot.Collections.Array
            {
                new Godot.Collections.Dictionary
                {
                    ["ResourceId"] = "mulch",
                    ["Amount"] = "4.0"
                }
            }
        };
        production.Recipes.Add(dictionaryRecipe);
        wallet.SetAmount("bark", 2);
        bool dictionaryStarted = production.StartProduction("dictionary_planks");
        production.Tick(0.75);
        bool dictionaryProduction = dictionaryStarted
            && wallet.GetAmount("bark") == 0
            && wallet.GetAmount("mulch") == 4
            && Mathf.IsEqualApprox(production.FindRecipe("dictionary_planks")?.EffectiveDurationSeconds ?? 0f, 0.01f);
        if (!dictionaryProduction)
        {
            GridProductionRecipe? readRecipe = production.FindRecipe("dictionary_planks");
            Failure = $"GridProduction dictionary recipe details: started={dictionaryStarted}, bark={wallet.GetAmount("bark")}, mulch={wallet.GetAmount("mulch")}, recipeFound={readRecipe != null}, duration={readRecipe?.EffectiveDurationSeconds.ToString() ?? "null"}, state={production.State}.";
            root.QueueFree();
            return false;
        }

        wallet.SetAmount("wood", 2);
        bool invalidDeltaStarted = production.StartProduction("planks");
        production.Tick(double.NaN);
        production.Tick(double.PositiveInfinity);
        production.Tick(-1.0);
        bool invalidDeltaIgnored = invalidDeltaStarted
            && production.State == GridProductionComponent.ProductionState.Producing
            && Mathf.IsEqualApprox(production.EffectiveRemainingSeconds, 2f)
            && production.Progress01 < 0.01f;
        production.Tick(2.1);
        bool invalidDeltaCycleStillCompletes = production.State == GridProductionComponent.ProductionState.Idle
            && wallet.GetAmount("plank") == 9;
        root.QueueFree();

        if (!Expect(started, "GridProduction did not start by consuming inputs."))
            return false;

        if (!Expect(doubleStartRejected, "GridProduction allowed a second start while already producing and may double-spend inputs."))
            return false;

        if (!Expect(progressed, "GridProduction did not report in-progress cycle progress."))
            return false;

        if (!Expect(completed, "GridProduction did not complete by adding outputs."))
            return false;

        if (!Expect(loopStarted && looped, "GridProduction did not complete a looped cycle and stop when inputs ran out."))
            return false;

        if (!Expect(rejectedMissingInputs, "GridProduction did not reject production with missing inputs."))
            return false;

        if (!Expect(invalidDeltaIgnored, "GridProduction let invalid deltas change production progress."))
            return false;

        if (!Expect(invalidDeltaCycleStillCompletes, "GridProduction did not complete after ignoring invalid deltas."))
            return false;

        return true;
    }

    private bool VerifyGridProductionPanel()
    {
        var root = new Control { Name = "GridProductionPanelSmokeRoot" };
        AddChild(root);

        var wallet = new GridResourceWalletComponent
        {
            Name = "Resources",
            ApplyStartingResourcesOnReady = false
        };
        wallet.SetAmount("wood", 4);
        root.AddChild(wallet);

        var machines = new Node { Name = "ProductionBuildings" };
        root.AddChild(machines);

        var workshop = new Node2D { Name = "Workshop" };
        machines.AddChild(workshop);

        var recipe = new GridProductionRecipe
        {
            RecipeId = "planks",
            DisplayName = "Planks",
            DurationSeconds = 2f
        };
        recipe.Inputs.Add(new GridResourceAmount { ResourceId = "wood", Amount = 2 });
        recipe.Outputs.Add(new GridResourceAmount { ResourceId = "plank", Amount = 3 });

        var production = new GridProductionComponent
        {
            Name = "Production",
            ResourceWalletPath = new NodePath("../../../Resources"),
            ActiveRecipeId = "planks",
            Loop = false
        };
        production.Recipes.Add(recipe);
        workshop.AddChild(production);

        var panel = new GridProductionPanelComponent
        {
            Name = "ProductionPanel",
            ProductionRootPath = new NodePath("../ProductionBuildings"),
            BuildInEditor = false,
            GenerateControlsWhenPathsEmpty = true,
            AutoRefresh = false
        };
        root.AddChild(panel);
        panel.RebuildPanel();

        string machinePath = production.GetPath().ToString();
        bool initial = panel.SummaryText() == "Machines 1 | Active 0"
            && panel.VisibleMachineRowCount() == 1
            && panel.TextForMachine(machinePath).Contains("Workshop: Idle Planks");

        bool started = panel.StartMachine(machinePath)
            && production.State == GridProductionComponent.ProductionState.Producing
            && wallet.GetAmount("wood") == 2
            && panel.SummaryText() == "Machines 1 | Active 1"
            && panel.TextForMachine(machinePath).Contains("Producing Planks");

        production.Tick(1.0);
        panel.RefreshPanel();
        bool progressed = panel.TextForMachine(machinePath).Contains("50%");

        bool paused = panel.PauseMachine(machinePath)
            && production.State == GridProductionComponent.ProductionState.Paused
            && panel.TextForMachine(machinePath).Contains("Paused Planks");

        bool resumed = panel.ResumeMachine(machinePath)
            && production.State == GridProductionComponent.ProductionState.Producing;

        bool cancelled = panel.CancelMachine(machinePath, refundInputs: true)
            && production.State == GridProductionComponent.ProductionState.Idle
            && wallet.GetAmount("wood") == 4
            && panel.SummaryText() == "Machines 1 | Active 0";
        root.QueueFree();

        if (!Expect(initial, "GridProductionPanel did not render the idle production machine."))
            return false;

        if (!Expect(started, "GridProductionPanel did not start production through GridProductionComponent."))
            return false;

        if (!Expect(progressed, "GridProductionPanel did not render production progress."))
            return false;

        if (!Expect(paused && resumed, "GridProductionPanel did not pause and resume production."))
            return false;

        if (!Expect(cancelled, "GridProductionPanel did not cancel production and refund inputs."))
            return false;

        return true;
    }

    private bool VerifyGridObjectiveTracker()
    {
        var clearLand = new GridObjectiveDefinition
        {
            ObjectiveId = "clear_land",
            DisplayName = "Clear Land",
            TargetCount = 3,
            AutoComplete = true,
            ActiveOnStart = true
        };

        var workshop = new GridObjectiveDefinition
        {
            ObjectiveId = "build workshop",
            DisplayName = "Build Workshop",
            TargetCount = 1,
            AutoComplete = true,
            ActiveOnStart = false,
            HiddenUntilActive = true
        };

        var tracker = new GridObjectiveTrackerComponent
        {
            Name = "Objectives",
            AutoActivateAll = false,
            ParticipatesInSave = false
        };
        tracker.Objectives.Add(clearLand);
        tracker.Objectives.Add(workshop);
        tracker.Objectives.Add(new Godot.Collections.Dictionary
        {
            ["objective_id"] = "dict_goal",
            ["display_name"] = "Dictionary Goal",
            ["target_count"] = -4,
            ["active_on_start"] = true
        });
        AddChild(tracker);

        bool activeOnStart = tracker.IsActive("clear_land")
            && tracker.IsActive("dict_goal")
            && !tracker.IsActive("build_workshop")
            && tracker.GetTarget("clear land") == 3
            && tracker.GetTarget("dict_goal") == 1;

        bool progressed = tracker.AddProgress("clear land")
            && tracker.GetProgress("clear_land") == 1
            && !tracker.IsComplete("clear_land");

        bool completed = tracker.AddProgress("clear_land", 2)
            && tracker.GetProgress("clear_land") == 3
            && tracker.IsComplete("clear land");

        bool activated = tracker.SetObjectiveActive("build_workshop", true)
            && tracker.AddProgress("build workshop")
            && tracker.IsComplete("build_workshop");

        Godot.Collections.Dictionary snapshot = tracker.CaptureState();
        tracker.ResetObjective("clear_land");
        bool reset = tracker.GetProgress("clear_land") == 0 && !tracker.IsComplete("clear_land");
        tracker.RestoreState(snapshot);
        bool restored = tracker.GetProgress("clear_land") == 3
            && tracker.IsComplete("clear_land")
            && tracker.IsComplete("build_workshop");

        tracker.QueueFree();

        if (!Expect(activeOnStart, "GridObjectiveTracker did not activate authored start objectives."))
            return false;

        if (!Expect(progressed && completed, "GridObjectiveTracker did not progress and complete an objective."))
            return false;

        if (!Expect(activated, "GridObjectiveTracker did not activate and complete a hidden objective."))
            return false;

        if (!Expect(reset && restored, "GridObjectiveTracker did not capture and restore objective state."))
            return false;

        return true;
    }

    private bool VerifyGridObjectivePanel()
    {
        var root = new Control { Name = "GridObjectivePanelSmokeRoot" };
        AddChild(root);

        var clearLand = new GridObjectiveDefinition
        {
            ObjectiveId = "clear_land",
            DisplayName = "Clear Land",
            TargetCount = 3,
            AutoComplete = true,
            ActiveOnStart = true
        };

        var buildBase = new GridObjectiveDefinition
        {
            ObjectiveId = "build_base",
            DisplayName = "Build Base",
            TargetCount = 1,
            AutoComplete = true,
            ActiveOnStart = true
        };

        var tracker = new GridObjectiveTrackerComponent
        {
            Name = "Objectives",
            ParticipatesInSave = false
        };
        tracker.Objectives.Add(clearLand);
        tracker.Objectives.Add(buildBase);
        root.AddChild(tracker);

        var panel = new GridObjectivePanelComponent
        {
            Name = "ObjectivesPanel",
            ObjectiveTrackerPath = new NodePath("../Objectives"),
            BuildInEditor = false,
            GenerateControlsWhenPathsEmpty = true,
            AutoRefresh = false
        };
        root.AddChild(panel);
        panel.RebuildPanel();

        bool initial = panel.SummaryText() == "Goals 2 | Done 0"
            && panel.VisibleObjectiveRowCount() == 2
            && panel.TextForObjective("clear_land").Contains("Clear Land: 0/3 Active");

        tracker.AddProgress("clear_land", 3);
        panel.RefreshPanel();
        bool completed = panel.SummaryText() == "Goals 2 | Done 1"
            && panel.TextForObjective("clear_land").Contains("Clear Land: 3/3 Done");

        panel.HideCompleted = true;
        panel.RefreshPanel();
        bool hidden = panel.VisibleObjectiveRowCount() == 1
            && panel.TextForObjective("build_base").Contains("Build Base: 0/1 Active");

        root.QueueFree();

        if (!Expect(initial, "GridObjectivePanel did not render active objectives."))
            return false;

        if (!Expect(completed, "GridObjectivePanel did not render objective completion."))
            return false;

        if (!Expect(hidden, "GridObjectivePanel did not hide completed objectives."))
            return false;

        return true;
    }

    private bool VerifyGridObjectiveEventBinder()
    {
        var root = new Node { Name = "GridObjectiveEventBinderSmokeRoot" };
        AddChild(root);

        var tracker = new GridObjectiveTrackerComponent
        {
            Name = "Objectives",
            AutoActivateAll = true,
            ParticipatesInSave = false
        };
        tracker.Objectives.Add(new GridObjectiveDefinition { ObjectiveId = "clear_land", TargetCount = 1 });
        tracker.Objectives.Add(new GridObjectiveDefinition { ObjectiveId = "build_workshop", TargetCount = 1 });
        tracker.Objectives.Add(new GridObjectiveDefinition { ObjectiveId = "gather_wood", TargetCount = 2 });
        tracker.Objectives.Add(new GridObjectiveDefinition { ObjectiveId = "produce_planks", TargetCount = 1 });
        root.AddChild(tracker);

        var jobs = new GridJobQueueComponent
        {
            Name = "Jobs",
            RemoveCompletedJobs = false
        };
        root.AddChild(jobs);

        var buildSites = new GridBuildSiteComponent { Name = "BuildSites" };
        root.AddChild(buildSites);

        var resourceRoot = new Node { Name = "ResourceNodes" };
        root.AddChild(resourceRoot);
        var tree = new GridResourceNodeComponent
        {
            Name = "Tree",
            UseExplicitCell = true,
            Cell = new Vector2I(1, 1),
            ResourceId = "wood",
            Amount = 2,
            AmountPerGather = 2
        };
        resourceRoot.AddChild(tree);

        var productionRoot = new Node { Name = "ProductionBuildings" };
        root.AddChild(productionRoot);
        var wallet = new GridResourceWalletComponent
        {
            Name = "Resources",
            ApplyStartingResourcesOnReady = false
        };
        wallet.SetAmount("wood", 2);
        root.AddChild(wallet);
        var machine = new GridProductionComponent
        {
            Name = "Production",
            ResourceWalletPath = new NodePath("../../Resources"),
            Loop = false
        };
        var recipe = new GridProductionRecipe
        {
            RecipeId = "planks",
            DurationSeconds = 0.01f
        };
        recipe.Inputs.Add(new GridResourceAmount { ResourceId = "wood", Amount = 1 });
        recipe.Outputs.Add(new GridResourceAmount { ResourceId = "plank", Amount = 1 });
        machine.Recipes.Add(recipe);
        productionRoot.AddChild(machine);

        var binder = new GridObjectiveEventBinderComponent
        {
            Name = "ObjectiveEvents",
            ObjectiveTrackerPath = new NodePath("../Objectives"),
            JobQueuePath = new NodePath("../Jobs"),
            BuildSitePath = new NodePath("../BuildSites"),
            ResourceNodesRootPath = new NodePath("../ResourceNodes"),
            ProductionRootPath = new NodePath("../ProductionBuildings"),
            AutoConnect = false
        };
        root.AddChild(binder);
        binder.ConnectSystems();

        string jobId = jobs.AddJob(Vector2I.Zero, "clear_land");
        jobs.CompleteJob(jobId, "worker");
        bool jobProgress = tracker.IsComplete("clear_land");

        buildSites.EmitSignal(GridBuildSiteComponent.SignalName.BuildSiteCompleted, "workshop", "build_1", new Node2D(), 2, 2);
        bool buildProgress = tracker.IsComplete("build_workshop");

        tree.Gather();
        bool gatherProgress = tracker.IsComplete("gather_wood");

        bool productionStarted = machine.StartProduction("planks");
        machine.Tick(0.02);
        bool productionProgress = productionStarted && tracker.IsComplete("produce_planks");

        root.QueueFree();

        if (!Expect(jobProgress, "GridObjectiveEventBinder did not advance completed job objectives."))
            return false;

        if (!Expect(buildProgress, "GridObjectiveEventBinder did not advance completed build objectives."))
            return false;

        if (!Expect(gatherProgress, "GridObjectiveEventBinder did not advance gathered resource objectives."))
            return false;

        if (!Expect(productionProgress, "GridObjectiveEventBinder did not advance production objectives."))
            return false;

        return true;
    }

    private bool VerifyGridWorkerSpawner()
    {
        var root = new Node { Name = "GridWorkerSpawnerSmokeRoot" };
        AddChild(root);

        var grid = new GridProjectionComponent
        {
            Name = "Grid",
            Projection = GridProjectionComponent.GridProjection.TopDown,
            TileSize = new Vector2(16, 16)
        };
        root.AddChild(grid);

        var navigation = new GridNavigationComponent
        {
            Name = "Navigation",
            GridPath = new NodePath("../Grid"),
            BoundsSize = new Vector2I(16, 16)
        };
        root.AddChild(navigation);

        var jobs = new GridJobQueueComponent { Name = "Jobs" };
        root.AddChild(jobs);

        var units = new Node2D { Name = "Units" };
        root.AddChild(units);

        var spawner = new GridWorkerSpawnerComponent
        {
            Name = "WorkerSpawner",
            UnitScene = ResourceLoader.Load<PackedScene>("res://addons/beep_game_builder_cs/templates/scenes/grid_worker_unit.tscn"),
            UnitsRootPath = new NodePath("../Units"),
            GridPath = new NodePath("../Grid"),
            NavigationPath = new NodePath("../Navigation"),
            JobQueuePath = new NodePath("../Jobs"),
            WorkerIdPrefix = "truck",
            MaxWorkers = 1,
            DriveCharacterBody = false
        };
        root.AddChild(spawner);

        Node2D? unit = spawner.SpawnWorker(new Vector2I(2, 3));
        GridPathFollowerComponent? follower = unit == null ? null : EntityComponent.FindComponent<GridPathFollowerComponent>(unit, recursive: false);
        GridWorkerComponent? worker = unit == null ? null : EntityComponent.FindComponent<GridWorkerComponent>(unit, recursive: false);

        bool spawned = unit != null
            && unit.GetParent() == units
            && unit.GlobalPosition.IsEqualApprox(grid.CellToWorld(new Vector2I(2, 3)))
            && unit.GetNodeOrNull<Sprite2D>("Sprite2D") != null
            && follower != null
            && worker != null
            && worker.WorkerId == "truck_1";

        string jobId = jobs.AddJob(new Vector2I(4, 3), "clear_land");
        bool claimed = worker != null
            && worker.ClaimNextJob()
            && jobs.GetJobState(jobId) == GridJobQueueComponent.GridJobState.Claimed;

        Node2D? overflow = spawner.SpawnWorker(new Vector2I(3, 3));
        bool maxRejected = overflow == null && spawner.SpawnedCount == 1;
        root.QueueFree();

        if (!Expect(spawned, "GridWorkerSpawner did not create a wired worker/truck unit at the requested cell."))
            return false;

        if (!Expect(claimed, "GridWorkerSpawner-created worker could not claim a queued grid job."))
            return false;

        if (!Expect(maxRejected, "GridWorkerSpawner did not enforce MaxWorkers."))
            return false;

        return true;
    }

    private bool VerifyGridWorkerSpawnerUsesCellDataTerrain()
    {
        var root = new Node { Name = "GridWorkerSpawnerTerrainSmokeRoot" };
        AddChild(root);

        var grid = new GridProjectionComponent
        {
            Name = "Grid",
            Projection = GridProjectionComponent.GridProjection.TopDown,
            TileSize = new Vector2(16, 16)
        };
        root.AddChild(grid);

        var cells = new GridCellDataComponent { Name = "Cells", DefaultTerrainKind = "grass" };
        root.AddChild(cells);
        cells.SetTerrainKind(new Vector2I(1, 1), "water");
        cells.AddFlag(new Vector2I(2, 1), GridCellDataComponent.CellFlags.Blocked);

        var placement = new GridPlacementComponent
        {
            Name = "Placement",
            GridPath = new NodePath("../Grid")
        };
        root.AddChild(placement);
        placement.SetOccupied(new Vector2I(3, 1), true);

        var navigation = new GridNavigationComponent
        {
            Name = "Navigation",
            GridPath = new NodePath("../Grid"),
            BoundsSize = new Vector2I(8, 8)
        };
        root.AddChild(navigation);

        var jobs = new GridJobQueueComponent { Name = "Jobs" };
        root.AddChild(jobs);

        var units = new Node2D { Name = "Units" };
        root.AddChild(units);

        var spawner = new GridWorkerSpawnerComponent
        {
            Name = "WorkerSpawner",
            UnitsRootPath = new NodePath("../Units"),
            GridPath = new NodePath("../Grid"),
            NavigationPath = new NodePath("../Navigation"),
            JobQueuePath = new NodePath("../Jobs"),
            CellDataPath = new NodePath("../Cells"),
            PlacementPath = new NodePath("../Placement"),
            MaxWorkers = 4,
            DriveCharacterBody = false
        };
        root.AddChild(spawner);

        bool rejectsWater = spawner.SpawnWorker(new Vector2I(1, 1)) == null;
        bool rejectsBlocked = spawner.SpawnWorker(new Vector2I(2, 1)) == null;
        bool rejectsOccupied = spawner.SpawnWorker(new Vector2I(3, 1)) == null;
        bool acceptsGrass = spawner.CanSpawnAt(new Vector2I(4, 1))
            && spawner.SpawnWorker(new Vector2I(4, 1)) != null
            && spawner.SpawnedCount == 1;

        root.QueueFree();

        if (!Expect(rejectsWater, "GridWorkerSpawner did not reject a water spawn cell from GridCellData."))
            return false;

        if (!Expect(rejectsBlocked, "GridWorkerSpawner did not reject a blocked spawn cell from GridCellData."))
            return false;

        if (!Expect(rejectsOccupied, "GridWorkerSpawner did not reject an occupied spawn cell from GridPlacement."))
            return false;

        if (!Expect(acceptsGrass, "GridWorkerSpawner did not allow a valid grass spawn cell after terrain/occupancy checks."))
            return false;

        return true;
    }

    private bool VerifyGridWorkerSpawnerBoundsInvalidTuning()
    {
        var root = new Node { Name = "GridWorkerSpawnerBoundsSmokeRoot" };
        AddChild(root);

        var grid = new GridProjectionComponent
        {
            Name = "Grid",
            Projection = GridProjectionComponent.GridProjection.TopDown,
            TileSize = new Vector2(16, 16)
        };
        root.AddChild(grid);

        var navigation = new GridNavigationComponent
        {
            Name = "Navigation",
            GridPath = new NodePath("../Grid"),
            BoundsSize = new Vector2I(8, 8)
        };
        root.AddChild(navigation);

        var jobs = new GridJobQueueComponent { Name = "Jobs" };
        root.AddChild(jobs);

        var units = new Node2D { Name = "Units" };
        root.AddChild(units);

        var spawner = new GridWorkerSpawnerComponent
        {
            Name = "WorkerSpawner",
            UnitsRootPath = new NodePath("../Units"),
            GridPath = new NodePath("../Grid"),
            NavigationPath = new NodePath("../Navigation"),
            JobQueuePath = new NodePath("../Jobs"),
            WorkerIdPrefix = "bad/prefix",
            MaxWorkers = 0,
            InitialWorkers = 99,
            DefaultUnitSpeed = float.NaN,
            DriveCharacterBody = false
        };
        root.AddChild(spawner);

        Node2D? first = spawner.SpawnWorker(new Vector2I(1, 1));
        Node2D? overflow = spawner.SpawnWorker(new Vector2I(2, 1));
        GridWorkerComponent? worker = first == null ? null : EntityComponent.FindComponent<GridWorkerComponent>(first, recursive: false);
        GridPathFollowerComponent? follower = first == null ? null : EntityComponent.FindComponent<GridPathFollowerComponent>(first, recursive: false);

        bool bounded = first != null
            && overflow == null
            && spawner.SpawnedCount == 1
            && spawner.EffectiveMaxWorkers == 1
            && spawner.EffectiveInitialWorkers == 1
            && worker?.WorkerId == "bad_prefix_1"
            && follower != null
            && float.IsFinite(follower.Speed)
            && follower.Speed >= 0f;
        root.QueueFree();

        if (!Expect(bounded, "GridWorkerSpawner did not bound invalid max/initial/speed tuning or sanitize worker ids."))
            return false;

        return true;
    }

    private bool VerifyGridWorkerSpawnerPanel()
    {
        var root = new Control { Name = "GridWorkerSpawnerPanelSmokeRoot" };
        AddChild(root);

        var grid = new GridProjectionComponent
        {
            Name = "Grid",
            Projection = GridProjectionComponent.GridProjection.TopDown,
            TileSize = new Vector2(16, 16)
        };
        root.AddChild(grid);

        var navigation = new GridNavigationComponent
        {
            Name = "Navigation",
            GridPath = new NodePath("../Grid"),
            BoundsSize = new Vector2I(8, 8)
        };
        root.AddChild(navigation);

        var jobs = new GridJobQueueComponent { Name = "Jobs" };
        root.AddChild(jobs);

        var units = new Node2D { Name = "Units" };
        root.AddChild(units);

        var spawner = new GridWorkerSpawnerComponent
        {
            Name = "WorkerSpawner",
            UnitsRootPath = new NodePath("../Units"),
            GridPath = new NodePath("../Grid"),
            NavigationPath = new NodePath("../Navigation"),
            JobQueuePath = new NodePath("../Jobs"),
            MaxWorkers = 1,
            WorkerIdPrefix = "truck"
        };
        root.AddChild(spawner);

        var panel = new GridWorkerSpawnerPanelComponent
        {
            Name = "BasePanel",
            SpawnerPath = new NodePath("../WorkerSpawner"),
            BuildInEditor = false,
            GenerateControlsWhenPathsEmpty = true,
            SpawnButtonText = "Spawn Truck"
        };
        root.AddChild(panel);
        panel.RebuildPanel();

        bool initial = panel.CountText() == "Workers: 0/1";
        bool spawned = panel.RequestSpawn()
            && spawner.SpawnedCount == 1
            && panel.CountText() == "Workers: 1/1";
        bool maxBlocked = !panel.RequestSpawn()
            && spawner.SpawnedCount == 1
            && panel.CountText() == "Workers: 1/1";
        root.QueueFree();

        if (!Expect(initial, "GridWorkerSpawnerPanel did not render the initial worker count."))
            return false;

        if (!Expect(spawned, "GridWorkerSpawnerPanel did not spawn through GridWorkerSpawnerComponent."))
            return false;

        if (!Expect(maxBlocked, "GridWorkerSpawnerPanel did not respect GridWorkerSpawnerComponent.MaxWorkers."))
            return false;

        if (!VerifyGridWorkerSpawnerPanelSceneControls())
            return false;

        return true;
    }

    private bool VerifyGridWorkerSpawnerPanelSceneControls()
    {
        var root = new Control { Name = "GridWorkerSpawnerPanelSceneControlsSmokeRoot" };
        AddChild(root);

        var grid = new GridProjectionComponent
        {
            Name = "Grid",
            Projection = GridProjectionComponent.GridProjection.TopDown,
            TileSize = new Vector2(16, 16)
        };
        root.AddChild(grid);

        var navigation = new GridNavigationComponent
        {
            Name = "Navigation",
            GridPath = new NodePath("../Grid"),
            BoundsSize = new Vector2I(8, 8)
        };
        root.AddChild(navigation);

        var jobs = new GridJobQueueComponent { Name = "Jobs" };
        root.AddChild(jobs);

        var units = new Node2D { Name = "Units" };
        root.AddChild(units);

        var spawner = new GridWorkerSpawnerComponent
        {
            Name = "WorkerSpawner",
            UnitsRootPath = new NodePath("../Units"),
            GridPath = new NodePath("../Grid"),
            NavigationPath = new NodePath("../Navigation"),
            JobQueuePath = new NodePath("../Jobs"),
            MaxWorkers = 2,
            WorkerIdPrefix = "truck"
        };
        root.AddChild(spawner);

        var panel = new GridWorkerSpawnerPanelComponent
        {
            Name = "BasePanel",
            SpawnerPath = new NodePath("../WorkerSpawner"),
            TitleLabelPath = new NodePath("Panel/Content/Title"),
            CountLabelPath = new NodePath("Panel/Content/Count"),
            SpawnButtonPath = new NodePath("Panel/Content/SpawnButton"),
            BuildInEditor = false,
            GenerateControlsWhenPathsEmpty = false,
            SpawnButtonText = "Spawn Truck"
        };
        root.AddChild(panel);

        var shell = new PanelContainer { Name = "Panel" };
        panel.AddChild(shell);
        var content = new VBoxContainer { Name = "Content" };
        shell.AddChild(content);
        var title = new Label { Name = "Title" };
        var count = new Label { Name = "Count" };
        var button = new Button { Name = "SpawnButton" };
        content.AddChild(title);
        content.AddChild(count);
        content.AddChild(button);

        panel.RebuildPanel();
        bool bound = panel.UsesSceneControls()
            && panel.GetNodeOrNull<PanelContainer>("Panel") == shell
            && title.Text == "Base"
            && count.Text == "Workers: 0/2"
            && button.Text == "Spawn Truck";

        button.EmitSignal(Button.SignalName.Pressed);
        bool pressed = spawner.SpawnedCount == 1
            && count.Text == "Workers: 1/2";

        root.QueueFree();

        if (!Expect(bound, "GridWorkerSpawnerPanel did not bind existing scene-authored controls."))
            return false;

        if (!Expect(pressed, "GridWorkerSpawnerPanel scene-authored button did not spawn through GridWorkerSpawnerComponent."))
            return false;

        return true;
    }

    private bool VerifySelectionJobCommand()
    {
        var root = new Node { Name = "SelectionJobCommandSmokeRoot" };
        AddChild(root);

        var selection = new GridSelectionComponent
        {
            Name = "Selection",
            UseMouseInput = false
        };
        root.AddChild(selection);

        var queue = new GridJobQueueComponent
        {
            Name = "Jobs",
            UniqueCellKind = true,
            RemoveCompletedJobs = false
        };
        root.AddChild(queue);

        var command = new GridSelectionJobCommandComponent
        {
            Name = "ClearLandCommand",
            SelectionPath = new NodePath("../Selection"),
            JobQueuePath = new NodePath("../Jobs"),
            JobKind = "clear_land",
            WorkSeconds = 0.25f,
            Priority = 3,
            ClearSelectionAfterQueue = true
        };
        root.AddChild(command);

        selection.BeginDrag(new Vector2I(1, 1));
        selection.FinishDrag(new Vector2I(2, 2));

        int queued = command.QueueSelectedCells();
        bool countOk = queued == 4 && queue.QueuedCount == 4;
        bool cleared = selection.GetSelectedCells().Count == 0;

        int duplicateQueued = command.QueueRectangle(new Vector2I(1, 1), new Vector2I(2, 2));
        bool duplicateSuppressed = duplicateQueued == 4 && queue.QueuedCount == 4;

        int extraQueued = command.QueueRectangle(new Vector2I(3, 1), new Vector2I(3, 2), "prepare_pad", 0.5f, 5);
        bool extraOk = extraQueued == 2 && queue.QueuedCount == 6;
        root.QueueFree();

        if (!Expect(countOk, $"GridSelectionJobCommand queued {queued} selected jobs and queue count {queue.QueuedCount}, expected 4."))
            return false;

        if (!Expect(cleared, "GridSelectionJobCommand did not clear selection after queueing."))
            return false;

        if (!Expect(duplicateSuppressed, "GridSelectionJobCommand did not preserve unique cell/kind queue semantics."))
            return false;

        if (!Expect(extraOk, "GridSelectionJobCommand did not queue a second job kind over explicit cells."))
            return false;

        return true;
    }

    private bool VerifySelectionJobCommandUsesTerrainRules()
    {
        var root = new Node { Name = "SelectionJobCommandTerrainSmokeRoot" };
        AddChild(root);

        var selection = new GridSelectionComponent
        {
            Name = "Selection",
            UseMouseInput = false
        };
        root.AddChild(selection);

        var queue = new GridJobQueueComponent
        {
            Name = "Jobs",
            UniqueCellKind = true,
            RemoveCompletedJobs = false
        };
        root.AddChild(queue);

        var cells = new GridCellDataComponent
        {
            Name = "Cells",
            DefaultTerrainKind = "grass"
        };
        cells.SetTerrainKind(new Vector2I(1, 0), "water");
        cells.AddFlag(new Vector2I(2, 0), GridCellDataComponent.CellFlags.Blocked);
        root.AddChild(cells);

        var navigation = new GridNavigationComponent
        {
            Name = "Navigation",
            BoundsSize = new Vector2I(4, 4)
        };
        root.AddChild(navigation);

        var command = new GridSelectionJobCommandComponent
        {
            Name = "ClearLandCommand",
            SelectionPath = new NodePath("../Selection"),
            JobQueuePath = new NodePath("../Jobs"),
            CellDataPath = new NodePath("../Cells"),
            NavigationPath = new NodePath("../Navigation"),
            JobKind = "clear_land",
            WorkSeconds = 0.25f,
            ClearSelectionAfterQueue = false
        };
        root.AddChild(command);

        var requested = new Godot.Collections.Array
        {
            new Vector2I(0, 0),
            new Vector2I(1, 0),
            new Vector2I(2, 0),
            new Vector2I(9, 0)
        };

        int queued = command.QueueCells(requested);
        bool filtered = queued == 2
            && queue.QueuedCount == 2
            && command.CanQueueJobAt(new Vector2I(0, 0))
            && command.CanQueueJobAt(new Vector2I(2, 0))
            && !command.CanQueueJobAt(new Vector2I(1, 0))
            && !command.CanQueueJobAt(new Vector2I(9, 0));

        root.QueueFree();

        if (!Expect(filtered, "GridSelectionJobCommand did not skip water/out-of-bounds jobs while keeping clearable blocked cells queueable."))
            return false;

        return true;
    }

    private bool VerifySelectionJobCommandBoundsInvalidTuning()
    {
        var root = new Node { Name = "SelectionJobCommandBoundsSmokeRoot" };
        AddChild(root);

        var selection = new GridSelectionComponent
        {
            Name = "Selection",
            UseMouseInput = false
        };
        root.AddChild(selection);

        var queue = new GridJobQueueComponent
        {
            Name = "Jobs",
            RemoveCompletedJobs = false
        };
        root.AddChild(queue);

        var command = new GridSelectionJobCommandComponent
        {
            Name = "ClearLandCommand",
            SelectionPath = new NodePath("../Selection"),
            JobQueuePath = new NodePath("../Jobs"),
            WorkSeconds = float.NaN
        };
        root.AddChild(command);

        int queued = command.QueueRectangle(Vector2I.Zero, Vector2I.Zero, workSeconds: float.NaN);
        string jobId = queue.GetJobs().Count > 0 ? queue.GetJobs()[0]["id"].AsString() : "";
        float workSeconds = string.IsNullOrEmpty(jobId) ? 0f : queue.GetJobWorkSeconds(jobId);
        bool bounded = queued == 1
            && float.IsFinite(command.EffectiveWorkSeconds)
            && float.IsFinite(workSeconds)
            && workSeconds >= 0.01f;
        root.QueueFree();

        if (!Expect(bounded, $"GridSelectionJobCommand did not bound invalid work seconds. queued={queued}, work={workSeconds}."))
            return false;

        return true;
    }

    private bool VerifyGridCellData()
    {
        var cells = new GridCellDataComponent();
        Vector2I cell = new(3, 4);

        cells.SetTerrainKind(cell, "soil");
        cells.AddFlag(cell, GridCellDataComponent.CellFlags.Blocked);
        cells.ClearLand(cell);
        cells.Till(cell);
        cells.Water(cell);

        bool planted = cells.PlantCrop(cell, "turnip", 2);
        cells.AdvanceDay();
        bool waterCleared = !cells.HasFlag(cell, GridCellDataComponent.CellFlags.Watered);
        bool notReady = !cells.HasFlag(cell, GridCellDataComponent.CellFlags.HarvestReady);
        cells.AdvanceDay();
        bool ready = cells.HasFlag(cell, GridCellDataComponent.CellFlags.HarvestReady);

        var saved = cells.GetCells();
        var restored = new GridCellDataComponent();
        restored.LoadCells(saved);

        bool roundTripped = restored.GetTerrainKind(cell) == "soil"
            && restored.GetCropId(cell) == "turnip"
            && restored.GetCropAgeDays(cell) == 2
            && restored.HasFlag(cell, GridCellDataComponent.CellFlags.HarvestReady);

        bool harvested = restored.HarvestCrop(cell)
            && restored.GetCropId(cell) == ""
            && !restored.HasFlag(cell, GridCellDataComponent.CellFlags.Planted);

        if (!Expect(planted, "GridCellData did not plant a crop on tilled land."))
            return false;

        if (!Expect(waterCleared, "GridCellData did not clear watered state on day advance."))
            return false;

        if (!Expect(notReady, "GridCellData marked crop ready too early."))
            return false;

        if (!Expect(ready, "GridCellData did not mark mature crop harvest-ready."))
            return false;

        if (!Expect(roundTripped, "GridCellData did not round-trip terrain/crop/flags."))
            return false;

        if (!Expect(harvested, "GridCellData did not harvest and clear crop flags."))
            return false;

        return true;
    }

    private bool VerifyGridToolActions()
    {
        var root = new Node { Name = "GridToolActionSmokeRoot" };
        AddChild(root);

        var cells = new GridCellDataComponent { Name = "Cells" };
        cells.SetTerrainKind(new Vector2I(7, 7), "water");
        root.AddChild(cells);

        var selection = new GridSelectionComponent { Name = "Selection", UseMouseInput = false };
        root.AddChild(selection);

        var jobs = new GridJobQueueComponent { Name = "Jobs" };
        root.AddChild(jobs);

        var roads = new GridRoadComponent { Name = "Roads" };
        root.AddChild(roads);

        var navigation = new GridNavigationComponent
        {
            Name = "Navigation",
            BoundsSize = new Vector2I(16, 16)
        };
        root.AddChild(navigation);

        var tools = new GridToolActionComponent
        {
            Name = "Tools",
            CellDataPath = new NodePath("../Cells"),
            SelectionPath = new NodePath("../Selection"),
            JobQueuePath = new NodePath("../Jobs"),
            RoadPath = new NodePath("../Roads"),
            NavigationPath = new NodePath("../Navigation"),
            CropId = "potato",
            CropDaysToMature = 1,
            JobKind = "clear_land",
            TreatBlockedTerrainKindsAsUnworkable = true
        };
        root.AddChild(tools);

        Vector2I cropCell = new(2, 2);
        bool hoed = tools.ApplyToCell(cropCell, GridToolActionComponent.ToolAction.Hoe);
        bool watered = tools.ApplyToCell(cropCell, GridToolActionComponent.ToolAction.Water);
        bool planted = tools.ApplyToCell(cropCell, GridToolActionComponent.ToolAction.Plant);
        cells.AdvanceDay();
        bool harvested = tools.ApplyToCell(cropCell, GridToolActionComponent.ToolAction.Harvest);

        selection.SelectCell(new Vector2I(4, 4));
        selection.SelectCell(new Vector2I(4, 5), additive: true);
        int queued = tools.Apply(GridToolActionComponent.ToolAction.QueueJob);
        Vector2I roadCell = new(6, 6);
        bool roadPlaced = tools.ApplyToCell(roadCell, GridToolActionComponent.ToolAction.Road)
            && roads.HasRoad(roadCell)
            && roads.GetRoadKind(roadCell) == "dirt_path";
        bool roadRemoved = tools.ApplyToCell(roadCell, GridToolActionComponent.ToolAction.RemoveRoad)
            && !roads.HasRoad(roadCell);
        bool rejectsWaterWork = !tools.ApplyToCell(new Vector2I(7, 7), GridToolActionComponent.ToolAction.Clear)
            && !tools.ApplyToCell(new Vector2I(7, 7), GridToolActionComponent.ToolAction.Hoe);
        bool rejectsWaterJob = !tools.ApplyToCell(new Vector2I(7, 7), GridToolActionComponent.ToolAction.QueueJob);
        int countAfterWaterJob = jobs.QueuedCount;
        bool rejectsOutOfBoundsJob = !tools.ApplyToCell(new Vector2I(99, 99), GridToolActionComponent.ToolAction.QueueJob);
        int countAfterOutOfBoundsJob = jobs.QueuedCount;
        bool rejectsImpossibleJob = rejectsWaterJob
            && rejectsOutOfBoundsJob
            && countAfterWaterJob == 2
            && countAfterOutOfBoundsJob == 2;

        bool toolFlowOk = hoed
            && watered
            && planted
            && cells.GetCropId(cropCell) == ""
            && harvested
            && cells.HasFlag(cropCell, GridCellDataComponent.CellFlags.Tilled);
        bool jobsOk = queued == 2 && jobs.QueuedCount == 2;
        root.QueueFree();

        if (!Expect(toolFlowOk, "GridToolAction did not apply hoe/water/plant/harvest flow."))
            return false;

        if (!Expect(jobsOk, $"GridToolAction queued {queued} jobs with queue count {jobs.QueuedCount}, expected 2. waterRejected={rejectsWaterJob}, afterWater={countAfterWaterJob}, outOfBoundsRejected={rejectsOutOfBoundsJob}, afterOutOfBounds={countAfterOutOfBoundsJob}, waterTerrain={cells.GetTerrainKind(new Vector2I(7, 7))}, blockedKinds={tools.BlockedTerrainKinds.Count}, treatBlockedTerrain={tools.TreatBlockedTerrainKindsAsUnworkable}."))
            return false;

        if (!Expect(roadPlaced && roadRemoved, "GridToolAction did not place and remove roads through GridRoadComponent."))
            return false;

        if (!Expect(rejectsWaterWork && rejectsImpossibleJob, $"GridToolAction did not reject water/out-of-bounds work through terrain/navigation rules. waterWork={rejectsWaterWork}, waterJob={rejectsWaterJob}, outOfBounds={rejectsOutOfBoundsJob}, afterWater={countAfterWaterJob}, afterOutOfBounds={countAfterOutOfBoundsJob}, waterTerrain={cells.GetTerrainKind(new Vector2I(7, 7))}, blockedKinds={tools.BlockedTerrainKinds.Count}, treatBlockedTerrain={tools.TreatBlockedTerrainKindsAsUnworkable}."))
            return false;

        return true;
    }

    private bool VerifyGridTerrainGenerator()
    {
        var root = new Node { Name = "GridTerrainGeneratorSmokeRoot" };
        AddChild(root);

        var cells = new GridCellDataComponent
        {
            Name = "Cells",
            DefaultTerrainKind = "grass"
        };
        root.AddChild(cells);

        var generator = new TerrainGeneratorComponent
        {
            Name = "TerrainGenerator",
            CellDataPath = new NodePath("../Cells"),
            Mode = TerrainMode.Plain,
            Preset = TerrainPreset.Sea,
            BoundsSize = new Vector2I(3, 2)
        };
        root.AddChild(generator);

        int generated = generator.GenerateTerrain();
        int generatedCellCount = cells.CellCount;
        string generatedKind = cells.GetTerrainKind(new Vector2I(2, 1));
        bool generatedCellData = generated == 6
            && generatedCellCount == 6
            && generatedKind == "deep_water";

        var nav = new GridNavigationComponent
        {
            Name = "Navigation",
            BoundsSize = new Vector2I(3, 2),
            Diagonals = GridNavigationComponent.DiagonalPolicy.Never,
            CellDataPath = new NodePath("../Cells")
        };
        root.AddChild(nav);

        var waterPath = nav.FindCellPath(Vector2I.Zero, new Vector2I(2, 0));
        bool navigationUsesGeneratedTerrain = waterPath.Count == 0;

        generator.Mode = TerrainMode.Plain;
        generator.Preset = TerrainPreset.Sand;
        generator.BoundsSize = new Vector2I(2, 1);
        int sandGenerated = generator.GenerateTerrain();
        bool localSettingsWork = sandGenerated == 2
            && cells.CellCount == 2
            && cells.GetTerrainKind(Vector2I.Zero) == "sand";

        root.QueueFree();

        // Report what was actually seen: a bare false here cost a whole debugging
        // pass, because three different facts share one bool.
        if (!Expect(generatedCellData,
                $"TerrainGeneratorComponent did not write generated terrain kinds into GridCellData "
                + $"(generated={generated} expected 6, cellCount={generatedCellCount} expected 6, "
                + $"kind at (2,1)='{generatedKind}' expected 'deep_water')."))
            return false;

        if (!Expect(navigationUsesGeneratedTerrain, "GridNavigation did not consume terrain generated into GridCellData."))
            return false;

        if (!Expect(localSettingsWork, "GridTerrainGenerator did not generate from its own local settings."))
            return false;

        return true;
    }

    private bool VerifyGridToolActionsBoundInvalidTuning()
    {
        var root = new Node { Name = "GridToolActionBoundsSmokeRoot" };
        AddChild(root);

        var cells = new GridCellDataComponent { Name = "Cells" };
        root.AddChild(cells);

        var jobs = new GridJobQueueComponent { Name = "Jobs" };
        root.AddChild(jobs);

        var roads = new GridRoadComponent { Name = "Roads" };
        root.AddChild(roads);

        var tools = new GridToolActionComponent
        {
            Name = "Tools",
            CellDataPath = new NodePath("../Cells"),
            JobQueuePath = new NodePath("../Jobs"),
            RoadPath = new NodePath("../Roads"),
            CropId = " ",
            CropDaysToMature = -10,
            JobKind = " ",
            JobWorkSeconds = float.NaN,
            RoadKind = " ",
            RoadCostMultiplier = float.NaN
        };
        root.AddChild(tools);

        Vector2I cell = new(1, 1);
        bool hoed = tools.ApplyToCell(cell, GridToolActionComponent.ToolAction.Hoe);
        bool planted = tools.ApplyToCell(cell, GridToolActionComponent.ToolAction.Plant);
        bool roaded = tools.ApplyToCell(cell, GridToolActionComponent.ToolAction.Road);
        bool queued = tools.ApplyToCell(cell, GridToolActionComponent.ToolAction.QueueJob);
        string jobId = jobs.GetJobs().Count > 0 ? jobs.GetJobs()[0]["id"].AsString() : "";
        bool bounded = hoed
            && planted
            && cells.GetCropId(cell) == "crop"
            && roaded
            && roads.GetRoadKind(cell) == "dirt_path"
            && Mathf.IsEqualApprox(roads.GetTraversalCostMultiplier(cell), 0.55f)
            && queued
            && !string.IsNullOrEmpty(jobId)
            && jobs.GetJobKind(jobId) == "work"
            && float.IsFinite(jobs.GetJobWorkSeconds(jobId))
            && jobs.GetJobWorkSeconds(jobId) >= 0.01f
            && tools.EffectiveCropDaysToMature == 0;
        root.QueueFree();

        if (!Expect(bounded, "GridToolAction did not bound invalid crop, road, or job tuning."))
            return false;

        return true;
    }

    private bool VerifyGridLooseArrayInputs()
    {
        var root = new Node { Name = "GridLooseArrayInputsSmokeRoot" };
        AddChild(root);

        var grid = new GridProjectionComponent
        {
            Name = "Grid",
            Projection = GridProjectionComponent.GridProjection.TopDown,
            TileSize = new Vector2(10, 10)
        };
        root.AddChild(grid);

        var body = new Node2D { Name = "Body", GlobalPosition = grid.CellToWorld(Vector2I.Zero) };
        root.AddChild(body);
        var follower = new GridPathFollowerComponent
        {
            Name = "Follower",
            GridPath = new NodePath("../../Grid"),
            Speed = 100f,
            StopDistance = 0.05f,
            DriveCharacterBody = false,
            SetZIndexFromY = false
        };
        body.AddChild(follower);

        var pathCells = new Godot.Collections.Array
        {
            new Vector2I(1, 0),
            new Godot.Collections.Dictionary { ["x"] = 2, ["y"] = 0 },
            new Godot.Collections.Dictionary { ["cell"] = new Vector2(3f, 0f) },
            "bad"
        };
        bool pathStarted = follower.SetCellPath(pathCells);
        for (int i = 0; i < 20 && follower.IsMoving; i++)
            follower.AdvancePath(0.05);

        bool pathOk = pathStarted
            && !follower.IsMoving
            && follower.DestinationCell == new Vector2I(3, 0)
            && body.GlobalPosition.DistanceTo(grid.CellToWorld(new Vector2I(3, 0))) <= 0.1f;

        var queue = new GridJobQueueComponent
        {
            Name = "Jobs",
            UniqueCellKind = true,
            RemoveCompletedJobs = false
        };
        root.AddChild(queue);

        var command = new GridSelectionJobCommandComponent
        {
            Name = "Command",
            JobQueuePath = new NodePath("../Jobs"),
            JobKind = "clear_land",
            WorkSeconds = 0.1f
        };
        root.AddChild(command);

        var jobCells = new Godot.Collections.Array
        {
            new Vector2I(4, 0),
            new Godot.Collections.Dictionary { ["cell"] = new Vector2I(5, 0) },
            new Resource(),
            "bad"
        };
        int queued = command.QueueCells(jobCells);
        bool queueOk = queued == 2 && queue.QueuedCount == 2;

        var cellData = new GridCellDataComponent { Name = "Cells" };
        root.AddChild(cellData);
        var tools = new GridToolActionComponent
        {
            Name = "Tools",
            CellDataPath = new NodePath("../Cells")
        };
        root.AddChild(tools);

        var toolCells = new Godot.Collections.Array
        {
            new Vector2I(6, 0),
            new Godot.Collections.Dictionary { ["x"] = 7, ["y"] = 0 },
            "bad"
        };
        int applied = tools.ApplyToCells(toolCells, GridToolActionComponent.ToolAction.Hoe);
        bool toolsOk = applied == 2
            && cellData.HasFlag(new Vector2I(6, 0), GridCellDataComponent.CellFlags.Tilled)
            && cellData.HasFlag(new Vector2I(7, 0), GridCellDataComponent.CellFlags.Tilled);

        root.QueueFree();

        if (!Expect(pathOk, "GridPathFollower did not accept loose authored cell arrays without typed-array casts."))
            return false;

        if (!Expect(queueOk, $"GridSelectionJobCommand queued {queued} loose-array jobs with queue count {queue.QueuedCount}, expected 2."))
            return false;

        if (!Expect(toolsOk, $"GridToolAction applied {applied} loose-array cells, expected 2."))
            return false;

        return true;
    }

    private bool VerifyGridMalformedStateInputs()
    {
        var root = new Node { Name = "GridMalformedStateSmokeRoot" };
        AddChild(root);

        var calendar = new GridCalendarComponent { Name = "Calendar", DaysPerSeason = 28 };
        root.AddChild(calendar);
        calendar.RestoreState(new Godot.Collections.Dictionary
        {
            ["year"] = "2",
            ["season"] = "3",
            ["day_of_season"] = "99",
            ["absolute_day"] = new Resource(),
            ["day_clock"] = "12.5",
            ["days_per_season"] = "14"
        });
        bool calendarOk = calendar.Year == 2
            && calendar.Season == GridCalendarComponent.GridSeason.Winter
            && calendar.DayOfSeason == 14
            && Mathf.IsEqualApprox(calendar.DayProgress, 12.5f / calendar.EffectiveSecondsPerDay);

        var wallet = new GridResourceWalletComponent { Name = "Wallet", ApplyStartingResourcesOnReady = false };
        root.AddChild(wallet);
        wallet.RestoreState(new Godot.Collections.Dictionary
        {
            ["wood"] = "5",
            ["stone"] = new Resource(),
            ["oil"] = double.NaN,
            ["food"] = -2
        });
        bool walletOk = wallet.GetAmount("wood") == 5
            && wallet.GetAmount("stone") == 0
            && wallet.GetAmount("oil") == 0
            && wallet.GetAmount("food") == 0;

        var node = new GridResourceNodeComponent
        {
            Name = "ResourceNode",
            Cell = Vector2I.Zero,
            Amount = 8,
            AmountPerGather = 2
        };
        root.AddChild(node);
        node.RestoreState(new Godot.Collections.Dictionary
        {
            ["cell"] = new Godot.Collections.Dictionary { ["x"] = "2", ["y"] = "3" },
            ["resource_id"] = "wood",
            ["amount"] = "4",
            ["amount_per_gather"] = new Resource(),
            ["depleted"] = new Resource()
        });
        bool nodeOk = node.UseExplicitCell
            && node.Cell == new Vector2I(2, 3)
            && node.Amount == 4
            && node.AmountPerGather == 2
            && !node.IsDepleted;

        var gridObject = new GridObjectComponent
        {
            Name = "Object",
            Cell = Vector2I.Zero,
            Footprint = Vector2I.One,
            BlocksNavigation = true,
            Selectable = true,
            Complete = true
        };
        root.AddChild(gridObject);
        gridObject.RestoreState(new Godot.Collections.Dictionary
        {
            ["object_id"] = "pump",
            ["cell"] = new Godot.Collections.Dictionary { ["x"] = "4", ["y"] = "5" },
            ["footprint"] = new Resource(),
            ["blocks_navigation"] = "false",
            ["selectable"] = new Resource(),
            ["complete"] = "true",
            ["metadata"] = new Resource()
        });
        bool objectOk = gridObject.Cell == new Vector2I(4, 5)
            && gridObject.Footprint == Vector2I.One
            && !gridObject.BlocksNavigation
            && gridObject.Selectable
            && gridObject.Complete;

        var cellData = new GridCellDataComponent { Name = "Cells" };
        root.AddChild(cellData);
        cellData.LoadCells(new Godot.Collections.Array
        {
            new Godot.Collections.Dictionary
            {
                ["cell"] = new Godot.Collections.Dictionary { ["x"] = "6", ["y"] = "7" },
                ["terrain"] = "sand",
                ["flags"] = "6",
                ["crop_age_days"] = new Resource(),
                ["metadata"] = new Resource()
            },
            new Resource()
        });
        bool cellsOk = cellData.CellCount == 1
            && cellData.GetFlags(new Vector2I(6, 7)) == 6
            && cellData.GetCell(new Vector2I(6, 7))["crop_age_days"].AsInt32() == 0;

        var jobs = new GridJobQueueComponent { Name = "Jobs", RemoveCompletedJobs = false };
        root.AddChild(jobs);
        jobs.LoadJobs(new Godot.Collections.Array
        {
            new Godot.Collections.Dictionary
            {
                ["id"] = "job_1",
                ["kind"] = "clear_land",
                ["cell"] = new Godot.Collections.Dictionary { ["x"] = "8", ["y"] = "9" },
                ["priority"] = "2",
                ["work_seconds"] = "3.5",
                ["state"] = "claimed",
                ["claimed_by"] = "worker"
            },
            new Godot.Collections.Dictionary
            {
                ["id"] = "bad_job",
                ["cell"] = new Resource()
            },
            "bad"
        });
        bool jobsOk = jobs.GetJobCell("job_1") == new Vector2I(8, 9)
            && jobs.GetJobState("job_1") == GridJobQueueComponent.GridJobState.Claimed
            && Mathf.IsEqualApprox(jobs.GetJobWorkSeconds("job_1"), 3.5f)
            && jobs.ClaimedCount == 1
            && !jobs.HasJob("bad_job");

        var roads = new GridRoadComponent { Name = "Roads", DefaultRoadKind = "dirt" };
        root.AddChild(roads);
        roads.LoadRoads(new Godot.Collections.Array
        {
            new Godot.Collections.Dictionary
            {
                ["cell"] = new Godot.Collections.Dictionary { ["x"] = "10", ["y"] = "11" },
                ["kind"] = "stone",
                ["cost_multiplier"] = "0.25"
            },
            new Vector2(12f, 13f),
            new Resource()
        });
        bool roadsOk = roads.RoadCount == 2
            && roads.HasRoad(new Vector2I(10, 11))
            && roads.GetRoadKind(new Vector2I(10, 11)) == "stone"
            && roads.HasRoad(new Vector2I(12, 13));

        var tracker = new GridObjectiveTrackerComponent { Name = "Objectives" };
        tracker.Objectives.Add(new GridObjectiveDefinition
        {
            ObjectiveId = "clear_land",
            TargetCount = 5,
            ActiveOnStart = false
        });
        root.AddChild(tracker);
        tracker.RestoreState(new Godot.Collections.Dictionary
        {
            ["objectives"] = new Godot.Collections.Array
            {
                new Godot.Collections.Dictionary
                {
                    ["objective_id"] = "clear_land",
                    ["progress"] = "3",
                    ["active"] = "true",
                    ["completed"] = new Resource()
                },
                new Resource()
            }
        });
        bool objectivesOk = tracker.GetProgress("clear_land") == 3
            && tracker.IsActive("clear_land")
            && !tracker.IsComplete("clear_land");

        bool definitionsOk = GridBuildDefinition.TryRead(new Godot.Collections.Dictionary
        {
            ["build_id"] = "well",
            ["footprint"] = new Godot.Collections.Dictionary { ["x"] = "2", ["y"] = "2" },
            ["build_seconds"] = "3.25",
            ["blocks_navigation"] = "false",
            ["costs"] = new Godot.Collections.Array
            {
                new Godot.Collections.Dictionary { ["resource_id"] = "wood", ["amount"] = "2" },
                new Godot.Collections.Dictionary { ["resource_id"] = "stone", ["amount"] = double.NaN },
                new Resource()
            }
        }, out GridBuildDefinition? build)
            && build != null
            && build.BuildId == "well"
            && build.Footprint == new Vector2I(2, 2)
            && Mathf.IsEqualApprox(build.BuildSeconds, 3.25f)
            && !build.BlocksNavigation
            && GridResourceAmount.TryRead(new Godot.Collections.Dictionary { ["resource_id"] = "wood", ["amount"] = "2" }, out _, out int amount)
            && amount == 2
            && GridCropDefinition.TryRead(new Godot.Collections.Dictionary { ["crop_id"] = "bean", ["days_to_mature"] = "4", ["summer"] = "true" }, out GridCropDefinition? crop)
            && crop != null
            && crop.DaysToMature == 4
            && crop.Summer
            && GridObjectiveDefinition.TryRead(new Godot.Collections.Dictionary { ["objective_id"] = "build_base", ["target_count"] = "2", ["active_on_start"] = "false" }, out GridObjectiveDefinition? objective)
            && objective != null
            && objective.TargetCount == 2
            && !objective.ActiveOnStart
            && GridProductionRecipe.TryRead(new Godot.Collections.Dictionary { ["recipe_id"] = "planks", ["duration_seconds"] = "2.5" }, out GridProductionRecipe? recipe)
            && recipe != null
            && Mathf.IsEqualApprox(recipe.DurationSeconds, 2.5f);

        root.QueueFree();

        if (!Expect(calendarOk, "GridCalendar did not ignore malformed grid saved state values."))
            return false;

        if (!Expect(walletOk, "GridResourceWallet did not ignore malformed grid saved state values."))
            return false;

        if (!Expect(nodeOk, "GridResourceNode did not ignore malformed grid saved state values."))
            return false;

        if (!Expect(objectOk, "GridObject did not ignore malformed grid saved state values."))
            return false;

        if (!Expect(cellsOk, "GridCellData did not ignore malformed grid saved state values."))
            return false;

        if (!Expect(jobsOk, "GridJobQueue did not ignore malformed grid saved state values."))
            return false;

        if (!Expect(roadsOk, "GridRoad did not ignore malformed grid saved state values."))
            return false;

        if (!Expect(objectivesOk, "GridObjectiveTracker did not ignore malformed grid saved state values."))
            return false;

        if (!Expect(definitionsOk, "Grid definition resources did not ignore malformed grid authored data values."))
            return false;

        return true;
    }

    private bool VerifyGridToolPalette()
    {
        var root = new Control { Name = "GridToolPaletteSmokeRoot" };
        AddChild(root);

        var cells = new GridCellDataComponent { Name = "Cells" };
        root.AddChild(cells);

        var selection = new GridSelectionComponent { Name = "Selection", UseMouseInput = false };
        selection.SelectCell(new Vector2I(3, 3));
        root.AddChild(selection);

        var tools = new GridToolActionComponent
        {
            Name = "Tools",
            CellDataPath = new NodePath("../Cells"),
            SelectionPath = new NodePath("../Selection"),
            ApplyToSelectionWhenPresent = true,
            CurrentAction = GridToolActionComponent.ToolAction.Hoe
        };
        root.AddChild(tools);

        var interaction = new GridInteractionModeComponent
        {
            Name = "InteractionMode",
            UseMouseInput = false,
            ManageChildMouseInput = false
        };
        root.AddChild(interaction);

        var palette = new GridToolPaletteComponent
        {
            Name = "ToolPalette",
            ToolActionPath = new NodePath("../Tools"),
            InteractionModePath = new NodePath("../InteractionMode"),
            BuildInEditor = false,
            GenerateControlsWhenPathsEmpty = true,
            IncludeApplyButton = true
        };
        root.AddChild(palette);
        palette.RebuildPalette();

        bool countOk = palette.VisibleToolButtonCount() == 8;
        bool selected = palette.SelectTool(GridToolActionComponent.ToolAction.Hoe)
            && palette.SelectedActionName() == "Hoe"
            && interaction.CurrentMode == GridInteractionModeComponent.InteractionMode.Tool;
        int applied = palette.ApplySelectedTool();
        bool appliedOk = applied == 1
            && cells.HasFlag(new Vector2I(3, 3), GridCellDataComponent.CellFlags.Tilled);

        palette.SelectTool(GridToolActionComponent.ToolAction.Water);
        int watered = palette.ApplySelectedTool();
        bool switchedOk = palette.SelectedActionName() == "Water"
            && watered == 1
            && cells.HasFlag(new Vector2I(3, 3), GridCellDataComponent.CellFlags.Watered);
        root.QueueFree();

        if (!Expect(countOk, "GridToolPalette did not build the expected tool buttons."))
            return false;

        if (!Expect(selected, "GridToolPalette did not select the requested tool."))
            return false;

        if (!Expect(appliedOk, "GridToolPalette did not apply the selected hoe tool."))
            return false;

        if (!Expect(switchedOk, "GridToolPalette did not switch and apply the water tool."))
            return false;

        if (!VerifyGridToolPaletteSceneButtons())
            return false;

        return true;
    }

    private bool VerifyGridToolPaletteSceneButtons()
    {
        var root = new Control { Name = "GridToolPaletteSceneButtonsSmokeRoot" };
        AddChild(root);

        var cells = new GridCellDataComponent { Name = "Cells" };
        root.AddChild(cells);

        var selection = new GridSelectionComponent { Name = "Selection", UseMouseInput = false };
        selection.SelectCell(new Vector2I(3, 3));
        root.AddChild(selection);

        var tools = new GridToolActionComponent
        {
            Name = "Tools",
            CellDataPath = new NodePath("../Cells"),
            SelectionPath = new NodePath("../Selection"),
            ApplyToSelectionWhenPresent = true
        };
        root.AddChild(tools);

        var interaction = new GridInteractionModeComponent
        {
            Name = "InteractionMode",
            UseMouseInput = false,
            ManageChildMouseInput = false
        };
        root.AddChild(interaction);

        var palette = new GridToolPaletteComponent
        {
            Name = "ToolPalette",
            ToolActionPath = new NodePath("../Tools"),
            InteractionModePath = new NodePath("../InteractionMode"),
            BuildInEditor = false,
            GenerateControlsWhenPathsEmpty = false,
            BoundActionNames = new[] { "Hoe", "Water", "Road" },
            BoundButtonPaths = new[] { new NodePath("Row/Hoe"), new NodePath("Row/Water"), new NodePath("Row/Road") }
        };
        root.AddChild(palette);

        var row = new VBoxContainer { Name = "Row" };
        palette.AddChild(row);
        var hoe = new Button { Name = "Hoe", Text = "Hoe" };
        var water = new Button { Name = "Water", Text = "Water" };
        var road = new Button { Name = "Road", Text = "Road" };
        row.AddChild(hoe);
        row.AddChild(water);
        row.AddChild(road);

        palette.RebuildPalette();
        bool bound = palette.UsesSceneButtons()
            && palette.VisibleToolButtonCount() == 3
            && hoe.ToggleMode
            && water.ToggleMode
            && road.ToggleMode;

        hoe.EmitSignal(Button.SignalName.Pressed);
        bool selected = tools.CurrentAction == GridToolActionComponent.ToolAction.Hoe
            && interaction.CurrentMode == GridInteractionModeComponent.InteractionMode.Tool
            && hoe.ButtonPressed;

        water.EmitSignal(Button.SignalName.Pressed);
        bool switched = tools.CurrentAction == GridToolActionComponent.ToolAction.Water
            && !hoe.ButtonPressed
            && water.ButtonPressed;

        root.QueueFree();

        if (!Expect(bound, "GridToolPalette did not bind scene-authored tool buttons."))
            return false;

        if (!Expect(selected, "GridToolPalette scene-authored button did not select a tool."))
            return false;

        if (!Expect(switched, "GridToolPalette did not refresh scene-authored button selection state."))
            return false;

        return true;
    }

    private bool VerifyGridMinimap()
    {
        var root = new Control { Name = "GridMinimapSmokeRoot" };
        AddChild(root);

        var grid = new GridProjectionComponent
        {
            Name = "Grid",
            Projection = GridProjectionComponent.GridProjection.TopDown,
            TileSize = new Vector2(16, 16)
        };
        root.AddChild(grid);

        var navigation = new GridNavigationComponent
        {
            Name = "Navigation",
            BoundsOrigin = new Vector2I(0, 0),
            BoundsSize = new Vector2I(10, 10)
        };
        root.AddChild(navigation);

        var roads = new GridRoadComponent
        {
            Name = "Roads",
            GridPath = new NodePath("../Grid")
        };
        roads.SetRoad(new Vector2I(2, 2), true);
        root.AddChild(roads);

        var selection = new GridSelectionComponent
        {
            Name = "Selection",
            UseMouseInput = false
        };
        selection.SelectCell(new Vector2I(3, 3));
        root.AddChild(selection);

        var jobs = new GridJobQueueComponent { Name = "Jobs" };
        jobs.AddJob(new Vector2I(4, 4), "clear_land");
        root.AddChild(jobs);

        var units = new Node2D { Name = "Units" };
        var unit = new Node2D { Name = "Worker", GlobalPosition = grid.CellToWorld(new Vector2I(5, 5)) };
        units.AddChild(unit);
        root.AddChild(units);

        var minimap = new GridMinimapComponent
        {
            Name = "Minimap",
            GridPath = new NodePath("../Grid"),
            NavigationPath = new NodePath("../Navigation"),
            RoadPath = new NodePath("../Roads"),
            SelectionPath = new NodePath("../Selection"),
            JobQueuePath = new NodePath("../Jobs"),
            UnitsRootPath = new NodePath("../Units"),
            BoundsSize = new Vector2I(10, 10),
            CustomMinimumSize = new Vector2(100, 100),
            AutoRefresh = false
        };
        root.AddChild(minimap);
        minimap.RebuildMinimap();

        Vector2 center = minimap.CellToMinimap(new Vector2I(5, 5));
        bool counts = minimap.VisibleRoadCount() == 1
            && minimap.VisibleJobCount() == 1
            && minimap.VisibleUnitCount() == 1;
        bool mapped = center.X > 50f && center.Y > 50f;
        root.QueueFree();

        if (!Expect(counts, "GridMinimap did not resolve roads, jobs, and units from exported paths."))
            return false;

        if (!Expect(mapped, "GridMinimap did not map grid cells into minimap coordinates."))
            return false;

        return true;
    }

    private bool VerifyGridCropCatalog()
    {
        var root = new Node { Name = "GridCropCatalogSmokeRoot" };
        AddChild(root);

        var cells = new GridCellDataComponent { Name = "Cells" };
        cells.Till(new Vector2I(1, 1));
        cells.Till(new Vector2I(2, 1));
        root.AddChild(cells);

        var calendar = new GridCalendarComponent
        {
            Name = "Calendar",
            CellDataPath = new NodePath("../Cells")
        };
        calendar.SetDate(1, GridCalendarComponent.GridSeason.Spring, 1);
        root.AddChild(calendar);

        var catalog = new GridCropCatalogComponent { Name = "Crops", AllowUnknownCrops = false };
        catalog.Crops.Add(new GridCropDefinition
        {
            CropId = "strawberry",
            DisplayName = "Strawberry",
            DaysToMature = 6,
            Spring = true,
            Summer = false,
            YieldItemId = "strawberry",
            YieldCount = 2
        });
        catalog.Crops.Add(new GridCropDefinition
        {
            CropId = "melon",
            DisplayName = "Melon",
            DaysToMature = 8,
            Spring = false,
            Summer = true
        });
        catalog.Crops.Add(new Godot.Collections.Dictionary
        {
            ["crop_id"] = "wildflower",
            ["display_name"] = "Wildflower",
            ["days_to_mature"] = -2,
            ["yield_item_id"] = "wildflower",
            ["yield_count"] = -3,
            ["spring"] = true
        });
        root.AddChild(catalog);

        var wallet = new GridResourceWalletComponent
        {
            Name = "Resources",
            ApplyStartingResourcesOnReady = false
        };
        root.AddChild(wallet);

        var tools = new GridToolActionComponent
        {
            Name = "Tools",
            CellDataPath = new NodePath("../Cells"),
            CropCatalogPath = new NodePath("../Crops"),
            CalendarPath = new NodePath("../Calendar"),
            ResourceWalletPath = new NodePath("../Resources"),
            CropDaysToMature = 99
        };
        root.AddChild(tools);

        tools.CropId = "melon";
        bool rejectedWrongSeason = !tools.ApplyToCell(new Vector2I(1, 1), GridToolActionComponent.ToolAction.Plant);

        tools.CropId = "strawberry";
        bool planted = tools.ApplyToCell(new Vector2I(1, 1), GridToolActionComponent.ToolAction.Plant);
        bool catalogDaysUsed = cells.GetCell(new Vector2I(1, 1))["crop_days_to_mature"].AsInt32() == 6;
        for (int i = 0; i < 6; i++)
            cells.AdvanceDay();
        bool harvestedWithYield = tools.ApplyToCell(new Vector2I(1, 1), GridToolActionComponent.ToolAction.Harvest)
            && wallet.GetAmount("strawberry") == 2;

        var springCrops = catalog.CropIdsForSeason(GridCalendarComponent.GridSeason.Spring);
        bool cropLookupOk = springCrops.Count == 2
            && springCrops.Contains("strawberry")
            && springCrops.Contains("wildflower")
            && catalog.YieldItem("strawberry") == "strawberry"
            && catalog.YieldCount("strawberry") == 2;
        bool dictionaryCropOk = catalog.CanPlant("wildflower", GridCalendarComponent.GridSeason.Spring)
            && catalog.DaysToMature("wildflower", 99) == 0
            && catalog.YieldCount("wildflower") == 1;
        root.QueueFree();

        if (!Expect(rejectedWrongSeason, "GridCropCatalog did not reject a crop in the wrong season."))
            return false;

        if (!Expect(planted, "GridCropCatalog did not allow a crop in the correct season."))
            return false;

        if (!Expect(catalogDaysUsed, "GridToolAction did not use catalog maturity days."))
            return false;

        if (!Expect(harvestedWithYield, "GridToolAction did not add catalog harvest yield to the resource wallet."))
            return false;

        if (!Expect(cropLookupOk, "GridCropCatalog seasonal lookup/yield data failed."))
            return false;

        if (!Expect(dictionaryCropOk, "GridCropCatalog did not read dictionary-authored crop definitions safely."))
            return false;

        return true;
    }

    private bool VerifyGridCellOverlay()
    {
        var root = new Node { Name = "GridCellOverlaySmokeRoot" };
        AddChild(root);

        var grid = new GridProjectionComponent
        {
            Name = "Grid",
            TileSize = new Vector2(16, 16)
        };
        root.AddChild(grid);

        var cells = new GridCellDataComponent { Name = "Cells" };
        cells.Till(new Vector2I(1, 1));
        cells.Water(new Vector2I(1, 1));
        cells.Till(new Vector2I(2, 1));
        cells.PlantCrop(new Vector2I(2, 1), "turnip", 0);
        var colorProbe = new GridCellOverlayComponent();
        bool wateredColor = colorProbe.ColorForFlags(cells.GetFlags(new Vector2I(1, 1))).IsEqualApprox(colorProbe.WateredColor);
        cells.AdvanceDay();
        root.AddChild(cells);

        var overlay = new GridCellOverlayComponent
        {
            Name = "CellOverlay",
            GridPath = new NodePath("../Grid"),
            CellDataPath = new NodePath("../Cells")
        };
        root.AddChild(overlay);

        bool countOk = overlay.VisibleCellCount() == 2;
        bool readyColor = overlay.ColorForCell(new Vector2I(2, 1)).IsEqualApprox(overlay.HarvestReadyColor);
        root.QueueFree();

        if (!Expect(countOk, "GridCellOverlay did not count visible cell states."))
            return false;

        if (!Expect(wateredColor, "GridCellOverlay did not prefer watered color over tilled color."))
            return false;

        if (!Expect(readyColor, "GridCellOverlay did not prefer harvest-ready color over planted color."))
            return false;

        return true;
    }

    private bool VerifyGridVisualHelpersBoundInvalidTuning()
    {
        var root = new Node { Name = "GridVisualBoundsSmokeRoot" };
        AddChild(root);

        var navigation = new GridNavigationComponent
        {
            Name = "Navigation",
            UseBounds = true,
            BoundsSize = new Vector2I(0, -4)
        };
        root.AddChild(navigation);

        var minimap = new GridMinimapComponent
        {
            Name = "Minimap",
            NavigationPath = new NodePath("../Navigation"),
            PreferNavigationBounds = true,
            BoundsSize = new Vector2I(0, 0),
            CustomMinimumSize = new Vector2(float.NaN, -10f),
            AutoRefresh = false
        };
        root.AddChild(minimap);
        minimap.RebuildMinimap();
        Vector2 point = minimap.CellToMinimap(Vector2I.Zero);

        var overlay = new GridCellOverlayComponent
        {
            OutlineWidth = float.NaN
        };

        bool bounded = float.IsFinite(point.X)
            && float.IsFinite(point.Y)
            && minimap.EffectiveBoundsSize().X == 1
            && minimap.EffectiveBoundsSize().Y == 1
            && float.IsFinite(overlay.EffectiveOutlineWidth)
            && overlay.EffectiveOutlineWidth >= 0f;
        root.QueueFree();

        if (!Expect(bounded, $"Grid visual helpers did not bound invalid minimap/overlay sizing. point={point}, bounds={minimap.EffectiveBoundsSize()}."))
            return false;

        return true;
    }

    private bool VerifyGridTileMapLayerBridge()
    {
        var root = new Node { Name = "GridTileMapLayerBridgeSmokeRoot" };
        AddChild(root);

        var layer = new TileMapLayer { Name = "VisualTileLayer" };
        root.AddChild(layer);

        var cells = new GridCellDataComponent { Name = "Cells" };
        root.AddChild(cells);
        int cellsChanged = 0;
        cells.CellsChanged += () => cellsChanged++;

        var roads = new GridRoadComponent { Name = "Roads" };
        root.AddChild(roads);
        int roadsChanged = 0;
        roads.RoadsChanged += () => roadsChanged++;

        var bridge = new GridTileMapLayerBridgeComponent
        {
            Name = "TileMapBridge",
            TileMapLayerPath = new NodePath("../VisualTileLayer"),
            CellDataPath = new NodePath("../Cells"),
            RoadPath = new NodePath("../Roads"),
            ClearedAtlas = new Vector2I(1, 0),
            TilledAtlas = new Vector2I(2, 0),
            RoadAtlas = new Vector2I(7, 0)
        };
        root.AddChild(bridge);

        Vector2I cell = new(2, 2);
        cells.ClearLand(cell);
        bool clearedAtlas = bridge.AtlasForCell(cell) == new Vector2I(1, 0);
        cells.Till(cell);
        bool tilledAtlas = bridge.AtlasForCell(cell) == new Vector2I(2, 0);
        roads.SetRoad(cell, "dirt_path", 0.55f);
        bool roadAtlas = bridge.AtlasForCell(cell) == new Vector2I(7, 0);
        bridge.Rebuild();
        bool noTileSetNoise = bridge.PaintedCellCount() == 0;

        var loadedCells = new Godot.Collections.Array
        {
            new Godot.Collections.Dictionary
            {
                ["cell"] = new Vector2I(6, 6),
                ["terrain"] = "sand",
                ["flags"] = 0
            }
        };
        cells.LoadCells(loadedCells);
        cells.ClearCells();

        var loadedRoads = new Godot.Collections.Array
        {
            new Godot.Collections.Dictionary
            {
                ["cell"] = new Vector2I(7, 7),
                ["kind"] = "stone_path",
                ["cost_multiplier"] = 0.4f
            }
        };
        roads.LoadRoads(loadedRoads);
        roads.ClearRoads();
        bool bulkSignalsOk = cellsChanged >= 2 && roadsChanged >= 2;

        root.QueueFree();

        if (!Expect(clearedAtlas && tilledAtlas && roadAtlas, "GridTileMapLayerBridge did not choose atlas coordinates from cell and road state."))
            return false;

        if (!Expect(noTileSetNoise, "GridTileMapLayerBridge should not paint cells before a TileSet is assigned."))
            return false;

        if (!Expect(bulkSignalsOk, "Grid cell/road bulk load and clear operations must emit whole-state change signals for visual bridges."))
            return false;

        return true;
    }

    private bool VerifyGridCalendar()
    {
        var root = new Node { Name = "GridCalendarSmokeRoot" };
        AddChild(root);

        var cells = new GridCellDataComponent { Name = "Cells" };
        cells.Till(new Vector2I(1, 1));
        cells.PlantCrop(new Vector2I(1, 1), "radish", 2);
        root.AddChild(cells);

        var calendar = new GridCalendarComponent
        {
            Name = "Calendar",
            CellDataPath = new NodePath("../Cells"),
            DaysPerSeason = 2
        };
        root.AddChild(calendar);

        calendar.AdvanceDay();
        bool firstDay = calendar.DayOfSeason == 2
            && calendar.Season == GridCalendarComponent.GridSeason.Spring
            && !cells.HasFlag(new Vector2I(1, 1), GridCellDataComponent.CellFlags.HarvestReady);

        calendar.AdvanceDay();
        bool seasonRolled = calendar.DayOfSeason == 1
            && calendar.Season == GridCalendarComponent.GridSeason.Summer
            && calendar.Year == 1
            && cells.HasFlag(new Vector2I(1, 1), GridCellDataComponent.CellFlags.HarvestReady);

        calendar.SetDate(1, GridCalendarComponent.GridSeason.Winter, 2);
        calendar.AdvanceDay();
        bool yearRolled = calendar.Year == 2
            && calendar.Season == GridCalendarComponent.GridSeason.Spring
            && calendar.DayOfSeason == 1;

        var snapshot = calendar.CaptureState();
        calendar.SetDate(5, GridCalendarComponent.GridSeason.Fall, 2);
        calendar.RestoreState(snapshot);
        bool restored = calendar.Year == 2
            && calendar.Season == GridCalendarComponent.GridSeason.Spring
            && calendar.DayOfSeason == 1;

        calendar.SecondsPerDay = float.NaN;
        calendar.DaysPerSeason = -5;
        calendar.RestoreState(new Godot.Collections.Dictionary
        {
            ["year"] = 0,
            ["season"] = 99,
            ["day_of_season"] = 500,
            ["day_clock"] = double.NaN,
            ["days_per_season"] = -12
        });
        calendar.AutoAdvance = true;
        calendar._Process(double.NaN);
        bool invalidStateBounded = calendar.EffectiveSecondsPerDay > 0f
            && calendar.EffectiveDaysPerSeason == 1
            && calendar.Year == 1
            && calendar.Season == GridCalendarComponent.GridSeason.Winter
            && calendar.DayOfSeason == 1
            && Mathf.IsZeroApprox(calendar.DayProgress);
        root.QueueFree();

        if (!Expect(firstDay, "GridCalendar did not advance the first day without early crop maturity."))
            return false;

        if (!Expect(seasonRolled, "GridCalendar did not roll season and mature crops after enough days."))
            return false;

        if (!Expect(yearRolled, "GridCalendar did not roll winter into a new spring year."))
            return false;

        if (!Expect(restored, "GridCalendar did not restore captured date state."))
            return false;

        if (!Expect(invalidStateBounded, "GridCalendar did not bound invalid saved/configured time values."))
            return false;

        return true;
    }

    private bool VerifyGridCalendarHud()
    {
        var root = new Control { Name = "GridCalendarHudSmokeRoot" };
        AddChild(root);

        var calendar = new GridCalendarComponent
        {
            Name = "Calendar",
            DaysPerSeason = 2,
            SecondsPerDay = 10f
        };
        calendar.SetDate(1, GridCalendarComponent.GridSeason.Spring, 1);
        root.AddChild(calendar);

        var hud = new GridCalendarHudComponent
        {
            Name = "CalendarHud",
            CalendarPath = new NodePath("../Calendar"),
            BuildInEditor = false,
            GenerateControlsWhenPathsEmpty = true,
            ShowProgress = true,
            ShowAdvanceButton = true
        };
        root.AddChild(hud);
        hud.RebuildHud();

        bool initial = hud.DateText() == "Year 1, Spring 1"
            && Mathf.IsZeroApprox(hud.DayProgress01());
        bool advanced = hud.RequestAdvanceDay()
            && hud.DateText() == "Year 1, Spring 2";
        bool rolled = hud.RequestAdvanceDay()
            && hud.DateText() == "Year 1, Summer 1";
        root.QueueFree();

        if (!Expect(initial, "GridCalendarHud did not render the initial calendar date."))
            return false;

        if (!Expect(advanced, "GridCalendarHud did not advance the calendar by one day."))
            return false;

        if (!Expect(rolled, "GridCalendarHud did not refresh after season rollover."))
            return false;

        return true;
    }

    private bool VerifyGridWorldStateRoundTrip()
    {
        var root = new Node { Name = "GridWorldStateSmokeRoot" };
        AddChild(root);

        var cells = new GridCellDataComponent { Name = "Cells" };
        cells.Till(new Vector2I(1, 2));
        cells.PlantCrop(new Vector2I(1, 2), "bean", 1);
        root.AddChild(cells);

        var placement = new GridPlacementComponent { Name = "Placement" };
        placement.SetOccupied(new Vector2I(2, 3), true);
        root.AddChild(placement);

        var navigation = new GridNavigationComponent { Name = "Navigation" };
        navigation.SetBlocked(new Vector2I(4, 5), true);
        root.AddChild(navigation);

        var roads = new GridRoadComponent { Name = "Roads" };
        roads.SetRoad(new Vector2I(5, 5), "stone_path", 0.4f);
        root.AddChild(roads);

        var objectsRoot = new Node2D { Name = "Objects" };
        root.AddChild(objectsRoot);

        var depot = new Node2D { Name = "Depot" };
        objectsRoot.AddChild(depot);
        var gridObject = new GridObjectComponent
        {
            Name = "GridObject",
            ObjectId = "base_depot",
            DisplayName = "Base Depot",
            ObjectKind = "base",
            Description = "Starting hub.",
            Cell = new Vector2I(9, 1),
            Footprint = new Vector2I(2, 1),
            BlocksNavigation = true,
            PlacementPath = new NodePath("../../../Placement"),
            NavigationPath = new NodePath("../../../Navigation"),
            ReserveFootprintOnReady = true
        };
        depot.AddChild(gridObject);
        gridObject.ReserveFootprint();

        var selection = new GridSelectionComponent { Name = "Selection", UseMouseInput = false };
        selection.SelectCell(new Vector2I(6, 7));
        root.AddChild(selection);

        var jobs = new GridJobQueueComponent { Name = "Jobs", RemoveCompletedJobs = false };
        string jobId = jobs.AddJob(new Vector2I(8, 9), "repair", 0.75f, 2);
        root.AddChild(jobs);

        var state = new GridWorldStateComponent
        {
            Name = "State",
            PlacementPath = new NodePath("../Placement"),
            NavigationPath = new NodePath("../Navigation"),
            SelectionPath = new NodePath("../Selection"),
            JobQueuePath = new NodePath("../Jobs"),
            CellDataPath = new NodePath("../Cells"),
            RoadPath = new NodePath("../Roads"),
            ObjectsRootPath = new NodePath("../Objects")
        };
        root.AddChild(state);

        var snapshot = state.CaptureState();

        gridObject.SetCell(new Vector2I(11, 1));
        gridObject.Description = "Changed.";
        gridObject.SetMetadataValue("mode", "temporary");
        cells.ClearCells();
        placement.ClearOccupied();
        navigation.ClearBlocked();
        roads.ClearRoads();
        selection.ClearSelection();
        jobs.ClearJobs();

        state.RestoreState(snapshot);

        bool cellDataRestored = cells.GetCropId(new Vector2I(1, 2)) == "bean"
            && cells.HasFlag(new Vector2I(1, 2), GridCellDataComponent.CellFlags.Planted);
        bool occupiedRestored = placement.IsOccupied(new Vector2I(2, 3));
        bool blockedRestored = navigation.IsBlocked(new Vector2I(4, 5));
        bool roadRestored = roads.HasRoad(new Vector2I(5, 5))
            && roads.GetRoadKind(new Vector2I(5, 5)) == "stone_path";
        bool objectRestored = gridObject.Cell == new Vector2I(9, 1)
            && gridObject.Description == "Starting hub."
            && gridObject.EffectiveCategory == "base"
            && placement.IsOccupied(new Vector2I(9, 1))
            && placement.IsOccupied(new Vector2I(10, 1))
            && navigation.IsBlocked(new Vector2I(9, 1))
            && navigation.IsBlocked(new Vector2I(10, 1));
        bool selectionRestored = selection.IsSelected(new Vector2I(6, 7));
        bool jobRestored = jobs.HasJob(jobId)
            && jobs.GetJobCell(jobId) == new Vector2I(8, 9)
            && jobs.GetJobKind(jobId) == "repair";
        root.QueueFree();

        if (!Expect(cellDataRestored, "GridWorldState did not restore grid cell data."))
            return false;

        if (!Expect(occupiedRestored, "GridWorldState did not restore placement occupancy."))
            return false;

        if (!Expect(blockedRestored, "GridWorldState did not restore navigation blocked cells."))
            return false;

        if (!Expect(roadRestored, "GridWorldState did not restore road cells."))
            return false;

        if (!Expect(objectRestored, "GridWorldState did not restore grid object state and footprint reservations."))
            return false;

        if (!Expect(selectionRestored, "GridWorldState did not restore selected cells."))
            return false;

        if (!Expect(jobRestored, "GridWorldState did not restore queued jobs."))
            return false;

        return true;
    }

    private bool Expect(bool condition, string failure)
    {
        if (condition)
            return true;

        Failure = failure;
        return false;
    }
}

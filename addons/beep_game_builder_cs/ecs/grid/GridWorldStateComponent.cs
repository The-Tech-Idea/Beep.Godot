using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Captures and restores the reusable grid toolkit state: cell data, roads,
    /// placement occupancy, navigation blocked cells, grid objects, selection, and jobs.
    /// The snapshot is a Godot Dictionary, so it can be stored in GameStateData,
    /// JSON, config files, or custom save systems.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridWorldStateComponent : Node, ISaveable
    {
        [Signal] public delegate void StateCapturedEventHandler();
        [Signal] public delegate void StateRestoredEventHandler();

        [Export] public bool ParticipatesInSave { get; set; } = true;
        [Export] public string SaveKey { get; set; } = "grid_world.state";
        [Export] public NodePath PlacementPath { get; set; } = new("");
        [Export] public NodePath NavigationPath { get; set; } = new("");
        [Export] public NodePath SelectionPath { get; set; } = new("");
        [Export] public NodePath JobQueuePath { get; set; } = new("");
        [Export] public NodePath CellDataPath { get; set; } = new("");
        [Export] public NodePath RoadPath { get; set; } = new("");
        [Export] public NodePath ObjectsRootPath { get; set; } = new("");
        [Export] public bool CaptureCellData { get; set; } = true;
        [Export] public bool CapturePlacementOccupancy { get; set; } = true;
        [Export] public bool CaptureNavigationBlocks { get; set; } = true;
        [Export] public bool CaptureRoads { get; set; } = true;
        [Export] public bool CaptureGridObjects { get; set; } = true;
        [Export] public bool CaptureSelection { get; set; } = true;
        [Export] public bool CaptureJobs { get; set; } = true;

        private const int SnapshotVersion = 1;

        private GridPlacementComponent? _placement;
        private GridNavigationComponent? _navigation;
        private GridSelectionComponent? _selection;
        private GridJobQueueComponent? _jobs;
        private GridCellDataComponent? _cellData;
        private GridRoadComponent? _roads;
        private Node? _objectsRoot;

        public override void _Ready()
        {
            ResolveReferences();
            if (!Engine.IsEditorHint() && ParticipatesInSave)
                AddToGroup(SaveableHelper.Group);
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            if (ParticipatesInSave)
                RemoveFromGroup(SaveableHelper.Group);
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (string.IsNullOrWhiteSpace(SaveKey))
                return new[] { "SaveKey must not be empty." };
            return System.Array.Empty<string>();
        }

        public Godot.Collections.Dictionary CaptureState()
        {
            ResolveReferences();
            var state = new Godot.Collections.Dictionary
            {
                ["version"] = SnapshotVersion
            };

            if (CaptureCellData && _cellData != null)
                state["cell_data"] = _cellData.GetCells();

            if (CapturePlacementOccupancy && _placement != null)
                state["occupied_cells"] = _placement.GetOccupiedCells();

            if (CaptureNavigationBlocks && _navigation != null)
                state["blocked_cells"] = _navigation.GetBlockedCells();

            if (CaptureRoads && _roads != null)
                state["roads"] = _roads.GetRoads();

            if (CaptureGridObjects)
                state["grid_objects"] = CaptureGridObjectStates();

            if (CaptureSelection && _selection != null)
                state["selected_cells"] = _selection.GetSelectedCells();

            if (CaptureJobs && _jobs != null)
                state["jobs"] = _jobs.GetJobs();

            EmitSignal(SignalName.StateCaptured);
            return state;
        }

        public void RestoreState(Godot.Collections.Dictionary state)
        {
            ResolveReferences();

            if (CaptureCellData && _cellData != null)
                _cellData.LoadCells(ReadArray(state, "cell_data"));

            if (CaptureGridObjects)
                ReleaseGridObjectFootprints();

            if (CapturePlacementOccupancy && _placement != null)
            {
                _placement.ClearOccupied();
                foreach (Vector2I cell in ReadCells(state, "occupied_cells"))
                    _placement.SetOccupied(cell, true);
            }

            if (CaptureNavigationBlocks && _navigation != null)
            {
                _navigation.ClearBlocked();
                foreach (Vector2I cell in ReadCells(state, "blocked_cells"))
                    _navigation.SetBlocked(cell, true);
            }

            if (CaptureRoads && _roads != null)
                _roads.LoadRoads(ReadArray(state, "roads"));

            if (CaptureGridObjects)
                RestoreGridObjectStates(ReadArray(state, "grid_objects"));

            if (CaptureSelection && _selection != null)
            {
                _selection.ClearSelection();
                bool first = true;
                foreach (Vector2I cell in ReadCells(state, "selected_cells"))
                {
                    _selection.SelectCell(cell, additive: !first);
                    first = false;
                }
            }

            if (CaptureJobs && _jobs != null)
                _jobs.LoadJobs(ReadArray(state, "jobs"));

            EmitSignal(SignalName.StateRestored);
        }

        public void Save(GameBuilder.GameStateData state)
        {
            if (string.IsNullOrWhiteSpace(SaveKey))
                return;

            state.GameData[SaveKey] = CaptureState();
        }

        public void Load(GameBuilder.GameStateData state)
        {
            if (string.IsNullOrWhiteSpace(SaveKey))
                return;

            if (state.GameData.TryGetValue(SaveKey, out Variant value)
                && GridVariantReader.TryDictionary(value, out Godot.Collections.Dictionary saved))
                RestoreState(saved);
        }

        private void ResolveReferences()
        {
            if (_placement == null || !GodotObject.IsInstanceValid(_placement))
                _placement = !PlacementPath.IsEmpty
                    ? GetNodeOrNull<GridPlacementComponent>(PlacementPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridPlacementComponent>(GetTree()?.CurrentScene) : null;

            if (_navigation == null || !GodotObject.IsInstanceValid(_navigation))
                _navigation = !NavigationPath.IsEmpty
                    ? GetNodeOrNull<GridNavigationComponent>(NavigationPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridNavigationComponent>(GetTree()?.CurrentScene) : null;

            if (_selection == null || !GodotObject.IsInstanceValid(_selection))
                _selection = !SelectionPath.IsEmpty
                    ? GetNodeOrNull<GridSelectionComponent>(SelectionPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridSelectionComponent>(GetTree()?.CurrentScene) : null;

            if (_jobs == null || !GodotObject.IsInstanceValid(_jobs))
                _jobs = !JobQueuePath.IsEmpty
                    ? GetNodeOrNull<GridJobQueueComponent>(JobQueuePath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridJobQueueComponent>(GetTree()?.CurrentScene) : null;

            if (_cellData == null || !GodotObject.IsInstanceValid(_cellData))
                _cellData = !CellDataPath.IsEmpty
                    ? GetNodeOrNull<GridCellDataComponent>(CellDataPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridCellDataComponent>(GetTree()?.CurrentScene) : null;

            if (_roads == null || !GodotObject.IsInstanceValid(_roads))
                _roads = !RoadPath.IsEmpty
                    ? GetNodeOrNull<GridRoadComponent>(RoadPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridRoadComponent>(GetTree()?.CurrentScene) : null;

            if (_objectsRoot == null || !GodotObject.IsInstanceValid(_objectsRoot))
                _objectsRoot = !ObjectsRootPath.IsEmpty
                    ? GetNodeOrNull<Node>(ObjectsRootPath)
                    : IsInsideTree() ? GetTree()?.CurrentScene : null;
        }

        private static Godot.Collections.Array ReadArray(Godot.Collections.Dictionary state, string key)
            => GridVariantReader.Array(state, key);

        private Godot.Collections.Array<Godot.Collections.Dictionary> CaptureGridObjectStates()
        {
            var result = new Godot.Collections.Array<Godot.Collections.Dictionary>();
            foreach (GridObjectComponent gridObject in GridObjects())
            {
                var entry = new Godot.Collections.Dictionary
                {
                    ["path"] = GetPathTo(gridObject).ToString(),
                    ["state"] = gridObject.CaptureState()
                };
                result.Add(entry);
            }

            return result;
        }

        private void RestoreGridObjectStates(Godot.Collections.Array objects)
        {
            foreach (Variant value in objects)
            {
                if (!GridVariantReader.TryDictionary(value, out Godot.Collections.Dictionary entry))
                    continue;

                string path = DictString(entry, "path", "");
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                GridObjectComponent? gridObject = GetNodeOrNull<GridObjectComponent>(new NodePath(path));
                if (gridObject == null || !GodotObject.IsInstanceValid(gridObject))
                    continue;

                if (entry.ContainsKey("state")
                    && GridVariantReader.TryDictionary(entry["state"], out Godot.Collections.Dictionary objectState))
                    gridObject.RestoreState(objectState);
            }
        }

        private void ReleaseGridObjectFootprints()
        {
            foreach (GridObjectComponent gridObject in GridObjects())
                gridObject.ReleaseFootprint();
        }

        private List<GridObjectComponent> GridObjects()
        {
            ResolveReferences();
            var objects = new List<GridObjectComponent>();

            if (IsInsideTree())
            {
                foreach (Node node in GetTree().GetNodesInGroup(GridObjectComponent.ComponentGroupName))
                {
                    if (node is not GridObjectComponent gridObject)
                        continue;
                    if (_objectsRoot != null && !IsNodeWithin(gridObject, _objectsRoot))
                        continue;
                    objects.Add(gridObject);
                }
            }

            if (objects.Count == 0 && _objectsRoot != null)
                CollectGridObjects(_objectsRoot, objects);

            return objects;
        }

        private static void CollectGridObjects(Node node, List<GridObjectComponent> objects)
        {
            if (node is GridObjectComponent gridObject)
                objects.Add(gridObject);

            foreach (Node child in node.GetChildren())
                CollectGridObjects(child, objects);
        }

        private static bool IsNodeWithin(Node node, Node root)
        {
            for (Node? current = node; current != null; current = current.GetParent())
                if (current == root)
                    return true;
            return false;
        }

        private static Godot.Collections.Array<Vector2I> ReadCells(Godot.Collections.Dictionary state, string key)
        {
            var cells = new Godot.Collections.Array<Vector2I>();
            foreach (Variant value in ReadArray(state, key))
            {
                Vector2I cell = GridVariantReader.Vector2I(value, new Vector2I(int.MinValue, int.MinValue));
                if (cell.X != int.MinValue && cell.Y != int.MinValue)
                    cells.Add(cell);
            }
            return cells;
        }

        private static string DictString(Godot.Collections.Dictionary dict, string key, string fallback)
            => dict.ContainsKey(key) ? dict[key].AsString() : fallback;
    }
}

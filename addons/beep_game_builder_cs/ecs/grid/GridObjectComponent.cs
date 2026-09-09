using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Common identity/inspection component for placed grid objects. Add it under
    /// any Node2D building, prop, machine, resource, or unit scene to expose the
    /// object's grid cell, footprint, ids, and state through normal Godot exports.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridObjectComponent : GameplayComponent
    {
        public const string ComponentGroupName = "grid_objects";

        [Signal] public delegate void GridObjectChangedEventHandler(string objectId, int x, int y);

        [Export] public string ObjectId { get; set; } = "";
        [Export] public string DisplayName { get; set; } = "";
        [Export] public string ObjectKind { get; set; } = "";
        [Export] public string Category { get; set; } = "";
        [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
        private Vector2I _cell;
        private Vector2I _footprint = Vector2I.One;
        [Export] public Vector2I Cell
        {
            get => _cell;
            set { _cell = value; RefreshChunkPins(); }
        }
        [Export] public Vector2I Footprint
        {
            get => _footprint;
            set { _footprint = value; RefreshChunkPins(); }
        }
        [Export] public bool BlocksNavigation { get; set; } = true;
        /// <summary>Optional cell-anchor binding for stationary objects. Child art supplies visual offsets.</summary>
        [Export] public NodePath GridPath { get; set; } = new("");
        [Export] public NodePath PlacementPath { get; set; } = new("");
        [Export] public NodePath NavigationPath { get; set; } = new("");
        [Export] public bool ReserveFootprintOnReady { get; set; } = false;
        [Export] public bool ReservePlacementFootprint { get; set; } = true;
        [Export] public bool ReserveNavigationFootprint { get; set; } = true;
        [Export] public bool ReleaseReservedFootprintOnExit { get; set; } = true;
        [Export] public bool Selectable { get; set; } = true;
        [Export] public bool Complete { get; set; } = true;
        [Export] public Godot.Collections.Dictionary Metadata { get; set; } = new();

        private readonly HashSet<Vector2I> _reservedPlacementCells = new();
        private readonly HashSet<Vector2I> _reservedNavigationCells = new();
        private GridPlacementComponent? _placement;
        private GridNavigationComponent? _navigation;
        private GridProjectionComponent? _grid;

        public string EffectiveCategory => !string.IsNullOrWhiteSpace(Category) ? Category : ObjectKind;

        public override void _Ready()
        {
            if (string.IsNullOrEmpty(ComponentGroup))
                ComponentGroup = ComponentGroupName;
            AddToGroup(ComponentGroupName);
            BindGrid();
            ApplyParentMetadata();
            RefreshChunkPins();
            if (!Engine.IsEditorHint() && ReserveFootprintOnReady)
                ReserveFootprint();
        }

        public override void _ExitTree()
        {
            ReleaseChunkPins();
            RequestReady();
            if (_grid is not null && GodotObject.IsInstanceValid(_grid)) _grid.GeometryChanged -= RefreshPosition;
            _grid = null;
            if (ReleaseReservedFootprintOnExit)
                ReleaseFootprint();
        }

        public void Configure(
            string objectId,
            string displayName,
            string category,
            Vector2I cell,
            Vector2I footprint,
            bool blocksNavigation,
            bool complete = true)
        {
            bool wasReserved = HasReservedFootprint;
            if (wasReserved)
                ReleaseFootprint();

            ObjectId = GridIds.Normalize(objectId);
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? ObjectId : displayName.Trim();
            Category = string.IsNullOrWhiteSpace(category) ? "" : category.Trim();
            if (string.IsNullOrWhiteSpace(ObjectKind))
                ObjectKind = Category;
            _cell = cell;
            _footprint = new Vector2I(Mathf.Max(1, footprint.X), Mathf.Max(1, footprint.Y));
            RefreshChunkPins();
            BlocksNavigation = blocksNavigation;
            Complete = complete;
            ApplyParentMetadata();
            if (wasReserved || ReserveFootprintOnReady)
                ReserveFootprint();
            EmitSignal(SignalName.GridObjectChanged, ObjectId, Cell.X, Cell.Y);
        }

        public void SetCell(Vector2I cell)
        {
            bool wasReserved = HasReservedFootprint;
            if (wasReserved)
                ReleaseFootprint();

            Cell = cell;
            ApplyParentMetadata();
            if (wasReserved || ReserveFootprintOnReady)
                ReserveFootprint();
            EmitSignal(SignalName.GridObjectChanged, ObjectId, Cell.X, Cell.Y);
        }

        public void ReserveFootprint()
        {
            ResolveReferences();
            foreach (Vector2I cell in FootprintCells())
            {
                if (ReservePlacementFootprint && _placement != null)
                {
                    _placement.SetOccupied(cell, true);
                    _reservedPlacementCells.Add(cell);
                }

                // BlocksNavigation gates ONLY the navigation half. It used to
                // gate the whole method, so a walkable object reserved no
                // placement occupancy either - and anything could be built on
                // top of it.
                if (BlocksNavigation && ReserveNavigationFootprint && _navigation != null)
                {
                    _navigation.SetBlocked(cell, true);
                    _reservedNavigationCells.Add(cell);
                }
            }
        }

        public void ReleaseFootprint()
        {
            ResolveReferences();
            if (_placement != null)
            {
                foreach (Vector2I cell in _reservedPlacementCells)
                    _placement.SetOccupied(cell, false);
            }

            if (_navigation != null)
            {
                foreach (Vector2I cell in _reservedNavigationCells)
                    _navigation.SetBlocked(cell, false);
            }

            _reservedPlacementCells.Clear();
            _reservedNavigationCells.Clear();
        }

        public void SetMetadataValue(string key, Variant value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            Metadata[key.Trim()] = value;
            ApplyParentMetadata();
            EmitSignal(SignalName.GridObjectChanged, ObjectId, Cell.X, Cell.Y);
        }

        public Variant GetMetadataValue(string key)
            => Metadata.ContainsKey(key) ? Metadata[key] : default;

        public Godot.Collections.Dictionary CaptureState()
            => new()
            {
                ["object_id"] = ObjectId,
                ["display_name"] = DisplayName,
                ["object_kind"] = ObjectKind,
                ["category"] = Category,
                ["description"] = Description,
                ["cell"] = Cell,
                ["footprint"] = Footprint,
                ["blocks_navigation"] = BlocksNavigation,
                ["reserve_footprint_on_ready"] = ReserveFootprintOnReady,
                ["reserve_placement_footprint"] = ReservePlacementFootprint,
                ["reserve_navigation_footprint"] = ReserveNavigationFootprint,
                ["selectable"] = Selectable,
                ["complete"] = Complete,
                ["metadata"] = Metadata.Duplicate(deep: true)
            };

        public void RestoreState(Godot.Collections.Dictionary state)
        {
            bool wasReserved = HasReservedFootprint;
            if (wasReserved)
                ReleaseFootprint();

            ObjectId = GridVariantReader.String(state, "object_id", ObjectId);
            DisplayName = GridVariantReader.String(state, "display_name", DisplayName);
            ObjectKind = GridVariantReader.String(state, "object_kind", ObjectKind);
            Category = GridVariantReader.String(state, "category", Category);
            Description = GridVariantReader.String(state, "description", Description);
            _cell = GridVariantReader.Vector2I(state, "cell", Cell);
            _footprint = GridVariantReader.Vector2I(state, "footprint", Footprint);
            RefreshChunkPins();
            BlocksNavigation = GridVariantReader.Bool(state, "blocks_navigation", BlocksNavigation);
            ReserveFootprintOnReady = GridVariantReader.Bool(state, "reserve_footprint_on_ready", ReserveFootprintOnReady);
            ReservePlacementFootprint = GridVariantReader.Bool(state, "reserve_placement_footprint", ReservePlacementFootprint);
            ReserveNavigationFootprint = GridVariantReader.Bool(state, "reserve_navigation_footprint", ReserveNavigationFootprint);
            Selectable = GridVariantReader.Bool(state, "selectable", Selectable);
            Complete = GridVariantReader.Bool(state, "complete", Complete);
            if (state.ContainsKey("metadata") && state["metadata"].VariantType == Variant.Type.Dictionary)
                Metadata = state["metadata"].AsGodotDictionary().Duplicate(deep: true);
            ApplyParentMetadata();
            if (wasReserved || ReserveFootprintOnReady)
                ReserveFootprint();
            EmitSignal(SignalName.GridObjectChanged, ObjectId, Cell.X, Cell.Y);
        }

        public void ApplyParentMetadata()
        {
            Node? parent = GetParent();
            if (parent == null)
                return;

            parent.SetMeta("grid_object_id", ObjectId);
            parent.SetMeta("grid_object_display_name", DisplayName);
            parent.SetMeta("grid_object_kind", ObjectKind);
            parent.SetMeta("grid_object_category", Category);
            parent.SetMeta("grid_object_description", Description);
            parent.SetMeta("grid_object_cell", Cell);
            parent.SetMeta("grid_object_footprint", Footprint);
            parent.SetMeta("grid_object_blocks_navigation", BlocksNavigation);
            parent.SetMeta("grid_object_complete", Complete);
            RefreshPosition();
        }

        private void BindGrid()
        {
            var grid = GridPath.IsEmpty ? null : GetNodeOrNull<GridProjectionComponent>(GridPath);
            if (grid == _grid) return;
            if (_grid is not null && GodotObject.IsInstanceValid(_grid)) _grid.GeometryChanged -= RefreshPosition;
            _grid = grid;
            if (_grid is not null) _grid.GeometryChanged += RefreshPosition;
        }

        /// <summary>Repositions the parent from saved cell identity, never by inverse-picking its old position.</summary>
        public void RefreshPosition()
        {
            BindGrid();
            if (_grid is null || GetParent() is not Node2D body) return;
            foreach (Node child in body.GetChildren())
                if (child is GridPathFollowerComponent { IsMoving: true }) return;
            Vector2 position = _grid.CellToWorld(Cell);
            if (position.IsFinite()) body.GlobalPosition = position;
        }

        private bool HasReservedFootprint => _reservedPlacementCells.Count > 0 || _reservedNavigationCells.Count > 0;

        private IEnumerable<Vector2I> FootprintCells()
        {
            int width = Mathf.Max(1, Footprint.X);
            int height = Mathf.Max(1, Footprint.Y);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    yield return new Vector2I(Cell.X + x, Cell.Y + y);
        }

        private void ResolveReferences()
        {
            Resolve(PlacementPath, ref _placement);
            Resolve(NavigationPath, ref _navigation);
        }

        internal void BindPlacementContext(GridProjectionComponent grid, GridPlacementComponent placement,
            GridNavigationComponent? navigation, GridCellDataComponent? cells)
        {
            // Release the previous owner's reservations before changing the binding.
            ReleaseFootprint();
            GridPath = GetPathTo(grid);
            PlacementPath = GetPathTo(placement);
            NavigationPath = navigation is null ? new NodePath("") : GetPathTo(navigation);
            _placement = placement;
            _navigation = navigation;
            ChunkCellDataPath = cells is null ? new NodePath("") : GetPathTo(cells);
            BindGrid();
        }




    }
}

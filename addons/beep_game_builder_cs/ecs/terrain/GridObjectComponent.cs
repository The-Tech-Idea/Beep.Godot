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
    public partial class GridObjectComponent : EntityComponent
    {
        public const string ComponentGroupName = "grid_objects";

        [Signal] public delegate void GridObjectChangedEventHandler(string objectId, int x, int y);

        [Export] public string ObjectId { get; set; } = "";
        [Export] public string DisplayName { get; set; } = "";
        [Export] public string ObjectKind { get; set; } = "";
        [Export] public string Category { get; set; } = "";
        [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
        [Export] public Vector2I Cell { get; set; } = Vector2I.Zero;
        [Export] public Vector2I Footprint { get; set; } = Vector2I.One;
        [Export] public bool BlocksNavigation { get; set; } = true;
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

        public string EffectiveCategory => !string.IsNullOrWhiteSpace(Category) ? Category : ObjectKind;

        public override void _Ready()
        {
            if (string.IsNullOrEmpty(ComponentGroup))
                ComponentGroup = ComponentGroupName;
            AddToGroup(ComponentGroupName);
            ApplyParentMetadata();
            if (!Engine.IsEditorHint() && ReserveFootprintOnReady)
                ReserveFootprint();
        }

        public override void _ExitTree()
        {
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

            ObjectId = NormalizeId(objectId);
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? ObjectId : displayName.Trim();
            Category = string.IsNullOrWhiteSpace(category) ? "" : category.Trim();
            if (string.IsNullOrWhiteSpace(ObjectKind))
                ObjectKind = Category;
            Cell = cell;
            Footprint = new Vector2I(Mathf.Max(1, footprint.X), Mathf.Max(1, footprint.Y));
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
            if (!BlocksNavigation)
                return;

            ResolveReferences();
            foreach (Vector2I cell in FootprintCells())
            {
                if (ReservePlacementFootprint && _placement != null)
                {
                    _placement.SetOccupied(cell, true);
                    _reservedPlacementCells.Add(cell);
                }

                if (ReserveNavigationFootprint && _navigation != null)
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

            ObjectId = DictString(state, "object_id", ObjectId);
            DisplayName = DictString(state, "display_name", DisplayName);
            ObjectKind = DictString(state, "object_kind", ObjectKind);
            Category = DictString(state, "category", Category);
            Description = DictString(state, "description", Description);
            Cell = DictVector2I(state, "cell", Cell);
            Footprint = DictVector2I(state, "footprint", Footprint);
            BlocksNavigation = DictBool(state, "blocks_navigation", BlocksNavigation);
            ReserveFootprintOnReady = DictBool(state, "reserve_footprint_on_ready", ReserveFootprintOnReady);
            ReservePlacementFootprint = DictBool(state, "reserve_placement_footprint", ReservePlacementFootprint);
            ReserveNavigationFootprint = DictBool(state, "reserve_navigation_footprint", ReserveNavigationFootprint);
            Selectable = DictBool(state, "selectable", Selectable);
            Complete = DictBool(state, "complete", Complete);
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
            if (_placement == null || !GodotObject.IsInstanceValid(_placement))
                _placement = !PlacementPath.IsEmpty
                    ? GetNodeOrNull<GridPlacementComponent>(PlacementPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridPlacementComponent>(GetTree()?.CurrentScene) : null;

            if (_navigation == null || !GodotObject.IsInstanceValid(_navigation))
                _navigation = !NavigationPath.IsEmpty
                    ? GetNodeOrNull<GridNavigationComponent>(NavigationPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridNavigationComponent>(GetTree()?.CurrentScene) : null;
        }

        private static string NormalizeId(string value)
            => string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant().Replace(' ', '_');

        private static string DictString(Godot.Collections.Dictionary dict, string key, string fallback)
            => dict.ContainsKey(key) ? dict[key].AsString() : fallback;

        private static bool DictBool(Godot.Collections.Dictionary dict, string key, bool fallback)
            => GridVariantReader.Bool(dict, key, fallback);

        private static Vector2I DictVector2I(Godot.Collections.Dictionary dict, string key, Vector2I fallback)
            => GridVariantReader.Vector2I(dict, key, fallback);
    }
}

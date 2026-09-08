using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Shows a built world's own report in a Label.
    ///
    /// Each demo formatted this itself, and the copies disagreed about the same
    /// field: one printed "continents", another "landmasses", a third omitted
    /// lakes entirely. None of them was wrong about the map - they were wrong
    /// about each other, which is what a status line assembled per scene always
    /// ends up being. TerrainWorldComponent.StatusLine is the one description.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TerrainWorldStatusComponent : Node
    {
        private NodePath _worldPath = new("");
        private NodePath _labelPath = new("");
        [Export] public NodePath WorldPath
        {
            get => _worldPath;
            set { _worldPath = value; if (IsNodeReady() && !Engine.IsEditorHint()) Refresh(); }
        }
        [Export] public NodePath LabelPath
        {
            get => _labelPath;
            set { _labelPath = value; if (IsNodeReady() && !Engine.IsEditorHint()) Refresh(); }
        }

        /// <summary>Text shown before the first world is built.</summary>
        [Export] public string PendingText { get; set; } = "generating...";

        private TerrainWorldComponent? _world;
        private Label? _label;

        public override void _Ready()
        {
            if (Engine.IsEditorHint())
                return;

            Refresh();
        }

        public override void _ExitTree()
        {
            if (_world is not null && GodotObject.IsInstanceValid(_world))
                _world.WorldBuilt -= OnWorldBuilt;
            _world = null;
            _label = null;
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (WorldPath.IsEmpty)
                return new[] { "WorldPath should point to a TerrainWorldComponent." };
            if (LabelPath.IsEmpty)
                return new[] { "LabelPath should point to the Label that shows the report." };
            return System.Array.Empty<string>();
        }

        private void OnWorldBuilt(Vector2I size)
        {
            _ = size;
            Refresh();
        }

        /// <summary>Rebinds to the current paths and displays an already-built world immediately.</summary>
        public void Refresh()
        {
            var next = WorldPath.IsEmpty ? null : GetNodeOrNull<TerrainWorldComponent>(WorldPath);
            if (_world != next)
            {
                if (GodotObject.IsInstanceValid(_world)) _world!.WorldBuilt -= OnWorldBuilt;
                _world = next;
                if (_world is not null && !Engine.IsEditorHint()) _world.WorldBuilt += OnWorldBuilt;
            }
            _label = LabelPath.IsEmpty ? null : GetNodeOrNull<Label>(LabelPath);
            if (_label is not null)
                _label.Text = _world?.BuiltSize.X > 0 ? _world.StatusLine() : PendingText;
        }
    }
}

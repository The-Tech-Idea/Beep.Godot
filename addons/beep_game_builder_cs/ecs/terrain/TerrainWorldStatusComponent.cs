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
        [Export] public NodePath WorldPath { get; set; } = new("");
        [Export] public NodePath LabelPath { get; set; } = new("");

        /// <summary>Text shown before the first world is built.</summary>
        [Export] public string PendingText { get; set; } = "generating...";

        private TerrainWorldComponent? _world;
        private Label? _label;

        public override void _Ready()
        {
            if (Engine.IsEditorHint())
                return;

            _world = WorldPath.IsEmpty ? null : GetNodeOrNull<TerrainWorldComponent>(WorldPath);
            _label = LabelPath.IsEmpty ? null : GetNodeOrNull<Label>(LabelPath);

            if (_label is not null)
                _label.Text = PendingText;

            if (_world is not null)
                _world.WorldBuilt += OnWorldBuilt;
        }

        public override void _ExitTree()
        {
            if (_world is not null && GodotObject.IsInstanceValid(_world))
                _world.WorldBuilt -= OnWorldBuilt;
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
            if (_label is not null && _world is not null)
                _label.Text = _world.StatusLine();
        }
    }
}

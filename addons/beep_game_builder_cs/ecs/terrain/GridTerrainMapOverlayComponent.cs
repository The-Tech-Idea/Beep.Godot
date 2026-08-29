using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Draws the generator's gameplay layers over the painted terrain: resource
    /// markers and player start positions.
    ///
    /// This draws with the primitive canvas API rather than sprites so it needs
    /// no art, and it lives on its own node so the painted terrain layers are
    /// untouched. It reads the generator directly, which is the one owner of
    /// this data.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridTerrainMapOverlayComponent : Node2D
    {
        [Export] public NodePath TerrainGeneratorPath { get; set; } = new("");

        [ExportGroup("Map")]
        [Export] public Vector2I BoundsSize { get; set; } = new(48, 30);
        [Export(PropertyHint.Range, "1,256,1")] public int TileSize { get; set; } = 64;

        [ExportGroup("Display")]
        [Export] public bool ShowResources { get; set; } = true;
        [Export] public bool ShowStartPositions { get; set; } = true;
        [Export(PropertyHint.Range, "0.05,0.5,0.01")] public float ResourceRadiusTiles { get; set; } = 0.16f;
        [Export(PropertyHint.Range, "0.1,1.0,0.01")] public float StartRadiusTiles { get; set; } = 0.42f;

        private GridTerrainGeneratorComponent? _generator;

        public override void _Ready() => Refresh();

        public override string[] _GetConfigurationWarnings()
            => TerrainGeneratorPath.IsEmpty
                ? new[] { "TerrainGeneratorPath should point to a GridTerrainGeneratorComponent." }
                : System.Array.Empty<string>();

        /// <summary>Re-reads the generator and repaints the markers.</summary>
        public void Refresh()
        {
            if (_generator == null || !GodotObject.IsInstanceValid(_generator))
                _generator = TerrainGeneratorPath.IsEmpty
                    ? null
                    : GetNodeOrNull<GridTerrainGeneratorComponent>(TerrainGeneratorPath);

            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_generator == null)
                return;

            float tile = Mathf.Max(1, TileSize);
            Vector2I size = new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));

            if (ShowResources)
                DrawResources(size, tile);

            if (ShowStartPositions)
                DrawStartPositions(tile);
        }

        private void DrawResources(Vector2I size, float tile)
        {
            float radius = Mathf.Max(1.0f, ResourceRadiusTiles * tile);

            for (int y = 0; y < size.Y; y++)
            {
                for (int x = 0; x < size.X; x++)
                {
                    string resource = _generator!.ResourceAt(new Vector2I(x, y));
                    if (resource.Length == 0)
                        continue;

                    Vector2 centre = new((x + 0.5f) * tile, (y + 0.5f) * tile);
                    Color colour = ColourFor(resource);

                    // A dark rim keeps a marker legible on any biome under it.
                    DrawCircle(centre, radius + Mathf.Max(1.0f, tile * 0.03f), new Color(0.05f, 0.05f, 0.07f, 0.75f));
                    DrawCircle(centre, radius, colour);
                }
            }
        }

        private void DrawStartPositions(float tile)
        {
            float radius = Mathf.Max(2.0f, StartRadiusTiles * tile);
            float thickness = Mathf.Max(2.0f, tile * 0.07f);
            var ring = new Color(1.0f, 0.98f, 0.85f);
            var shadow = new Color(0.05f, 0.05f, 0.07f, 0.85f);

            foreach (Vector2I cell in _generator!.GetStartPositions())
            {
                Vector2 centre = new((cell.X + 0.5f) * tile, (cell.Y + 0.5f) * tile);
                DrawArc(centre, radius + (thickness * 0.5f), 0.0f, Mathf.Tau, 32, shadow, thickness * 1.8f);
                DrawArc(centre, radius, 0.0f, Mathf.Tau, 32, ring, thickness);
                DrawCircle(centre, thickness * 0.9f, ring);
            }
        }

        /// <summary>
        /// Category colours rather than one per resource: at map zoom the useful
        /// question is "is that strategic or luxury", not which of twenty it is.
        /// </summary>
        private static Color ColourFor(string resource)
            => TerrainResourceStage.CategoryOf(resource) switch
            {
                ResourceCategory.Strategic => new Color(0.92f, 0.32f, 0.26f),
                ResourceCategory.Luxury => new Color(0.86f, 0.62f, 0.95f),
                _ => new Color(0.98f, 0.84f, 0.32f),
            };
    }
}

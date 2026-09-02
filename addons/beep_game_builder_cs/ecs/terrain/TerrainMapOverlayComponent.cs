using Godot;
using System.Collections.Generic;

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
    public partial class TerrainMapOverlayComponent : Node2D
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

        /// <summary>
        /// Whether this overlay builds itself once the scene is ready. Turn it
        /// off where a controller (TerrainWorldComponent) generates the world
        /// first and drives Rebuild, so the map is not built twice - and so it
        /// is not built once first with whatever BoundsSize happens to be
        /// authored in the scene, ahead of the controller's own generated size.
        /// Every sibling renderer already has this; this one did not, which is
        /// exactly the gap that let the terrain lab self-rebuild once against a
        /// stale scene-authored BoundsSize before TerrainWorldComponent ever
        /// got to configure it.
        /// </summary>
        [Export] public bool RefreshOnReady { get; set; } = true;

        /// <summary>One baked resource marker: where, how big, what colour.</summary>
        private readonly record struct ResourceMarker(Vector2 Centre, float Radius, float RimRadius, Color Colour);

        private TerrainGeneratorComponent? _generator;

        /// <summary>
        /// Built once per Rebuild rather than read from the generator on every
        /// _Draw. _Draw runs on every canvas redraw - a window resize, a sibling
        /// node invalidating the frame - not only after a real change to the
        /// map, so re-scanning the whole bounds and re-querying the generator
        /// per cell there paid the cost of a full rebuild for free, repeatedly.
        /// </summary>
        private readonly List<ResourceMarker> _resourceMarkers = new();
        private readonly List<Vector2> _startMarkers = new();
        private float _startRadius;
        private float _startThickness;

        public override void _Ready()
        {
            if (RefreshOnReady && !Engine.IsEditorHint())
                CallDeferred(nameof(Rebuild));
        }

        public override string[] _GetConfigurationWarnings()
            => TerrainGeneratorPath.IsEmpty
                ? new[] { "TerrainGeneratorPath should point to a TerrainGeneratorComponent." }
                : System.Array.Empty<string>();

        /// <summary>Re-reads the generator and repaints the markers.</summary>
        public void Rebuild()
        {
            // Markers, so the stack's marker slot - above the props, because a
            // forest must never hide the thing the player is meant to click.
            //
            // This node set no z of its own, and the lab scene supplied 60. That
            // is the same defect as a private RenderZIndex export, only harder to
            // find: the number is not in the source at all, so the component
            // reads as though it has no opinion while a scene quietly holds the
            // answer. It happened to be above the props and happened to work.
            ZIndex = TerrainLayers.ZForMarkers();
            ZAsRelative = false;

            if (_generator == null || !GodotObject.IsInstanceValid(_generator))
                _generator = TerrainGeneratorPath.IsEmpty
                    ? null
                    : GetNodeOrNull<TerrainGeneratorComponent>(TerrainGeneratorPath);

            _resourceMarkers.Clear();
            _startMarkers.Clear();

            if (_generator is null)
            {
                QueueRedraw();
                return;
            }

            TerrainBoundsCheck.WarnIfMismatched(Name, BoundsSize, _generator.BoundsSize);

            float tile = Mathf.Max(1, TileSize);
            Vector2I size = new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));

            if (ShowResources)
            {
                // Resolved ONCE for the whole scan rather than once per cell;
                // see TerrainGeneratorComponent.ResolveField.
                GeneratedTerrainField field = _generator.ResolveField();
                float radius = Mathf.Max(1.0f, ResourceRadiusTiles * tile);
                float rim = radius + Mathf.Max(1.0f, tile * 0.03f);
                for (int y = 0; y < size.Y; y++)
                {
                    for (int x = 0; x < size.X; x++)
                    {
                        string resource = field.ResourceAtCell(new Vector2I(x, y));
                        if (resource.Length == 0)
                            continue;

                        Vector2 centre = new((x + 0.5f) * tile, (y + 0.5f) * tile);
                        _resourceMarkers.Add(new ResourceMarker(centre, radius, rim, ColourFor(resource)));
                    }
                }
            }

            if (ShowStartPositions)
            {
                _startRadius = Mathf.Max(2.0f, StartRadiusTiles * tile);
                _startThickness = Mathf.Max(2.0f, tile * 0.07f);
                foreach (Vector2I cell in _generator.GetStartPositions())
                    _startMarkers.Add(new Vector2((cell.X + 0.5f) * tile, (cell.Y + 0.5f) * tile));
            }

            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_generator == null)
            {
                GD.PushWarning($"[{Name}] no generator at TerrainGeneratorPath; the map overlay was not drawn.");
                return;
            }

            DrawResources();
            DrawStartPositions();
        }

        private void DrawResources()
        {
            foreach (ResourceMarker marker in _resourceMarkers)
            {
                // A dark rim keeps a marker legible on any biome under it.
                DrawCircle(marker.Centre, marker.RimRadius, new Color(0.05f, 0.05f, 0.07f, 0.75f));
                DrawCircle(marker.Centre, marker.Radius, marker.Colour);
            }
        }

        private void DrawStartPositions()
        {
            var ring = new Color(1.0f, 0.98f, 0.85f);
            var shadow = new Color(0.05f, 0.05f, 0.07f, 0.85f);

            foreach (Vector2 centre in _startMarkers)
            {
                DrawArc(centre, _startRadius + (_startThickness * 0.5f), 0.0f, Mathf.Tau, 32, shadow, _startThickness * 1.8f);
                DrawArc(centre, _startRadius, 0.0f, Mathf.Tau, 32, ring, _startThickness);
                DrawCircle(centre, _startThickness * 0.9f, ring);
            }
        }

        /// <summary>
        /// Category colours rather than one per resource: at map zoom the useful
        /// question is "is that strategic or luxury", not which of twenty it is.
        /// </summary>
        // Asks the SAME catalog the generator actually placed this resource
        // from, falling back to a cross-catalog search only for an id that
        // predates the generator's current ResourceSet (a saved map re-opened
        // under a different one). TerrainResourceStage.CategoryOf alone only
        // ever searches the three shipped catalogs, so a game with its own
        // custom ResourceCatalog (Resources) had every marker it placed read
        // back as the default Bonus colour regardless of its real category.
        private Color ColourFor(string resource)
        {
            ResourceCategory category = (_generator?.Resources ?? ResourceCatalogs.For(_generator?.ResourceSet ?? ResourceSet.Historical))
                .Find(resource)?.Category
                ?? TerrainResourceStage.CategoryOf(resource);
            return category switch
            {
                ResourceCategory.Strategic => new Color(0.92f, 0.32f, 0.26f),
                ResourceCategory.Luxury => new Color(0.86f, 0.62f, 0.95f),
                _ => new Color(0.98f, 0.84f, 0.32f),
            };
        }
    }
}

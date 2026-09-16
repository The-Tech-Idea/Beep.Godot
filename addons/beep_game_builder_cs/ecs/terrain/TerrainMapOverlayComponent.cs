using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Draws the generator's gameplay layers over the terrain: player start
    /// positions, their reserved start areas, and the underground survey.
    ///
    /// This draws with the primitive canvas API rather than sprites so it needs
    /// no art, and it lives on its own node so the terrain layers are untouched.
    /// It reads the generator directly, which is the one owner of this data.
    ///
    /// It draws NO surface resources. TerrainResourceRendererComponent is the one
    /// resource drawer (icons from the resource set's sheet, live or generated).
    /// This overlay used to draw the same cells a second time as category-coloured
    /// discs, so a scene wiring both showed discs under icons, and a scene wiring
    /// only this showed a classification the icon sheet does not (VIEW-13).
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TerrainMapOverlayComponent : TerrainRendererComponent, ISerializationListener
    {
        [Export] public NodePath TerrainGeneratorPath { get; set; } = new("");
        [Export] public NodePath GridPath { get; set; } = new("");

        [ExportGroup("Map")]
        [Export] public Vector2I BoundsSize { get; set; } = new(48, 30);
        [Export] public Vector2I BoundsOrigin { get; set; } = Vector2I.Zero;
        [Export(PropertyHint.Range, "1,256,1")] public int TileSize { get; set; } = 64;

        [ExportGroup("Display")]
        [Export] public bool ShowStartPositions { get; set; } = true;

        /// <summary>
        /// The survey view: underground deposits drawn as translucent patches,
        /// one hue per resource, denser where the field is richer. Gated by
        /// GridProspectingComponent when ProspectingPath is wired, so an
        /// unsurveyed basin stays a secret.
        /// </summary>
        [Export] public bool ShowUndergroundResources { get; set; } = true;
        [Export] public NodePath ProspectingPath { get; set; } = new("");
        /// <summary>Optional live drawdown store. Depleted underground cells are not shown.</summary>
        [Export] public NodePath SubsurfaceStorePath { get; set; } = new("");
        [Export(PropertyHint.Range, "0.1,1.0,0.01")] public float StartRadiusTiles { get; set; } = 0.42f;

        /// <summary>
        /// Each start's generated area outlined in the start's colour, with its headquarters
        /// footprint, and its start ring in the same colour - so which start a region belongs
        /// to reads at a glance. Draws nothing on a map generated without start areas.
        /// </summary>
        [Export] public bool ShowStartAreas { get; set; }

        /// <summary>
        /// The GridStartAreaComponent holding the faction catalog and the start assignment. Wired,
        /// a start is drawn in the colour of the FACTION that holds it rather than in this
        /// overlay's fixed per-index palette - so a player's region here, their tint on the
        /// minimap and their colour in a lobby are one fact.
        ///
        /// Not a catalog path: the catalog maps a faction to a colour, and only the assignment
        /// knows which faction holds start k. Reading the catalog directly would mean assuming
        /// catalog order IS start order, which is exactly what LockedStart breaks.
        /// </summary>
        [Export] public NodePath StartAreaPath { get; set; } = new("");

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

        /// <summary>One baked underground patch: which tile, what colour.</summary>
        private readonly record struct UndergroundPatch(Vector2[] Corners, Color Colour);

        private TerrainGeneratorComponent? _generator;
        private GridProspectingComponent? _prospecting;
        private GridSubsurfaceStoreComponent? _store;
        private GridProjectionComponent? _grid;
        private GridStartAreaComponent? _startArea;
        private bool RequiresGenerator => ShowStartPositions || ShowUndergroundResources || ShowStartAreas;
        public int UndergroundPatchCount => _undergroundPatches.Count;
        /// <summary>Start rings baked by the last rebuild, one per placed start position.</summary>
        public int StartMarkerCount => _startMarkers.Count;
        /// <summary>Area border and headquarters outline segments baked by the last rebuild.</summary>
        public int StartAreaSegmentCount => _areaSegments.Count;

        /// <summary>One baked segment's colour (transparent when there is no such segment). Test hook.</summary>
        internal Color StartAreaSegmentColour(int index)
            => index >= 0 && index < _areaSegments.Count ? _areaSegments[index].Colour : Colors.Transparent;

        /// <summary>The start a baked segment belongs to, as a start index. Test hook.</summary>
        internal int StartAreaSegmentStart(int index)
            => index >= 0 && index < _areaSegments.Count ? _areaSegments[index].Start : -1;

        /// <summary>
        /// One colour per start index, fixed so start k is the same colour on every map and in
        /// every session. Past 24 starts the palette repeats.
        /// </summary>
        private static readonly Color[] StartPalette =
        {
            new("3f6fe0"), new("e03c31"), new("3db54a"), new("f2c618"), new("2ec4d6"), new("9b4fd6"),
            new("f28a1e"), new("e85aa8"), new("1e9e86"), new("a6d62e"), new("9c6b3c"), new("24408f"),
            new("8f2430"), new("7a8f24"), new("7fb8f0"), new("f09a7f"), new("7ff0b8"), new("b8a0f0"),
            new("d6a62e"), new("d62eb8"), new("6a7a8c"), new("2e6b3a"), new("f06a5a"), new("ede6c8"),
        };

        /// <summary>
        /// The colour of start k: its faction's when one holds it, else the fixed palette. The
        /// palette is not a fallback for missing configuration - it is the answer on a map with no
        /// factions at all, which is most of them, and it is why an unassigned start still reads as
        /// a distinct start rather than as nothing.
        /// </summary>
        private Color StartColour(int index)
        {
            if (_startArea is not null && _startArea.ColourOfStart(index) is { A: > 0f } faction)
                return faction;
            return StartPalette[Mathf.PosMod(index, StartPalette.Length)];
        }

        private readonly record struct AreaSegment(Vector2 From, Vector2 To, Color Colour, bool Headquarters, int Start);

        /// <summary>
        /// Built once per Rebuild rather than read from the generator on every
        /// _Draw. _Draw runs on every canvas redraw - a window resize, a sibling
        /// node invalidating the frame - not only after a real change to the
        /// map, so re-scanning the whole bounds and re-querying the generator
        /// per cell there paid the cost of a full rebuild for free, repeatedly.
        /// </summary>
        private readonly List<UndergroundPatch> _undergroundPatches = new();
        private readonly List<(Vector2 Centre, int Index)> _startMarkers = new();
        private readonly List<AreaSegment> _areaSegments = new();
        private float _startRadius;
        private float _startThickness;

        public override void _Ready()
        {
            ResolveSources();
            if (RefreshOnReady && !Engine.IsEditorHint())
                CallDeferred(nameof(Rebuild));
        }

        public override void _ExitTree()
        {
            DisconnectSources();
            ClearRebuildQueued();
        }

        // The grid, prospecting and store subscriptions are C# events whose targets the
        // engine does not restore across an assembly reload, and _ExitTree is not called on
        // one - so they are released before serialization and rebound after it.
        public void OnBeforeSerialize()
        {
            DisconnectSources();
            ClearRebuildQueued();
        }

        public void OnAfterDeserialize() => CallDeferred(nameof(RestoreSources));

        private void RestoreSources()
        {
            if (!IsInsideTree()) return;
            ResolveSources();
            QueueRebuild();
        }

        public override void _EnterTree()
        {
            if (HasRebuildAttempt && !Engine.IsEditorHint())
                Callable.From(() =>
                {
                    if (!IsInsideTree()) return;
                    ResolveSources();
                    QueueRebuild();
                }).CallDeferred();
        }
        public override string[] _GetConfigurationWarnings()
            => RequiresGenerator && TerrainGeneratorPath.IsEmpty
                ? new[] { "TerrainGeneratorPath should point to a TerrainGeneratorComponent." }
                : System.Array.Empty<string>();

        /// <summary>Re-reads the generator and repaints the markers.</summary>
        public override void Rebuild()
        {
            HasRebuildAttempt = true;
            ClearRebuildQueued();
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

            ResolveSources();

            _undergroundPatches.Clear();
            _startMarkers.Clear();
            _areaSegments.Clear();

            if ((RequiresGenerator && _generator is null) || (!GridPath.IsEmpty && _grid is null))
            {
                // Reported HERE, once per rebuild. _Draw used to carry this
                // warning, and _Draw runs on every canvas redraw - a window
                // resize, an unrelated sibling invalidating the frame - so an
                // unwired overlay spammed the same warning every frame.
                GD.PushWarning($"[{Name}] configured generator or grid is missing; the map overlay was not drawn.");
                QueueRedraw();
                return;
            }

            if (RequiresGenerator && _generator is not null)
                TerrainBoundsCheck.WarnIfMismatched(Name, BoundsSize, _generator.BoundsSize);

            float tile = Mathf.Max(1, TileSize);
            Vector2[] firstCorners = CellOutline(BoundsOrigin, _grid);
            if (firstCorners.Length >= 4)
                tile = Mathf.Min(firstCorners[0].DistanceTo(firstCorners[1]), firstCorners[1].DistanceTo(firstCorners[2]));
            Vector2I size = new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));

            if (ShowUndergroundResources)
            {
                // Resolved ONCE for the whole scan rather than once per cell;
                // see TerrainGeneratorComponent.ResolveField.
                GeneratedTerrainField field = _generator!.ResolveField();
                for (int y = 0; y < size.Y; y++)
                {
                    for (int x = 0; x < size.X; x++)
                    {
                        var sample = new Vector2I(x, y);
                        Vector2I cell = BoundsOrigin + sample;
                        if ((ProspectingPath.IsEmpty || _prospecting?.IsDiscovered(cell) == true)
                            && (SubsurfaceStorePath.IsEmpty || _store?.RemainingAt(cell) > 0))
                        {
                            string underground = field.UndergroundResourceAtCell(sample);
                            if (underground.Length > 0)
                            {
                                float richness = field.UndergroundRichnessAtCell(sample);
                                Vector2[] corners = CellOutline(cell, _grid);
                                if (corners.Length >= 3)
                                    _undergroundPatches.Add(new UndergroundPatch(corners, UndergroundColourFor(underground, richness)));
                            }
                        }
                    }
                }
            }

            _startRadius = Mathf.Max(2.0f, StartRadiusTiles * tile);
            _startThickness = Mathf.Max(2.0f, tile * 0.07f);
            if (ShowStartPositions)
            {
                Godot.Collections.Array<Vector2I> starts = _generator!.GetStartPositions();
                for (int index = 0; index < starts.Count; index++)
                {
                    Vector2 centre = CellPosition(BoundsOrigin + starts[index], _grid);
                    if (centre.IsFinite()) _startMarkers.Add((centre, index));
                }
            }

            if (ShowStartAreas)
                BakeStartAreas(_generator!.ResolveField(), size);

            QueueRedraw();
        }

        /// <summary>
        /// Every area's border and every headquarters footprint's outline, as the shared sides of
        /// neighbouring cell polygons - so the lines follow a square or a diamond grid alike.
        /// </summary>
        private void BakeStartAreas(GeneratedTerrainField field, Vector2I size)
        {
            Vector2I origin = BoundsOrigin;
            var bounds = new Rect2I(origin, size);
            foreach (TerrainOverlayEdges.Edge edge in TerrainOverlayEdges.Collect(bounds, cell => field.StartAreaAtCell(cell - origin)))
                AddSegment(edge, edge.Id - 1, headquarters: false);

            foreach (TerrainStartAreaReport report in field.StartAreas)
            {
                var footprint = new Rect2I(origin + report.Origin, report.Footprint);
                foreach (TerrainOverlayEdges.Edge edge in TerrainOverlayEdges.Collect(footprint, _ => 1))
                    AddSegment(edge, report.Index, headquarters: true);
            }
        }

        private void AddSegment(TerrainOverlayEdges.Edge edge, int start, bool headquarters)
        {
            if (TerrainOverlayEdges.SharedSide(CellOutline(edge.Cell, _grid), CellOutline(edge.Neighbour, _grid), out Vector2 from, out Vector2 to)
                && from.IsFinite() && to.IsFinite())
                _areaSegments.Add(new AreaSegment(from, to, StartColour(start), headquarters, start));
        }

        public override void _Draw()
        {
            // Underground first: it is the ground the markers sit over.
            foreach (UndergroundPatch patch in _undergroundPatches)
                DrawColoredPolygon(patch.Corners, patch.Colour);

            DrawStartAreas();
            DrawStartPositions();
        }

        private void DrawStartAreas()
        {
            var shadow = new Color(0.05f, 0.05f, 0.07f, 0.85f);
            // The shadow pass first for every segment, so one border's shadow never covers
            // another's colour where they meet at a corner.
            foreach (AreaSegment segment in _areaSegments)
                DrawLine(segment.From, segment.To, shadow, _startThickness * (segment.Headquarters ? 2.4f : 1.8f));
            foreach (AreaSegment segment in _areaSegments)
                DrawLine(segment.From, segment.To, segment.Headquarters ? segment.Colour.Lightened(0.35f) : segment.Colour,
                    _startThickness * (segment.Headquarters ? 1.4f : 1.0f));
        }

        private void ResolveSources()
        {
            _generator = TerrainGeneratorPath.IsEmpty ? null : GetNodeOrNull<TerrainGeneratorComponent>(TerrainGeneratorPath);
            var prospecting = ProspectingPath.IsEmpty ? null : GetNodeOrNull<GridProspectingComponent>(ProspectingPath);
            var store = SubsurfaceStorePath.IsEmpty ? null : GetNodeOrNull<GridSubsurfaceStoreComponent>(SubsurfaceStorePath);
            var grid = GridPath.IsEmpty ? null : GetNodeOrNull<GridProjectionComponent>(GridPath);
            var startArea = StartAreaPath.IsEmpty ? null : GetNodeOrNull<GridStartAreaComponent>(StartAreaPath);
            if (_prospecting == prospecting && _store == store && _grid == grid && _startArea == startArea) return;
            DisconnectSources();
            _prospecting = prospecting;
            _store = store;
            _grid = grid;
            _startArea = startArea;
            if (Engine.IsEditorHint()) return;
            if (_prospecting is not null) _prospecting.DiscoveryChanged += QueueRebuild;
            if (_grid is not null) _grid.GeometryChanged += QueueRebuild;
            // The segments are baked with their colours, so a start changing hands has to re-bake,
            // not merely redraw.
            if (_startArea is not null) _startArea.AssignmentChanged += QueueRebuild;
            if (_store is not null)
            {
                _store.DepositChanged += OnDepositChanged;
                _store.StateRestored += QueueRebuild;
            }
        }

        private void DisconnectSources()
        {
            if (GodotObject.IsInstanceValid(_prospecting)) _prospecting!.DiscoveryChanged -= QueueRebuild;
            if (GodotObject.IsInstanceValid(_store))
            {
                _store!.DepositChanged -= OnDepositChanged;
                _store.StateRestored -= QueueRebuild;
            }
            _prospecting = null;
            _store = null;
            if (GodotObject.IsInstanceValid(_grid)) _grid!.GeometryChanged -= QueueRebuild;
            _grid = null;
            if (GodotObject.IsInstanceValid(_startArea)) _startArea!.AssignmentChanged -= QueueRebuild;
            _startArea = null;
        }

        /// <summary>Logical cell center in overlay-local coordinates.</summary>
        public Vector2 CellPosition(Vector2I cell)
        {
            var grid = GridPath.IsEmpty ? null : GetNodeOrNull<GridProjectionComponent>(GridPath);
            return !GridPath.IsEmpty && grid is null ? new Vector2(float.NaN, float.NaN) : CellPosition(cell, grid);
        }

        private Vector2 CellPosition(Vector2I cell, GridProjectionComponent? grid)
            => grid is null ? ((Vector2)cell + Vector2.One * 0.5f) * Mathf.Max(1, TileSize)
                : ToLocal(grid.CellToWorld(cell));

        /// <summary>Logical cell polygon in overlay-local coordinates, shared with patch drawing.</summary>
        public Vector2[] CellOutline(Vector2I cell)
        {
            var grid = GridPath.IsEmpty ? null : GetNodeOrNull<GridProjectionComponent>(GridPath);
            return !GridPath.IsEmpty && grid is null ? System.Array.Empty<Vector2>() : CellOutline(cell, grid);
        }

        private Vector2[] CellOutline(Vector2I cell, GridProjectionComponent? grid)
        {
            if (grid is not null)
            {
                Vector2[] points = grid.CellCorners(cell);
                for (int i = 0; i < points.Length; i++) points[i] = ToLocal(grid.ToGlobal(points[i]));
                return points;
            }
            float tile = Mathf.Max(1, TileSize);
            Vector2 corner = (Vector2)cell * tile;
            return new[] { corner, corner + new Vector2(tile, 0), corner + Vector2.One * tile, corner + new Vector2(0, tile) };
        }

        private void OnDepositChanged(int x, int y, string resourceId, int remaining) => QueueRebuild();
        /// <summary>
        /// One stable hue per underground id (from its characters, so it never
        /// changes between runs), translucent, denser where the field is
        /// richer - a basin reads as a shaded body with a bright core.
        /// </summary>
        private static Color UndergroundColourFor(string id, float richness)
        {
            int sum = 0;
            foreach (char c in id)
                sum = (sum * 31) + c;
            float hue = Mathf.PosMod(sum * 0.6180339887f, 1.0f);
            float alpha = 0.16f + (0.22f * Mathf.Clamp(richness, 0.0f, 1.0f));
            return Color.FromHsv(hue, 0.65f, 0.95f, alpha);
        }

        private void DrawStartPositions()
        {
            var cream = new Color(1.0f, 0.98f, 0.85f);
            var shadow = new Color(0.05f, 0.05f, 0.07f, 0.85f);

            foreach ((Vector2 centre, int index) in _startMarkers)
            {
                // A ring takes its start's colour when the areas are shown, so ring and region match.
                Color ring = ShowStartAreas ? StartColour(index) : cream;
                DrawArc(centre, _startRadius + (_startThickness * 0.5f), 0.0f, Mathf.Tau, 32, shadow, _startThickness * 1.8f);
                DrawArc(centre, _startRadius, 0.0f, Mathf.Tau, 32, ring, _startThickness);
                DrawCircle(centre, _startThickness * 0.9f, ring);
            }
        }
    }
}

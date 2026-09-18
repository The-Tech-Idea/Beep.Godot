using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Which projection a world is drawn in. Public and owned here, because a
    /// game chooses it as readily as a lab does.
    /// </summary>
    public enum TerrainProjection
    {
        Painted = 0,
        Tiles = 1,
        Isometric = 2,

        /// <summary>
        /// Isometric drawn from an authored TileSet. Same projection as
        /// Isometric, different art and different borders: the block renderer
        /// stacks voxels and meets terrains hard, this hands runs of cells to
        /// Godot's SetCellsTerrainConnect so the joins are transition tiles.
        /// </summary>
        IsometricAutotile = 3,
    }

    /// <summary>
    /// Creates a world and draws it. THE map/world creation component.
    ///
    /// Everything a game needs to make a map is here and nothing a lab needs is:
    /// no controls, no panel, no camera. A developer builds their own creation
    /// screen by dropping this in, setting the axes, and calling Build - or by
    /// setting BuildOnReady and writing no code at all.
    ///
    /// This existed only inside the lab's scene controller, which meant the one
    /// piece worth reusing was welded to a particular panel of OptionButtons in
    /// a particular test scene. Every demo then reimplemented the same three
    /// steps - generate, rebuild the renderers, report - as another hundred-line
    /// controller, and each copy drifted: one reported continents, another
    /// landmasses, a third framed the map by scaling the world while the rest
    /// moved a camera.
    ///
    /// The axes are exported, so a scene configures a world the way it
    /// configures anything else. TerrainMapSetup owns what each axis means and
    /// TerrainGeneratorComponent.ApplyMapSetup owns how it reaches the
    /// generator; this component owns neither, it just carries the choice.
    ///
    /// THE RECIPE IS THE SAVE. The generated world - relief, elevation,
    /// resources, start positions, the kind every cell began as - is a pure
    /// function of the axes above and the seed, so that is what this
    /// component persists: the recipe, not the eight-layer result. On load it
    /// regenerates the same world and draws it, and writes NOTHING into the
    /// grid's cells: the live map is GridCellDataComponent's, saved by
    /// GridWorldStateComponent with every edit the player made, and a
    /// regenerated world must not paint over it. That is the split OpenTTD
    /// and Widelands keep - one saved tile array, and generation is what fills
    /// it the first time. Before this the seed was never saved at all, so a
    /// reload drew whatever the scene's authored Seed said, over cells restored
    /// from a different world.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TerrainWorldComponent : Node, ISaveable
    {
        /// <summary>
        /// Raised after a world is generated and drawn - a new one from
        /// <see cref="NewWorld"/> or the saved one from <see cref="RestoreWorld"/>.
        /// A camera framing the map cares that there is a world, not which door
        /// it came through.
        /// </summary>
        [Signal] public delegate void WorldBuiltEventHandler(Vector2I size);

        [ExportGroup("Pipeline")]
        [Export] public NodePath GeneratorPath { get; set; } = new("");
        /// <summary>Shared live map. When empty, uses the generator's CellDataPath.</summary>
        [Export] public NodePath CellDataPath { get; set; } = new("");
        [Export] public NodePath GridPath { get; set; } = new("");
        [Export] public NodePath NavigationPath { get; set; } = new("");
        [Export] public NodePath CollisionPath { get; set; } = new("");

        [ExportGroup("Renderers")]
        /// <summary>
        /// Each renderer is optional. A scene wires only the projections it
        /// offers, and a missing one simply cannot be selected - rather than
        /// being half-drawn, which is what a shared "draw everything" path does
        /// when a renderer is absent.
        /// </summary>
        [Export] public NodePath PaintedRendererPath { get; set; } = new("");
        [Export] public NodePath TileRendererPath { get; set; } = new("");
        [Export] public NodePath IsometricRendererPath { get; set; } = new("");
        [Export] public NodePath IsometricAutotileRendererPath { get; set; } = new("");
        [Export] public Godot.Collections.Array<NodePath> StructureLayerPaths { get; set; } = new();
        [Export] public NodePath FeaturesPath { get; set; } = new("");
        [Export] public NodePath IsometricFeaturesPath { get; set; } = new("");
        [Export] public NodePath MapOverlayPath { get; set; } = new("");

        /// <summary>
        /// Relief objects and resource icons, both placed through the gameplay
        /// grid and so drawn under every view that binds it: Painted, Tiles and
        /// IsometricAutotile.
        /// </summary>
        [Export] public NodePath ReliefRendererPath { get; set; } = new("");
        [Export] public NodePath ResourceRendererPath { get; set; } = new("");

        /// <summary>
        /// The invisible layers that make each cell queryable. Independent of
        /// which projection is on screen, so a game reading the map keeps working
        /// when the player switches view.
        /// </summary>
        [Export] public NodePath DataLayersPath { get; set; } = new("");

        /// <summary>
        /// Optional <c>Spawns</c> node (a Node2D) this world writes one Start_&lt;k&gt; marker into
        /// per player start, as docs/game-builder/TILEMAP_OUTPUT.md requires of a published map.
        /// Markers are ordinary scene nodes, so a map saved as a .tscn keeps its starts with no
        /// generator; GridStartAreaComponent.SpawnsRootPath reads them back.
        /// </summary>
        [Export] public NodePath SpawnsPath { get; set; } = new("");

        [ExportGroup("World")]
        [Export] public TerrainShape MapType { get; set; } = TerrainShape.Continents;
        [Export] public TerrainMapSize MapSize { get; set; } = TerrainMapSize.Standard;
        [Export] public TerrainWorldAge WorldAge { get; set; } = TerrainWorldAge.Mature;
        [Export] public TerrainTemperature Temperature { get; set; } = TerrainTemperature.Temperate;
        [Export] public TerrainRainfall Rainfall { get; set; } = TerrainRainfall.Normal;
        [Export] public TerrainSeaLevel SeaLevel { get; set; } = TerrainSeaLevel.Normal;
        [Export] public TerrainResourceLevel ResourceLevel { get; set; } = TerrainResourceLevel.Normal;
        [Export] public ResourceSet Resources { get; set; } = ResourceSet.Historical;
        [Export] public int Seed { get; set; } = 31415;

        /// <summary>
        /// Radius, in cells, of the area reserved around every player start; 0 leaves starts as
        /// single tiles. The kit an area is checked and stocked against is the generator's
        /// StartKit resource, which - like Resources' catalogue - is not part of the saved recipe.
        /// </summary>
        [Export(PropertyHint.Range, "0,32,1")] public int StartAreaRadius { get; set; }

        /// <summary>
        /// How much richer the underground grows away from the starts, 0 to 2 (FEAT-14). Part of the
        /// recipe: it changes every deposit's richness, so a world saved with it regenerates with it.
        /// Zero measures no distance at all.
        /// </summary>
        [Export(PropertyHint.Range, "0,2,0.05")] public float StartDistanceScaling { get; set; }

        [ExportGroup("Exact Recipe")]
        [Export] public bool UseCustomBounds { get; set; }
        [Export] public Vector2I CustomBounds { get; set; } = new(64, 64);
        [Export] public bool UseCustomLandCoverage { get; set; }
        /// <summary>Land footprint before inland lakes/rivers. Overrides the shape/sea-level preset.</summary>
        [Export(PropertyHint.Range, "0.05,0.92,0.01")] public float LandCoverage { get; set; } = 0.65f;
        [Export] public bool UseCustomClimateSpan { get; set; }
        /// <summary>Latitude range independent of tile resolution; zero is one latitude, one is planetary.</summary>
        [Export(PropertyHint.Range, "0,1,0.001")] public float ClimateLatitudeSpan { get; set; } = 0.12f;
        /// <summary>
        /// Take lake coverage from <see cref="LakeCoverage"/> instead of from Rainfall - and set that
        /// to zero for a world with NO LAKES.
        ///
        /// Lakes are otherwise derived: ApplyMapSetup writes LakeCoverage from the Rainfall axis
        /// (Arid 0.015, Normal 0.05, Wet 0.095) on every single build, so a value typed onto the
        /// generator through this path is overwritten before anything reads it. That leaves Rainfall
        /// as the only lever, and Rainfall moves lakes, rivers AND vegetation together - so there
        /// was no way to ask for a map with no lakes without also draining the rivers and stripping
        /// the woods. This is the same escape hatch the bounds, land coverage and climate span
        /// already have, and it leaves Rainfall owning the derivation wherever it is off.
        /// </summary>
        [Export] public bool UseCustomLakes { get; set; }
        /// <summary>Share of the map given to lakes when <see cref="UseCustomLakes"/> is on. Zero draws none.</summary>
        [Export(PropertyHint.Range, "0,0.35,0.01")] public float LakeCoverage { get; set; } = 0.05f;

        [ExportGroup("Drawing")]
        private TerrainProjection _projection = TerrainProjection.Painted;
        [Export] public TerrainProjection Projection
        {
            get => _projection;
            set
            {
                if (value != _projection && HasPendingTerrainEdits())
                { GD.PushWarning("Apply or discard terrain/structure edits before switching projection."); return; }
                _projection = value;
                RefreshStructureVisibility();
            }
        }

        public bool HasPendingTerrainEdits()
        {
            if (!IsInsideTree()) return false;
            foreach (NodePath path in new[] { TileRendererPath, IsometricAutotileRendererPath })
                if (!path.IsEmpty && GetNodeOrNull<Node>(path) is { } renderer && TerrainLibraryEditSession.Blocks(renderer)) return true;
            foreach (var layer in RegisteredStructureLayers())
                if (layer.HasPendingStructureEdits()) return true;
            return false;
        }
        /// <summary>Optional flat-map art and prop limits; isometric art is independently authored.</summary>
        [Export] public TerrainMapArt? MapArt { get; set; }
        [Export] public TerrainPropSizing? PropSizing { get; set; }

        /// <summary>
        /// How this world's sea looks, in every view that draws one. Unassigned, every view uses
        /// the shipped defaults - which is still ONE sea, not one per renderer (VIEW-04).
        /// </summary>
        [Export] public TerrainWaterLook? WaterLook { get; set; }

        /// <summary>
        /// Build a NEW world once the scene is ready. This is what lets a demo
        /// be a configured node rather than a controller script. Yields to a
        /// save: when a load has already restored the recipe by the time the
        /// deferred build runs, the build does not happen - a new world here
        /// would fill the cells the save is about to restore, or just did.
        /// </summary>
        [Export] public bool BuildOnReady { get; set; } = true;

        [ExportGroup("Save")]
        /// <summary>
        /// Whether this world joins the game's saveables. Off for a lab or a
        /// map viewer that has no game state to belong to.
        /// </summary>
        [Export] public bool ParticipatesInSave { get; set; } = true;

        /// <summary>The GameData key the recipe is saved under.</summary>
        [Export] public string SaveKey { get; set; } = "terrain_world.recipe";

        /// <summary>
        /// Generates the map IN THE EDITOR, and saves it with the scene.
        ///
        /// This is what makes the addon a map-authoring tool rather than only a
        /// runtime one: press it and the renderers build their layers as
        /// children of this scene, owned by it, so they persist. A developer can
        /// then hand-edit the result - move a tile, place a building - and keep
        /// it.
        ///
        /// BuildOnReady is deliberately NOT honoured in the editor: regenerating
        /// every time a scene is opened would be slow and would overwrite an
        /// authored map with a fresh one. This button is the design-time trigger,
        /// and until it existed every component was [Tool] and inert - loaded in
        /// the editor, showing settings that could not be applied to anything.
        /// </summary>
        [ExportToolButton("Generate map")]
        public Callable GenerateMap => Callable.From(NewWorld);

        /// <summary>Bumped when the saved recipe's shape changes.</summary>
        private const int RecipeVersion = 5;

        private bool _restoredFromSave;

        private TerrainGeneratorComponent? _generator;
        private TerrainPaintedRendererComponent? _painted;
        private TerrainTileRendererComponent? _tiles;
        private TerrainIsometricRendererComponent? _iso;
        private TerrainIsometricAutotileRendererComponent? _isometricAutotile;
        private TerrainFeatureRendererComponent? _features;
        private TerrainIsometricFeatureRendererComponent? _isometricFeatures;
        private TerrainMapOverlayComponent? _overlay;
        private TerrainReliefRendererComponent? _relief;
        private TerrainResourceRendererComponent? _resources;
        private TerrainDataLayersComponent? _dataLayers;

        /// <summary>The size of the world last built, in tiles.</summary>
        public Vector2I BuiltSize { get; private set; }

        public override void _Ready()
        {
            AddToGroup(StructureWorldGroup);
            RefreshStructureVisibility();
            if (Engine.IsEditorHint())
                return;

            if (ParticipatesInSave)
                AddToGroup(SaveableHelper.Group);
            if (BuildOnReady)
            {
                if (ParticipatesInSave) GameApp.Instance?.Saves?.BeginWorldLoad(this);
                CallDeferred(nameof(NewWorldOnReady));
            }
        }

        public override void _ExitTree()
        {
            RemoveFromGroup(StructureWorldGroup);
            RetireGeneration();
            if (ParticipatesInSave)
                RemoveFromGroup(SaveableHelper.Group);
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (RecipeError() is { } error) return new[] { error };
            if (GeneratorPath.IsEmpty)
                return new[] { "GeneratorPath should point to a TerrainGeneratorComponent." };
            if (ParticipatesInSave && string.IsNullOrWhiteSpace(SaveKey))
                return new[] { "SaveKey must not be empty while ParticipatesInSave is on." };
            return System.Array.Empty<string>();
        }

        /// <summary>
        /// The deferred BuildOnReady target. A save restored between _Ready and
        /// this call has already put the right world up; building a new one
        /// now would refill the cells that save restored.
        /// </summary>
        private void NewWorldOnReady()
        {
            var saves = ParticipatesInSave ? GameApp.Instance?.Saves : null;
            bool success = false;
            try
            {
                if (BuiltSize.X > 0 || _restoredFromSave || saves?.HasPendingSaveRecord(SaveKey) == true)
                {
                    success = true;
                    return;
                }
                NewWorld();
                success = BuiltSize.X > 0 && BuiltSize.Y > 0;
            }
            finally { saves?.CompleteWorldLoad(this, success); }
        }

        /// <summary>
        /// Generates a NEW world from the axes and seed, fills the grid's cells
        /// with it, and draws it in the configured projection. The generator
        /// acts as the map loader here - it writes the cells once, and from
        /// then on the cells are the map.
        ///
        /// The size comes from the size AXIS rather than from a renderer's own
        /// bounds, and is then pushed to every renderer - so the projections
        /// cannot end up drawing different extents of the same world.
        /// </summary>
        public void NewWorld()
        {
            if (HasPendingTerrainEdits()) { GD.PushWarning("Apply or discard terrain edits before generating a world."); return; }
            RetireGeneration();
            if (!ConfigureGenerator(out Vector2I size))
                return;

            _generator!.GenerateTerrain();
            Draw(size);
            BuiltSize = size;
            _builtRecipe = CaptureRecipe();

            EmitSignal(SignalName.WorldBuilt, size);
        }

        /// <summary>
        /// Regenerates the SAVED world from its recipe and draws it - the
        /// field, the data layers, every renderer - and writes nothing into
        /// the grid's cells. Those are the live map, restored by
        /// GridWorldStateComponent with the player's edits in them; painting
        /// the generator's original kinds back over them is exactly the bug
        /// this method exists not to have.
        ///
        /// Order-free with respect to the other saveables: the world, the grid
        /// state and the subsurface store each write disjoint stores, and the
        /// store reads the regenerated layers lazily, never inside its Load.
        /// </summary>
        public void RestoreWorld()
        {
            if (HasPendingTerrainEdits()) { GD.PushWarning("Apply or discard terrain edits before restoring a world."); return; }
            RetireGeneration();
            if (!ConfigureGenerator(out Vector2I size))
                return;

            // Regenerate recipe-only data without overwriting the live map.
            _generator!.ResolveField();
            Draw(size);
            BuiltSize = size;
            _builtRecipe = CaptureRecipe();

            EmitSignal(SignalName.WorldBuilt, size);
        }

        /// <summary>Switches or refreshes views without generating or replacing live cells.</summary>
        public void Redraw()
        {
            if (HasPendingTerrainEdits()) { GD.PushWarning("Apply or discard terrain edits before redrawing."); return; }
            Resolve();
            if (BuiltSize.X <= 0 || BuiltSize.Y <= 0 || !BindCellSource()) return;
            Draw(BuiltSize, rebuildRecipeData: false);
            EmitSignal(SignalName.WorldBuilt, BuiltSize);
        }

        private bool BindCellSource()
        {
            Node sourceOwner = CellDataPath.IsEmpty ? (Node?)_generator ?? this : this;
            NodePath sourcePath = CellDataPath.IsEmpty ? _generator?.CellDataPath ?? new NodePath("") : CellDataPath;
            var cells = sourcePath.IsEmpty ? null : sourceOwner.GetNodeOrNull<GridCellDataComponent>(sourcePath);
            if (!sourcePath.IsEmpty && cells is null)
            {
                GD.PushWarning($"[{Name}] live cell source '{sourcePath}' on '{sourceOwner.Name}' is missing; world build/redraw cancelled.");
                return false;
            }

            NodePath PathFrom(Node node) => cells is null ? new NodePath("") : node.GetPathTo(cells);
            if (_generator is not null && cells is not null) _generator.CellDataPath = PathFrom(_generator);
            if (_painted is not null) _painted.CellDataPath = PathFrom(_painted);
            if (_tiles is not null) _tiles.CellDataPath = PathFrom(_tiles);
            if (_iso is not null) _iso.CellDataPath = PathFrom(_iso);
            if (_isometricAutotile is not null) _isometricAutotile.CellDataPath = PathFrom(_isometricAutotile);
            if (_features is not null) _features.CellDataPath = PathFrom(_features);
            if (_relief is not null) _relief.CellDataPath = PathFrom(_relief);
            if (!CollisionPath.IsEmpty && GetNodeOrNull<TerrainCollisionComponent>(CollisionPath) is { } collision)
                collision.CellDataPath = PathFrom(collision);
            if (!NavigationPath.IsEmpty && GetNodeOrNull<GridNavigationComponent>(NavigationPath) is { } navigation)
                navigation.CellDataPath = PathFrom(navigation);
            return true;
        }

        /// <summary>
        /// Pushes the axes onto the generator. Shared by both doors, so a new
        /// world and a restored one are configured identically.
        ///
        /// FIVE MORE GENERATOR SETTINGS ARE OVERWRITTEN HERE, beyond the eleven
        /// ApplyMapSetup names on TerrainGeneratorComponent: BoundsSize, Seed,
        /// ResourceSet, and - the two that matter - UseClimateBiomeMaps and
        /// UseScaleRules are both forced to true unconditionally. Both carry
        /// their own doc comments describing them as an Inspector switch; both
        /// are, in fact, always on for any scene that builds its world through
        /// this component. MinBiomeRegionFraction is derived from scale rules.
        /// ClimateLatitudeSpan is also derived unless this world's saved
        /// UseCustomClimateSpan override supplies the geographic range.
        /// StartAreaRadius is this world's own recipe value, so the generator's
        /// Inspector radius is discarded for a world built here; the generator's
        /// StartKit is not touched. StartDistanceScaling is the same: this world's
        /// recipe value replaces the generator's own.
        ///
        /// LakeCoverage is derived by ApplyMapSetup from Rainfall, and is overwritten here after it
        /// only when this world's UseCustomLakes override supplies a share directly - which is the
        /// one way to ask for a map with no lakes without also draining its rivers and stripping its
        /// woods, since Rainfall moves all three together.
        /// </summary>
        private bool ConfigureGenerator(out Vector2I size)
        {
            Resolve();
            size = UseCustomBounds ? CustomBounds : TerrainMapSetup.BoundsFor(MapSize);
            if (RecipeError() is { } error)
            {
                GD.PushWarning($"[{Name}] {error} World generation cancelled.");
                return false;
            }
            if (_generator is null)
            {
                GD.PushWarning($"[{Name}] no TerrainGeneratorComponent at GeneratorPath; no world was created.");
                return false;
            }

            if (!BindCellSource()) return false;
            _generator.BoundsSize = size;
            _generator.Seed = Mathf.Max(0, Seed);
            _generator.ApplyMapSetup(
                (int)MapType, (int)WorldAge, (int)Temperature,
                (int)Rainfall, (int)SeaLevel, (int)ResourceLevel);
            _generator.ResourceSet = Resources;
            _generator.StartAreaRadius = Mathf.Clamp(StartAreaRadius, 0, 32);
            _generator.StartDistanceScaling = StartDistanceScaling;
            if (UseCustomLandCoverage) _generator.LandmassScale = LandCoverage;
            // AFTER ApplyMapSetup, which derives LakeCoverage from Rainfall and would overwrite this.
            if (UseCustomLakes) _generator.LakeCoverage = Mathf.Clamp(LakeCoverage, 0.0f, 0.35f);

            // The climate model and the scale rules are what make the axes mean
            // what TerrainMapSetup says they mean; a world built without them
            // would answer to the same dials differently. See the doc comment
            // above: this is a documented, unconditional override.
            _generator.UseClimateBiomeMaps = true;
            _generator.UseScaleRules = true;
            _generator.UseCustomClimateSpan = UseCustomClimateSpan;
            if (UseCustomClimateSpan) _generator.ClimateLatitudeSpan = ClimateLatitudeSpan;
            return true;
        }

        private string? RecipeError()
        {
            if (UseCustomClimateSpan && (!float.IsFinite(ClimateLatitudeSpan) || ClimateLatitudeSpan < 0 || ClimateLatitudeSpan > 1))
                return "ClimateLatitudeSpan must be finite and between zero and one.";
            if (UseCustomBounds && (CustomBounds.X < 1 || CustomBounds.Y < 1))
                return "CustomBounds must have positive width and height.";
            if (UseCustomLandCoverage && (!float.IsFinite(LandCoverage) || LandCoverage < 0.05f || LandCoverage > 0.92f))
                return "LandCoverage must be finite and between 0.05 and 0.92.";
            if (!float.IsFinite(StartDistanceScaling) || StartDistanceScaling < 0f || StartDistanceScaling > 2f)
                return "StartDistanceScaling must be finite and between zero and two.";
            return null;
        }

        // ---- the recipe, saved ------------------------------------------------

        /// <summary>
        /// The recipe: the world axes and the seed, plus the size they produced
        /// as a check. Not the layers - eight times ten thousand derivable
        /// tiles - and not the generator's forty-field settings record, which
        /// ConfigureGenerator overwrites from these anyway. The axes are the
        /// authored document; everything else is derived from them.
        /// </summary>
        public Godot.Collections.Dictionary CaptureState() => _builtRecipe?.Duplicate(true) ?? CaptureRecipe();

        private Godot.Collections.Dictionary CaptureRecipe() => new()
        {
            ["version"] = RecipeVersion,
            ["map_type"] = (int)MapType,
            ["map_size"] = (int)MapSize,
            ["use_custom_bounds"] = UseCustomBounds,
            ["custom_bounds"] = CustomBounds,
            ["use_custom_land_coverage"] = UseCustomLandCoverage,
            ["land_coverage"] = LandCoverage,
            ["use_custom_climate_span"] = UseCustomClimateSpan,
            ["climate_latitude_span"] = ClimateLatitudeSpan,
            ["world_age"] = (int)WorldAge,
            ["temperature"] = (int)Temperature,
            ["rainfall"] = (int)Rainfall,
            ["sea_level"] = (int)SeaLevel,
            ["resource_level"] = (int)ResourceLevel,
            ["resources"] = (int)Resources,
            ["seed"] = Seed,
            ["start_area_radius"] = StartAreaRadius,
            ["start_distance_scaling"] = StartDistanceScaling,
            ["built_size"] = BuiltSize,
        };

        /// <summary>
        /// Reads a saved recipe into the axes and regenerates that world - see
        /// <see cref="RestoreWorld"/> for what it does and does not touch. Marks
        /// the world as restored so a still-pending BuildOnReady stands down.
        /// </summary>
        public void RestoreState(Godot.Collections.Dictionary state)
        {
            MapType = (TerrainShape)GridVariantReader.Int(state, "map_type", (int)MapType);
            MapSize = (TerrainMapSize)GridVariantReader.Int(state, "map_size", (int)MapSize);
            UseCustomBounds = GridVariantReader.Bool(state, "use_custom_bounds", false);
            CustomBounds = GridVariantReader.Vector2I(state, "custom_bounds", new Vector2I(64, 64));
            UseCustomLandCoverage = GridVariantReader.Bool(state, "use_custom_land_coverage", false);
            LandCoverage = GridVariantReader.Float(state, "land_coverage", 0.65f);
            UseCustomClimateSpan = GridVariantReader.Bool(state, "use_custom_climate_span", false);
            ClimateLatitudeSpan = GridVariantReader.Float(state, "climate_latitude_span", 0.12f);
            WorldAge = (TerrainWorldAge)GridVariantReader.Int(state, "world_age", (int)WorldAge);
            Temperature = (TerrainTemperature)GridVariantReader.Int(state, "temperature", (int)Temperature);
            Rainfall = (TerrainRainfall)GridVariantReader.Int(state, "rainfall", (int)Rainfall);
            SeaLevel = (TerrainSeaLevel)GridVariantReader.Int(state, "sea_level", (int)SeaLevel);
            ResourceLevel = (TerrainResourceLevel)GridVariantReader.Int(state, "resource_level", (int)ResourceLevel);
            Resources = (ResourceSet)GridVariantReader.Int(state, "resources", (int)Resources);
            Seed = GridVariantReader.Int(state, "seed", Seed);
            // Absent before recipe version 4, whose worlds had no start areas.
            StartAreaRadius = GridVariantReader.Int(state, "start_area_radius", 0);
            // Absent before recipe version 5, whose worlds scaled nothing by distance.
            StartDistanceScaling = GridVariantReader.Float(state, "start_distance_scaling", 0f);
            Vector2I savedSize = GridVariantReader.Vector2I(state, "built_size", Vector2I.Zero);

            _restoredFromSave = true;
            RestoreWorld();

            // The one thing worth checking after a regeneration: the same axes
            // must reproduce the same extent. If they do not, the meaning of a
            // size step changed since the save, and the restored cells no
            // longer line up with the world drawn under them.
            if (savedSize != Vector2I.Zero && savedSize != BuiltSize)
                GD.PushWarning($"[{Name}] the saved recipe was built at {savedSize} but reproduces {BuiltSize}; the restored cells and the regenerated world no longer share an extent.");
        }

        public void Save(GameBuilder.GameStateData state)
        {
            if (!string.IsNullOrWhiteSpace(SaveKey))
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

        /// <summary>The generator's own report on the world it just made.</summary>
        public Godot.Collections.Dictionary Diagnostics()
        {
            Resolve();
            return _generator?.GetGenerationDiagnostics() ?? new Godot.Collections.Dictionary();
        }

        /// <summary>
        /// One line describing the built world. Every figure comes from the
        /// generator's diagnostics, so a caller's status text cannot disagree
        /// with the map it labels - which is how one demo came to report
        /// continents while another reported landmasses for the same field.
        /// </summary>
        public string StatusLine()
        {
            Resolve();
            if (_generator is null)
                return string.Empty;

            Godot.Collections.Dictionary d = _generator.GetGenerationDiagnostics();
            return string.Format(
                "{0} x {1}  |  {2}  |  land {3:P0}  ocean {4:P0}  lakes {5:P0}  rivers {6:P1}"
                + "  |  {7} of {8} landmasses  |  {9} resources  {10} in water  {11} cells underground"
                + "  {12} of {13} starts  |  {14} ms",
                BuiltSize.X, BuiltSize.Y,
                LandformName(_generator.Landform),
                d["land_footprint_coverage"].AsSingle(),
                d["ocean_coverage"].AsSingle(),
                d["lake_coverage"].AsSingle(),
                d["river_coverage"].AsSingle(),
                d["land_component_count"].AsInt32(),
                d["requested_landmass_count"].AsInt32(),
                d["resource_count"].AsInt32(),
                d["liquid_resource_count"].AsInt32(),
                d["underground_cell_count"].AsInt32(),
                d["start_position_count"].AsInt32(),
                d["requested_start_position_count"].AsInt32(),
                d["generation_milliseconds"].AsInt64()) + StartAreaStatus(d) + ViewStatus();
        }

        private static string StartAreaStatus(Godot.Collections.Dictionary d)
        {
            int areas = d["start_area_count"].AsInt32();
            return areas > 0 ? $"  |  areas {d["start_area_usable_count"].AsInt32()} of {areas} usable" : "";
        }

        private string ViewStatus() => ActiveViewProblem() is { } problem ? " | View incomplete: " + problem : "";

        /// <summary>Why the active tile view did not draw the world, or null when it did.</summary>
        private string? ActiveViewProblem()
        {
            Godot.Collections.Dictionary? report = Projection switch
            {
                TerrainProjection.Tiles => _tiles?.GetPaintDiagnostics(),
                TerrainProjection.IsometricAutotile => _isometricAutotile?.GetPaintDiagnostics(),
                _ => null
            };
            if (report is null || !report.TryGetValue("valid", out var valid) || valid.AsBool()) return null;
            if (report.TryGetValue("reason", out var reason)) return reason.AsString();
            return $"{report["missing"].AsInt32()} unmatched, {report["unmapped"].AsInt32()} unbound cells";
        }

        private static string LandformName(TerrainGeneratorComponent.LandformMode landform)
            => landform switch
            {
                TerrainGeneratorComponent.LandformMode.Island => "island",
                TerrainGeneratorComponent.LandformMode.Archipelago => "archipelago",
                _ => "mainland",
            };

        private void Resolve()
        {
            // Inspector path edits must also replace references to still-live nodes.
            _generator = ResolvePath<TerrainGeneratorComponent>(GeneratorPath);
            _painted = ResolvePath<TerrainPaintedRendererComponent>(PaintedRendererPath);
            _tiles = ResolvePath<TerrainTileRendererComponent>(TileRendererPath);
            _iso = ResolvePath<TerrainIsometricRendererComponent>(IsometricRendererPath);
            _isometricAutotile = ResolvePath<TerrainIsometricAutotileRendererComponent>(IsometricAutotileRendererPath);
            _features = ResolvePath<TerrainFeatureRendererComponent>(FeaturesPath);
            _isometricFeatures = ResolvePath<TerrainIsometricFeatureRendererComponent>(IsometricFeaturesPath);
            _overlay = ResolvePath<TerrainMapOverlayComponent>(MapOverlayPath);
            _relief = ResolvePath<TerrainReliefRendererComponent>(ReliefRendererPath);
            _resources = ResolvePath<TerrainResourceRendererComponent>(ResourceRendererPath);
            _dataLayers = ResolvePath<TerrainDataLayersComponent>(DataLayersPath);
        }

        /// <summary>Resolve the current path, including edits that point to another live node.</summary>
        private T? ResolvePath<T>(NodePath path) where T : Node
            => path.IsEmpty ? null : GetNodeOrNull<T>(path);
    }
}

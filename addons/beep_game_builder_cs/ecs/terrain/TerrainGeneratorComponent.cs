using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Builds one deterministic terrain field shared by gameplay and rendering.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TerrainGeneratorComponent : Node
    {
        public enum LandformMode
        {
            Mainland,
            Island,
            Archipelago
        }

        [Signal] public delegate void TerrainGeneratedEventHandler(int cellCount);

        [Export] public NodePath CellDataPath { get; set; } = new("");
        [Export] public bool GenerateOnReady { get; set; } = false;
        [Export] public bool GenerateInEditor { get; set; } = false;
        [Export] public bool ClearExistingCells { get; set; } = true;

        [ExportGroup("Map")]
        [Export] public Vector2I BoundsOrigin { get; set; } = Vector2I.Zero;
        [Export] public Vector2I BoundsSize { get; set; } = new(64, 64);
        [Export] public string DefaultTerrainKind { get; set; } = "grass";

        [ExportGroup("Generation")]
        [Export] public TerrainMode Mode { get; set; } = TerrainMode.ProceduralNoise;
        [Export] public TerrainPreset Preset { get; set; } = TerrainPreset.Grassland;
        [Export] public int Seed { get; set; } = 12345;

        [ExportGroup("Landform")]
        [Export] public LandformMode Landform { get; set; } = LandformMode.Mainland;
        [Export(PropertyHint.Range, "0.05,0.92,0.01")] public float LandmassScale { get; set; } = 0.70f;
        [Export(PropertyHint.Range, "2,12,1")] public int ArchipelagoIslandCount { get; set; } = 4;
        [Export(PropertyHint.Range, "2,24,1")] public int TopologySamplesPerCell { get; set; } = 12;

        [ExportGroup("Noise")]
        [Export] public FastNoiseLite.NoiseTypeEnum NoiseType { get; set; } = FastNoiseLite.NoiseTypeEnum.Perlin;
        [Export] public FastNoiseLite.FractalTypeEnum FractalType { get; set; } = FastNoiseLite.FractalTypeEnum.Fbm;
        [Export] public float Frequency { get; set; } = 0.012f;
        [Export] public int Octaves { get; set; } = 5;
        [Export] public float Lacunarity { get; set; } = 2.0f;
        [Export] public float Gain { get; set; } = 0.48f;
        /// <summary>
        /// How hard running water cuts the land, 0 to 4. Zero leaves the height
        /// field exactly as the noise made it.
        ///
        /// The two halves of the erosion stage respond differently across the
        /// range: stream-power incision keeps growing with the dial, while the
        /// hillslope smoothing saturates at its maximum stable rate - its
        /// coefficient is bounded at 1, because past that point the pass would
        /// amplify high-frequency speckle instead of smoothing it.
        /// </summary>
        [Export(PropertyHint.Range, "0,4,0.05")] public float ErosionStrength { get; set; } = 1.0f;

        /// <summary>
        /// Width of the sand beach where land meets the OPEN SEA, in tiles.
        ///
        /// Zero disables ocean beaches. Positive widths are inward Euclidean
        /// offsets of the fine ocean contour, not a minimum whole-cell ring.
        /// This changes ground cover, not the land/water footprint or relief.
        /// </summary>
        [Export(PropertyHint.Range, "0,4,0.05")] public float BeachWidth { get; set; } = 1.0f;

        [ExportGroup("Feature Biome Coverage")]
        /// <summary>
        /// How fine woodland stands are, relative to the standard stand
        /// (TerrainFeatureStage.StandWavelengthTiles): 1 is the standard, 2 halves a stand's
        /// width, 0.5 doubles it. Stands are measured in tiles, so the same value makes woods
        /// of the same size on every map.
        /// </summary>
        [Export(PropertyHint.Range, "0.02,4,0.01")] public float FeatureFrequencyMultiplier { get; set; } = 1.0f;

        [ExportGroup("Lake Features")]
        [Export(PropertyHint.Range, "0,0.35,0.01")] public float LakeCoverage { get; set; } = 0.05f;
        [Export(PropertyHint.Range, "0.02,1,0.01")] public float LakeFrequencyMultiplier { get; set; } = 0.10f;
        /// <summary>
        /// Width of the sandy rim around a LAKE, in tiles.
        ///
        /// It shipped at zero, from when the rim had no implementation and turning it on would have
        /// changed maps tuned without it. The implementation has existed and been guarded since
        /// (terrain_lake_bank_probe drives 0, 0.25, 1 and 2), but the default stayed - so every
        /// generated map had lakes with no shore at all. Measured on the painted demo, seed 31415:
        /// seven lakes of 6 to 14 cells, and not one cell carrying a bank width. What that draws is
        /// water stopping dead against grass, inside the shading of the basin it sits in - which the
        /// owner reported on 2026-09-18 as a lake that "looks bad", a dark stain with a puddle in it.
        ///
        /// A lake reads as a lake because it has a shore. Two thirds of a tile is enough to draw one
        /// at a 64-pixel cell and stays well inside the widths the probe covers.
        /// </summary>
        [Export(PropertyHint.Range, "0,3,0.05")] public float LakeShoreWidth { get; set; } = 0.65f;

        [ExportGroup("River Features")]
        [Export(PropertyHint.Range, "0,4,0.05")] public float RiverDensity { get; set; } = 1.0f;
        /// <summary>
        /// A river's bank, as a multiple of that river's OWN width. Zero draws none.
        ///
        /// Not a width in tiles, because a river does not have one width: the carve already sizes
        /// every river from its flow accumulation (radius 1 to 3 samples - about 0.125 to 0.375
        /// tiles at eight samples a cell), so a proportion gives a trunk a broad bank and a
        /// headwater stream a thin one from a single number.
        ///
        /// Scale matters more than it looks. The ocean's BeachWidth is 1.0 TILE against a sea with
        /// no far side; a stream is a quarter of a tile across. Banking rivers at a lake's 0.65
        /// tiles - tried on 2026-09-18 - put five times the river's own width of sand around every
        /// thread of a dense network, and the map came out a desert holding two ponds. At 1.0 here a
        /// stream's bank is an eighth of the ocean's beach, which is the proportion its water is.
        /// </summary>
        [Export(PropertyHint.Range, "0,3,0.05")] public float RiverBankScale { get; set; } = 1.0f;

        [ExportGroup("Gameplay")]
        // Every optional layer is a dial rather than a separate on/off flag, so
        // there is one owner per setting: zero means the layer is not generated.
        [Export(PropertyHint.Range, "0,24,1")] public int StartPositionCount { get; set; } = 6;

        /// <summary>
        /// Cells reserved around each start as that player's area, 0 to 32. Zero generates no
        /// start areas and leaves start selection exactly as it was. Above zero a start must
        /// hold the kit's headquarters footprint, and each start gets a validated, kitted area
        /// (see TerrainStartAreaStage and GetStartAreaReports). Not derived by ApplyMapSetup.
        /// </summary>
        [Export(PropertyHint.Range, "0,32,1")] public int StartAreaRadius { get; set; }

        /// <summary>What every start area must provide. Empty uses the kit defaults with no resources.</summary>
        [Export] public TerrainStartKit? StartKit { get; set; }

        /// <summary>
        /// How strongly the far country is richer, 0 to 2 (FEAT-14). Above zero the generator measures
        /// every cell's distance to the nearest start (StartDistanceAt) and scales underground richness
        /// by it - from (1 - s/2) at a start to (1 + s/2) at the farthest cell. Zero measures nothing
        /// and leaves every deposit as laid. Not derived by ApplyMapSetup.
        /// </summary>
        [Export(PropertyHint.Range, "0,2,0.05")] public float StartDistanceScaling { get; set; }

        [Export(PropertyHint.Range, "0,4,0.05")] public float ResourceDensity { get; set; } = 1.0f;

        /// <summary>
        /// Which catalogue resources are drawn from. A map is a setting, and the
        /// setting decides what is worth extracting - a lunar survey has no
        /// cattle, an oilfield no ivory.
        /// </summary>
        [Export] public ResourceSet ResourceSet { get; set; } = ResourceSet.Historical;

        /// <summary>
        /// The resource set this world uses, shared with the game. Assign one
        /// and both the map and the economy read it: the generator to decide
        /// what ground holds what, a resource node to know what a deposit is
        /// worth. Left empty, ResourceSet picks a shipped catalog.
        /// </summary>
        [Export] public ResourceCatalog? Resources { get; set; }

        /// <summary>
        /// Multiplier on vegetation, 0 to 4. One grows the woodland share shipped strategy maps
        /// grow at normal rainfall, 18% of land (TerrainFeatureStage), and the oasis chance scales
        /// by the same factor. Zero grows no features at all, jungle and marsh included.
        /// ApplyMapSetup derives it from Rainfall (TerrainMapSetup.VegetationScaleFor).
        /// </summary>
        [Export(PropertyHint.Range, "0,4,0.05")] public float FeatureDensity { get; set; } = 1.0f;

        [ExportGroup("Relief")]
        [Export(PropertyHint.Range, "0,0.9,0.01")] public float HillsFraction { get; set; } = 0.16f;
        [Export(PropertyHint.Range, "0,0.9,0.01")] public float MountainsFraction { get; set; } = 0.07f;
        [Export(PropertyHint.Range, "0,3,0.05")] public float HillshadeStrength { get; set; } = 1.0f;

        [ExportGroup("Climate Maps")]
        [Export] public bool UseClimateBiomeMaps { get; set; } = false;

        /// <summary>
        /// Smoothing passes that pull the rainfall biomes into coherent regions.
        ///
        /// The biome table classifies each sample on its own, so wherever
        /// rainfall wanders over a threshold it leaves a lone tile of something
        /// else. A painter blends that away; a tilemap draws it as confetti.
        /// Zero is off - existing scenes keep the map they were tuned against.
        /// One or two is a place; many erodes everything toward a single kind.
        /// </summary>
        [Export(PropertyHint.Range, "0,6,1")] public int BiomeCoherencePasses { get; set; }

        /// <summary>
        /// Derive the climate window and the minimum biome region from the map's
        /// own size, instead of setting them by hand.
        ///
        /// See TerrainScaleRules. A map covers the climate range its height can
        /// plausibly span, and a biome must reach a minimum size in tiles to
        /// survive - so the number of biomes falls out of the area. A small
        /// island gets one climate and one or two biomes; a full-size map gets
        /// pole to pole and many. Off by default so existing scenes keep the map
        /// they were tuned against. MinBiomeRegionFraction is derived while it
        /// is on; ClimateLatitudeSpan is derived unless UseCustomClimateSpan is on.
        /// </summary>
        [Export] public bool UseScaleRules { get; set; }

        /// <summary>Use an explicit geographic span while retaining biome region scale rules.</summary>
        [Export] public bool UseCustomClimateSpan { get; set; }

        /// <summary>
        /// Smallest a biome region may be, as a fraction of the land.
        ///
        /// The constraint that stops a small map holding every climate at once,
        /// and the way Civilization does it: not a cap on how many biomes a map
        /// may have, but a minimum size each must reach, stated relative to the
        /// landmass. A continent has room for several; an island does not, so it
        /// ends up with one or two - the count falls out of the area rather than
        /// being declared. Anything short is handed to the biome bordering it
        /// most. Zero is off.
        /// </summary>
        [Export(PropertyHint.Range, "0,0.5,0.01")] public float MinBiomeRegionFraction { get; set; }

        /// <summary>
        /// How many of a cell's eight neighbours must share its kind for it to
        /// keep it. Higher smooths harder.
        /// </summary>
        [Export(PropertyHint.Range, "1,8,1")] public int BiomeCoherenceKeep { get; set; } = 3;

        /// <summary>
        /// Guaranteed ocean ring at the map edge, in tiles.
        ///
        /// One tile is enough to keep land off the border, but not enough to
        /// keep the border out of sight: water that close to a coast is still
        /// shallow, so it is drawn see-through and shows its bed - while
        /// everything past the map is opaque open sea by definition. The two
        /// meet in a hard step, dead straight because the map edge is straight.
        /// The margin wants to be wider than the distance over which water
        /// closes to opaque, so the boundary always falls under opaque sea.
        /// </summary>
        [Export(PropertyHint.Range, "0,16,0.5")] public float OceanMarginTiles { get; set; } = 1.0f;

        /// <summary>
        /// Weight on the fractal relative to the radial falloff: how ragged the
        /// coastline is. The falloff sets the overall landmass, the fractal
        /// breaks its outline up. Too low and the coast relaxes onto the bare
        /// ellipse - and a coast that runs straight along a grid axis for
        /// several tiles draws every block's side at the same screen height,
        /// which merges their bottom edges into one hard unbroken line.
        /// </summary>
        [Export(PropertyHint.Range, "0,4,0.05")] public float CoastlineRaggedness { get; set; } = 1.45f;

        /// <summary>
        /// How much altitude cools a tile, as a fraction of the temperature
        /// range. Real, but it acts on ELEVATION while terrain is drawn from
        /// discrete relief - so a high but flat plateau can be cooled into snow
        /// and then drawn as flat white ground beside grassland. Turn it down on
        /// a map whose highlands should stay green.
        /// </summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float AltitudeCooling { get; set; } = 0.35f;

        /// <summary>
        /// How much of the pole-to-equator range the map covers. One is a whole
        /// world; anything less is a region, and a small map wants to BE a
        /// region - otherwise fifty tiles are asked to hold every climate on
        /// the planet and the result is an island with an ice cap and a jungle.
        /// </summary>
        [Export(PropertyHint.Range, "0,1,0.001")] public float ClimateLatitudeSpan { get; set; } = 1.0f;

        /// <summary>
        /// Where that band sits: 0 is the equator, 1 a pole. Only read when the
        /// span is under one.
        /// </summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float ClimateLatitudeCentre { get; set; } = 0.55f;

        [Export(PropertyHint.Range, "0.1,4,0.01")] public float TemperatureFrequencyMultiplier { get; set; } = 0.72f;
        [Export(PropertyHint.Range, "0.1,4,0.01")] public float MoistureFrequencyMultiplier { get; set; } = 1.35f;

        private GridCellDataComponent? _cells;
        private TerrainGenerationSettings? _fieldSettings;
        private GeneratedTerrainField? _field;

        public Vector2I EffectiveBoundsSize => new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));

        public override void _Ready()
        {
            ResolveReferences();
            UpdateConfigurationWarnings();
            if (GenerateOnReady && (!Engine.IsEditorHint() || GenerateInEditor))
                CallDeferred(nameof(GenerateTerrain));
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (CellDataPath.IsEmpty)
                return new[] { "CellDataPath should point to a GridCellDataComponent." };
            if (BoundsSize.X <= 0 || BoundsSize.Y <= 0)
                return new[] { "BoundsSize must be greater than zero." };
            return Array.Empty<string>();
        }

        /// <summary>
        /// Generates the field for the current settings and, where a
        /// GridCellDataComponent is wired, loads it with the result - the map
        /// loader's one write. Returns how many cells were written: zero with
        /// no cells wired, which is a legitimate shape (a map viewer, a lab
        /// with no grid) and not a failure - the field is generated and every
        /// renderer draws from it regardless. Restoring a saved world does not
        /// come through here at all; TerrainWorldComponent.RestoreWorld
        /// resolves the field without touching the cells.
        /// </summary>
        public int GenerateTerrain()
        {
            ResolveReferences();
            TerrainGenerationSettings settings = CurrentSettings();
            GeneratedTerrainField field = FieldFor(settings);
            if (_cells == null)
            {
                EmitSignal(SignalName.TerrainGenerated, 0);
                return 0;
            }

            int generated = _cells.LoadGeneratedCells(GeneratedCells(settings, field), ClearExistingCells, GridIds.NormalizeOr(DefaultTerrainKind, "grass"));
            EmitSignal(SignalName.TerrainGenerated, generated);
            return generated;
        }

        // Stream the typed handoff: retaining a second map-sized list is not
        // necessary when the cell store consumes every record exactly once.
        private static IEnumerable<(Vector2I Cell, string Terrain, string Feature, int Relief, float Shade, float Elevation, string WaterSource, GridTerrainWaterPatch WaterPatch, string InlandTerrain, float BeachWidth, GridTerrainWaterPatch? LakePatch, float LakeWidth, int StartArea)> GeneratedCells(
            TerrainGenerationSettings settings, GeneratedTerrainField field)
        {
            for (int y = 0; y < settings.Size.Y; y++)
            {
                for (int x = 0; x < settings.Size.X; x++)
                {
                    yield return (
                        settings.Origin + new Vector2I(x, y),
                        field.TerrainAtCell(new Vector2I(x, y)),
                        field.FeatureAtCell(new Vector2I(x, y)),
                        (int)field.ReliefAtCell(new Vector2I(x, y)),
                        field.ShadeAtPosition(new Vector2(x + 0.5f, y + 0.5f)),
                        field.ElevationAtCell(new Vector2I(x, y)),
                        field.WaterSourceAtCell(new Vector2I(x, y)),
                        field.WaterPatchAtCell(new Vector2I(x, y)),
                        field.InlandTerrainAtCell(new Vector2I(x, y)), field.BeachWidth,
                        field.LakeShoreWidth > 0f ? field.LakePatchAtCell(new Vector2I(x, y)) : null,
                        // Every shore, not only the flat ones: the shoreline stage banks inland
                        // water whatever the ground behind it does, and this width is what the
                        // painted view draws that bank with.
                        //
                        // PER CELL, not the map-wide LakeShoreWidth it used to send: a river's bank
                        // is sized from that river's own flow, so a trunk edges wider than the
                        // stream feeding it, and a lake keeps the single width a lake shore has.
                        field.ShoreWidthAtCell(new Vector2I(x, y)),
                        field.StartAreaAtCell(new Vector2I(x, y)));
                }
            }

        }

        // NOTE: these are deliberately NOT overloads. Godot exposes script
        // methods by name and keeps only one method per name, so an overloaded
        // public API silently becomes unreachable from GDScript.
        public string TerrainKindAt(Vector2I localCell)
            => FieldFor(CurrentSettings()).TerrainAtCell(localCell);

        /// <summary>The LIQUID-stratum resource in a water tile, or empty.</summary>
        public string LiquidResourceAt(Vector2I localCell)
            => FieldFor(CurrentSettings()).LiquidResourceAtCell(localCell);

        /// <summary>The UNDERGROUND resource beneath a tile, or empty.</summary>
        public string UndergroundResourceAt(Vector2I localCell)
            => FieldFor(CurrentSettings()).UndergroundResourceAtCell(localCell);

        /// <summary>Underground richness 0..1 where a deposit exists, else 0.</summary>
        public float UndergroundRichnessAt(Vector2I localCell)
            => FieldFor(CurrentSettings()).UndergroundRichnessAtCell(localCell);

        /// <summary>Underground depth band, as (int)ResourceDepth.</summary>
        public int UndergroundDepthAt(Vector2I localCell)
            => FieldFor(CurrentSettings()).UndergroundDepthAtCell(localCell);

        public string TerrainKindAtPosition(Vector2 localPosition)
            => FieldFor(CurrentSettings()).TerrainAtPosition(localPosition);

        public string WaterSourceAt(Vector2I localCell)
            => FieldFor(CurrentSettings()).WaterSourceAtCell(localCell);

        /// <summary>
        /// THE LAYER PLAN: the terrain kinds this map actually contains, in the
        /// order they are drawn - by TerrainLayers level, then by the canonical
        /// kind order.
        ///
        /// The engine decides the layers, not the renderers. Each renderer used
        /// to work this out for itself: the tile view carried a hard-coded list
        /// of thirteen biomes and built a layer for every one whether the map had
        /// it or not, the isometric autotile view gathered its own set, and the
        /// data layers scanned the map again. Three answers to one question, and
        /// only the map knows the answer.
        ///
        /// A renderer takes this and materialises it however its projection
        /// needs - the flat views one layer per kind, because Godot's own
        /// guidance is that terrains on a shared layer erase each other, and the
        /// isometric view one layer per level, because there the stack IS the
        /// height. Same plan, different materialisation.
        /// </summary>
        public Godot.Collections.Array<string> TerrainKindsPresent()
        {
            TerrainGenerationSettings settings = CurrentSettings();
            GeneratedTerrainField field = FieldFor(settings);

            var present = new HashSet<string>(StringComparer.Ordinal);
            for (int y = 0; y < settings.Size.Y; y++)
            {
                for (int x = 0; x < settings.Size.X; x++)
                {
                    string kind = field.TerrainAtCell(new Vector2I(x, y));
                    if (kind.Length > 0)
                        present.Add(kind);
                }
            }

            var ordered = new Godot.Collections.Array<string>();
            foreach (string kind in TerrainTileSets.Kinds)
            {
                if (present.Contains(kind))
                    ordered.Add(kind);
            }
            // Anything the map holds that the canonical list does not name still
            // needs a layer, or it would silently go undrawn.
            foreach (string kind in present)
            {
                if (!ordered.Contains(kind))
                    ordered.Add(kind);
            }
            return ordered;
        }

        /// <summary>
        /// The TerrainLayers levels this map actually uses, ascending. A view
        /// that stacks by height builds from this rather than assuming the whole
        /// range exists.
        /// </summary>
        public Godot.Collections.Array<int> TerrainLevelsPresent()
        {
            var levels = new SortedSet<int>();
            foreach (string kind in TerrainKindsPresent())
                levels.Add(TerrainLayers.LevelForKind(kind));

            var ordered = new Godot.Collections.Array<int>();
            foreach (int level in levels)
                ordered.Add(level);
            return ordered;
        }

        /// <summary>
        /// True where the position is ocean or lake. Prop placement asks this
        /// directly rather than inferring it from a terrain kind, so a new water
        /// terrain kind can never quietly become somewhere plants may grow.
        /// </summary>
        public bool IsWaterAtPosition(Vector2 localPosition)
            => FieldFor(CurrentSettings()).IsWaterAtPosition(localPosition);

        /// <summary>
        /// Fraction of the area around a position that is water, 0 to 1. The
        /// renderer fades the water edge with this so a coastline is a curve
        /// rather than a staircase of sample-sized steps.
        /// </summary>
        public float WaterFractionAt(Vector2 localPosition)
            => FieldFor(CurrentSettings()).WaterFractionAtPosition(localPosition);

        /// <summary>
        /// Relief shading for the painted base colour, 1 being unlit. Lets the
        /// painter show hills without the biome having to encode them.
        /// </summary>
        public float ShadeAtPosition(Vector2 localPosition)
            => FieldFor(CurrentSettings()).ShadeAtPosition(localPosition);

        public float ShadeAtCell(Vector2I localCell)
            => FieldFor(CurrentSettings()).ShadeAtPosition(new Vector2(localCell.X + 0.5f, localCell.Y + 0.5f));

        /// <summary>Flat, hills or mountains, per gameplay tile.</summary>
        public int ReliefAt(Vector2I localCell)
            => (int)FieldFor(CurrentSettings()).ReliefAtCell(localCell);

        /// <summary>
        /// Land height at a tile, 0 to 1, with water at 0.
        ///
        /// Relief says which BAND a tile is in - flat, hills, mountains - and
        /// every tile of a range shares one band, so a renderer working from
        /// relief alone can only draw a range flat-topped. Height is what says
        /// which part of that range is its crest.
        /// </summary>
        public float ElevationAt(Vector2I localCell)
            => FieldFor(CurrentSettings()).ElevationAtCell(localCell);

        /// <summary>
        /// Base colour at a position, interpolated between neighbouring terrain
        /// samples so biome boundaries are not drawn as field-sized blocks. The
        /// caller supplies the terrain-kind to colour mapping, which stays the
        /// renderer's concern.
        /// </summary>
        public Color BlendedColourAt(Vector2 localPosition, Func<string, Color> colourFor)
            => FieldFor(CurrentSettings()).BlendedBaseColour(localPosition, colourFor);

        /// <summary>
        /// Which landmass a cell belongs to; 0 is water. Two land cells sharing
        /// an id are reachable without crossing water.
        /// </summary>
        public int ContinentAt(Vector2I localCell)
            => FieldFor(CurrentSettings()).ContinentAtCell(localCell);

        /// <summary>The resource on a cell, or empty where there is none.</summary>
        public string ResourceAt(Vector2I localCell)
            => FieldFor(CurrentSettings()).ResourceAtCell(localCell);

        /// <summary>
        /// The terrain feature on a tile - "woods", "jungle", "marsh", "oasis" -
        /// or empty. A feature sits on the terrain rather than replacing it.
        /// </summary>
        public string FeatureAt(Vector2I localCell)
            => FieldFor(CurrentSettings()).FeatureAtCell(localCell);

        /// <summary>Fair player start tiles, in gameplay cell coordinates.</summary>
        public Godot.Collections.Array<Vector2I> GetStartPositions()
        {
            var positions = new Godot.Collections.Array<Vector2I>();
            foreach (Vector2I cell in FieldFor(CurrentSettings()).StartPositions)
                positions.Add(cell);
            return positions;
        }

        /// <summary>Which start's reserved area a cell is in: 0 none, k+1 start k (StartAreaRadius above zero).</summary>
        public int StartAreaAt(Vector2I localCell)
            => FieldFor(CurrentSettings()).StartAreaAtCell(localCell);

        /// <summary>
        /// A cell's distance to the nearest start, in whole cells, water included - or -1 when this
        /// map measured none (StartDistanceScaling zero, or no starts). What a game reads to make the
        /// far country more dangerous as well as richer.
        /// </summary>
        public int StartDistanceAt(Vector2I localCell)
            => FieldFor(CurrentSettings()).StartDistanceAtCell(localCell);

        /// <summary>
        /// What the start kit's Neutral entries placed between the starts: placements (resource, cell,
        /// relaxation) and problems. Both empty when the kit has no Neutral entry. Cells are generator-local.
        /// </summary>
        public Godot.Collections.Dictionary GetNeutralSiteReport()
            => FieldFor(CurrentSettings()).NeutralSites.ToDictionary();

        /// <summary>
        /// One Dictionary per start, in start order - index, origin, footprint, cell_count, exits,
        /// placements (resource, cell, relaxation), problems and usable. Empty without start areas.
        /// Cells are generator-local, as GetStartPositions returns them.
        /// </summary>
        public Godot.Collections.Array<Godot.Collections.Dictionary> GetStartAreaReports()
        {
            var reports = new Godot.Collections.Array<Godot.Collections.Dictionary>();
            foreach (TerrainStartAreaReport report in FieldFor(CurrentSettings()).StartAreas)
                reports.Add(report.ToDictionary());
            return reports;
        }

        /// <summary>
        /// Configures the generator from a map type and the five climate axes.
        ///
        /// This is the ONE place that turns a chosen world into generator
        /// settings. It used to live in the lab controller, which meant any
        /// other caller - a game, a test - had to reproduce the same thirty
        /// lines and would drift from it. The axes are applied on top of the
        /// shape's own values, so a map type stays recognisable whatever the
        /// weather is set to.
        ///
        /// Indices rather than enums so GDScript can call it.
        /// </summary>
        /// <summary>
        /// Copies the world axes onto this generator's own settings.
        ///
        /// THESE ELEVEN SETTINGS ARE DERIVED, and a developer needs to know it:
        /// Landform, ArchipelagoIslandCount, StartPositionCount, LandmassScale,
        /// HillsFraction, MountainsFraction, ClimateLatitudeCentre, LakeCoverage,
        /// RiverDensity, FeatureDensity and ResourceDensity are all overwritten here. TerrainWorldComponent.Build calls this every time, so a value
        /// typed into the Inspector for any of them is replaced before it is ever
        /// read - accepted, stored, and quietly discarded.
        ///
        /// They stay exported because a game may drive this generator directly,
        /// without the axes, and then they are the only way to say what a map
        /// should be. The rule is simply which owner is in play: set the AXES on
        /// TerrainWorldComponent, or set these and do not use that component.
        /// Setting both means the axes win.
        /// </summary>
        public void ApplyMapSetup(
            int mapType, int worldAge, int temperature, int rainfall, int seaLevel, int resources)
        {
            TerrainShapeDefinition shape = TerrainShapePresets.Get(
                (TerrainShape)Mathf.Clamp(mapType, 0, TerrainShapePresets.Order.Length - 1));

            var age = (TerrainWorldAge)Mathf.Clamp(worldAge, 0, 2);
            var warmth = (TerrainTemperature)Mathf.Clamp(temperature, 0, 2);
            var rain = (TerrainRainfall)Mathf.Clamp(rainfall, 0, 2);
            var seas = (TerrainSeaLevel)Mathf.Clamp(seaLevel, 0, 2);
            var resourceLevel = (TerrainResourceLevel)Mathf.Clamp(resources, 0, 2);

            Landform = shape.Landform;
            ArchipelagoIslandCount = shape.IslandCount;
            StartPositionCount = shape.StartPositions;
            LandmassScale = Mathf.Clamp(
                shape.LandCoverage * TerrainMapSetup.LandScaleFor(seas), 0.05f, 0.92f);

            // World age is relief: a young world keeps its mountains, an old one
            // is worn flat.
            float relief = TerrainMapSetup.ReliefScaleFor(age);
            HillsFraction = Mathf.Clamp(shape.HillsFraction * relief, 0.0f, 0.9f);
            MountainsFraction = Mathf.Clamp(shape.MountainsFraction * relief, 0.0f, 0.9f);

            // Temperature moves the map's latitude band rather than sprinkling
            // snow on it, so a cold world is genuinely at a high latitude.
            ClimateLatitudeCentre = TerrainMapSetup.LatitudeCentreFor(warmth);

            // Rainfall is water and growth, the way Civilization scopes it: lakes,
            // rivers and vegetation. It does not touch the ground - desert belongs to
            // the latitude (TerrainClimateStage), and a flat moisture shift for
            // Rainfall turned a map that was dry to begin with entirely to desert.
            float wet = TerrainMapSetup.WaterScaleFor(rain);
            LakeCoverage = Mathf.Clamp(0.05f * wet, 0.0f, 0.35f);
            RiverDensity = Mathf.Clamp(1.0f * wet, 0.0f, 4.0f);
            FeatureDensity = Mathf.Clamp(TerrainMapSetup.VegetationScaleFor(rain), 0.0f, 4.0f);

            ResourceDensity = Mathf.Clamp(
                TerrainMapSetup.ResourceScaleFor(resourceLevel), 0.0f, 4.0f);
        }

        public Godot.Collections.Dictionary GetGenerationDiagnostics()
            => FieldFor(CurrentSettings()).Diagnostics.ToDictionary();

        /// <summary>
        /// The generated field for the current settings, resolved once.
        ///
        /// Every per-position accessor on this component rebuilds the settings
        /// record just to find this same field, and that record is assembled
        /// from around forty Godot property reads and then compared field by
        /// field. That is far more expensive than the sample it guards, so a
        /// renderer walking millions of pixels must hold the field rather than
        /// call back through the component for each one.
        /// </summary>
        internal GeneratedTerrainField ResolveField() => FieldFor(CurrentSettings());

        internal TerrainGenerationSettings CaptureGenerationSettings() => CurrentSettings();

        internal Godot.Collections.Dictionary CaptureConfiguration()
        {
            var configuration = new Godot.Collections.Dictionary();
            foreach (Godot.Collections.Dictionary property in GetPropertyList())
            {
                var usage = (PropertyUsageFlags)property["usage"].AsInt64();
                if ((usage & (PropertyUsageFlags.ScriptVariable | PropertyUsageFlags.Storage)) !=
                    (PropertyUsageFlags.ScriptVariable | PropertyUsageFlags.Storage)) continue;
                StringName name = property["name"].AsStringName();
                configuration[name] = Get(name);
            }
            return configuration;
        }

        internal void ApplyConfiguration(Godot.Collections.Dictionary configuration)
        {
            foreach (Variant name in configuration.Keys) Set(name.AsStringName(), configuration[name]);
        }

        internal GridCellDataComponent.GeneratedPublication? PreparePublication(TerrainGenerationSettings settings, GeneratedTerrainField field)
        {
            ResolveReferences();
            return _cells is null ? null : new GridCellDataComponent.GeneratedPublication(_cells,
                GeneratedCells(settings, field), GridIds.NormalizeOr(DefaultTerrainKind, "grass"));
        }

        internal bool PublicationMatches(GridCellDataComponent.GeneratedPublication publication)
        {
            ResolveReferences();
            return publication.IsFor(_cells);
        }

        internal void PublishField(TerrainGenerationSettings settings, GeneratedTerrainField field,
            GridCellDataComponent.GeneratedPublication? publication = null)
        {
            _fieldSettings = settings;
            _field = field;
            if (publication is null) GenerateTerrain();
            else
            {
                publication.Commit();
                EmitSignal(SignalName.TerrainGenerated, publication.Loaded);
            }
        }

        private GeneratedTerrainField FieldFor(TerrainGenerationSettings settings)
        {
            if (_field is not null && _fieldSettings is { } cached && cached == settings)
                return _field;

            _field = TerrainFieldBuilder.Build(settings);
            _fieldSettings = settings;
            return _field;
        }

        private TerrainGenerationSettings CurrentSettings()
        {
            ResolveReferences();
            // Settings live HERE, on the generator. They were once read off a
            // RENDERER instead, behind a flag - two owners for one fact, with the
            // flag deciding which won, and a pipeline that could not compile
            // without a renderer present. A renderer draws what the generator
            // decided; it does not decide it.
            Vector2I size = EffectiveBoundsSize;

            // Region sizing remains automatic even when a game supplies the
            // geographic climate span independently of its tile resolution.
            TerrainScaleRules.Rules scale = UseScaleRules
                ? TerrainScaleRules.For(size, LandmassScale)
                : new TerrainScaleRules.Rules(
                    Mathf.Clamp(ClimateLatitudeSpan, 0.0f, 1.0f),
                    Mathf.Clamp(MinBiomeRegionFraction, 0.0f, 0.5f));

            return new TerrainGenerationSettings(
                BoundsOrigin, size, Mode, Preset, Seed, NoiseType, FractalType,
                Mathf.Max(0.0001f, Frequency), Mathf.Clamp(Octaves, 1, 10), Mathf.Max(1.0f, Lacunarity), Mathf.Clamp(Gain, 0.0f, 1.0f),
                Landform, Mathf.Clamp(LandmassScale, 0.05f, 0.92f),
                Mathf.Clamp(ArchipelagoIslandCount, 2, 12), Mathf.Clamp(TopologySamplesPerCell, 2, 24),
                Mathf.Clamp(ErosionStrength, 0.0f, 4.0f), Mathf.Clamp(BeachWidth, 0.0f, 4.0f),
                Mathf.Max(0.02f, FeatureFrequencyMultiplier),
                Mathf.Clamp(LakeCoverage, 0.0f, 0.35f), Mathf.Max(0.02f, LakeFrequencyMultiplier), Mathf.Clamp(LakeShoreWidth, 0.0f, 3.0f),
                Mathf.Clamp(RiverDensity, 0.0f, 4.0f), Mathf.Clamp(RiverBankScale, 0.0f, 3.0f),
                Mathf.Clamp(StartPositionCount, 0, 24),
                Mathf.Clamp(StartAreaRadius, 0, 32), StartKit, Mathf.Clamp(StartDistanceScaling, 0.0f, 2.0f),
                Mathf.Clamp(ResourceDensity, 0.0f, 4.0f), ResourceSet, Resources, Mathf.Clamp(HillsFraction, 0.0f, 0.9f),
                Mathf.Clamp(MountainsFraction, 0.0f, 0.9f), Mathf.Clamp(HillshadeStrength, 0.0f, 3.0f),
                Mathf.Clamp(FeatureDensity, 0.0f, 4.0f),
                UseClimateBiomeMaps,
                UseScaleRules,
                Mathf.Max(0, BiomeCoherencePasses),
                scale.MinRegionFraction,
                Mathf.Clamp(BiomeCoherenceKeep, 1, 8),
                Mathf.Max(0.0f, OceanMarginTiles),
                Mathf.Max(0.0f, CoastlineRaggedness),
                Mathf.Clamp(AltitudeCooling, 0.0f, 1.0f),
                UseCustomClimateSpan ? Mathf.Clamp(ClimateLatitudeSpan, 0.0f, 1.0f) : scale.LatitudeSpan,
                Mathf.Clamp(ClimateLatitudeCentre, 0.0f, 1.0f),
                Mathf.Max(0.1f, TemperatureFrequencyMultiplier), Mathf.Max(0.1f, MoistureFrequencyMultiplier));
        }

        private void ResolveReferences()
            => _cells = CellDataPath.IsEmpty ? null : GetNodeOrNull<GridCellDataComponent>(CellDataPath);

    }
}

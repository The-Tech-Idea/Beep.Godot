using Godot;
using System;

namespace Beep.ECS
{
    /// <summary>
    /// Builds one deterministic terrain field shared by gameplay and rendering.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridTerrainGeneratorComponent : Node
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
        [Export(PropertyHint.Range, "0,0.98,0.01")] public float SeaCoverage { get; set; } = 0.12f;
        /// <summary>
        /// Width of the sand beach where land meets the OPEN SEA, in tiles.
        ///
        /// It reads in tiles because that is the unit anyone thinks in, and
        /// because the previous rule - "land touching the sea" - was one SAMPLE
        /// wide, an eighth of a tile, which never survived the reduction to
        /// tiles. The setting existed, the rule existed, and the map had no
        /// beaches at all.
        /// </summary>
        [Export(PropertyHint.Range, "0,4,0.05")] public float BeachWidth { get; set; } = 1.0f;
        /// <summary>
        /// NOT ENFORCED. Nothing reads this: it is threaded into the settings
        /// record and no stage consults it.
        ///
        /// The fact it would own is already owned. Rock and snow are what the
        /// biome table gives a PEAK - see TerrainBiomeStage and the PeakKinds
        /// set in TerrainScaleConstraintStage - so where rock begins is decided
        /// by RELIEF, not by an elevation threshold. Wiring this up would put a
        /// second, disagreeing answer beside that one.
        ///
        /// Resolving the duplicate means dropping one of the two, which is
        /// Fahad's call, not the implementer's.
        /// </summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float RockLevel { get; set; } = 0.82f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float Dryness { get; set; } = 0.25f;

        [ExportGroup("Feature Biome Coverage")]
        /// <summary>
        /// NOT ENFORCED - and wiring it up would FIGHT the scale rules.
        ///
        /// How much swamp a map has is already decided, by rainfall and the
        /// moisture bands in TerrainBiomeStage, scaled through the Rainfall axis
        /// in TerrainMapSetup. A direct coverage dial would force a share of
        /// swamp regardless of climate or map size, which is precisely the
        /// "four environments in one small area" behaviour TerrainScaleRules and
        /// TerrainCoherenceStage exist to prevent.
        ///
        /// So this is not a missing implementation to build - it is a second
        /// owner of a fact the climate axes already own.
        /// </summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float SwampCoverage { get; set; } = 0.0f;
        /// <summary>
        /// NOT ENFORCED - and wiring it up would FIGHT the scale rules.
        ///
        /// Snow follows LATITUDE, through TerrainMapSetup.LatitudeCentreFor and
        /// the MinLatitudeSpan in TerrainScaleRules. Forcing a fixed share of
        /// snow would put an arctic patch on a small equatorial island, which is
        /// the exact case those rules were written to stop.
        /// </summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float SnowCoverage { get; set; } = 0.0f;
        /// <summary>
        /// NOT ENFORCED. Same owner as SnowCoverage: latitude decides ice.
        /// </summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float IceCoverage { get; set; } = 0.0f;
        [Export(PropertyHint.Range, "0.02,4,0.01")] public float FeatureFrequencyMultiplier { get; set; } = 0.18f;

        [ExportGroup("Lake Features")]
        [Export(PropertyHint.Range, "0,0.35,0.01")] public float LakeCoverage { get; set; } = 0.05f;
        [Export(PropertyHint.Range, "0.02,1,0.01")] public float LakeFrequencyMultiplier { get; set; } = 0.10f;
        /// <summary>
        /// Width of the sandy rim around a LAKE, in tiles. Zero by default: it
        /// had no implementation at all until now, and turning it on for every
        /// existing scene would change maps that were tuned without it.
        /// </summary>
        [Export(PropertyHint.Range, "0,3,0.05")] public float LakeShoreWidth { get; set; }

        [ExportGroup("River Features")]
        [Export(PropertyHint.Range, "0,4,0.05")] public float RiverDensity { get; set; } = 1.0f;

        [ExportGroup("Gameplay")]
        // Every optional layer is a dial rather than a separate on/off flag, so
        // there is one owner per setting: zero means the layer is not generated.
        [Export(PropertyHint.Range, "0,24,1")] public int StartPositionCount { get; set; } = 6;
        [Export(PropertyHint.Range, "0,4,0.05")] public float ResourceDensity { get; set; } = 1.0f;

        /// <summary>
        /// Which catalogue resources are drawn from. A map is a setting, and the
        /// setting decides what is worth extracting - a lunar survey has no
        /// cattle, an oilfield no ivory.
        /// </summary>
        [Export] public ResourceSet ResourceSet { get; set; } = ResourceSet.Historical;
        [Export(PropertyHint.Range, "0,4,0.05")] public float FeatureDensity { get; set; } = 1.0f;

        [ExportGroup("Relief")]
        [Export(PropertyHint.Range, "0,0.9,0.01")] public float HillsFraction { get; set; } = 0.16f;
        [Export(PropertyHint.Range, "0,0.9,0.01")] public float MountainsFraction { get; set; } = 0.07f;
        [Export(PropertyHint.Range, "0,3,0.05")] public float HillshadeStrength { get; set; } = 1.0f;

        [ExportGroup("Climate Maps")]
        [Export] public bool UseClimateBiomeMaps { get; set; } = false;

        /// <summary>
        /// Assign the rainfall biomes by QUOTA - a named fraction of the land
        /// each - instead of by fixed rainfall cutoffs.
        ///
        /// Fixed cutoffs read a noise field whose spread moves with map size,
        /// frequency and octaves, so the same numbers produce wildly different
        /// maps. Quotas are what Civilization uses and they hold their
        /// proportions on any map. Off by default so existing scenes keep the
        /// distribution they were tuned against.
        /// </summary>
        [Export] public bool UseBiomeQuotas { get; set; }

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
        /// they were tuned against; ClimateLatitudeSpan and
        /// MinBiomeRegionFraction are ignored while it is on.
        /// </summary>
        [Export] public bool UseScaleRules { get; set; }

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

        /// <summary>Driest fraction of the land that becomes desert.</summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float DesertFraction { get; set; } = 0.08f;

        /// <summary>The next-driest fraction, which becomes dry grassland.</summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float DryGrassFraction { get; set; } = 0.30f;

        /// <summary>Wettest fraction of the land that becomes swamp.</summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float SwampFraction { get; set; } = 0.04f;

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
        [Export(PropertyHint.Range, "0.05,1,0.01")] public float ClimateLatitudeSpan { get; set; } = 1.0f;

        /// <summary>
        /// Where that band sits: 0 is the equator, 1 a pole. Only read when the
        /// span is under one.
        /// </summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float ClimateLatitudeCentre { get; set; } = 0.55f;
        [Export(PropertyHint.Range, "0.1,4,0.01")] public float TemperatureFrequencyMultiplier { get; set; } = 0.72f;
        [Export(PropertyHint.Range, "0.1,4,0.01")] public float MoistureFrequencyMultiplier { get; set; } = 1.35f;
        [Export(PropertyHint.Range, "0.1,8,0.01")] public float FertilityFrequencyMultiplier { get; set; } = 2.25f;
        /// <summary>
        /// NOT ENFORCED. The temperature at which cold biomes begin is owned by
        /// the latitude model - TerrainMapSetup.LatitudeCentreFor sets where the
        /// map sits pole-to-equator, and TerrainBiomeStage reads that.
        ///
        /// Not to be confused with TemperatureComponent.ColdThreshold, which is
        /// a different setting, in degrees, and IS enforced.
        /// </summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float ColdThreshold { get; set; } = 0.22f;
        /// <summary>
        /// NOT ENFORCED. The moisture level at which ground turns wetland is
        /// owned by the moisture bands in TerrainBiomeStage, positioned by
        /// Dryness, which the Rainfall axis sets.
        /// </summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float WetlandThreshold { get; set; } = 0.72f;
        /// <summary>
        /// NOT ENFORCED, and a threshold is the wrong shape for this anyway.
        ///
        /// Land quality already exists and is already used: TerrainStartPositionStage
        /// scores food, production, fresh water and sea access over the ring a
        /// first city would work, and takes starts greedily by that score. That
        /// is fertility, implemented.
        ///
        /// A THRESHOLD would be worse than the score it duplicates: rejecting
        /// every candidate below a bar can leave players unplaced on a poor map,
        /// where ranking always seats them somewhere and seats them fairly.
        /// </summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float FertilityThreshold { get; set; } = 0.24f;

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
        /// Sets every world-shaping dial from a named preset.
        ///
        /// Applied as a whole, not as a patch: the dials are not independent, so
        /// leaving some at whatever the last selection set them to produces
        /// worlds that belong to no preset at all. Size, seed and render dials
        /// are deliberately untouched - those are the developer's, not the
        /// world type's.
        /// </summary>
        public void ApplyWorldPreset(TerrainWorldPreset preset)
        {
            TerrainWorldDefinition world = TerrainWorldPresets.Get(preset);
            Landform = world.Landform;
            Preset = world.Climate;
            LandmassScale = world.LandCoverage;
            ArchipelagoIslandCount = world.IslandCount;
            // Mainland takes its coverage from 1 - SeaCoverage and ignores
            // LandmassScale entirely, so a preset asking for 44% land silently
            // got 88%. Deriving the sea from the requested land keeps
            // LandCoverage meaning the same thing in every preset, which is what
            // anyone writing one will assume.
            SeaCoverage = world.Landform == LandformMode.Mainland
                ? Mathf.Clamp(1.0f - world.LandCoverage, 0.0f, 0.98f)
                : world.SeaCoverage;
            LakeCoverage = world.LakeCoverage;
            RiverDensity = world.RiverDensity;
            HillsFraction = world.HillsFraction;
            MountainsFraction = world.MountainsFraction;
            SwampCoverage = world.SwampCoverage;
            SnowCoverage = world.SnowCoverage;
            IceCoverage = world.IceCoverage;
            Dryness = world.Dryness;
            FeatureDensity = world.FeatureDensity;
            ResourceDensity = world.ResourceDensity;
            ResourceSet = world.Resources;
            StartPositionCount = world.StartPositions;
        }

        public int GenerateTerrain()
        {
            ResolveReferences();
            if (_cells == null)
            {
                GD.PushWarning($"[{Name}] GridTerrainGeneratorComponent cannot generate without GridCellDataComponent.");
                return 0;
            }

            TerrainGenerationSettings settings = CurrentSettings();
            GeneratedTerrainField field = FieldFor(settings);
            var generated = new Godot.Collections.Array();
            for (int y = 0; y < settings.Size.Y; y++)
            {
                for (int x = 0; x < settings.Size.X; x++)
                {
                    generated.Add(new Godot.Collections.Dictionary
                    {
                        ["cell"] = settings.Origin + new Vector2I(x, y),
                        ["terrain"] = field.TerrainAtCell(new Vector2I(x, y)),
                        ["flags"] = 0
                    });
                }
            }

            _cells.DefaultTerrainKind = NormalizeKind(DefaultTerrainKind);
            _cells.LoadCells(generated, ClearExistingCells);
            EmitSignal(SignalName.TerrainGenerated, generated.Count);
            return generated.Count;
        }

        // NOTE: these are deliberately NOT overloads. Godot exposes script
        // methods by name and keeps only one method per name, so an overloaded
        // public API silently becomes unreachable from GDScript.
        public string TerrainKindAt(Vector2I localCell)
            => FieldFor(CurrentSettings()).TerrainAtCell(localCell);

        public string TerrainKindAtPosition(Vector2 localPosition)
            => FieldFor(CurrentSettings()).TerrainAtPosition(localPosition);

        public string WaterSourceAt(Vector2I localCell)
            => FieldFor(CurrentSettings()).WaterSourceAtCell(localCell);

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
            // Settings live HERE, on the generator. They used to be read off the
            // PainterlyTerrainComponent whenever UsePainterSettings was set, which
            // made a RENDERER the owner of the generator's inputs - two owners for
            // one fact, with a flag deciding which won, and a pipeline that could
            // not compile without a renderer present.
            Vector2I size = EffectiveBoundsSize;

            // Either the map's size decides the climate window and the minimum
            // biome region, or the exports do. One owner per fact: when the
            // rules are on they are the answer, and the two exports are not
            // quietly blended with them.
            TerrainScaleRules.Rules scale = UseScaleRules
                ? TerrainScaleRules.For(size, LandmassScale)
                : new TerrainScaleRules.Rules(
                    Mathf.Clamp(ClimateLatitudeSpan, 0.05f, 1.0f),
                    Mathf.Clamp(MinBiomeRegionFraction, 0.0f, 0.5f));

            return new TerrainGenerationSettings(
                BoundsOrigin, size, Mode, Preset, Seed, NoiseType, FractalType,
                Mathf.Max(0.0001f, Frequency), Mathf.Clamp(Octaves, 1, 10), Mathf.Max(1.0f, Lacunarity), Mathf.Clamp(Gain, 0.0f, 1.0f),
                Landform, Mathf.Clamp(LandmassScale, 0.05f, 0.92f), Mathf.Clamp(SeaCoverage, 0.0f, 0.98f),
                Mathf.Clamp(ArchipelagoIslandCount, 2, 12), Mathf.Clamp(TopologySamplesPerCell, 2, 24),
                Mathf.Clamp(BeachWidth, 0.0f, 4.0f), Mathf.Clamp(RockLevel, 0.0f, 1.0f), Mathf.Clamp(Dryness, 0.0f, 1.0f),
                Mathf.Clamp(SwampCoverage, 0.0f, 1.0f), Mathf.Clamp(SnowCoverage, 0.0f, 1.0f), Mathf.Clamp(IceCoverage, 0.0f, 1.0f), Mathf.Max(0.02f, FeatureFrequencyMultiplier),
                Mathf.Clamp(LakeCoverage, 0.0f, 0.35f), Mathf.Max(0.02f, LakeFrequencyMultiplier), Mathf.Clamp(LakeShoreWidth, 0.0f, 3.0f),
                Mathf.Clamp(RiverDensity, 0.0f, 4.0f), Mathf.Clamp(StartPositionCount, 0, 24),
                Mathf.Clamp(ResourceDensity, 0.0f, 4.0f), ResourceSet, Mathf.Clamp(HillsFraction, 0.0f, 0.9f),
                Mathf.Clamp(MountainsFraction, 0.0f, 0.9f), Mathf.Clamp(HillshadeStrength, 0.0f, 3.0f),
                Mathf.Clamp(FeatureDensity, 0.0f, 4.0f),
                UseClimateBiomeMaps,
                UseScaleRules,
                UseBiomeQuotas,
                Mathf.Max(0, BiomeCoherencePasses),
                scale.MinRegionFraction,
                Mathf.Clamp(BiomeCoherenceKeep, 1, 8),
                DesertFraction, DryGrassFraction, SwampFraction,
                Mathf.Max(0.0f, OceanMarginTiles),
                Mathf.Max(0.0f, CoastlineRaggedness),
                Mathf.Clamp(AltitudeCooling, 0.0f, 1.0f),
                scale.LatitudeSpan,
                Mathf.Clamp(ClimateLatitudeCentre, 0.0f, 1.0f),
                Mathf.Max(0.1f, TemperatureFrequencyMultiplier), Mathf.Max(0.1f, MoistureFrequencyMultiplier), Mathf.Max(0.1f, FertilityFrequencyMultiplier),
                Mathf.Clamp(ColdThreshold, 0.0f, 1.0f), Mathf.Clamp(WetlandThreshold, 0.0f, 1.0f), Mathf.Clamp(FertilityThreshold, 0.0f, 1.0f));
        }

        private void ResolveReferences()
        {
            if (_cells == null || !GodotObject.IsInstanceValid(_cells))
                _cells = !CellDataPath.IsEmpty
                    ? GetNodeOrNull<GridCellDataComponent>(CellDataPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridCellDataComponent>(GetTree()?.CurrentScene) : null;

        }

        private static string NormalizeKind(string value)
            => string.IsNullOrWhiteSpace(value) ? "grass" : value.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
    }
}

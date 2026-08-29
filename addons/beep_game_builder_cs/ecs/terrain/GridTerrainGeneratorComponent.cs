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
        [Export] public NodePath PainterlyTerrainPath { get; set; } = new("");
        [Export] public bool UsePainterSettings { get; set; } = true;
        [Export] public bool GenerateOnReady { get; set; } = false;
        [Export] public bool GenerateInEditor { get; set; } = false;
        [Export] public bool ClearExistingCells { get; set; } = true;

        [ExportGroup("Map")]
        [Export] public Vector2I BoundsOrigin { get; set; } = Vector2I.Zero;
        [Export] public Vector2I BoundsSize { get; set; } = new(64, 64);
        [Export] public string DefaultTerrainKind { get; set; } = "grass";

        [ExportGroup("Generation")]
        [Export] public PainterlyTerrainComponent.TerrainMode Mode { get; set; } = PainterlyTerrainComponent.TerrainMode.ProceduralNoise;
        [Export] public PainterlyTerrainComponent.TerrainPreset Preset { get; set; } = PainterlyTerrainComponent.TerrainPreset.Grassland;
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
        [Export(PropertyHint.Range, "0,1,0.01")] public float BeachWidth { get; set; } = 0.035f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float RockLevel { get; set; } = 0.82f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float Dryness { get; set; } = 0.25f;

        [ExportGroup("Feature Biome Coverage")]
        [Export(PropertyHint.Range, "0,1,0.01")] public float SwampCoverage { get; set; } = 0.0f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float SnowCoverage { get; set; } = 0.0f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float IceCoverage { get; set; } = 0.0f;
        [Export(PropertyHint.Range, "0.02,4,0.01")] public float FeatureFrequencyMultiplier { get; set; } = 0.18f;

        [ExportGroup("Lake Features")]
        [Export(PropertyHint.Range, "0,0.35,0.01")] public float LakeCoverage { get; set; } = 0.05f;
        [Export(PropertyHint.Range, "0.02,1,0.01")] public float LakeFrequencyMultiplier { get; set; } = 0.10f;
        [Export(PropertyHint.Range, "0,0.25,0.01")] public float LakeShoreWidth { get; set; } = 0.04f;

        [ExportGroup("River Features")]
        [Export(PropertyHint.Range, "0,4,0.05")] public float RiverDensity { get; set; } = 1.0f;

        [ExportGroup("Gameplay")]
        // Every optional layer is a dial rather than a separate on/off flag, so
        // there is one owner per setting: zero means the layer is not generated.
        [Export(PropertyHint.Range, "0,24,1")] public int StartPositionCount { get; set; } = 6;
        [Export(PropertyHint.Range, "0,4,0.05")] public float ResourceDensity { get; set; } = 1.0f;
        [Export(PropertyHint.Range, "0,4,0.05")] public float FeatureDensity { get; set; } = 1.0f;

        [ExportGroup("Relief")]
        [Export(PropertyHint.Range, "0,0.9,0.01")] public float HillsFraction { get; set; } = 0.16f;
        [Export(PropertyHint.Range, "0,0.9,0.01")] public float MountainsFraction { get; set; } = 0.07f;
        [Export(PropertyHint.Range, "0,3,0.05")] public float HillshadeStrength { get; set; } = 1.0f;

        [ExportGroup("Climate Maps")]
        [Export] public bool UseClimateBiomeMaps { get; set; } = false;
        [Export(PropertyHint.Range, "0.1,4,0.01")] public float TemperatureFrequencyMultiplier { get; set; } = 0.72f;
        [Export(PropertyHint.Range, "0.1,4,0.01")] public float MoistureFrequencyMultiplier { get; set; } = 1.35f;
        [Export(PropertyHint.Range, "0.1,8,0.01")] public float FertilityFrequencyMultiplier { get; set; } = 2.25f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float ColdThreshold { get; set; } = 0.22f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float WetlandThreshold { get; set; } = 0.72f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float FertilityThreshold { get; set; } = 0.24f;

        private GridCellDataComponent? _cells;
        private PainterlyTerrainComponent? _terrain;
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
            if (UsePainterSettings && PainterlyTerrainPath.IsEmpty)
                return new[] { "PainterlyTerrainPath should point to a PainterlyTerrainComponent when UsePainterSettings is enabled." };
            if (BoundsSize.X <= 0 || BoundsSize.Y <= 0)
                return new[] { "BoundsSize must be greater than zero." };
            return Array.Empty<string>();
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
            Vector2I size = UsePainterSettings && _terrain != null
                ? new Vector2I(Mathf.Max(1, _terrain.WidthTiles), Mathf.Max(1, _terrain.HeightTiles))
                : EffectiveBoundsSize;
            PainterlyTerrainComponent.TerrainMode mode = UsePainterSettings && _terrain != null ? _terrain.Mode : Mode;
            PainterlyTerrainComponent.TerrainPreset preset = UsePainterSettings && _terrain != null ? _terrain.Preset : Preset;
            int seed = UsePainterSettings && _terrain != null ? _terrain.Seed : Seed;
            FastNoiseLite.NoiseTypeEnum noiseType = UsePainterSettings && _terrain != null ? _terrain.NoiseType : NoiseType;
            FastNoiseLite.FractalTypeEnum fractalType = UsePainterSettings && _terrain != null ? _terrain.FractalType : FractalType;
            float frequency = UsePainterSettings && _terrain != null ? _terrain.Frequency : Frequency;
            int octaves = UsePainterSettings && _terrain != null ? _terrain.Octaves : Octaves;
            float lacunarity = UsePainterSettings && _terrain != null ? _terrain.Lacunarity : Lacunarity;
            float gain = UsePainterSettings && _terrain != null ? _terrain.Gain : Gain;
            float beachWidth = UsePainterSettings && _terrain != null ? _terrain.BeachWidth : BeachWidth;
            float rockLevel = UsePainterSettings && _terrain != null ? _terrain.RockLevel : RockLevel;
            float dryness = UsePainterSettings && _terrain != null ? _terrain.Dryness : Dryness;

            return new TerrainGenerationSettings(
                BoundsOrigin, size, mode, preset, seed, noiseType, fractalType,
                Mathf.Max(0.0001f, frequency), Mathf.Clamp(octaves, 1, 10), Mathf.Max(1.0f, lacunarity), Mathf.Clamp(gain, 0.0f, 1.0f),
                Landform, Mathf.Clamp(LandmassScale, 0.05f, 0.92f), Mathf.Clamp(SeaCoverage, 0.0f, 0.98f),
                Mathf.Clamp(ArchipelagoIslandCount, 2, 12), Mathf.Clamp(TopologySamplesPerCell, 2, 24),
                Mathf.Clamp(beachWidth, 0.0f, 1.0f), Mathf.Clamp(rockLevel, 0.0f, 1.0f), Mathf.Clamp(dryness, 0.0f, 1.0f),
                Mathf.Clamp(SwampCoverage, 0.0f, 1.0f), Mathf.Clamp(SnowCoverage, 0.0f, 1.0f), Mathf.Clamp(IceCoverage, 0.0f, 1.0f), Mathf.Max(0.02f, FeatureFrequencyMultiplier),
                Mathf.Clamp(LakeCoverage, 0.0f, 0.35f), Mathf.Max(0.02f, LakeFrequencyMultiplier), Mathf.Clamp(LakeShoreWidth, 0.0f, 0.25f),
                Mathf.Clamp(RiverDensity, 0.0f, 4.0f), Mathf.Clamp(StartPositionCount, 0, 24),
                Mathf.Clamp(ResourceDensity, 0.0f, 4.0f), Mathf.Clamp(HillsFraction, 0.0f, 0.9f),
                Mathf.Clamp(MountainsFraction, 0.0f, 0.9f), Mathf.Clamp(HillshadeStrength, 0.0f, 3.0f),
                Mathf.Clamp(FeatureDensity, 0.0f, 4.0f),
                UseClimateBiomeMaps, Mathf.Max(0.1f, TemperatureFrequencyMultiplier), Mathf.Max(0.1f, MoistureFrequencyMultiplier), Mathf.Max(0.1f, FertilityFrequencyMultiplier),
                Mathf.Clamp(ColdThreshold, 0.0f, 1.0f), Mathf.Clamp(WetlandThreshold, 0.0f, 1.0f), Mathf.Clamp(FertilityThreshold, 0.0f, 1.0f));
        }

        private void ResolveReferences()
        {
            if (_cells == null || !GodotObject.IsInstanceValid(_cells))
                _cells = !CellDataPath.IsEmpty
                    ? GetNodeOrNull<GridCellDataComponent>(CellDataPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridCellDataComponent>(GetTree()?.CurrentScene) : null;

            if (_terrain == null || !GodotObject.IsInstanceValid(_terrain))
                _terrain = !PainterlyTerrainPath.IsEmpty
                    ? GetNodeOrNull<PainterlyTerrainComponent>(PainterlyTerrainPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<PainterlyTerrainComponent>(GetTree()?.CurrentScene) : null;
        }

        private static string NormalizeKind(string value)
            => string.IsNullOrWhiteSpace(value) ? "grass" : value.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
    }
}

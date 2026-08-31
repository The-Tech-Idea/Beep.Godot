using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// The map setup axes, as the strategy games actually present them:
    ///
    ///   Map type · Map size · World age · Temperature · Rainfall · Sea level · Resources
    ///
    /// This is Civilization V's advanced setup, and it is used here because the
    /// factorisation is better than the one it replaces. Twelve combined "world
    /// types" mixed geography with weather, so choosing "Frozen World" also
    /// chose one landmass, and choosing "Archipelago" also chose temperate. The
    /// axes below are independent: any climate can be applied to any shape.
    ///
    /// Two of them are worth naming, because they are what the combined presets
    /// were hiding:
    ///
    /// - WORLD AGE is relief, not weather. A young world has had little erosion,
    ///   so it is hills and mountains; an old one is worn to plains. "Highlands"
    ///   and "Great Plains" were map types in the old list, which is why they
    ///   could not be combined with anything - they were this axis wearing a
    ///   shape's clothes.
    /// - TEMPERATURE and RAINFALL are separate. Hot and dry is a desert, hot and
    ///   wet is a jungle, and a single "climate" dial cannot say both.
    ///
    /// Every level is a MULTIPLIER on the shape's own values rather than a
    /// replacement, so "Archipelago, high seas" is still recognisably an
    /// archipelago.
    /// </summary>
    public enum TerrainWorldAge
    {
        Young,
        Mature,
        Old,
    }

    public enum TerrainTemperature
    {
        Cold,
        Temperate,
        Hot,
    }

    public enum TerrainRainfall
    {
        Arid,
        Normal,
        Wet,
    }

    public enum TerrainSeaLevel
    {
        Low,
        Normal,
        High,
    }

    public enum TerrainResourceLevel
    {
        Sparse,
        Normal,
        Abundant,
    }

    /// <summary>
    /// Map sizes as named steps. Raw width and height are two numbers a
    /// developer has to invent every time, and most of the pairs they could
    /// invent are worse than these.
    /// </summary>
    public enum TerrainMapSize
    {
        Tiny,
        Small,
        Standard,
        Large,
        Huge,
    }

    public static class TerrainMapSetup
    {
        public static readonly string[] WorldAgeNames = { "Young  (mountainous)", "Mature", "Old  (eroded)" };
        public static readonly string[] TemperatureNames = { "Cold", "Temperate", "Hot" };
        public static readonly string[] RainfallNames = { "Arid", "Normal", "Wet" };
        public static readonly string[] SeaLevelNames = { "Low  (more land)", "Normal", "High  (more sea)" };
        public static readonly string[] ResourceLevelNames = { "Sparse", "Normal", "Abundant" };

        public static readonly string[] MapSizeNames =
        {
            "Tiny  32x32", "Small  48x48", "Standard  64x64", "Large  96x60", "Huge  128x80",
        };

        public static Vector2I BoundsFor(TerrainMapSize size) => size switch
        {
            TerrainMapSize.Tiny => new Vector2I(32, 32),
            TerrainMapSize.Small => new Vector2I(48, 48),
            TerrainMapSize.Large => new Vector2I(96, 60),
            TerrainMapSize.Huge => new Vector2I(128, 80),
            _ => new Vector2I(64, 64),
        };

        /// <summary>
        /// Relief multiplier. A young world has had little time to erode, so it
        /// keeps its hills and mountains; an old one is worn down to plains.
        /// </summary>
        public static float ReliefScaleFor(TerrainWorldAge age) => age switch
        {
            TerrainWorldAge.Young => 2.10f,
            TerrainWorldAge.Old => 0.35f,
            _ => 1.0f,
        };

        /// <summary>
        /// Where the map sits in the pole-to-equator range. Hot pushes it toward
        /// the equator, which is what removes the tundra and snow; cold pushes it
        /// toward a pole, which is what brings them back. This is the same
        /// latitude window the scale rules use, so a cold map is genuinely at a
        /// high latitude rather than having snow sprinkled over it.
        /// </summary>
        public static float LatitudeCentreFor(TerrainTemperature temperature) => temperature switch
        {
            TerrainTemperature.Cold => 0.78f,
            TerrainTemperature.Hot => 0.22f,
            _ => 0.52f,
        };

        /// <summary>Land multiplier. Higher seas leave less of it.</summary>
        public static float LandScaleFor(TerrainSeaLevel level) => level switch
        {
            TerrainSeaLevel.Low => 1.30f,
            TerrainSeaLevel.High => 0.70f,
            _ => 1.0f,
        };

        /// <summary>Lakes, rivers and vegetation all follow rainfall.</summary>
        public static float WaterScaleFor(TerrainRainfall rainfall) => rainfall switch
        {
            TerrainRainfall.Arid => 0.30f,
            TerrainRainfall.Wet => 1.90f,
            _ => 1.0f,
        };

        /// <summary>
        /// Aridity, which the biome table reads directly rather than as a
        /// multiplier: it is a position on the dry-to-wet scale, not an amount.
        /// </summary>
        public static float DrynessFor(TerrainRainfall rainfall) => rainfall switch
        {
            TerrainRainfall.Arid => 0.80f,
            TerrainRainfall.Wet => 0.08f,
            _ => 0.25f,
        };

        public static float ResourceScaleFor(TerrainResourceLevel level) => level switch
        {
            TerrainResourceLevel.Sparse => 0.45f,
            TerrainResourceLevel.Abundant => 1.90f,
            _ => 1.0f,
        };
    }
}

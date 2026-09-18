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
    /// - TEMPERATURE and RAINFALL are separate, and scoped the way Civilization
    ///   scopes them. Temperature places the map on the globe, so it decides the
    ///   ground: desert in the dry belt, tundra toward a pole. Rainfall decides
    ///   what grows and flows on that ground: lakes, rivers and woodland.
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

        /// <summary>Resource catalogues, in ResourceSet's own declaration order.</summary>
        public static readonly string[] ResourceSetNames =
            { "Historical", "Oil and gas", "Space exploration" };

        /// <summary>
        /// The projections a world can be drawn in, in TerrainProjection's own
        /// order - so a chooser's selected index IS the projection and no second
        /// mapping can drift from it.
        /// </summary>
        public static readonly string[] ProjectionNames =
            { "Painted", "Game tiles", "Isometric", "Isometric tiles" };

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

        /// <summary>
        /// Land multiplier. Higher seas leave less of it.
        ///
        /// Sea level is a TRIM, not a different map. Civilization moves the
        /// land/water ratio by roughly 7-12% across its three settings, and that
        /// is the useful range: enough that a high-seas world feels tighter,
        /// little enough that the map type still decides the shape. At the 1.30
        /// and 0.70 this used to carry, the measured swing was +33%/-28% - sea
        /// level was overpowering the map type it is supposed to modify.
        /// </summary>
        public static float LandScaleFor(TerrainSeaLevel level) => level switch
        {
            TerrainSeaLevel.Low => 1.12f,
            TerrainSeaLevel.High => 0.88f,
            _ => 1.0f,
        };

        /// <summary>
        /// Lakes and rivers follow rainfall by this scale; vegetation by
        /// <see cref="VegetationScaleFor"/>. Rainfall does not reach the ground at all: which land is
        /// desert is the latitude's decision (TerrainClimateStage), as Civilization scopes the axes.
        /// </summary>
        public static float WaterScaleFor(TerrainRainfall rainfall) => rainfall switch
        {
            TerrainRainfall.Arid => 0.30f,
            TerrainRainfall.Wet => 1.90f,
            _ => 1.0f,
        };

        /// <summary>
        /// Vegetation against normal rainfall: the generator's FeatureDensity, which scales
        /// TerrainFeatureStage's woodland share of land and the oasis chance.
        ///
        /// Civilization VI caps forest at a share of land plots: FeatureGenerator.lua's iForestPercent
        /// of 18, moved by -4 for arid rainfall and +4 for wet, so 14, 18 and 22%. These are those
        /// caps against normal. Civilization V's map scripter describes the axis the same way:
        /// "Rainfall affects feature types: forest, jungle, marsh, oasis, etc. More rain = more
        /// foliage."
        ///
        /// Rainfall once also shifted every tile's moisture, -0.35 arid and +0.20 wet. Tuned on
        /// Oilfield Days' maritime basin, that turned every inland cell of a scale-rules lab map to
        /// desert when arid, because those maps are mostly dry grass to begin with.
        /// </summary>
        public static float VegetationScaleFor(TerrainRainfall rainfall) => rainfall switch
        {
            TerrainRainfall.Arid => 14.0f / 18.0f,
            TerrainRainfall.Wet => 22.0f / 18.0f,
            _ => 1.0f,
        };


        public static float ResourceScaleFor(TerrainResourceLevel level) => level switch
        {
            TerrainResourceLevel.Sparse => 0.45f,
            TerrainResourceLevel.Abundant => 1.90f,
            _ => 1.0f,
        };
    }
}

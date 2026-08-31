using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Every noise channel one generation run needs, each on its own seed offset
    /// so changing one stage's frequency can never shift another stage's pattern.
    /// </summary>
    internal sealed class TerrainNoiseSet
    {
        private TerrainNoiseSet(
            FastNoiseLite shape,
            FastNoiseLite shapeWarpX,
            FastNoiseLite shapeWarpY,
            FastNoiseLite ridge,
            FastNoiseLite roughness,
            FastNoiseLite moisture,
            FastNoiseLite temperature,
            FastNoiseLite lake,
            FastNoiseLite detail,
            FastNoiseLite vegetation)
        {
            Shape = shape;
            ShapeWarpX = shapeWarpX;
            ShapeWarpY = shapeWarpY;
            Ridge = ridge;
            Roughness = roughness;
            Moisture = moisture;
            Temperature = temperature;
            Lake = lake;
            Detail = detail;
            Vegetation = vegetation;
        }

        /// <summary>Continental fractal that decides where land is.</summary>
        public FastNoiseLite Shape { get; }

        public FastNoiseLite ShapeWarpX { get; }
        public FastNoiseLite ShapeWarpY { get; }

        /// <summary>Drives mountain ranges via a ridged transform.</summary>
        public FastNoiseLite Ridge { get; }

        public FastNoiseLite Roughness { get; }
        public FastNoiseLite Moisture { get; }
        public FastNoiseLite Temperature { get; }
        public FastNoiseLite Lake { get; }
        public FastNoiseLite Detail { get; }

        /// <summary>
        /// Where vegetation MASSES. Woods used to be an independent dice roll per
        /// tile, which gives uniform speckle however the odds are weighted: every
        /// tile decides alone, so a stand can never form an edge. A field makes
        /// forest a connected shape with clearings, the same way the shape
        /// fractal makes land a landmass instead of scattered dots.
        /// </summary>
        public FastNoiseLite Vegetation { get; }

        public static TerrainNoiseSet Create(TerrainGenerationSettings settings)
        {
            // The continental fractal is scaled to the MAP, not to the painter's
            // texture frequency: one landmass should span the map and N should
            // each span about 1/sqrt(N) of it. Driving it from the painter's
            // Frequency would make landmass count depend on a texture setting.
            float shapeFrequency = 1.0f / TerrainLandmassStage.FeatureTiles(settings);

            return new TerrainNoiseSet(
                Create(settings, 91127, shapeFrequency),
                Create(settings, 91159, shapeFrequency * 1.7f),
                Create(settings, 91193, shapeFrequency * 1.7f),
                Create(settings, 92221, shapeFrequency * 3.0f),
                Create(settings, 92251, shapeFrequency * 3.1f),
                Create(settings, 9719, Mathf.Max(0.004f, shapeFrequency * 1.25f * settings.MoistureFrequencyMultiplier)),
                Create(settings, 19739, Mathf.Max(0.004f, shapeFrequency * 0.85f * settings.TemperatureFrequencyMultiplier)),
                Create(settings, 51053, Mathf.Max(0.02f, shapeFrequency * 2.4f * settings.LakeFrequencyMultiplier)),
                Create(settings, 71069, Mathf.Max(0.05f, settings.Frequency * 3.2f)),
                // Stands are a few tiles across, so this runs coarser than the
                // detail noise and finer than the continents.
                Create(settings, 33427, Mathf.Max(0.01f, shapeFrequency * 2.2f * settings.FeatureFrequencyMultiplier)));
        }

        private static FastNoiseLite Create(TerrainGenerationSettings settings, int seedOffset, float frequency) => new()
        {
            Seed = settings.Seed + seedOffset,
            NoiseType = settings.NoiseType,
            FractalType = settings.FractalType,
            Frequency = Mathf.Max(0.0001f, frequency),
            FractalOctaves = settings.Octaves,
            FractalLacunarity = settings.Lacunarity,
            FractalGain = settings.Gain,
        };
    }
}

using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Every noise channel one generation run needs, each on its own seed offset
    /// so changing one stage's frequency can never shift another stage's pattern.
    /// </summary>
    internal sealed class TerrainNoiseSet : System.IDisposable
    {
        public void Dispose()
        {
            Ridge.Dispose();
            Roughness.Dispose();
            Moisture.Dispose();
            Temperature.Dispose();
            Lake.Dispose();
            Vegetation.Dispose();
        }

        private TerrainNoiseSet(
            FastNoiseLite ridge,
            FastNoiseLite roughness,
            FastNoiseLite moisture,
            FastNoiseLite temperature,
            FastNoiseLite lake,
            FastNoiseLite vegetation)
        {
            Ridge = ridge;
            Roughness = roughness;
            Moisture = moisture;
            Temperature = temperature;
            Lake = lake;
            Vegetation = vegetation;
        }

        /// <summary>Drives mountain ranges via a ridged transform.</summary>
        public FastNoiseLite Ridge { get; }

        public FastNoiseLite Roughness { get; }
        public FastNoiseLite Moisture { get; }
        public FastNoiseLite Temperature { get; }
        public FastNoiseLite Lake { get; }

        /// <summary>
        /// Where vegetation MASSES. Woods used to be an independent dice roll per
        /// tile, which gives uniform speckle however the odds are weighted: every
        /// tile decides alone, so a stand can never form an edge. A field makes
        /// forest a connected shape with clearings, the same way the landmass
        /// stage grows connected land instead of scattered dots.
        /// </summary>
        public FastNoiseLite Vegetation { get; }

        public static TerrainNoiseSet Create(TerrainGenerationSettings settings)
        {
            // The channels below are scaled to the MAP, not to the painter's
            // texture frequency: a feature should span a fraction of the map, and
            // how many there are should not depend on a texture setting.
            float shapeFrequency = 1.0f / TerrainLandmassStage.FeatureTiles(settings);

            return new TerrainNoiseSet(
                Create(settings, 92221, shapeFrequency * 3.0f),
                Create(settings, 92251, shapeFrequency * 3.1f),
                Create(settings, 9719, Mathf.Max(0.004f, shapeFrequency * 1.25f * settings.MoistureFrequencyMultiplier)),
                Create(settings, 19739, Mathf.Max(0.004f, shapeFrequency * 0.85f * settings.TemperatureFrequencyMultiplier)),
                Create(settings, 51053, Mathf.Max(0.02f, shapeFrequency * 2.4f * settings.LakeFrequencyMultiplier)),
                // Stands are a few TILES across whatever the map's size, so this one is not
                // scaled to the map like the channels above. It was, at 2.2 times the continental
                // frequency and a multiplier of 0.18: coarser than the continents, and a hundred
                // tiles a wavelength on a 144-tile map. See TerrainFeatureStage.StandWavelengthTiles.
                Create(settings, 33427, settings.FeatureFrequencyMultiplier / TerrainFeatureStage.StandWavelengthTiles));
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

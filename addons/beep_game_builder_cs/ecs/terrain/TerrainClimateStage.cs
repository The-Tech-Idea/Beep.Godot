using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Assigns temperature and moisture, the two axes the biome table reads.
    ///
    /// Temperature is driven primarily by LATITUDE, with a lapse-rate penalty
    /// for altitude and a little noise so the bands are not ruler-straight.
    /// Latitude banding is what gives a Civilization map its readable structure:
    /// ice at the poles, tundra below it, deserts in the horse latitudes,
    /// jungle on the equator.
    ///
    /// Moisture combines a fractal with proximity to water and a rain-shadow
    /// term, so continental interiors and the lee of mountain ranges dry out
    /// instead of every coastline looking the same.
    /// </summary>
    internal static class TerrainClimateStage
    {
        /// <summary>Prevailing wind direction, in samples, for the rain shadow.</summary>
        private const int WindStepX = -1;

        public static void Apply(TerrainWorld world, TerrainNoiseSet noise, TerrainGenerationSettings settings)
        {
            // How far, in samples, the climate bands may meander north or south.
            float bandWander = world.Height * 0.11f;

            for (int y = 0; y < world.Height; y++)
            {
                for (int x = 0; x < world.Width; x++)
                {
                    int index = world.Index(x, y);
                    Vector2 at = world.TileCentre(x, y);

                    // Displace the row before taking latitude, so every band -
                    // ice, tundra, the desert belts - bends together as one
                    // coherent climate rather than as separate stripes.
                    float latitude = world.Latitude(
                        y,
                        noise.Temperature.GetNoise2D(at.X, at.Y) * bandWander,
                        settings.ClimateLatitudeSpan,
                        settings.ClimateLatitudeCentre);

                    // Warm at the equator, cold at the poles, then cooled by
                    // altitude so highlands are colder than the plains below.
                    float temperature = 1.0f - (latitude * latitude * 1.15f);
                    temperature -= world.Elevation[index] * settings.AltitudeCooling;
                    world.Temperature[index] = Mathf.Clamp(temperature, 0.0f, 1.0f);

                    float fractal = TerrainGeometry.Normalized(noise.Moisture.GetNoise2D(at.X, at.Y));
                    float moisture = (fractal * 0.62f) + (Maritime(world, index) * 0.38f);
                    moisture -= RainShadow(world, x, y);
                    moisture -= SubtropicalAridity(latitude);

                    // Cold air holds less water, which is what keeps high
                    // latitudes as tundra rather than swamp.
                    moisture *= Mathf.Lerp(0.55f, 1.0f, world.Temperature[index]);
                    world.Moisture[index] = Mathf.Clamp(moisture, 0.0f, 1.0f);
                }
            }
        }

        /// <summary>1 on the coast, falling away inland.</summary>
        private static float Maritime(TerrainWorld world, int index)
        {
            int distance = world.CoastDistance[index];
            if (distance == int.MaxValue)
                return 0.0f;

            float reach = Mathf.Max(1.0f, world.SamplesPerCell * 6.5f);
            return 1.0f - Mathf.Clamp(distance / reach, 0.0f, 1.0f);
        }

        /// <summary>
        /// The dry belts either side of the equator. Air rising at the equator
        /// sheds its water and descends around 30 degrees latitude already dry,
        /// which is why the world's great deserts sit in two bands rather than
        /// scattered at random. Modelling it is what makes a generated map read
        /// as a world instead of noise.
        /// </summary>
        private static float SubtropicalAridity(float latitude)
        {
            const float centre = 0.34f;
            const float width = 0.17f;
            float offset = (latitude - centre) / width;
            return Mathf.Exp(-offset * offset) * 0.20f;
        }

        /// <summary>
        /// Dries a tile that has higher ground upwind of it, producing the arid
        /// belt on the lee side of a mountain range.
        /// </summary>
        private static float RainShadow(TerrainWorld world, int x, int y)
        {
            if (!world.Land[world.Index(x, y)])
                return 0.0f;

            float here = world.Elevation[world.Index(x, y)];
            float highestUpwind = here;
            int reach = Mathf.Max(2, world.SamplesPerCell * 4);

            for (int step = 1; step <= reach; step++)
            {
                int atX = x + (WindStepX * step);
                if (!world.InBounds(atX, y))
                    break;
                highestUpwind = Mathf.Max(highestUpwind, world.Elevation[world.Index(atX, y)]);
            }

            return Mathf.Clamp((highestUpwind - here) * 0.85f, 0.0f, 0.45f);
        }
    }
}

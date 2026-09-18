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

        /// <summary>
        /// Distance inland, in kilometres, over which the sea's share of moisture falls to 1/e.
        ///
        /// Measured, not tuned: on non-forested land annual precipitation declines exponentially with
        /// distance from the ocean, with a global mean e-folding length of about 600 km (Makarieva,
        /// Gorshkov and Li 2009, "Precipitation on land versus distance from the ocean", Ecological
        /// Complexity 6:302-307). Forests are placed after climate here, so the non-forest figure is
        /// the one that applies.
        ///
        /// It replaces a reach of 6.5 CELLS. A cell has no size of its own, so that one constant was
        /// about a kilometre on Oilfield Days' 24 km basin and about 270 km on a lab map - and the
        /// bigger the map, the more of its land sat past the reach as desert, whatever the climate
        /// axes said: a temperate 144-cell basin came out half desert.
        /// </summary>
        private const float MaritimeEFoldingKilometres = 600.0f;

        public static void Apply(TerrainGenerationBuffer world, TerrainNoiseSet noise, TerrainGenerationSettings settings)
        {
            // How far, in samples, the climate bands may meander north or south.
            float bandWander = world.Height * 0.11f;

            // The ground a sample covers, from the same span the latitude bands are drawn from.
            float kilometresPerSample = world.KilometresPerSample(settings.ClimateLatitudeSpan);

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
                    float moisture = (fractal * 0.62f) + (Maritime(world, index, kilometresPerSample) * 0.38f);
                    moisture -= RainShadow(world, x, y);
                    moisture -= SubtropicalAridity(latitude);

                    // Cold air holds less water, which is what keeps high
                    // latitudes as tundra rather than swamp.
                    moisture *= Mathf.Lerp(0.55f, 1.0f, world.Temperature[index]);
                    world.Moisture[index] = Mathf.Clamp(moisture, 0.0f, 1.0f);
                }
            }
        }

        /// <summary>1 on the coast, falling away inland by distance on the ground.</summary>
        private static float Maritime(TerrainGenerationBuffer world, int index, float kilometresPerSample)
        {
            int distance = world.CoastDistance[index];
            if (distance == int.MaxValue)
                return 0.0f;

            return Mathf.Exp(-(distance * kilometresPerSample) / MaritimeEFoldingKilometres);
        }

        /// <summary>
        /// The dry belts either side of the equator. Air rising at the equator
        /// sheds its water and descends around 30 degrees latitude already dry,
        /// which is why the world's great deserts sit in two bands rather than
        /// scattered at random. Modelling it is what makes a generated map read
        /// as a world instead of noise, and it is where a Hot world's desert comes
        /// from: Rainfall does not reach the ground (TerrainMapSetup).
        ///
        /// The belt is where the deserts are, not where the air sinks: subtropical
        /// deserts "occur at a restricted latitudinal range between 15-35 degrees
        /// latitude" (ACC Physical Geology, 2nd edition, 23.2) - latitude 0.17-0.39
        /// here - so it is centred on 25 degrees and at half strength at 15 and 35.
        /// It used to sit at 31 degrees and reach 43 at half strength, with a peak
        /// of 0.20 chosen while every interior past 6.5 cells had no coastal
        /// moisture at all. Once the coast's reach was measured in kilometres that
        /// belt could not dry a hot map: a hot Continents lab map had no desert and
        /// was greener than a temperate one. At a peak of 0.35 hot lab maps are
        /// 29-36% desert, while the game's temperate basin is 98% grass.
        /// </summary>
        private static float SubtropicalAridity(float latitude)
        {
            const float centre = 0.28f;
            const float width = 0.13f;
            float offset = (latitude - centre) / width;
            return Mathf.Exp(-offset * offset) * 0.35f;
        }

        /// <summary>
        /// Dries a tile that has higher ground upwind of it, producing the arid
        /// belt on the lee side of a mountain range.
        /// </summary>
        private static float RainShadow(TerrainGenerationBuffer world, int x, int y)
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

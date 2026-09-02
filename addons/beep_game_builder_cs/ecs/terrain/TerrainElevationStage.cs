using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Builds land elevation and classifies it into flat / hills / mountains.
    ///
    /// Elevation combines distance from the coast with a RIDGED fractal. The
    /// coast term guarantees the shoreline is low and continental interiors are
    /// high, which is what stops mountains appearing as cliffs straight out of
    /// the sea. The ridged fractal is what makes highlands form connected
    /// ranges rather than round blobs.
    ///
    /// Hills and mountains are cut by PERCENTILE of land elevation, the way
    /// Civilization's hillsFrac/peaksFrac work, so "a fifth of the land is
    /// hills" holds regardless of how the noise happened to come out.
    /// </summary>
    internal static class TerrainElevationStage
    {

        public static void Apply(TerrainWorld world, TerrainNoiseSet noise)
        {
            int[] fromWater = TerrainGeometry.DistanceTo(Negate(world.Land), world.Width, world.Height);
            fromWater.CopyTo(world.CoastDistance, 0);

            // Normalize the inland term against the widest landmass, so
            // elevation reads the same on a small island and a big continent.
            int deepest = 1;
            for (int index = 0; index < world.Count; index++)
            {
                if (world.Land[index] && fromWater[index] != int.MaxValue)
                    deepest = Mathf.Max(deepest, fromWater[index]);
            }

            for (int y = 0; y < world.Height; y++)
            {
                for (int x = 0; x < world.Width; x++)
                {
                    int index = world.Index(x, y);
                    if (!world.Land[index])
                    {
                        world.Elevation[index] = 0.0f;
                        world.Relief[index] = TerrainRelief.Flat;
                        continue;
                    }

                    Vector2 at = world.TileCentre(x, y);
                    float inland = Mathf.Clamp(fromWater[index] / (float)deepest, 0.0f, 1.0f);
                    float ridge = TerrainGeometry.Ridged(noise.Ridge.GetNoise2D(at.X, at.Y));
                    float rough = TerrainGeometry.Normalized(noise.Roughness.GetNoise2D(at.X, at.Y));

                    // The ridged term carries most of the weight so highlands
                    // follow crests and form ranges. The inland term is only a
                    // gentle lift, enough to keep shorelines low; weighting it
                    // heavily instead turns every continental interior into one
                    // round central plateau.
                    world.Elevation[index] = Mathf.Clamp(
                        (Mathf.Sqrt(inland) * 0.28f) + (ridge * ridge * 0.57f) + (rough * 0.15f),
                        0.0f,
                        1.0f);
                }
            }

        }

        /// <summary>
        /// Cuts the height field into flat, hills and mountains.
        ///
        /// Separate from building the height because EROSION runs between the
        /// two. The bands are percentiles of the field, so classifying first
        /// and carving after would label the land from a shape that no longer
        /// exists - the valleys would be full of tiles still marked mountain.
        /// </summary>
        public static void Classify(TerrainWorld world, TerrainGenerationSettings settings)
        {
            // Zero fractions mean a game that does not want relief at all, so
            // nothing is promoted above flat.
            if (settings.HillsFraction <= 0.0f && settings.MountainsFraction <= 0.0f)
                return;

            float hills = settings.HillsFraction > 0.0f
                ? TerrainGeometry.Percentile(world.Elevation, world.Land, 1.0f - settings.HillsFraction)
                : float.PositiveInfinity;
            float mountains = settings.MountainsFraction > 0.0f
                ? TerrainGeometry.Percentile(world.Elevation, world.Land, 1.0f - settings.MountainsFraction)
                : float.PositiveInfinity;

            for (int index = 0; index < world.Count; index++)
            {
                if (!world.Land[index])
                    continue;

                world.Relief[index] = world.Elevation[index] >= mountains
                    ? TerrainRelief.Mountains
                    : world.Elevation[index] >= hills
                        ? TerrainRelief.Hills
                        : TerrainRelief.Flat;
            }
        }

        private static bool[] Negate(bool[] values)
        {
            var result = new bool[values.Length];
            for (int index = 0; index < values.Length; index++)
                result[index] = !values[index];
            return result;
        }
    }
}

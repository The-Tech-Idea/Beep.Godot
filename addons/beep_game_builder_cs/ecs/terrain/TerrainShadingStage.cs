using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Computes a hillshade multiplier from the elevation gradient, so slopes
    /// facing the light are brightened and slopes facing away are darkened.
    ///
    /// This is what lets relief be shown WITHOUT overwriting the biome: a
    /// grassland hill stays green and reads as a hill because it is shaded,
    /// rather than being recoloured to flat grey. Encoding relief in the terrain
    /// kind instead costs a biome per relief level and makes a varied map look
    /// like bare rock.
    /// </summary>
    internal static class TerrainShadingStage
    {
        /// <summary>Light direction, from the north-west, as in most relief maps.</summary>
        private static readonly Vector2 LightDirection = new Vector2(-1.0f, -1.0f).Normalized();

        /// <summary>Gain on the slope term. Elevation is 0..1 over the whole map.</summary>
        private const float Strength = 7.5f;

        private const float MinimumShade = 0.70f;
        private const float MaximumShade = 1.30f;

        public static void Apply(TerrainWorld world, TerrainGenerationSettings settings)
        {
            // Zero leaves every sample unlit, which is the flat look a game gets
            // when it does not want relief shading at all.
            if (settings.HillshadeStrength <= 0.0f)
                return;

            float strength = Strength * settings.HillshadeStrength;
            for (int y = 0; y < world.Height; y++)
            {
                for (int x = 0; x < world.Width; x++)
                {
                    int index = world.Index(x, y);
                    if (!world.Land[index])
                    {
                        world.Shade[index] = 1.0f;
                        continue;
                    }

                    // Central differences, clamped at the edges of the field.
                    float left = ElevationAt(world, x - 1, y);
                    float right = ElevationAt(world, x + 1, y);
                    float up = ElevationAt(world, x, y - 1);
                    float down = ElevationAt(world, x, y + 1);

                    float slopeX = (right - left) * 0.5f;
                    float slopeY = (down - up) * 0.5f;

                    // Dot the surface gradient with the light: a slope tilted
                    // toward the light gets a positive term, away gets negative.
                    float lit = ((slopeX * LightDirection.X) + (slopeY * LightDirection.Y)) * strength;
                    world.Shade[index] = Mathf.Clamp(1.0f + lit, MinimumShade, MaximumShade);
                }
            }
        }

        private static float ElevationAt(TerrainWorld world, int x, int y)
        {
            int clampedX = Mathf.Clamp(x, 0, world.Width - 1);
            int clampedY = Mathf.Clamp(y, 0, world.Height - 1);
            return world.Elevation[world.Index(clampedX, clampedY)];
        }
    }
}

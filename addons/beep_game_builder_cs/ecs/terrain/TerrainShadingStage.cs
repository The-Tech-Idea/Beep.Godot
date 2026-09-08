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

        public static void Apply(TerrainGenerationBuffer world, TerrainGenerationSettings settings)
        {
            // Zero leaves every sample unlit, which is the flat look a game gets
            // when it does not want relief shading at all.
            if (settings.HillshadeStrength <= 0.0f)
                return;

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

                    world.Shade[index] = FromGradient(left, right, up, down, settings.HillshadeStrength);
                }
            }
        }

        /// <summary>Local cell gradient over a snapshot whose water elevations are zero.</summary>
        public static float AtCell(float[] elevations, Vector2I size, Vector2I cell)
        {
            int row = cell.Y * size.X;
            return FromGradient(elevations[row + Mathf.Max(0, cell.X - 1)],
                elevations[row + Mathf.Min(size.X - 1, cell.X + 1)],
                elevations[Mathf.Max(0, cell.Y - 1) * size.X + cell.X],
                elevations[Mathf.Min(size.Y - 1, cell.Y + 1) * size.X + cell.X], 1f);
        }

        private static float FromGradient(float left, float right, float up, float down, float strength)
        {
            float lit = ((right - left) * LightDirection.X + (down - up) * LightDirection.Y)
                * 0.5f * Strength * strength;
            return Mathf.Clamp(1f + lit, MinimumShade, MaximumShade);
        }

        private static float ElevationAt(TerrainGenerationBuffer world, int x, int y)
        {
            int clampedX = Mathf.Clamp(x, 0, world.Width - 1);
            int clampedY = Mathf.Clamp(y, 0, world.Height - 1);
            return world.Elevation[world.Index(clampedX, clampedY)];
        }
    }
}

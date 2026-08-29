using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Decides where land is, using the standard procedural-map recipe:
    ///
    ///     landness = fbm(domain-warped position) - strength * d^power
    ///
    /// where d is the ELLIPTICAL normalized distance from the map centre, then
    /// thresholding that field at the level which yields the requested land
    /// coverage - the same way Civilization's FractalWorld hits its water
    /// percentage target.
    ///
    /// Three properties are load-bearing and easy to lose:
    ///
    /// - Coverage comes from a THRESHOLD, never from growing a seed outward. A
    ///   flood fill expands until it meets the rectangular map border and then
    ///   follows it, which renders as a rounded rectangle with straight,
    ///   axis-aligned coast.
    /// - d is ELLIPTICAL, so the falloff's contours are ellipses and the map
    ///   border becomes ocean by construction. A rectangular border clamp
    ///   instead cuts the coastline dead straight along the map edge.
    /// - Archipelago is NOT a set of placed islands. It is this same field with
    ///   a weaker falloff and finer grain, so separate landmasses are emergent.
    ///   Placing island centres explicitly makes each coast follow the Voronoi
    ///   midline between centres, which reads as straight channels and
    ///   square-cornered islands.
    /// </summary>
    internal static class TerrainLandmassStage
    {
        /// <summary>
        /// Weight on the fractal relative to the radial falloff: the
        /// coastline-raggedness dial. The falloff sets the overall landmass, the
        /// fractal breaks its outline up. Too low and the coast relaxes onto the
        /// bare ellipse.
        /// </summary>
        private const float FractalAmplitude = 1.45f;

        /// <summary>Guaranteed ocean ring, in tiles, at the very map edge.</summary>
        private const float HardBorderTiles = 1.0f;

        public static void Apply(TerrainWorld world, TerrainNoiseSet noise, TerrainGenerationSettings settings)
        {
            if (settings.Preset == PainterlyTerrainComponent.TerrainPreset.Sea)
                return;

            float[] landness = BuildLandnessField(world, noise, settings, out bool[] eligible);
            int targetCount = Mathf.Clamp(
                Mathf.RoundToInt(world.Count * settings.TargetLandCoverage), 1, world.Count);

            int landmasses = settings.RequestedLandmassCount;
            if (landmasses == 0)
            {
                SelectTopScoring(world.Land, landness, eligible, targetCount);
                return;
            }

            SolveThresholdForCoverage(world, landness, eligible, targetCount, landmasses);
        }

        /// <summary>
        /// Characteristic landmass size in tiles. One landmass spans the map; N
        /// landmasses each span about 1/sqrt(N) of it.
        /// </summary>
        public static float FeatureTiles(TerrainGenerationSettings settings)
        {
            float minSpan = Mathf.Min(settings.Size.X, settings.Size.Y);
            int landmasses = Mathf.Max(1, settings.RequestedLandmassCount);
            return Mathf.Max(4.0f, minSpan / Mathf.Sqrt(landmasses));
        }

        private static float[] BuildLandnessField(
            TerrainWorld world,
            TerrainNoiseSet noise,
            TerrainGenerationSettings settings,
            out bool[] eligible)
        {
            var landness = new float[world.Count];
            eligible = new bool[world.Count];

            bool island = settings.Landform == GridTerrainGeneratorComponent.LandformMode.Island;
            bool shaped = settings.Landform != GridTerrainGeneratorComponent.LandformMode.Mainland;

            // A strong falloff pulls everything toward one central mass; a weak
            // one lets the fractal break the field into many islands.
            float strength = island ? 0.95f : 0.42f;
            float power = island ? 2.00f : 1.50f;
            float warpAmplitude = FeatureTiles(settings) * 0.55f;

            float mapWidth = settings.Size.X;
            float mapHeight = settings.Size.Y;
            float centreX = mapWidth * 0.5f;
            float centreY = mapHeight * 0.5f;
            float radiusX = Mathf.Max(1.0f, centreX);
            float radiusY = Mathf.Max(1.0f, centreY);

            for (int y = 0; y < world.Height; y++)
            {
                for (int x = 0; x < world.Width; x++)
                {
                    int index = world.Index(x, y);
                    Vector2 at = world.TileCentre(x, y);

                    // Domain warping (Inigo Quilez): offset the sample point by a
                    // second noise field before evaluating the fractal, turning
                    // smooth blobby contours into folded, organic coastlines.
                    float warpX = noise.ShapeWarpX.GetNoise2D(at.X, at.Y) * warpAmplitude;
                    float warpY = noise.ShapeWarpY.GetNoise2D(at.X, at.Y) * warpAmplitude;

                    // Signed noise, deliberately NOT remapped to 0..1: fbm
                    // clusters near its midpoint, so a 0..1 remap has a far
                    // smaller usable spread than the falloff and the coastline
                    // collapses onto the bare ellipse.
                    float fractal = noise.Shape.GetNoise2D(at.X + warpX, at.Y + warpY) * FractalAmplitude;

                    float value = fractal;
                    if (shaped)
                    {
                        float offsetX = (at.X - centreX) / radiusX;
                        float offsetY = (at.Y - centreY) / radiusY;
                        float distance = Mathf.Sqrt((offsetX * offsetX) + (offsetY * offsetY));
                        value -= strength * Mathf.Pow(distance, power);
                    }

                    landness[index] = value;

                    float border = Mathf.Min(
                        Mathf.Min(at.X, mapWidth - at.X),
                        Mathf.Min(at.Y, mapHeight - at.Y));
                    eligible[index] = !shaped || border > HardBorderTiles;
                }
            }

            return landness;
        }

        /// <summary>
        /// Bisects the landness threshold until the surviving land, after the
        /// landmass-count constraint is applied, matches the requested coverage.
        /// Lowering the threshold can only add land, so coverage is monotonic in
        /// it and the bisection converges.
        /// </summary>
        private static void SolveThresholdForCoverage(
            TerrainWorld world,
            float[] landness,
            bool[] eligible,
            int targetCount,
            int landmasses)
        {
            float low = float.PositiveInfinity;
            float high = float.NegativeInfinity;
            for (int index = 0; index < landness.Length; index++)
            {
                if (!eligible[index])
                    continue;
                low = Mathf.Min(low, landness[index]);
                high = Mathf.Max(high, landness[index]);
            }
            if (!float.IsFinite(low) || !float.IsFinite(high))
                return;

            // Fragments below this are dust rather than playable islands.
            int minimumIslandCells = world.SamplesPerCell * world.SamplesPerCell * 2;

            // Each step halves the remaining threshold range, so 16 gets within
            // ~1/65000 of it - far finer than the coverage tolerance. Every step
            // re-labels connected components over the whole field, so the count
            // is the dominant cost of generation and is not worth raising.
            const int bisectionSteps = 16;

            // Every buffer the search needs is allocated once and reused by all
            // seventeen passes; the accepted candidate is kept by swapping the
            // two masks rather than copying the field.
            int count = landness.Length;
            var best = new bool[count];
            var candidate = new bool[count];
            var labels = new int[count];
            var stack = new int[count];
            var sizes = new List<int>();
            var order = new List<int>();

            LevelSet(world, landness, eligible, low, landmasses, minimumIslandCells,
                best, labels, stack, sizes, order);

            for (int iteration = 0; iteration < bisectionSteps; iteration++)
            {
                float middle = (low + high) * 0.5f;
                LevelSet(world, landness, eligible, middle, landmasses, minimumIslandCells,
                    candidate, labels, stack, sizes, order);

                if (CountTrue(candidate) >= targetCount)
                {
                    (best, candidate) = (candidate, best);
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            Array.Copy(best, world.Land, best.Length);
        }

        private static void LevelSet(
            TerrainWorld world,
            float[] landness,
            bool[] eligible,
            float threshold,
            int landmasses,
            int minimumIslandCells,
            bool[] mask,
            int[] labels,
            int[] stack,
            List<int> sizes,
            List<int> order)
        {
            for (int index = 0; index < landness.Length; index++)
                mask[index] = eligible[index] && landness[index] >= threshold;

            KeepLargestComponents(world, mask, landmasses, minimumIslandCells, labels, stack, sizes, order);
        }

        private static void KeepLargestComponents(
            TerrainWorld world,
            bool[] mask,
            int keepCount,
            int minimumCells,
            int[] labels,
            int[] stack,
            List<int> sizes,
            List<int> order)
        {
            int components = TerrainGeometry.LabelComponents(
                mask, world.Width, world.Height, labels, stack, sizes);

            // Rank component ids by size rather than moving their cells about,
            // so the whole decision is made over a list as long as the number of
            // landmasses, not as long as the map.
            order.Clear();
            for (int i = 0; i < components; i++)
                order.Add(i);
            // Ties break on component id, which is assigned in scan order. Size
            // alone would leave two equal landmasses to an unstable sort, so
            // which one survived the keep count could change between runs of the
            // same seed.
            order.Sort((left, right) =>
            {
                int bySize = sizes[right].CompareTo(sizes[left]);
                return bySize != 0 ? bySize : left.CompareTo(right);
            });

            int keep = Mathf.Min(keepCount, order.Count);
            for (int i = 1; i < keep; i++)
            {
                // Always keep the largest, so a small map still yields land.
                if (sizes[order[i]] < minimumCells)
                {
                    keep = i;
                    break;
                }
            }

            // A cell survives when its component ranked inside the keep count.
            for (int i = 0; i < order.Count; i++)
                sizes[order[i]] = i < keep ? 1 : 0;

            for (int index = 0; index < mask.Length; index++)
                mask[index] = labels[index] >= 0 && sizes[labels[index]] == 1;
        }

        /// <summary>
        /// Exact top-N selection, used where no landmass-count constraint
        /// applies, which both matches the requested coverage precisely and is
        /// cheaper than solving a threshold.
        /// </summary>
        private static void SelectTopScoring(bool[] land, float[] landness, bool[] eligible, int targetCount)
        {
            var candidates = new List<int>(landness.Length);
            for (int index = 0; index < landness.Length; index++)
            {
                if (eligible[index])
                    candidates.Add(index);
            }
            candidates.Sort((left, right) => landness[right].CompareTo(landness[left]));

            for (int i = 0; i < Mathf.Min(targetCount, candidates.Count); i++)
                land[candidates[i]] = true;
        }

        private static int CountTrue(bool[] values)
        {
            int count = 0;
            foreach (bool value in values)
            {
                if (value)
                    count++;
            }
            return count;
        }
    }
}

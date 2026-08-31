using Godot;
using System;
using System.Diagnostics;

namespace Beep.ECS
{
    /// <summary>
    /// Runs the world-generation pipeline. Each stage owns one decision and
    /// reads only what the stages before it have already settled:
    ///
    ///   1. Landmass  - where land is, at the requested coverage
    ///   2. Water     - lake basins, then ocean vs lake by connectivity
    ///   3. Elevation - height from the final coast, then hills and mountains
    ///   4. Climate   - latitude temperature, moisture with rain shadow
    ///   5. Rivers    - steepest descent from high wet ground to the sea
    ///   6. Shading   - hillshade from the elevation gradient
    ///   7. Biome     - the terrain kind every consumer reads
    ///   8. Tile reduction - collapse the sample field to one value per
    ///      gameplay tile, which is what the game and the renderer both read
    ///   9. Continents, resources, start positions - the gameplay layers,
    ///      which read the reduced tiles and never change them
    ///
    /// The order matters: lakes move the coastline, so elevation must be
    /// measured after them; climate needs elevation for lapse rate and rain
    /// shadow; rivers need both elevation to flow down and moisture to choose
    /// their sources; shading follows rivers so a carved river is not left
    /// lit like the hillside it replaced; and biome runs last so it can paint
    /// the rivers as water.
    /// </summary>
    internal static class TerrainFieldBuilder
    {
        /// <summary>
        /// Ceiling on sub-cell samples. Sampling finer than the gameplay grid is
        /// what lets a coastline curve within a tile instead of stepping around
        /// tile corners; it also sets how finely every BIOME boundary is drawn,
        /// since a border can only bend where there is a sample to bend it.
        /// The cost is quadratic, so it is capped and large maps step down.
        /// </summary>
        private const int MaxFieldSamples = 1_250_000;

        public static GeneratedTerrainField Build(TerrainGenerationSettings settings)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            int samplesPerCell = EffectiveSamplesPerCell(settings);
            var world = new TerrainWorld(
                settings.Size.X * samplesPerCell,
                settings.Size.Y * samplesPerCell,
                samplesPerCell);

            if (settings.Mode == TerrainMode.Plain)
                return BuildPlain(world, settings, stopwatch);

            TerrainNoiseSet noise = TerrainNoiseSet.Create(settings);
            TerrainLandmassStage.Apply(world, settings);
            // Freeze the landmass outline before lakes are carved out of it.
            world.Land.CopyTo(world.Footprint, 0);
            TerrainWaterStage.Apply(world, noise, settings);
            TerrainElevationStage.Apply(world, noise, settings);

            // Water shapes the land before the land is named. Erosion carves the
            // valleys, and only then is the height cut into hills and mountains,
            // because those bands are percentiles of a field erosion changes.
            TerrainErosionStage.Apply(world, settings);
            TerrainElevationStage.Classify(world, settings);
            TerrainClimateStage.Apply(world, noise, settings);
            TerrainRiverStage.Apply(world, settings);
            TerrainShadingStage.Apply(world, settings);
            TerrainBiomeStage.Apply(world, settings);

            // Straight after the biome table, and before anything reads terrain
            // kinds: features, resources and start positions all ask what a tile
            // IS, and they must see the map the renderer will draw.
            TerrainCoherenceStage.Apply(world, settings);

            // Everything above works at sub-tile resolution because that is what
            // makes good coastlines. This collapses it to one value per gameplay
            // tile, which is the generator's actual output.
            TerrainTileReductionStage.Apply(world);

            // Gameplay layers read the reduced tile grid, so they run last.
            TerrainContinentStage.Apply(world);

            // The land settles before anything is placed on it: a drained lake
            // becomes ground, and ground grows things.
            TerrainScaleConstraintStage.ApplyTerrain(world, settings);
            TerrainResourceStage.Apply(world, settings);
            TerrainFeatureStage.Apply(world, noise, settings);
            // Last, and on the reduced tile grid: a feature has to reach a size
            // in TILES to exist, which is only meaningful once tiles exist. It
            // runs after the feature stage because woods are placed there, and
            // before start positions, which should not be put on a lake that is
            // about to be drained.
            TerrainScaleConstraintStage.ApplyFeatures(world, settings);

            TerrainStartPositionStage.Apply(world, settings);

            stopwatch.Stop();
            return Finish(world, settings, stopwatch.ElapsedMilliseconds);
        }

        /// <summary>A single uniform terrain, with no generation at all.</summary>
        private static GeneratedTerrainField BuildPlain(
            TerrainWorld world,
            TerrainGenerationSettings settings,
            Stopwatch stopwatch)
        {
            string kind = PlainKind(settings.Preset);
            bool water = kind is "deep_water" or "shallow_water";

            Array.Fill(world.Terrain, kind);
            if (water)
                Array.Fill(world.Water, WaterBody.Ocean);
            else
            {
                Array.Fill(world.Land, true);
                Array.Fill(world.Footprint, true);
            }

            stopwatch.Stop();
            return Finish(world, settings, stopwatch.ElapsedMilliseconds);
        }

        private static GeneratedTerrainField Finish(
            TerrainWorld world,
            TerrainGenerationSettings settings,
            long elapsedMilliseconds)
        {
            int land = 0;
            int ocean = 0;
            int lake = 0;
            int river = 0;
            int footprint = 0;
            for (int index = 0; index < world.Count; index++)
            {
                if (world.Footprint[index])
                    footprint++;

                if (world.Land[index])
                    land++;
                else if (world.Water[index] == WaterBody.Lake)
                    lake++;
                else if (world.Water[index] == WaterBody.River)
                    river++;
                else
                    ocean++;
            }

            int continents = 0;
            foreach (int id in world.CellContinent)
                continents = Mathf.Max(continents, id);

            int resources = 0;
            foreach (string resource in world.Resource)
            {
                if (resource.Length > 0)
                    resources++;
            }

            int features = 0;
            foreach (string feature in world.Feature)
            {
                if (feature.Length > 0)
                    features++;
            }

            float total = Mathf.Max(1, world.Count);
            var diagnostics = new TerrainGenerationDiagnostics(
                settings.TargetLandCoverage,
                footprint / total,
                land / total,
                ocean / total,
                lake / total,
                river / total,
                settings.RequestedLandmassCount,
                TerrainGeometry.CountComponents(world.Footprint, world.Width, world.Height),
                continents,
                resources,
                world.StartPositions.Count,
                features,
                world.SamplesPerCell,
                world.Width,
                world.Height,
                elapsedMilliseconds);

            return new GeneratedTerrainField(world, diagnostics);
        }

        private static int EffectiveSamplesPerCell(TerrainGenerationSettings settings)
        {
            int samples = Mathf.Clamp(settings.TopologySamplesPerCell, 2, 24);
            while (samples > 2 && (long)settings.Size.X * settings.Size.Y * samples * samples > MaxFieldSamples)
                samples--;
            return samples;
        }

        private static string PlainKind(TerrainPreset preset) => preset switch
        {
            TerrainPreset.Desert => "desert",
            TerrainPreset.Sand => "sand",
            TerrainPreset.Ice => "ice",
            TerrainPreset.Sea => "deep_water",
            TerrainPreset.Rock => "rock",
            TerrainPreset.Lava => "lava",
            TerrainPreset.Swamp => "swamp",
            TerrainPreset.Snow => "snow",
            _ => "grass",
        };
    }
}

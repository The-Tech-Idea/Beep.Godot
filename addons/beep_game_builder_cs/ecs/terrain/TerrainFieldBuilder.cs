using Godot;
using System;
using System.Diagnostics;
using System.Threading;

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
            => BuildPrepared(settings, TerrainResourceRules.Capture(settings), TerrainStartKitRules.Capture(settings));

        internal static GeneratedTerrainField BuildPrepared(TerrainGenerationSettings settings,
            TerrainResourceRules resources, TerrainStartKitRules startKit, CancellationToken cancellation = default,
            Action<string, int>? progress = null)
        {
            cancellation.ThrowIfCancellationRequested();
            Stopwatch stopwatch = Stopwatch.StartNew();
            int completed = 0;
            void Run(string name, Action stage)
            {
                cancellation.ThrowIfCancellationRequested();
                progress?.Invoke(name, completed);
                stage();
                cancellation.ThrowIfCancellationRequested();
                completed++;
            }

            int samplesPerCell = EffectiveSamplesPerCell(settings);
            var world = new TerrainGenerationBuffer(
                settings.Size.X * samplesPerCell,
                settings.Size.Y * samplesPerCell,
                samplesPerCell);

            if (settings.Mode == TerrainMode.Plain)
            {
                progress?.Invoke("Plain terrain", 0);
                GeneratedTerrainField plain = BuildPlain(world, settings, stopwatch, cancellation);
                cancellation.ThrowIfCancellationRequested();
                progress?.Invoke("Complete", 20);
                return plain;
            }

            using TerrainNoiseSet noise = TerrainNoiseSet.Create(settings);
            Run("Landmass", () => TerrainLandmassStage.Apply(world, settings));
            // Freeze the landmass outline before lakes are carved out of it.
            world.Land.CopyTo(world.Footprint, 0);
            Run("Water", () => TerrainWaterStage.Apply(world, noise, settings));
            Run("Elevation", () => TerrainElevationStage.Apply(world, noise));

            // Water shapes the land before the land is named. Erosion carves the
            // valleys, and only then is the height cut into hills and mountains,
            // because those bands are percentiles of a field erosion changes.
            Run("Erosion", () => TerrainErosionStage.Apply(world, settings, cancellation));
            Run("Relief", () => TerrainElevationStage.Classify(world, settings));
            Run("Climate", () => TerrainClimateStage.Apply(world, noise, settings));
            Run("Rivers", () => TerrainRiverStage.Apply(world, settings, cancellation));
            world.ReleaseCoastDistances();
            Run("Shading", () => TerrainShadingStage.Apply(world, settings));
            Run("Biomes", () => TerrainBiomeStage.Apply(world, settings));

            // Straight after the biome table, and before anything reads terrain
            // kinds: features, resources and start positions all ask what a tile
            // IS, and they must see the map the renderer will draw.
            Run("Biome coherence", () => TerrainCoherenceStage.Apply(world, settings));
            world.CompactClimate(cancellation);

            // Everything above works at sub-tile resolution because that is what
            // makes good coastlines. This collapses it to one value per gameplay
            // tile, which is the generator's actual output.
            Run("Gameplay cells", () => TerrainTileReductionStage.Apply(world));

            // The land settles before anything is placed on it: a drained lake
            // becomes ground, and ground grows things.
            Run("Terrain constraints", () => TerrainScaleConstraintStage.ApplyTerrain(world, settings));
            Run("Shorelines", () => TerrainShorelineStage.Apply(world, settings));
            // Cleanup can reconnect land and turn water cells into land. Label
            // the final topology before resources and start positions read it.
            Run("Continents", () => TerrainContinentStage.Apply(world));
            Run("Resources", () => TerrainResourceStage.Apply(world, settings, resources));
            Run("Subsurface", () => TerrainSubsurfaceStage.Apply(world, settings, resources));
            Run("Features", () => TerrainFeatureStage.Apply(world, noise, settings));
            world.ReleaseClimate();
            // Last, and on the reduced tile grid: a feature has to reach a size
            // in TILES to exist, which is only meaningful once tiles exist. It
            // runs after the feature stage because woods are placed there, and
            // before start positions, which should not be put on a lake that is
            // about to be drained.
            Run("Feature constraints", () => TerrainScaleConstraintStage.ApplyFeatures(world, settings));

            Run("Start positions", () => TerrainStartPositionStage.Apply(world, settings, startKit, cancellation));
            // Reads the chosen starts and never moves one. With StartDistanceScaling zero and
            // the radius zero it returns without allocating, so a map that asks for neither is
            // built exactly as before; the scaling measures and scales even without areas.
            Run("Start areas", () => TerrainStartAreaStage.Apply(world, settings, startKit, resources, cancellation));

            stopwatch.Stop();
            cancellation.ThrowIfCancellationRequested();
            progress?.Invoke("Diagnostics", completed);
            GeneratedTerrainField result = Finish(world, settings, stopwatch.ElapsedMilliseconds, cancellation);
            cancellation.ThrowIfCancellationRequested();
            progress?.Invoke("Complete", 20);
            return result;
        }

        /// <summary>A single uniform terrain, with no generation at all.</summary>
        private static GeneratedTerrainField BuildPlain(
            TerrainGenerationBuffer world,
            TerrainGenerationSettings settings,
            Stopwatch stopwatch, CancellationToken cancellation)
        {
            string kind = PlainKind(settings.Preset);
            bool water = TerrainTileSets.IsWaterKind(kind);

            // BOTH resolutions. The stages work on the per-sample arrays and the
            // reduction stage turns those into the per-tile ones a game reads;
            // this path has no stages to run, so it must fill the per-tile arrays
            // itself. Filling only the samples left CellTerrain at its
            // constructor value, so every Plain map came back as the cell data's
            // DefaultTerrainKind and Preset decided nothing.
            Array.Fill(world.Terrain, kind);
            Array.Fill(world.CellTerrain, kind);
            Array.Fill(world.CellInlandTerrain, kind);
            if (water)
            {
                Array.Fill(world.Water, WaterBody.Ocean);
                Array.Fill(world.CellWater, WaterBody.Ocean);
            }
            else
            {
                Array.Fill(world.Land, true);
                Array.Fill(world.Footprint, true);
                // One flat landmass, so a plain map answers ContinentAtCell the
                // same way a generated one does rather than reading as water.
                Array.Fill(world.CellContinent, 1);
            }

            stopwatch.Stop();
            return Finish(world, settings, stopwatch.ElapsedMilliseconds, cancellation);
        }

        private static GeneratedTerrainField Finish(
            TerrainGenerationBuffer world,
            TerrainGenerationSettings settings,
            long elapsedMilliseconds, CancellationToken cancellation)
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

            int liquidResources = 0;
            foreach (string resource in world.CellLiquidResource)
            {
                if (resource.Length > 0)
                    liquidResources++;
            }

            int undergroundCells = 0;
            foreach (string resource in world.CellUndergroundResource)
            {
                if (resource.Length > 0)
                    undergroundCells++;
            }

            int features = 0;
            foreach (string feature in world.Feature)
            {
                if (feature.Length > 0)
                    features++;
            }

            int usableAreas = 0;
            int minAreaCells = 0, maxAreaCells = 0;
            for (int i = 0; i < world.StartAreas.Count; i++)
            {
                TerrainStartAreaReport report = world.StartAreas[i];
                if (report.Usable) usableAreas++;
                minAreaCells = i == 0 ? report.CellCount : Mathf.Min(minAreaCells, report.CellCount);
                maxAreaCells = Mathf.Max(maxAreaCells, report.CellCount);
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
                TerrainGeometry.CountComponents(world.Footprint, world.Width, world.Height, world.IntScratchA, world.IntScratchB),
                continents,
                resources,
                liquidResources,
                undergroundCells,
                settings.RequestedStartPositionCount,
                world.StartPositions.Count,
                world.StartAreas.Count,
                usableAreas,
                minAreaCells,
                maxAreaCells,
                world.NeutralSites.Placements.Count,
                features,
                world.SamplesPerCell,
                world.Width,
                world.Height,
                elapsedMilliseconds);

            // Output packing can allocate substantial memory; dead stage arrays must
            // no longer be rooted by the buffer while that happens.
            world.ReleaseGenerationScratch();
            return new GeneratedTerrainField(world, diagnostics, cancellation);
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

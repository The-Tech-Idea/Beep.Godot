using Godot;
using System.Collections.Generic;
using System.Threading;

namespace Beep.ECS
{
    /// <summary>
    /// Chooses fair player start tiles.
    ///
    /// Each candidate is scored over the ring of tiles a first city would
    /// actually work: food, production, fresh water and sea access. That is what
    /// separates a start on a green coast beside a river from one in the middle
    /// of a desert, and it is the difference between a map that looks good and a
    /// map that plays.
    ///
    /// Starts are then taken greedily by score subject to a minimum separation,
    /// and spread across continents before doubling up on one, so players are
    /// not all dropped onto the same landmass while another sits empty.
    /// </summary>
    internal static class TerrainStartPositionStage
    {
        /// <summary>Radius, in tiles, of the ring a start is judged on.</summary>
        private const int WorkRadius = 3;
        private readonly record struct Candidate(Vector2I Cell, float Score, int Continent);

        public static void Apply(TerrainGenerationBuffer world, TerrainGenerationSettings settings, CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            // The SAME clamp TerrainGenerationSettings.RequestedStartPositionCount
            // reports, so what this stage aims for and what a diagnostic calls
            // "requested" can never be two different numbers.
            int wanted = settings.RequestedStartPositionCount;
            if (wanted == 0)
                return;

            int wide = world.CellsWide;
            int high = world.CellsHigh;

            int count = 0;
            for (int y = 0; y < high; y++)
            {
                cancellation.ThrowIfCancellationRequested();
                for (int x = 0; x < wide; x++)
                    if (Eligible(world, world.CellIndex(x, y))) count++;
            }
            var candidates = new List<Candidate>(count);

            for (int cellY = 0; cellY < high; cellY++)
            {
                cancellation.ThrowIfCancellationRequested();
                for (int cellX = 0; cellX < wide; cellX++)
                {
                    int index = world.CellIndex(cellX, cellY);
                    if (!Eligible(world, index)) continue;

                    var cell = new Vector2I(cellX, cellY);
                    candidates.Add(new(cell, Score(world, cellX, cellY), world.CellContinent[index]));
                }
            }
            if (candidates.Count == 0)
                return;

            cancellation.ThrowIfCancellationRequested();
            candidates.Sort(static (left, right) => right.Score.CompareTo(left.Score));
            cancellation.ThrowIfCancellationRequested();

            float minimumSeparation = Mathf.Max(4.0f, Mathf.Min(wide, high) / (float)Mathf.Max(2, wanted) * 1.6f);
            var used = new HashSet<int>();

            // First pass gives every continent a start before any continent gets
            // a second; the second pass fills whatever is left by score.
            Take(world, candidates, minimumSeparation, wanted, used, true, cancellation);
            Take(world, candidates, minimumSeparation, wanted, used, false, cancellation);

            // Unlike a landmass shortfall - which the diagnostics report by
            // pairing RequestedLandmassCount beside LandComponentCount - a start
            // shortfall had nothing saying it happened at all. A small or
            // water-heavy map can genuinely run out of separated, land-locked
            // candidates before it reaches `wanted`.
            if (world.StartPositions.Count < wanted)
            {
                GD.PushWarning(
                    $"Only placed {world.StartPositions.Count} of {wanted} requested start "
                    + "positions - the map is too small or too fragmented to fit the rest at "
                    + "the required separation.");
            }
        }

        private static bool Eligible(TerrainGenerationBuffer world, int index)
            => world.CellWater[index] == WaterBody.None && world.CellRelief[index] != TerrainRelief.Mountains
                && TerrainKindCatalog.Standard.Startable(world.CellTerrain[index]);

        private static void Take(
            TerrainGenerationBuffer world,
            List<Candidate> candidates,
            float minimumSeparation,
            int wanted,
            HashSet<int> usedContinents,
            bool oncePerContinent,
            CancellationToken cancellation)
        {
            float minimumSeparationSquared = minimumSeparation * minimumSeparation;

            int visited = 0;
            foreach (Candidate entry in candidates)
            {
                if ((visited++ & 255) == 0) cancellation.ThrowIfCancellationRequested();
                Vector2I candidate = entry.Cell;
                if (world.StartPositions.Count >= wanted)
                    return;

                int on = entry.Continent;
                if (oncePerContinent && !usedContinents.Add(on))
                    continue;

                bool farEnough = true;
                foreach (Vector2I existing in world.StartPositions)
                {
                    float dx = existing.X - candidate.X;
                    float dy = existing.Y - candidate.Y;
                    if ((dx * dx) + (dy * dy) < minimumSeparationSquared)
                    {
                        farEnough = false;
                        break;
                    }
                }
                if (!farEnough)
                {
                    if (oncePerContinent)
                        usedContinents.Remove(on);
                    continue;
                }

                world.StartPositions.Add(candidate);
                usedContinents.Add(on);
            }
        }

        /// <summary>
        /// Judges the tiles a first city would work, not just the tile itself.
        /// </summary>
        private static float Score(TerrainGenerationBuffer world, int cellX, int cellY)
        {
            float food = 0.0f;
            float production = 0.0f;
            float freshWater = 0.0f;
            float seaAccess = 0.0f;
            int counted = 0;

            for (int offsetY = -WorkRadius; offsetY <= WorkRadius; offsetY++)
            {
                for (int offsetX = -WorkRadius; offsetX <= WorkRadius; offsetX++)
                {
                    int atX = cellX + offsetX;
                    int atY = cellY + offsetY;
                    if (atX < 0 || atY < 0 || atX >= world.CellsWide || atY >= world.CellsHigh)
                        continue;
                    if ((offsetX * offsetX) + (offsetY * offsetY) > WorkRadius * WorkRadius)
                        continue;

                    counted++;
                    int index = world.CellIndex(atX, atY);
                    string terrain = world.CellTerrain[index];
                    WaterBody water = world.CellWater[index];

                    food += terrain switch
                    {
                        "grass" => 1.0f,
                        "dry_grass" => 0.7f,
                        "jungle" => 0.6f,
                        "swamp" => 0.3f,
                        "shallow_water" => 0.5f,
                        "tundra" => 0.15f,
                        _ => 0.0f,
                    };

                    production += world.CellRelief[index] switch
                    {
                        TerrainRelief.Hills => 1.0f,
                        TerrainRelief.Mountains => 0.35f,
                        _ => 0.15f,
                    };

                    if (water is WaterBody.River or WaterBody.Lake)
                        freshWater = 1.0f;
                    if (water == WaterBody.Ocean)
                        seaAccess = 1.0f;
                }
            }

            if (counted == 0)
                return float.NegativeInfinity;

            // Fresh water is weighted heavily because a start without it is a
            // materially worse start, which is exactly the unfairness to avoid.
            return ((food / counted) * 1.6f)
                + ((production / counted) * 0.9f)
                + (freshWater * 0.8f)
                + (seaAccess * 0.35f);
        }
    }
}

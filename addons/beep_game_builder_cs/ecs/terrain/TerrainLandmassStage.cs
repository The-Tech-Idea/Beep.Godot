using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Places the land as a fixed number of GROWN landmasses.
    ///
    /// WHY NOT A THRESHOLDED NOISE FIELD. The previous version built one field -
    /// fractal noise minus a single radial falloff centred on the map - and cut
    /// it at whatever level hit the coverage target. Two failures follow from
    /// that shape and neither can be tuned away:
    ///
    /// - There is ONE falloff, at the map's centre, so there is one blob. A
    ///   "continents" map with several separate continents is not expressible;
    ///   the best it could do was one mass with fragments off its coast.
    /// - Thresholded fractal noise PERCOLATES. Near the level that gives a
    ///   playable amount of land, the surviving cells form a connected web
    ///   rather than compact bodies. Measured on a 128x80 map: the largest mass
    ///   filled 35% of its own bounding box for "continents", 30% for
    ///   "archipelago". A continent that occupies a third of the box it spans is
    ///   a spider, not a landmass.
    ///
    /// WHAT THIS DOES INSTEAD, which is how the strategy games and the tectonic
    /// generators do it: choose N well-separated seeds, then grow each one cell
    /// at a time until the requested amount of land is claimed. The count is
    /// then exact rather than emergent. Growth is ordered by distance from the
    /// seed, perturbed by noise, which is what keeps a mass compact while giving
    /// it a ragged coast: the ordering is the "compactness penalty" that stops
    /// spindly shapes.
    ///
    /// KEEPING THEM APART IS NOT FREE. Claiming each cell once does NOT make the
    /// masses separate - two growing alongside each other simply end up
    /// four-connected, and one component is one landmass however it was grown.
    /// Nor is it enough to reserve a buffer and ask who owns a cell; three
    /// versions of that leak, each documented where the test now lives. A mass
    /// may claim a cell only when no OTHER mass has claimed ground within the
    /// separating gap of it, and the gap has to be wide enough to survive the
    /// beach, which turns the water beside a coast into land.
    ///
    /// The noise is still doing the aesthetic work. It no longer decides WHERE
    /// land is - only what its edges look like.
    /// </summary>
    internal static class TerrainLandmassStage
    {
        /// <summary>
        /// Characteristic landmass size in tiles - one landmass spans the map,
        /// N landmasses each span about 1/sqrt(N) of it. Read by the noise set
        /// to choose its frequency.
        /// </summary>
        public static float FeatureTiles(TerrainGenerationSettings settings)
        {
            float minSpan = Mathf.Min(settings.Size.X, settings.Size.Y);
            int landmasses = Mathf.Max(1, LandmassCount(settings));
            return Mathf.Max(4.0f, minSpan / Mathf.Sqrt(landmasses));
        }

        /// <summary>
        /// How many separate landmasses the map should have. The rule lives on
        /// the settings so there is exactly one of it.
        /// </summary>
        public static int LandmassCount(TerrainGenerationSettings settings)
            => settings.RequestedLandmassCount;

        public static void Apply(TerrainWorld world, TerrainGenerationSettings settings)
        {
            // The world buffer is REUSED between generations, so the land has
            // to be cleared before any is placed. The version this replaced
            // assigned every cell from its threshold test, so it cleared as a
            // side effect; this one only ever sets land, and without the clear
            // each generation was laid on top of the last. That is what made the
            // landmass count wrong: the previous run's mass was still there,
            // welding this run's separate masses into one.
            Array.Fill(world.Land, false);

            if (settings.Preset == TerrainPreset.Sea)
                return;

            int target = Mathf.Clamp(
                Mathf.RoundToInt(world.Count * settings.TargetLandCoverage), 1, world.Count);

            bool[] eligible = Eligible(world, settings);
            int count = LandmassCount(settings);

            int[] seeds = PlaceSeeds(world, eligible, count, settings.Seed);
            if (seeds.Length == 0)
                return;

            Grow(world, settings, eligible, seeds, target);
        }

        /// <summary>
        /// Where land may be placed at all: everywhere except a guaranteed ocean
        /// ring at the map edge, so a coastline is never a straight cut along
        /// the border.
        /// </summary>
        private static bool[] Eligible(TerrainWorld world, TerrainGenerationSettings settings)
        {
            var eligible = new bool[world.Count];
            float margin = Mathf.Max(0.0f, settings.OceanMarginTiles);

            // The margin WAVES rather than running straight.
            //
            // A rectangular margin is a rectangular wall, and any landmass big
            // enough to reach it stops dead along a perfectly straight line. At
            // 42% land the masses have nowhere else to go, so they all reached
            // it: continents with one natural coast and two ruler-straight ones
            // where they met the frame. Discouraging growth near the edge does
            // not fix that on its own - the land still has to go somewhere, and
            // it still ends up against the wall.
            //
            // Making the wall itself irregular fixes it wherever it shows: a
            // coast pressed against a wavy margin is still a coast. The amount
            // of land is unchanged, only the shape of the water it stops at.
            // Wide enough to read as a coastline rather than a ripple, and
            // fractal so it has both bays and the smaller detail inside them. A
            // single smooth wave still looks drawn.
            float amplitude = Mathf.Max(2.0f, margin);
            var edge = new FastNoiseLite
            {
                Seed = settings.Seed + 5077,
                NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
                FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
                FractalOctaves = 3,
                FractalGain = 0.5f,
                Frequency = 0.055f,
            };

            for (int y = 0; y < world.Height; y++)
            {
                for (int x = 0; x < world.Width; x++)
                {
                    Vector2 at = world.TileCentre(x, y);
                    float border = Mathf.Min(
                        Mathf.Min(at.X, settings.Size.X - at.X),
                        Mathf.Min(at.Y, settings.Size.Y - at.Y));

                    float here = margin
                        + (amplitude * (0.5f + (0.5f * edge.GetNoise2D(at.X, at.Y))));

                    eligible[world.Index(x, y)] = border > here;
                }
            }
            return eligible;
        }

        /// <summary>
        /// N seeds on a JITTERED LATTICE, one per lattice cell.
        ///
        /// The obvious approach - draw random points, keep the ones far enough
        /// apart - is rejection sampling, and it fails in the way rejection
        /// sampling always fails: on a crowded map most draws are rejected, so
        /// the attempt budget runs out and the remaining seeds are never placed
        /// at all. Measured with that version: asking for eight landmasses gave
        /// two, and asking for twelve gave six - and NOT monotonically, because
        /// whether it worked depended on the luck of the draw.
        ///
        /// A lattice cannot fail that way. Divide the map into as many cells as
        /// there are landmasses and put one seed in each, jittered inside its own
        /// cell so the result is not a visible grid. Separation is then a
        /// property of the construction rather than something to test for, the
        /// count is exact, and it is O(N).
        /// </summary>
        private static int[] PlaceSeeds(TerrainWorld world, bool[] eligible, int count, int seed)
        {
            count = Mathf.Max(1, count);

            // Lattice proportioned to the map, so a wide map is divided into
            // more columns than rows rather than into squares that do not fit.
            float aspect = world.Width / (float)Mathf.Max(1, world.Height);
            int columns = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(count * aspect)), 1, count);
            int rows = Mathf.CeilToInt(count / (float)columns);

            var random = new RandomNumberGenerator { Seed = (ulong)seed };
            var cells = new List<Vector2I>();
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                    cells.Add(new Vector2I(column, row));
            }

            // Shuffled so that when the lattice has more cells than landmasses,
            // the spare cells are not always the same ones at the end.
            for (int i = cells.Count - 1; i > 0; i--)
            {
                int j = random.RandiRange(0, i);
                (cells[i], cells[j]) = (cells[j], cells[i]);
            }

            float cellWidth = world.Width / (float)columns;
            float cellHeight = world.Height / (float)rows;
            var placed = new List<int>();

            // Two lattice points can be pushed onto the SAME cell when the one
            // they wanted is in the ocean margin. Two masses sharing a seed is
            // one mass: the second finds its only cell already claimed, stops
            // immediately, and the map is short a landmass without anything
            // having reported a problem.
            var taken = new HashSet<int>();

            foreach (Vector2I cell in cells)
            {
                if (placed.Count >= count)
                    break;

                // Jitter stays inside the middle of the lattice cell, so two
                // seeds in neighbouring cells keep most of their spacing.
                float x = (cell.X + 0.5f + (random.Randf() - 0.5f) * 0.8f) * cellWidth;
                float y = (cell.Y + 0.5f + (random.Randf() - 0.5f) * 0.8f) * cellHeight;

                int found = NearestEligible(
                    world, eligible, taken, Mathf.RoundToInt(x), Mathf.RoundToInt(y),
                    Mathf.CeilToInt(Mathf.Max(cellWidth, cellHeight)));

                if (found < 0)
                    continue;

                taken.Add(found);
                placed.Add(found);
            }

            return placed.ToArray();
        }

        /// <summary>
        /// The eligible cell nearest a lattice point. A lattice cell can fall in
        /// the ocean margin at the map edge, and refusing to seed there would
        /// silently drop a landmass; walking outward finds the nearest place the
        /// mass can actually start.
        /// </summary>
        private static int NearestEligible(
            TerrainWorld world, bool[] eligible, HashSet<int> taken, int x, int y, int radius)
        {
            for (int ring = 0; ring <= radius; ring++)
            {
                for (int dy = -ring; dy <= ring; dy++)
                {
                    for (int dx = -ring; dx <= ring; dx++)
                    {
                        // Only the ring's edge - the inside was already tried.
                        if (ring > 0 && Mathf.Abs(dx) != ring && Mathf.Abs(dy) != ring)
                            continue;

                        int at = x + dx;
                        int to = y + dy;
                        if (!world.InBounds(at, to))
                            continue;

                        int index = world.Index(at, to);
                        if (eligible[index] && !taken.Contains(index))
                            return index;
                    }
                }
            }
            return -1;
        }

        /// <summary>
        /// True when some OTHER landmass has already claimed ground within the
        /// separating gap of this tile.
        ///
        /// This is the test that actually keeps the masses apart, and it took
        /// three wrong ones to get here. Every earlier attempt stamped a buffer
        /// around each claim and then asked who owned the cell being claimed:
        ///
        /// - stamping only unowned cells ("first wins") lets two buffers meet
        ///   midway and both masses claim up to that line;
        /// - stamping unconditionally ("last wins") lets a mass re-mark the
        ///   ground beside a rival's claim and take it;
        /// - stamping the NEAREST claim looks airtight and is not, because the
        ///   nearest claim to a contested cell is usually the claimer's OWN. A
        ///   mass therefore advances one step at a time into its rival's buffer,
        ///   winning every cell on the way, until the two claims are touching.
        ///   Traced: mass 2 claiming a cell it owned at distance 1, directly
        ///   beside a cell claimed by mass 0.
        ///
        /// Asking about FOREIGN claims instead has no such hole - it is the
        /// property we want, stated directly, rather than a proxy for it. The
        /// grid is one entry per TILE rather than per sample: separation is
        /// wanted in whole tiles, and a coarse grid makes the test 81 lookups
        /// instead of several thousand.
        /// </summary>
        private static bool ForeignClaimNear(
            int[] tileOwner, int tilesWide, int tilesHigh, int tx, int ty, int mass, int gapTiles)
        {
            int fromY = Mathf.Max(0, ty - gapTiles);
            int toY = Mathf.Min(tilesHigh - 1, ty + gapTiles);
            int fromX = Mathf.Max(0, tx - gapTiles);
            int toX = Mathf.Min(tilesWide - 1, tx + gapTiles);

            for (int y = fromY; y <= toY; y++)
            {
                int row = y * tilesWide;
                for (int x = fromX; x <= toX; x++)
                {
                    int at = tileOwner[row + x];
                    if (at >= 0 && at != mass)
                        return true;
                }
            }
            return false;
        }

        private static float Squared(TerrainWorld world, int left, int right)
        {
            float dx = (left % world.Width) - (right % world.Width);
            float dy = (left / world.Width) - (right / world.Width);
            return (dx * dx) + (dy * dy);
        }

        /// <summary>
        /// Grows every landmass together until the requested land is claimed.
        ///
        /// Round-robin so no mass runs away with the map, and each grows in
        /// order of perturbed distance from its own seed. That ordering is what
        /// makes the result a landmass: nearest-first alone gives a disc, and
        /// the noise term bends the order enough to give the disc a coastline
        /// without letting it grow a tendril across the map.
        /// </summary>
        private static void Grow(
            TerrainWorld world,
            TerrainGenerationSettings settings,
            bool[] eligible,
            int[] seeds,
            int target)
        {
            var claimed = new bool[world.Count];
            int samples = Mathf.Max(1, world.SamplesPerCell);

            // Which landmass has claimed each TILE, or -1. This is what keeps
            // the masses apart, and the resolution is deliberate: separation is
            // wanted in whole tiles, and one entry per tile makes the test that
            // uses it cheap enough to run on every candidate cell.
            int tilesWide = Mathf.CeilToInt(world.Width / (float)samples);
            int tilesHigh = Mathf.CeilToInt(world.Height / (float)samples);
            var tileOwner = new int[tilesWide * tilesHigh];
            Array.Fill(tileOwner, -1);

            // How wide the channel between two masses has to be.
            //
            // Two tiles of open water is not enough, and the reason is the
            // BEACH: it turns the ground either side of a coast into sand, which
            // is land, so a narrow channel is filled in from both banks and the
            // two masses read - and draw - as one. Measured at 128x80 asking for
            // four landmasses: two components, the larger 2839 tiles. Excluding
            // sand from the same count split it into 2006 + 314 + 63, which is
            // the sand isthmus showing up as exactly what it was.
            //
            // So the channel needs a water core that survives a beach eating
            // inward from each side, plus a tile of slack so the core is not lost
            // when the sample grid is reduced to tiles.
            int beachTiles = Mathf.CeilToInt(Mathf.Max(0.0f, settings.BeachWidth));
            int gapTiles = (beachTiles * 2) + 2;

            var frontiers = new List<PriorityQueue<int, float>>(seeds.Length);
            var share = new float[seeds.Length];

            // How far the noise may reorder growth: higher is a more ragged
            // coast, and too high stops the ordering being distance-like at all,
            // which is when tendrils appear.
            //
            // It is RELATIVE to how big each mass will be, not an
            // absolute number of cells. Growth is ordered by distance in samples
            // while the old wobble was scaled in tiles, so one fixed amount of
            // noise was a gentle coastline on a large mass and a 50% distortion
            // on a small one - which is why the smallest island on a map came
            // out spindly (20% of its own bounding box) while the largest was
            // fine. Every mass now gets the same proportion of wobble.
            float raggedness = Mathf.Clamp(settings.CoastlineRaggedness, 0.0f, 4.0f) * 0.45f;
            float radius = Mathf.Sqrt(target / (float)Mathf.Max(1, seeds.Length) / Mathf.Pi);
            float scale = Mathf.Max(1.0f, radius);
            float radiusTiles = Mathf.Max(2.0f, radius / samples);

            // The coastline gets its OWN noise, at the scale of the landmass it
            // is shaping. The shared shape noise is tuned for terrain detail, and
            // detail noise cannot make a coastline: it varies far faster than the
            // mass grows, so every wobble is averaged out within a step or two
            // and what comes back is a circle with a slightly fuzzy edge.
            // Rendered, the masses were discs.
            //
            // Bays and peninsulas are a LOW frequency effect - about one period
            // across a good fraction of the mass - with a few octaves on top for
            // the smaller irregularities. That is the difference between an
            // outline that reads as a coast and one that reads as a compass
            // drawing.
            var coast = new FastNoiseLite
            {
                Seed = settings.Seed + 7919,
                NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
                FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
                FractalOctaves = 4,
                FractalLacunarity = 2.0f,
                FractalGain = 0.5f,
                Frequency = 1.0f / (radiusTiles * 1.7f),
            };

            // Landmasses are not all the same size. Growing them in strict
            // round-robin gave every island on a map an identical area, which
            // together with the seed lattice made an archipelago look like a
            // tray of biscuits. Each mass gets a weight instead, and whichever
            // mass is furthest behind its own weight grows next.
            var weights = new float[seeds.Length];
            var weighting = new RandomNumberGenerator { Seed = (ulong)(settings.Seed + 104729) };
            float weightTotal = 0.0f;
            for (int mass = 0; mass < seeds.Length; mass++)
            {
                weights[mass] = Mathf.Lerp(0.45f, 1.85f, weighting.Randf());
                weightTotal += weights[mass];
            }
            for (int mass = 0; mass < seeds.Length; mass++)
                weights[mass] /= weightTotal;

            foreach (int seed in seeds)
            {
                var queue = new PriorityQueue<int, float>();
                queue.Enqueue(seed, 0.0f);
                frontiers.Add(queue);
            }

            int placed = 0;
            var exhausted = new bool[seeds.Length];
            int living = seeds.Length;

            while (placed < target && living > 0)
            {
                // Whichever mass is furthest behind the share its weight asks
                // for. That is plain round-robin when the weights are equal, and
                // either way it keeps the masses growing TOGETHER: a mass grown
                // to completion before its neighbour started would take all the
                // room and leave the neighbour a sliver.
                int mass = -1;
                float behind = float.MaxValue;
                for (int candidate = 0; candidate < seeds.Length; candidate++)
                {
                    if (exhausted[candidate])
                        continue;

                    float ratio = share[candidate] / weights[candidate];
                    if (ratio < behind)
                    {
                        behind = ratio;
                        mass = candidate;
                    }
                }

                if (mass < 0)
                    break;

                PriorityQueue<int, float> queue = frontiers[mass];
                int cell = -1;
                while (queue.Count > 0)
                {
                    int candidate = queue.Dequeue();
                    if (claimed[candidate] || !eligible[candidate])
                        continue;
                    if (ForeignClaimNear(
                            tileOwner, tilesWide, tilesHigh,
                            (candidate % world.Width) / samples,
                            (candidate / world.Width) / samples,
                            mass, gapTiles))
                        continue;
                    cell = candidate;
                    break;
                }

                if (cell < 0)
                {
                    // Boxed in by its neighbours or by the map edge. Its share
                    // passes to the masses that can still grow, so the map still
                    // reaches the land coverage that was asked for.
                    exhausted[mass] = true;
                    living--;
                    continue;
                }

                claimed[cell] = true;
                world.Land[cell] = true;
                tileOwner[(((cell / world.Width) / samples) * tilesWide)
                          + ((cell % world.Width) / samples)] = mass;
                placed++;
                share[mass]++;

                int x = cell % world.Width;
                int y = cell / world.Width;
                int seedX = seeds[mass] % world.Width;
                int seedY = seeds[mass] / world.Width;

                for (int side = 0; side < 4; side++)
                {
                    int nx = x + (side == 0 ? 1 : side == 1 ? -1 : 0);
                    int ny = y + (side == 2 ? 1 : side == 3 ? -1 : 0);
                    if (!world.InBounds(nx, ny))
                        continue;

                    int at = world.Index(nx, ny);
                    if (claimed[at] || !eligible[at])
                        continue;

                    float dx = nx - seedX;
                    float dy = ny - seedY;
                    float distance = Mathf.Sqrt((dx * dx) + (dy * dy));

                    Vector2 point = world.TileCentre(nx, ny);
                    float wobble = coast.GetNoise2D(point.X, point.Y) * raggedness * scale;

                    queue.Enqueue(at, distance + wobble);
                }
            }
        }
    }
}

using Godot;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Beep.ECS
{
    /// <summary>
    /// Reserves an area around every start, validates it, and places the start kit in it - and,
    /// where asked, measures how far every tile is from the starts and scales the far country by it.
    ///
    /// The start position stage chooses TILES; a playable start is an AREA - room for a
    /// headquarters, ways out, space from the neighbour and the resources every player is
    /// promised. This follows how the genre does it. Ensemble's random-map patent gives each
    /// player one contiguous area with a minimum distance between areas and water excluded,
    /// then places a per-player object kit in a nested loop per area and per object type,
    /// relaxing a critical object's constraints in a fixed order when it will not fit. Age of
    /// Empires II grows every player land from its origin simultaneously until they meet;
    /// 0 A.D. gives every base the same kit at authored distances; Age of Empires puts neutral
    /// objects BETWEEN player lands, at a minimum distance from every land (FEAT-14).
    ///
    /// Distance from the starts is Factorio's rule for its world: resources richer the farther
    /// they are from spawn, so leaving home pays. It is a fact about the starts, not about their
    /// areas, so it is measured whether or not areas are reserved.
    ///
    /// It never re-scores or moves a start. Unusable starts stay where they are and are
    /// reported, with the reason.
    /// </summary>
    internal static class TerrainStartAreaStage
    {
        /// <summary>Salt for the kit's candidate ordering hash.</summary>
        private const int KitHashSalt = 81929;

        /// <summary>Cells that must separate two kit placements of the same resource.</summary>
        private const int KitSpacing = 4;

        /// <summary>Radius of an underground deposit a kit entry stamps.</summary>
        private const int UndergroundDiscRadius = 2;

        /// <summary>
        /// How much nearer one start may be than the next for a tile to lie BETWEEN them: the width,
        /// in cells, of the band along the line where the two nearest starts are equally far.
        /// </summary>
        private const float NeutralBandCells = 2f;

        public static void Apply(TerrainGenerationBuffer world, TerrainGenerationSettings settings,
            TerrainStartKitRules kit, TerrainResourceRules resources, CancellationToken cancellation = default)
        {
            int starts = world.StartPositions.Count;
            if (starts == 0)
                return;

            // FEAT-14. Measured and applied before the kit, so the deposits the kit stamps keep the
            // richness the kit promises; only what the subsurface stage laid is scaled.
            if (settings.StartDistanceScaling > 0f)
            {
                MeasureStartDistance(world, cancellation);
                ScaleUndergroundRichness(world, settings.StartDistanceScaling, cancellation);
            }

            int radius = settings.StartAreaRadius;
            if (radius <= 0)
                return;

            cancellation.ThrowIfCancellationRequested();
            int wide = world.CellsWide;
            int high = world.CellsHigh;
            byte[] area = world.CellStartArea;
            var problems = new List<string>[starts];
            for (int k = 0; k < starts; k++) problems[k] = new List<string>();

            // Growth: one queue seeded with every footprint in start order, so all areas grow
            // at once and a contested cell goes to whichever reached it first. Every claimed
            // cell is 4-connected to its own footprint over claimable ground by construction.
            int[] queue = world.IntScratchA;
            int tail = 0;
            for (int k = 0; k < starts; k++)
            {
                foreach (Vector2I cell in GridFootprint.Cells(world.StartPositions[k], kit.HqFootprint))
                {
                    int index = world.CellIndex(cell.X, cell.Y);
                    if (area[index] != 0 || TooCloseToOtherArea(area, wide, high, cell.X, cell.Y, k, kit.AreaGap))
                    {
                        if (!problems[k].Contains("footprint_overlap")) problems[k].Add("footprint_overlap");
                        continue;
                    }
                    area[index] = (byte)(k + 1);
                    queue[tail++] = index;
                }
            }

            Span<int> around = stackalloc int[4];
            int radiusSquared = radius * radius;
            int head = 0;
            while (head < tail)
            {
                if ((head & 1023) == 0) cancellation.ThrowIfCancellationRequested();
                int current = queue[head++];
                int k = area[current] - 1;
                Vector2I origin = world.StartPositions[k];
                int sides = TerrainGeometry.Neighbours4(current, wide, high, around);
                for (int side = 0; side < sides; side++)
                {
                    int neighbour = around[side];
                    if (area[neighbour] != 0 || !Claimable(world, neighbour)) continue;
                    int x = neighbour % wide, y = neighbour / wide;
                    int dx = x - origin.X, dy = y - origin.Y;
                    if (dx * dx + dy * dy > radiusSquared) continue;
                    if (TooCloseToOtherArea(area, wide, high, x, y, k, kit.AreaGap)) continue;
                    area[neighbour] = (byte)(k + 1);
                    queue[tail++] = neighbour;
                }
            }

            // Each area's cells in scan order, so everything downstream is order-deterministic.
            var cells = new List<int>[starts];
            for (int k = 0; k < starts; k++) cells[k] = new List<int>();
            for (int index = 0; index < area.Length; index++)
                if (area[index] != 0) cells[area[index] - 1].Add(index);

            int minimumCells = kit.MinAreaCells > 0
                ? kit.MinAreaCells
                : Mathf.CeilToInt(0.6f * Mathf.Pi * radiusSquared);
            var kitCells = new HashSet<int>();
            for (int k = 0; k < starts; k++)
            {
                cancellation.ThrowIfCancellationRequested();
                Vector2I origin = world.StartPositions[k];
                var footprint = new Rect2I(origin, kit.HqFootprint);
                int exits = CountExits(world, area, footprint, k);
                if (exits < kit.ExitCount) problems[k].Add("no_exit");
                if (cells[k].Count < minimumCells) problems[k].Add("area_too_small");

                var placements = new List<TerrainStartAreaPlacement>();
                for (int entryIndex = 0; entryIndex < kit.Entries.Count; entryIndex++)
                {
                    // A neutral entry belongs to no start; it is placed once below, between them.
                    if (kit.Entries[entryIndex].Scope != TerrainStartKitScope.PerPlayer) continue;
                    PlaceEntry(world, settings, kit.Entries[entryIndex], entryIndex, resources, radius, k,
                        origin, footprint, cells[k], kitCells, placements, problems[k]);
                }

                world.StartAreas.Add(new TerrainStartAreaReport(k, origin, kit.HqFootprint, cells[k].Count,
                    exits, placements.AsReadOnly(), problems[k].AsReadOnly()));
            }

            // One warning lists every unusable start, as the start shortfall above it does: a
            // report on the field is not something a developer sees unless they ask for it.
            var unusable = new List<string>();
            foreach (TerrainStartAreaReport report in world.StartAreas)
                if (!report.Usable) unusable.Add($"start {report.Index} at {report.Origin}: {string.Join(", ", report.Problems)}");
            if (unusable.Count > 0)
                GD.PushWarning($"{unusable.Count} of {starts} start areas are unusable - {string.Join("; ", unusable)}.");

            // After every start has had its kit: a contested site takes what the players' own
            // areas left, never the reverse.
            PlaceNeutralSites(world, settings, kit, resources, area, kitCells, cancellation);
        }

        /// <summary>
        /// Every tile's distance to the nearest start, in whole cells, through the one exact
        /// Euclidean transform the coast and the shoreline already use - not a second one.
        /// </summary>
        private static void MeasureStartDistance(TerrainGenerationBuffer world, CancellationToken cancellation)
        {
            int wide = world.CellsWide, high = world.CellsHigh;
            var starts = new bool[wide * high];
            foreach (Vector2I start in world.StartPositions)
                if (world.CellInBounds(start.X, start.Y)) starts[world.CellIndex(start.X, start.Y)] = true;
            // The float scratch is sample-sized, never smaller than the cell grid; the transform
            // writes the first wide*high entries and the stage reads nothing else.
            float[] squared = world.FloatScratchA;
            TerrainEuclideanDistance.Squared(starts, new Vector2I(wide, high), true, squared, cancellation);
            ushort[] distance = world.CellStartDistance;
            for (int index = 0; index < distance.Length; index++)
                distance[index] = (ushort)Mathf.Min(ushort.MaxValue, Mathf.RoundToInt(Mathf.Sqrt(squared[index])));
        }

        /// <summary>
        /// Deposits the subsurface stage laid grow richer with distance from the nearest start:
        /// richness x lerp(1 - s/2, 1 + s/2, d / farthest) - a multiplicative pass over deposits that
        /// already exist, never a second placer. The result stays a deposit: at least the subsurface
        /// stage's own minimum, and at most 1, the scale every reader of richness bands and clamps
        /// against, so the far country saturates rather than leaving it.
        /// </summary>
        private static void ScaleUndergroundRichness(TerrainGenerationBuffer world, float scaling, CancellationToken cancellation)
        {
            ushort[] distance = world.CellStartDistance;
            int farthest = 1;
            foreach (ushort cells in distance) farthest = Math.Max(farthest, cells);
            string[] deposits = world.CellUndergroundResource;
            float[] richness = world.CellUndergroundRichness;
            float nearFactor = 1f - 0.5f * scaling, farFactor = 1f + 0.5f * scaling;
            for (int index = 0; index < deposits.Length; index++)
            {
                if ((index & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                if (deposits[index].Length == 0) continue;
                float factor = Mathf.Lerp(nearFactor, farFactor, distance[index] / (float)farthest);
                richness[index] = Mathf.Clamp(richness[index] * factor, TerrainSubsurfaceStage.MinimumRichness, 1f);
            }
        }

        /// <summary>
        /// The kit's Neutral entries, between the starts: land outside every area and its gap
        /// where the nearest start is at most <see cref="NeutralBandCells"/> nearer than the next.
        /// Count per start of each, through the same placement and relaxation as a player's own kit.
        /// </summary>
        private static void PlaceNeutralSites(TerrainGenerationBuffer world, TerrainGenerationSettings settings,
            TerrainStartKitRules kit, TerrainResourceRules resources, byte[] area, HashSet<int> kitCells,
            CancellationToken cancellation)
        {
            int starts = world.StartPositions.Count;
            var placements = new List<TerrainStartAreaPlacement>();
            var problems = new List<string>();
            float[]? nearest = null;
            bool neutral = false;
            for (int entryIndex = 0; entryIndex < kit.Entries.Count; entryIndex++)
            {
                TerrainStartKitRules.Entry entry = kit.Entries[entryIndex];
                if (entry.Scope != TerrainStartKitScope.Neutral) continue;
                neutral = true;
                if (starts < 2)
                {
                    // One start has no "between": the entry is a promise this map cannot keep.
                    problems.Add($"neutral_needs_two_starts:{entry.ResourceId}");
                    continue;
                }
                if (Definition(entry, resources, problems) is not { } definition) continue;

                nearest ??= NeutralBand(world, kit, area, cancellation);
                int wide = world.CellsWide;
                var candidates = new List<(float Order, int Index)>();
                for (int index = 0; index < nearest.Length; index++)
                {
                    if (float.IsNaN(nearest[index])) continue;
                    if (!definition.Supports(world.CellTerrain[index], world.CellRelief[index])) continue;
                    candidates.Add((TerrainGeometry.Hash01(index % wide, index / wide, settings.Seed + KitHashSalt + entryIndex * 131), index));
                }
                candidates.Sort(static (a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Index.CompareTo(b.Index));

                float minimum = entry.MinDistance;
                float maximum = entry.MaxDistance > 0 ? entry.MaxDistance : float.PositiveInfinity;
                float[] band = nearest;
                Place(world, entry, definition, resources, candidates,
                    index => band[index] >= minimum && band[index] <= maximum,
                    owner: 0, entry.Count * starts, kitCells, placements, problems);
            }

            if (!neutral) return;
            world.NeutralSites = new TerrainNeutralSitesReport(placements.AsReadOnly(), problems.AsReadOnly());
            if (problems.Count > 0)
                GD.PushWarning($"Neutral sites fell short - {string.Join(", ", problems)}.");
        }

        /// <summary>
        /// Per tile, the distance to the nearest start where the tile is in the neutral band, NaN
        /// where it is not: in an area, within the kit's gap of one, not claimable ground, or nearer
        /// one start than the next by more than the band.
        /// </summary>
        private static float[] NeutralBand(TerrainGenerationBuffer world, TerrainStartKitRules kit, byte[] area,
            CancellationToken cancellation)
        {
            int wide = world.CellsWide, high = world.CellsHigh;
            var nearest = new float[wide * high];
            for (int index = 0; index < nearest.Length; index++)
            {
                if ((index & 1023) == 0) cancellation.ThrowIfCancellationRequested();
                int x = index % wide, y = index / wide;
                if (area[index] != 0 || !Claimable(world, index)
                    || TooCloseToOtherArea(area, wide, high, x, y, -1, kit.AreaGap))
                {
                    nearest[index] = float.NaN;
                    continue;
                }
                float first = float.PositiveInfinity, second = float.PositiveInfinity;
                foreach (Vector2I start in world.StartPositions)
                {
                    float dx = x - start.X, dy = y - start.Y;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d < first) { second = first; first = d; }
                    else if (d < second) second = d;
                }
                nearest[index] = second - first <= NeutralBandCells ? first : float.NaN;
            }
            return nearest;
        }

        /// <summary>Dry, not mountainous and not blocked by default - ground an area may reserve.</summary>
        private static bool Claimable(TerrainGenerationBuffer world, int index)
            => world.CellWater[index] == WaterBody.None && world.CellRelief[index] != TerrainRelief.Mountains
                && !TerrainKindCatalog.Standard.BlockedByDefault(world.CellTerrain[index]);

        /// <summary>
        /// Whether another start's area holds a cell within <paramref name="gap"/> (Chebyshev) of this one.
        /// <paramref name="k"/> -1 asks about every area.
        /// </summary>
        private static bool TooCloseToOtherArea(byte[] area, int wide, int high, int x, int y, int k, int gap)
        {
            for (int dy = -gap; dy <= gap; dy++)
            {
                int ny = y + dy;
                if (ny < 0 || ny >= high) continue;
                for (int dx = -gap; dx <= gap; dx++)
                {
                    int nx = x + dx;
                    if (nx < 0 || nx >= wide) continue;
                    byte owner = area[ny * wide + nx];
                    if (owner != 0 && owner != k + 1) return true;
                }
            }
            return false;
        }

        /// <summary>Area cells outside the footprint that touch one of its sides.</summary>
        private static int CountExits(TerrainGenerationBuffer world, byte[] area, Rect2I footprint, int k)
        {
            int exits = 0;
            int wide = world.CellsWide, high = world.CellsHigh;
            for (int y = footprint.Position.Y - 1; y <= footprint.End.Y; y++)
            {
                for (int x = footprint.Position.X - 1; x <= footprint.End.X; x++)
                {
                    if (x < 0 || y < 0 || x >= wide || y >= high || footprint.HasPoint(new Vector2I(x, y))) continue;
                    // 4-adjacent to the footprint: exactly one axis is outside it; corners are not.
                    bool insideX = x >= footprint.Position.X && x < footprint.End.X;
                    bool insideY = y >= footprint.Position.Y && y < footprint.End.Y;
                    if (insideX == insideY) continue;
                    if (area[y * wide + x] == k + 1) exits++;
                }
            }
            return exits;
        }

        /// <summary>
        /// The catalog definition of an entry's resource, or null with the reason recorded: not in the
        /// world's catalog (a configuration fault whether or not the entry is critical), or liquid,
        /// which no area and no neutral band can hold - both exclude water by construction.
        /// </summary>
        private static TerrainResourceRules.Entry? Definition(TerrainStartKitRules.Entry entry,
            TerrainResourceRules resources, List<string> problems)
        {
            if (resources.Find(entry.ResourceId) is not { } definition)
            {
                problems.Add($"unknown_resource:{entry.ResourceId}");
                return null;
            }
            if (definition.Stratum == ResourceStratum.Liquid)
            {
                problems.Add($"kit_entry_unplaceable:{entry.ResourceId}");
                return null;
            }
            return definition;
        }

        private static void PlaceEntry(TerrainGenerationBuffer world, TerrainGenerationSettings settings,
            TerrainStartKitRules.Entry entry, int entryIndex, TerrainResourceRules resources, int radius, int k,
            Vector2I origin, Rect2I footprint, List<int> areaCells, HashSet<int> kitCells,
            List<TerrainStartAreaPlacement> placements, List<string> problems)
        {
            if (Definition(entry, resources, problems) is not { } definition) return;

            int wide = world.CellsWide;
            int minSquared = entry.MinDistance * entry.MinDistance;
            int maxDistance = entry.MaxDistance > 0 ? entry.MaxDistance : radius;
            int maxSquared = maxDistance * maxDistance;
            // Candidates: area cells outside the footprint whose ground supports the resource,
            // ordered by a seeded hash so every start draws the same way for a given seed.
            var candidates = new List<(float Order, int Index)>();
            foreach (int index in areaCells)
            {
                int x = index % wide, y = index / wide;
                if (footprint.HasPoint(new Vector2I(x, y))) continue;
                if (!definition.Supports(world.CellTerrain[index], world.CellRelief[index])) continue;
                candidates.Add((TerrainGeometry.Hash01(x, y, settings.Seed + KitHashSalt + entryIndex * 131), index));
            }
            candidates.Sort(static (a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Index.CompareTo(b.Index));

            // A start whose area already holds this underground resource has its kit: the promise is
            // that the deposit is within reach, not that the kit stamps another beside it.
            if (definition.Stratum == ResourceStratum.Underground)
                foreach (int index in areaCells)
                    if (world.CellUndergroundResource[index] == entry.ResourceId) return;

            Place(world, entry, definition, resources, candidates,
                index =>
                {
                    int dx = index % wide - origin.X, dy = index / wide - origin.Y;
                    int d = dx * dx + dy * dy;
                    return d >= minSquared && d <= maxSquared;
                },
                owner: (byte)(k + 1), entry.Count, kitCells, placements, problems);
        }

        /// <summary>
        /// The placement both scopes share: candidates in their seeded order, placed until the target
        /// is met, the constraints relaxed in a fixed order when the ground will not take it.
        ///
        /// Underground: (0) in band, (1) anywhere a candidate is, on empty cells. Surface: (0) as
        /// authored, (1) the band widened to every candidate, (2) same-resource spacing dropped,
        /// (3) a Bonus non-kit resource overwritten. Supported ground, dryness, relief and the
        /// ground the candidates were drawn from - an area, or the band between starts - are never
        /// relaxed. <paramref name="owner"/> is the area a stamped deposit may cover: k+1 for a
        /// start's own kit, 0 - no area - for a neutral site.
        /// </summary>
        private static void Place(TerrainGenerationBuffer world, TerrainStartKitRules.Entry entry,
            TerrainResourceRules.Entry definition, TerrainResourceRules resources,
            List<(float Order, int Index)> candidates, Func<int, bool> inBand, byte owner, int target,
            HashSet<int> kitCells, List<TerrainStartAreaPlacement> placements, List<string> problems)
        {
            int wide = world.CellsWide;
            if (definition.Stratum == ResourceStratum.Underground)
            {
                int stamped = 0;
                for (int level = 0; level <= 1 && stamped < target; level++)
                {
                    foreach ((_, int index) in candidates)
                    {
                        if (stamped >= target) break;
                        if (world.CellUndergroundResource[index].Length > 0) continue;
                        if (level == 0 && !inBand(index)) continue;
                        StampDeposit(world, definition, index, owner);
                        placements.Add(new(entry.ResourceId, new Vector2I(index % wide, index / wide), level));
                        stamped++;
                    }
                }
                if (stamped < target) problems.Add(Shortfall(entry));
                return;
            }

            int placed = 0;
            for (int level = 0; level <= 3 && placed < target; level++)
            {
                foreach ((_, int index) in candidates)
                {
                    if (placed >= target) break;
                    string existing = world.Resource[index];
                    if (level < 3 ? existing.Length > 0 : !Overwritable(resources, existing, index, kitCells)) continue;
                    if (level == 0 && !inBand(index)) continue;
                    if (level <= 1 && !SpacedFromSame(world, index, entry.ResourceId)) continue;
                    world.Resource[index] = entry.ResourceId;
                    kitCells.Add(index);
                    placements.Add(new(entry.ResourceId, new Vector2I(index % wide, index / wide), level));
                    placed++;
                }
            }
            if (placed < target) problems.Add(Shortfall(entry));
        }

        /// <summary>The problem an entry that fell short reports.</summary>
        private static string Shortfall(TerrainStartKitRules.Entry entry)
            => entry.Critical ? $"missing_critical:{entry.ResourceId}" : TerrainStartAreaReport.MissingPrefix + entry.ResourceId;

        /// <summary>A resource the kit may replace: present, not the kit's own, and authored Bonus.</summary>
        private static bool Overwritable(TerrainResourceRules resources, string existing, int index, HashSet<int> kitCells)
            => existing.Length > 0 && !kitCells.Contains(index)
                && resources.Find(existing) is { Category: ResourceCategory.Bonus };

        /// <summary>No placement of the same resource within <see cref="KitSpacing"/> cells.</summary>
        private static bool SpacedFromSame(TerrainGenerationBuffer world, int index, string id)
        {
            int wide = world.CellsWide, high = world.CellsHigh;
            int x = index % wide, y = index / wide;
            for (int ny = Math.Max(0, y - KitSpacing + 1); ny <= Math.Min(high - 1, y + KitSpacing - 1); ny++)
                for (int nx = Math.Max(0, x - KitSpacing + 1); nx <= Math.Min(wide - 1, x + KitSpacing - 1); nx++)
                {
                    int dx = nx - x, dy = ny - y;
                    if (dx * dx + dy * dy < KitSpacing * KitSpacing && world.Resource[ny * wide + nx] == id) return false;
                }
            return true;
        }

        /// <summary>
        /// A radius-2 underground deposit at half richness, on empty cells of <paramref name="owner"/>'s
        /// ground only - a start's own area (k+1), or for a neutral site no area at all (0) - and only
        /// where the resource can lie, by the same Supports rule the subsurface stage lays deposits by:
        /// the centre was chosen on supporting ground, and its rim must not run under ground that the
        /// catalog says never holds the resource.
        /// </summary>
        private static void StampDeposit(TerrainGenerationBuffer world, TerrainResourceRules.Entry definition, int centre, byte owner)
        {
            int wide = world.CellsWide, high = world.CellsHigh;
            int cx = centre % wide, cy = centre / wide;
            for (int y = Math.Max(0, cy - UndergroundDiscRadius); y <= Math.Min(high - 1, cy + UndergroundDiscRadius); y++)
                for (int x = Math.Max(0, cx - UndergroundDiscRadius); x <= Math.Min(wide - 1, cx + UndergroundDiscRadius); x++)
                {
                    int dx = x - cx, dy = y - cy;
                    int index = y * wide + x;
                    if (dx * dx + dy * dy > UndergroundDiscRadius * UndergroundDiscRadius) continue;
                    if (world.CellStartArea[index] != owner || world.CellUndergroundResource[index].Length > 0) continue;
                    if (!definition.Supports(world.CellTerrain[index], world.CellRelief[index])) continue;
                    world.CellUndergroundResource[index] = definition.Id;
                    world.CellUndergroundRichness[index] = 0.5f;
                    world.CellUndergroundDepth[index] = (byte)definition.Depth;
                }
        }
    }
}

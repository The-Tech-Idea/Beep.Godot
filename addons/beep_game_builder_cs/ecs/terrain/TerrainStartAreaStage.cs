using Godot;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Beep.ECS
{
    /// <summary>
    /// Reserves an area around every start, validates it, and places the start kit in it.
    ///
    /// The start position stage chooses TILES; a playable start is an AREA - room for a
    /// headquarters, ways out, space from the neighbour and the resources every player is
    /// promised. This follows how the genre does it. Ensemble's random-map patent gives each
    /// player one contiguous area with a minimum distance between areas and water excluded,
    /// then places a per-player object kit in a nested loop per area and per object type,
    /// relaxing a critical object's constraints in a fixed order when it will not fit. Age of
    /// Empires II grows every player land from its origin simultaneously until they meet;
    /// 0 A.D. gives every base the same kit at authored distances.
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

        public static void Apply(TerrainGenerationBuffer world, TerrainGenerationSettings settings,
            TerrainStartKitRules kit, TerrainResourceRules resources, CancellationToken cancellation = default)
        {
            int radius = settings.StartAreaRadius;
            int starts = world.StartPositions.Count;
            if (radius <= 0 || starts == 0)
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
                    PlaceEntry(world, settings, kit.Entries[entryIndex], entryIndex, resources, radius, k,
                        origin, footprint, cells[k], kitCells, placements, problems[k]);

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
        }

        /// <summary>Dry, not mountainous and not blocked by default - ground an area may reserve.</summary>
        private static bool Claimable(TerrainGenerationBuffer world, int index)
            => world.CellWater[index] == WaterBody.None && world.CellRelief[index] != TerrainRelief.Mountains
                && !TerrainKindCatalog.Standard.BlockedByDefault(world.CellTerrain[index]);

        /// <summary>Whether another start's area holds a cell within <paramref name="gap"/> (Chebyshev) of this one.</summary>
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

        private static void PlaceEntry(TerrainGenerationBuffer world, TerrainGenerationSettings settings,
            TerrainStartKitRules.Entry entry, int entryIndex, TerrainResourceRules resources, int radius, int k,
            Vector2I origin, Rect2I footprint, List<int> areaCells, HashSet<int> kitCells,
            List<TerrainStartAreaPlacement> placements, List<string> problems)
        {
            if (resources.Find(entry.ResourceId) is not { } definition)
            {
                // Not in the world's catalog (or resources are off): a configuration fault,
                // blocking whether or not the entry is critical.
                problems.Add($"unknown_resource:{entry.ResourceId}");
                return;
            }
            if (definition.Stratum == ResourceStratum.Liquid)
            {
                // Areas exclude water by construction, so a liquid entry can never be placed.
                problems.Add($"kit_entry_unplaceable:{entry.ResourceId}");
                return;
            }

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

            bool InBand(int index)
            {
                int dx = index % wide - origin.X, dy = index / wide - origin.Y;
                int d = dx * dx + dy * dy;
                return d >= minSquared && d <= maxSquared;
            }

            if (definition.Stratum == ResourceStratum.Underground)
            {
                foreach (int index in areaCells)
                    if (world.CellUndergroundResource[index] == entry.ResourceId) return;
                int stamped = 0;
                for (int level = 0; level <= 1 && stamped < entry.Count; level++)
                {
                    foreach ((_, int index) in candidates)
                    {
                        if (stamped >= entry.Count) break;
                        if (world.CellUndergroundResource[index].Length > 0) continue;
                        if (level == 0 && !InBand(index)) continue;
                        StampDeposit(world, definition, index, k);
                        placements.Add(new(entry.ResourceId, new Vector2I(index % wide, index / wide), level));
                        stamped++;
                    }
                }
                if (stamped < entry.Count)
                    problems.Add(entry.Critical ? $"missing_critical:{entry.ResourceId}" : TerrainStartAreaReport.MissingPrefix + entry.ResourceId);
                return;
            }

            // Surface. Relaxation order when short: (0) as authored, (1) the band widened to the
            // whole area, (2) same-resource spacing dropped, (3) a Bonus non-kit resource
            // overwritten. Supported ground, dryness and relief are never relaxed.
            int placed = 0;
            for (int level = 0; level <= 3 && placed < entry.Count; level++)
            {
                foreach ((_, int index) in candidates)
                {
                    if (placed >= entry.Count) break;
                    string existing = world.Resource[index];
                    if (level < 3 ? existing.Length > 0 : !Overwritable(resources, existing, index, kitCells)) continue;
                    if (level == 0 && !InBand(index)) continue;
                    if (level <= 1 && !SpacedFromSame(world, index, entry.ResourceId)) continue;
                    world.Resource[index] = entry.ResourceId;
                    kitCells.Add(index);
                    placements.Add(new(entry.ResourceId, new Vector2I(index % wide, index / wide), level));
                    placed++;
                }
            }
            if (placed < entry.Count)
                problems.Add(entry.Critical ? $"missing_critical:{entry.ResourceId}" : TerrainStartAreaReport.MissingPrefix + entry.ResourceId);
        }

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

        /// <summary>A radius-2 underground deposit at half richness, inside this start's area, on empty cells only.</summary>
        private static void StampDeposit(TerrainGenerationBuffer world, TerrainResourceRules.Entry definition, int centre, int k)
        {
            int wide = world.CellsWide, high = world.CellsHigh;
            int cx = centre % wide, cy = centre / wide;
            for (int y = Math.Max(0, cy - UndergroundDiscRadius); y <= Math.Min(high - 1, cy + UndergroundDiscRadius); y++)
                for (int x = Math.Max(0, cx - UndergroundDiscRadius); x <= Math.Min(wide - 1, cx + UndergroundDiscRadius); x++)
                {
                    int dx = x - cx, dy = y - cy;
                    int index = y * wide + x;
                    if (dx * dx + dy * dy > UndergroundDiscRadius * UndergroundDiscRadius) continue;
                    if (world.CellStartArea[index] != k + 1 || world.CellUndergroundResource[index].Length > 0) continue;
                    world.CellUndergroundResource[index] = definition.Id;
                    world.CellUndergroundRichness[index] = 0.5f;
                    world.CellUndergroundDepth[index] = (byte)definition.Depth;
                }
        }
    }
}

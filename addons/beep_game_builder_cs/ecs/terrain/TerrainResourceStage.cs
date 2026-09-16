using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Which catalogue a map draws its resources from. A map is a setting, and
    /// the setting decides what is worth digging up: a lunar survey has no
    /// cattle and an oilfield has no ivory. Rolling one catalogue and filtering
    /// afterwards would leave whole terrains barren, so each set is its own
    /// weighted list.
    /// </summary>
    public enum ResourceSet
    {
        /// <summary>Food, luxuries and strategics, as a historical 4X uses them.</summary>
        Historical = 0,

        /// <summary>Hydrocarbon extraction: what a licence block is bought for.</summary>
        OilAndGas = 1,

        /// <summary>Off-world prospecting: volatiles, regolith and refractory metals.</summary>
        SpaceExploration = 2,
    }

    /// <summary>
    /// Scatters resources across the map, each only on terrain that would
    /// actually produce it - fish in the sea, deer on tundra, gems in jungle,
    /// iron in the hills.
    ///
    /// Resources are chosen per GAMEPLAY CELL rather than per sample, because a
    /// resource is something a tile has. Placement is spaced so a map does not
    /// end up with a single corner holding everything, and is driven entirely by
    /// the seeded hash, so the same seed lays out the same resources.
    /// </summary>
    internal static class TerrainResourceStage
    {
        /// <summary>Share of eligible LAND tiles that receive a resource.</summary>
        private const float Density = 0.085f;

        /// <summary>
        /// Water carries far fewer resources than land. Without this the sea -
        /// which is most of the map - ends up holding most of the resources,
        /// and the ocean reads as a field of markers.
        /// </summary>
        private const float WaterDensityScale = 0.22f;

        /// <summary>Tiles that must separate two of the same resource.</summary>
        private const int SameResourceSpacing = 4;

        /// <summary>
        /// The catalog the world was configured with, or the shipped set the
        /// ResourceSet axis names. One object, and the game side reads the same
        /// one - which is what stops the map generating a resource the economy
        /// has never heard of.
        /// </summary>
        public static void Apply(TerrainGenerationBuffer world, TerrainGenerationSettings settings, TerrainResourceRules? rules = null)
        {
            if (settings.ResourceDensity <= 0.0f)
                return;

            rules ??= TerrainResourceRules.Capture(settings);
            int wide = world.CellsWide;
            int high = world.CellsHigh;
            var spacing = new PlacementRows(wide);
            var choices = new Dictionary<(string, TerrainRelief, ResourceStratum), WeightedChoices>();

            for (int cellY = 0; cellY < high; cellY++)
            {
                spacing.BeginRow(cellY);
                for (int cellX = 0; cellX < wide; cellX++)
                {
                    int cell = (cellY * wide) + cellX;
                    string terrain = world.CellTerrain[cell];
                    bool isLand = world.CellWater[cell] == WaterBody.None;

                    float density = (isLand ? Density : Density * WaterDensityScale) * settings.ResourceDensity;
                    if (TerrainGeometry.Hash01(cellX, cellY, settings.Seed + 63601) > density)
                        continue;

                    // Land cells choose among SURFACE resources, water cells
                    // among LIQUID ones - two strata, one loop, the same
                    // hashes as before so a seed lays the map out unchanged.
                    // Underground fields are a different shape entirely and
                    // have their own stage.
                    ResourceStratum stratum = isLand ? ResourceStratum.Surface : ResourceStratum.Liquid;
                    var key = (terrain, world.CellRelief[cell], stratum);
                    if (!choices.TryGetValue(key, out WeightedChoices? candidates))
                    {
                        candidates = new WeightedChoices(rules, terrain, world.CellRelief[cell], stratum);
                        choices.Add(key, candidates);
                    }
                    string chosen = candidates.Choose(settings.Seed, cellX, cellY);
                    if (chosen.Length == 0)
                        continue;

                    if (!spacing.TryPlace(cellX, cellY, chosen))
                        continue;

                    if (isLand)
                        world.Resource[cell] = chosen;
                    else
                        world.CellLiquidResource[cell] = chosen;
                }
            }
        }

        /// <summary>
        /// Picks among everything this terrain supports, weighted, using a hash
        /// so the choice is stable for a seed.
        /// </summary>
        private sealed class WeightedChoices
        {
            private readonly List<(string Id, float Weight)> _entries = new();
            private readonly float _total;

            public WeightedChoices(TerrainResourceRules rules, string terrain, TerrainRelief relief, ResourceStratum stratum)
            {
                foreach (TerrainResourceRules.Entry definition in rules.Entries)
                {
                    if (definition.Stratum != stratum || !definition.Supports(terrain, relief))
                        continue;
                    _entries.Add((definition.Id, definition.Weight));
                    _total += definition.Weight;
                }
            }

            public string Choose(int seed, int cellX, int cellY)
            {
                if (_total <= 0.0f)
                    return string.Empty;
                // Preserve catalog order and subtraction, including float rounding,
                // so this optimization does not change seeded resource placement.
                float roll = TerrainGeometry.Hash01(cellX, cellY, seed + 63611) * _total;
                foreach (var (id, weight) in _entries)
                {
                    roll -= weight;
                    if (roll <= 0.0f)
                        return id;
                }
                return string.Empty;
            }
        }

        /// <summary>
        /// Keeps copies of one resource apart, so a map does not end up with all
        /// its iron in a single valley.
        /// </summary>
        private sealed class PlacementRows
        {
            private readonly int _width;
            private readonly string?[] _rows;

            public PlacementRows(int width)
            {
                _width = width;
                _rows = new string?[checked(width * SameResourceSpacing)];
            }

            public void BeginRow(int y)
                => Array.Clear(_rows, y % SameResourceSpacing * _width, _width);

            public bool TryPlace(int x, int y, string id)
            {
                // Generation is row-major. Only this row and the preceding three
                // can contain a placement strictly less than four cells away.
                for (int nearY = Math.Max(0, y - SameResourceSpacing + 1); nearY <= y; nearY++)
                {
                    int dy = nearY - y;
                    int row = nearY % SameResourceSpacing * _width;
                    for (int nearX = Math.Max(0, x - SameResourceSpacing + 1);
                         nearX <= Math.Min(_width - 1, x + SameResourceSpacing - 1); nearX++)
                    {
                        int dx = nearX - x;
                        if (dx * dx + dy * dy < SameResourceSpacing * SameResourceSpacing && _rows[row + nearX] == id)
                            return false;
                    }
                }
                _rows[y % SameResourceSpacing * _width + x] = id;
                return true;
            }
        }

        /// <summary>
        /// Searches every catalogue, because a saved map may carry ids from a set
        /// the generator is no longer configured for.
        /// </summary>
        // NO CALLER since VIEW-13 (2026-09-15). Its one reader was the map overlay's
        // category-coloured resource discs, removed because TerrainResourceRendererComponent
        // is the one resource drawer. Resource categories are a live gameplay fact (the
        // catalogs author them); their intended presentation consumer is ENH-13's grid
        // minimap resource tints. Kept, not deleted: removal is the owner's call.
        // FEAT-09's start kit, which the plan pointed here for "is this a Bonus resource",
        // reads the category from the captured TerrainResourceRules instead: the stage runs
        // on a worker against a detached copy of the configured catalog, and this searches
        // every live catalog, so it could answer for an id the map's catalog does not hold.
        public static ResourceCategory CategoryOf(string id)
            => ResourceCatalogs.FindAnywhere(id)?.Category ?? ResourceCategory.Bonus;

    }
}

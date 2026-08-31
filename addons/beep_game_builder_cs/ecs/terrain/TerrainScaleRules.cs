using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Derives the climate and biome constraints from the SIZE of the map, so a
    /// map is plausible without anyone having to tune it.
    ///
    /// The rules exist because the same numbers cannot be right at every scale.
    /// A climate model written for a world, applied to an island, hands fifty
    /// tiles the entire range from ice cap to jungle - and the result is not a
    /// place, it is a sampler. Two rules fix that, and both are how the strategy
    /// games do it:
    ///
    /// 1. A MAP COVERS THE CLIMATE IT IS BIG ENOUGH TO COVER. Latitude bands are
    ///    a property of the planet, not of the rectangle you are looking at. A
    ///    map that is a fifth of a world's height gets a fifth of the range, so
    ///    a small island sits in ONE climate. A full-size map gets the lot, pole
    ///    to pole, which is what makes a world map readable.
    ///
    /// 2. A BIOME MUST EARN ITS PLACE. Every biome region has to reach a minimum
    ///    size in TILES - an absolute, not a share - so the number of biomes
    ///    falls out of the area instead of being declared. A small island has
    ///    room for one or two; a continent has room for many. This is also what
    ///    stops a small temperate island growing an ice cap on its one peak: two
    ///    tiles of arctic is a threshold clipped, not a climate, so it is
    ///    dissolved into the rock it sits on.
    ///
    /// The second rule stays honest at the poles without a special case. An
    /// island at the top or bottom of a world map is snow across nearly all of
    /// its land, which clears the minimum comfortably and is left alone. The
    /// rule never asks where the island is, only whether the climate it claims
    /// is big enough to be one.
    /// </summary>
    public static class TerrainScaleRules
    {
        /// <summary>
        /// Height, in tiles, of a map that spans a whole planet pole to pole.
        /// A map's latitude range is its height measured against this.
        /// </summary>
        public const int WorldHeightTiles = 240;

        /// <summary>
        /// Narrowest climate band a map may be given. Below this the biome table
        /// has no range left to work with and every tile comes out the same.
        /// </summary>
        public const float MinLatitudeSpan = 0.12f;

        /// <summary>
        /// How many TILES a biome region must cover to survive, at any map size.
        /// Absolute on purpose: it is what makes the biome COUNT scale with area
        /// rather than the region size scaling with it, which would leave every
        /// map equally cluttered.
        /// </summary>
        public const int RegionTilesTarget = 80;

        /// <summary>
        /// The largest share of a landmass its lakes may cover.
        ///
        /// The minimum size stops a map being puddled; this stops the opposite,
        /// which a minimum cannot catch: one lake big enough to swallow the
        /// island it sits on. Rendered, several islands in an archipelago were a
        /// ring of beach around open water, and they measured as spindly land
        /// because a ring IS spindly - 24% of its own bounding box. A lake is a
        /// feature OF a landmass, so it is bounded by that landmass rather than
        /// by an absolute size.
        /// </summary>
        public const float MaxLakeShareOfLandmass = 0.30f;

        /// <summary>
        /// Minimum size, in TILES, for each kind of feature. Absolute rather
        /// than a share of the map, which is the whole point: it is what makes
        /// the COUNT of features scale with area instead of every map being
        /// equally cluttered. A small island has room for one lake; a continent
        /// has room for many, and neither is told how many to have.
        ///
        /// The numbers say what the thing IS. Fewer than six tiles of raised
        /// ground is not a range, a three-tile watercourse is not a river, and a
        /// lone tree is not a wood.
        /// </summary>
        public const int MinLakeTiles = 8;
        public const int MinReliefTiles = 6;
        public const int MinRiverTiles = 6;
        public const int MinFeatureTiles = 5;

        /// <summary>Constraints for a map of this size and land coverage.</summary>
        public readonly record struct Rules(float LatitudeSpan, float MinRegionFraction);

        public static Rules For(Vector2I bounds, float landCoverage)
        {
            int height = Mathf.Max(1, bounds.Y);
            float span = Mathf.Clamp(height / (float)WorldHeightTiles, MinLatitudeSpan, 1.0f);

            // The stage wants a share of the land, so the absolute target is
            // converted using the land this map is expected to have. Coverage is
            // what the generator was ASKED for rather than what it produced -
            // the rules have to be known before the land exists.
            float landTiles = Mathf.Max(1.0f, bounds.X * height * Mathf.Clamp(landCoverage, 0.05f, 1.0f));
            float fraction = Mathf.Clamp(RegionTilesTarget / landTiles, 0.0f, 0.5f);

            return new Rules(span, fraction);
        }
    }
}

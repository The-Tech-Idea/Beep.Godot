using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// The one place the grid components agree on what a terrain-kind string
    /// means for building and working.
    ///
    /// Seven components used to carry their own copy of the blocked-kinds
    /// default list, their own Normalize, and their own allowed/blocked check
    /// loops - and the copies had already drifted: one normalizer forgot the
    /// space-and-dash replacement, and none of the default lists agreed with
    /// TerrainTileSets.IsWaterKind about shallow water until that was fixed by
    /// hand in six files at once. Same consolidation Hash01 got on the terrain
    /// side, for the same reason.
    ///
    /// GridNavigationComponent deliberately keeps its OWN blocked default
    /// (without shallow_water): units wade at a cost while nothing may be
    /// built in the shallows.
    /// </summary>
    internal static class GridTerrainRules
    {
        /// <summary>
        /// The build-side default: the kinds nothing should be built, roaded,
        /// spawned, or scattered on. A fresh array per call, because exported
        /// Godot arrays must not share one instance across components.
        /// </summary>
        public static Godot.Collections.Array<string> DefaultBlockedTerrainKinds() => new()
        {
            "water",
            "sea",
            "ocean",
            "deep_water",
            "shallow_water",
            "lava"
        };

        public static string Normalize(string value)
            => string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');

        /// <summary>True when the normalized kind matches any entry of the exported list.</summary>
        public static bool MatchesAny(string normalizedKind, Godot.Collections.Array<string> kinds)
        {
            if (normalizedKind.Length == 0)
                return false;

            foreach (string kind in kinds)
            {
                if (Normalize(kind) == normalizedKind)
                    return true;
            }
            return false;
        }

        /// <summary>An empty allowed list allows everything; otherwise membership decides.</summary>
        public static bool IsAllowed(string normalizedKind, Godot.Collections.Array<string> allowed)
            => allowed.Count == 0 || MatchesAny(normalizedKind, allowed);
    }
}

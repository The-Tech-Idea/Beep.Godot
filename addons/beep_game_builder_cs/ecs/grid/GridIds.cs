namespace Beep.ECS
{
    /// <summary>
    /// The one normaliser for grid and terrain ids - resource ids, terrain kinds,
    /// job kinds, object ids.
    ///
    /// Eighteen private copies used to carry this rule and they had drifted apart:
    /// some forgot the space-and-dash replacement, some the lower-casing, some
    /// invented a fallback of their own. The drift was not cosmetic - the wallet
    /// keyed "Iron Ore" with spaces kept, the build catalogue keyed it lower-cased
    /// with spaces kept, and the resource bar kept the author's case, so one
    /// resource read as three ids across three components and a cost never matched
    /// the coins that should have paid it. This is that rule, once, the way
    /// <see cref="GridTerrainRules.Normalize"/> already documented it.
    ///
    /// The normaliser never invents a default: a caller that wants "" to mean
    /// something says so through <see cref="NormalizeOr"/>, so an empty id is never
    /// silently turned into "grass" or "work" where the caller did not ask.
    /// </summary>
    internal static class GridIds
    {
        /// <summary>Canonical id: trim, lower-invariant, ' ' and '-' become '_'. Empty stays empty.</summary>
        public static string Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');

        /// <summary>The canonical id, or the caller's fallback when the input is empty.</summary>
        public static string NormalizeOr(string? value, string fallback)
            => string.IsNullOrWhiteSpace(value) ? fallback : Normalize(value);
    }
}

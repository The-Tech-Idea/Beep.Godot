using System;

namespace Beep.ECS
{
    /// <summary>
    /// Parse an authored button name to an enum value, the one rule the HUD toggle bars
    /// used to carry a byte-for-byte copy of per enum. It tries <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/>
    /// first (trimmed, case-insensitive), then a forgiving fallback that strips spaces,
    /// dashes and underscores and compares against each value's own name - so
    /// <c>"Remove Road"</c>, <c>"remove-road"</c> and <c>"RemoveRoad"</c> all resolve.
    /// </summary>
    public static class GridEnumNames
    {
        /// <summary>Parse <paramref name="value"/> to <typeparamref name="TEnum"/>; false (and default) when nothing matches.</summary>
        public static bool TryParse<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
        {
            if (Enum.TryParse(value?.Trim(), ignoreCase: true, out result))
                return true;

            string normalized = (value ?? "").Trim().Replace(" ", "").Replace("-", "").Replace("_", "");
            foreach (TEnum candidate in Enum.GetValues<TEnum>())
            {
                if (string.Equals(candidate.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    result = candidate;
                    return true;
                }
            }

            result = default;
            return false;
        }
    }
}

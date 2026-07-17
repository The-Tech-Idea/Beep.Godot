using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Builds PropertyHint.Enum hint strings for the four skin-catalog-backed concepts
    /// (genre, theme, palette, geometry). Single source of truth for the inspector
    /// dropdowns on ThemePresetComponent, BeepGenreScene and GameInfo.
    ///
    /// The catalog is JSON loaded at runtime, so the valid values cannot be expressed
    /// as a compile-time [Export(PropertyHint.Enum, "a,b,c")] literal — they are built
    /// here and applied from each type's _ValidateProperty override.
    ///
    /// CRITICAL — each concept stores a different form, matching how it is looked up:
    ///   genre  → id           (SkinCatalog.AllGenres keys)
    ///   theme  → id           (GetTheme does Themes.TryGetValue(id.ToLowerInvariant()))
    ///   palette→ DisplayName  (Palettes is keyed by DisplayName.ToLowerInvariant())
    ///   geometry→ DisplayName (GeometryProfile.ByName matches on DisplayName)
    /// Emitting DisplayNames where an id is expected compiles fine and silently breaks
    /// resolution at runtime.
    ///
    /// For a string property, each hint_string entry is BOTH the label and the stored
    /// value — the "Label:value" form is int-enum only.
    /// </summary>
    public static class SkinPropertyHints
    {
        /// <summary>Palette sentinel meaning "no HSV tint".</summary>
        public const string PaletteDefault = "Default";

        /// <summary>Geometry sentinel meaning "use the theme's own geometry".</summary>
        public const string GeometryDefault = "As-Authored";

        /// <summary>Turn a property into a closed enum dropdown. No-op when the hint is
        /// empty, which leaves the property as a plain text box rather than an unusable
        /// zero-entry dropdown.</summary>
        public static void ApplyEnum(Godot.Collections.Dictionary property, string hint)
        {
            if (string.IsNullOrEmpty(hint)) return;
            property["hint"] = (int)PropertyHint.Enum;
            property["hint_string"] = hint;
        }

        /// <summary>Turn a property into an *editable* dropdown — the listed values plus
        /// free text. Use where a value outside the catalog is meaningful, e.g. the empty
        /// string sentinels on BeepGenreScene ("" = no-op / use the genre default), which
        /// a closed enum could not represent.</summary>
        public static void ApplyEnumSuggestion(Godot.Collections.Dictionary property, string hint)
        {
            if (string.IsNullOrEmpty(hint)) return;
            property["hint"] = (int)PropertyHint.EnumSuggestion;
            property["hint_string"] = hint;
        }

        /// <summary>Genre ids.</summary>
        public static string GenreHint(string current)
            => Join(new List<string>(SkinCatalog.AllGenres.Keys), current);

        /// <summary>Theme ids within a genre.</summary>
        public static string ThemeHint(string genreId, string current)
        {
            var options = new List<string>();
            var genre = SafeGenre(genreId);
            if (genre != null)
                options.AddRange(genre.Themes.Keys);
            return Join(options, current);
        }

        /// <summary>Palette display names within a genre+theme. "Default" is always offered.</summary>
        public static string PaletteHint(string genreId, string themeId, string current)
        {
            var options = new List<string> { PaletteDefault };
            var theme = SafeTheme(genreId, themeId);
            if (theme != null)
                foreach (var palette in theme.Palettes.Values)
                    if (!palette.DisplayName.Equals(PaletteDefault, StringComparison.OrdinalIgnoreCase))
                        options.Add(palette.DisplayName);
            return Join(options, current);
        }

        /// <summary>Geometry profile display names. "As-Authored" is always offered, then
        /// this genre's own profile, then every other genre's — mirroring
        /// GeometryProfile.ByName, which searches across all genres.</summary>
        public static string GeometryHint(string genreId, string current)
        {
            var options = new List<string> { GeometryDefault };

            var own = SafeGenre(genreId)?.Geometry;
            if (own != null && !string.IsNullOrEmpty(own.DisplayName))
                options.Add(own.DisplayName);

            foreach (var genre in SkinCatalog.AllGenres.Values)
            {
                string? name = genre.Geometry?.DisplayName;
                if (!string.IsNullOrEmpty(name) && !options.Contains(name))
                    options.Add(name);
            }
            return Join(options, current);
        }

        private static GenreDef? SafeGenre(string genreId)
            => string.IsNullOrEmpty(genreId) ? null : SkinCatalog.GetGenre(genreId);

        private static ThemeDef? SafeTheme(string genreId, string themeId)
            => string.IsNullOrEmpty(genreId) || string.IsNullOrEmpty(themeId)
                ? null
                : SkinCatalog.GetTheme(genreId, themeId);

        /// <summary>Comma-join the options, keeping the currently stored value selectable
        /// even when the catalog doesn't know it — otherwise opening the inspector could
        /// silently rewrite a value that is valid but not yet loaded.</summary>
        private static string Join(List<string> options, string current)
        {
            // A comma inside an entry would split it into two bogus options.
            options.RemoveAll(o => string.IsNullOrEmpty(o) || o.Contains(','));

            if (!string.IsNullOrEmpty(current)
                && !current.Contains(',')
                && !options.Exists(o => o.Equals(current, StringComparison.OrdinalIgnoreCase)))
                options.Add(current);

            return options.Count == 0 ? "" : string.Join(",", options);
        }
    }
}

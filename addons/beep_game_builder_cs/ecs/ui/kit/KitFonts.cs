using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A type FAMILY, as a role rather than a filename.
    ///
    /// The art pass found **nine** families across the 59 reference images and the kit shipped
    /// **one** — every genre drew in whatever the theme's default font happened to be. rpgui is
    /// serif, racing is thin letter-spaced caps, Township is bold condensed outlined caps, the
    /// pixel games are bitmap: that is not decoration, it is most of what makes two themes of one
    /// genre read differently.
    /// </summary>
    public enum KitFontRole
    {
        /// <summary>The theme's own default. No override.</summary>
        Default,
        /// <summary>Technical sans — the neutral choice.</summary>
        Sans,
        /// <summary>Narrow technical caps: racing, sci-fi, carved-stone labels.</summary>
        Condensed,
        /// <summary>Soft display: the casual and cartoon families.</summary>
        Rounded,
        /// <summary>Heavy display, for banners and result screens.</summary>
        Heavy,
        /// <summary>Bitmap. Non-negotiable for the pixel register — a smooth face inside a
        /// stepped outline is the giveaway.</summary>
        Pixel,
        /// <summary>Fixed-pitch, nearest available to the typewriter/journal look.</summary>
        Mono,
        /// <summary>Old-style serif: rpg and survival storybook. **No CC0 face is shipped for
        /// this** — see <see cref="Resolve"/>.</summary>
        Serif,
        /// <summary>Gothic display: rpg. **No CC0 face is shipped.**</summary>
        Blackletter,
        /// <summary>Marker/handwriting: the diegetic journal. **No CC0 face is shipped.**</summary>
        Handwritten,
    }

    /// <summary>
    /// Resolves a <see cref="KitFontRole"/> to a real font, and says so loudly when it cannot.
    ///
    /// A missing font falls back to the theme default and renders *identically to having no font
    /// system at all* — which is the single most invisible way this feature can fail. Three roles
    /// (Serif, Blackletter, Handwritten) genuinely have no CC0 face in the shipped set; they warn
    /// once and return null so a developer knows to supply their own rather than wondering why
    /// their gothic rpg looks like everything else.
    /// </summary>
    public static class KitFonts
    {
        private const string Dir = "res://addons/beep_game_builder_cs/fonts/";

        /// <summary>Role → shipped file. Absent = no CC0 face available for that role.</summary>
        private static readonly Dictionary<KitFontRole, string> Files = new()
        {
            [KitFontRole.Sans] = "Kenney_Future.ttf",
            [KitFontRole.Condensed] = "Kenney_Future_Narrow.ttf",
            [KitFontRole.Rounded] = "Kenney_Blocks.ttf",
            [KitFontRole.Heavy] = "Kenney_Thick.ttf",
            [KitFontRole.Pixel] = "Kenney_Pixel.ttf",
            [KitFontRole.Mono] = "Kenney_Mini_Square_Mono.ttf",
            // Serif, Blackletter and Handwritten are deliberately absent. See fonts/LICENSE.txt.
        };

        private static readonly Dictionary<KitFontRole, Font?> _cache = new();
        private static readonly HashSet<KitFontRole> _warned = new();

        /// <summary>True when a role has a shipped face. Lets the gate assert coverage without
        /// triggering the warning.</summary>
        public static bool HasFace(KitFontRole role) => Files.ContainsKey(role);

        /// <summary>The file a role maps to, or null. For the gate.</summary>
        public static string? PathFor(KitFontRole role)
            => Files.TryGetValue(role, out string? f) ? Dir + f : null;

        /// <summary>
        /// The font for a role, or null to mean "use the theme default".
        ///
        /// Warns ONCE per role. The warning is the point: without it a theme declaring `Serif`
        /// renders in the default sans and looks exactly like a theme declaring nothing.
        /// </summary>
        public static Font? Resolve(KitFontRole role)
        {
            if (role == KitFontRole.Default) return null;
            if (_cache.TryGetValue(role, out var cached)) return cached;

            Font? font = null;
            if (!Files.TryGetValue(role, out string? file))
            {
                if (_warned.Add(role))
                    GD.PushWarning(
                        $"[KitFonts] role '{role}' has no CC0 face in this addon, so text falls "
                        + "back to the theme's default font and this theme will look like every "
                        + "other one. Serif / Blackletter / Handwritten are a known gap — see "
                        + "addons/beep_game_builder_cs/fonts/LICENSE.txt. Ship your own licensed "
                        + "face and point the theme at it.");
            }
            else
            {
                string path = Dir + file;
                font = ResourceLoader.Exists(path) ? GD.Load<Font>(path) : null;
                if (font == null && _warned.Add(role))
                    GD.PushWarning(
                        $"[KitFonts] role '{role}' maps to {path}, which is missing. Text falls "
                        + "back to the theme default — visually identical to having no font "
                        + "system. Re-copy the fonts folder.");
            }

            _cache[role] = font;
            return font;
        }
    }
}

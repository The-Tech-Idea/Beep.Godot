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
    /// Resolves a <see cref="KitFontRole"/> to a real font, and reports real missing resources.
    ///
    /// A missing font falls back to the theme default and renders *identically to having no font
    /// system at all* — which is the single most invisible way this feature can fail. Three roles
    /// (Serif, Blackletter, Handwritten) genuinely have no CC0 face in the shipped set; they use
    /// a deterministic substitute so runtime startup does not fill the Godot log for an
    /// already-known packaging choice.
    /// </summary>
    public static class KitFonts
    {
        private const string Dir = "res://addons/beep_game_builder_cs/fonts/";
        private const string BundledFallbackFile = "Kenney_Future.ttf";

        /// <summary>Role → shipped file. Absent = no CC0 face available for that role.</summary>
        private static readonly Dictionary<KitFontRole, string> Files = new()
        {
            [KitFontRole.Sans] = "NotoSans-Variable.ttf",
            [KitFontRole.Condensed] = "Audex-Regular.ttf",
            // Kenney_Future, not Kenney_Blocks.
            //
            // Blocks is a solid-square DISPLAY face: its counters -- the holes in B, U, O, A --
            // are filled, which is the whole look. That is fine on a title and unreadable as body
            // copy, and KitGeometry.Font is ONE face used for every string a genre draws, captions
            // included. Rendered at caption size the cardgame board showed "PLAY" as "PLRY" and
            // "BUTTONS" as a row of blocks. Three genres take this role (cardgame, platformer,
            // puzzle), so all three were illegible below title size.
            //
            // Future is the addon's own bundled fallback: a geometric game sans with real
            // counters that still reads as a game face rather than as office type. The soft
            // display look survives where it belongs, in the shape and material axes.
            [KitFontRole.Rounded] = "Kenney_Future.ttf",
            [KitFontRole.Heavy] = "Kenney_Thick.ttf",
            [KitFontRole.Pixel] = "Kenney_Pixel.ttf",
            [KitFontRole.Mono] = "Kenney_Mini_Square_Mono.ttf",
            // The storybook serif rpg and survival ask for, from the project's own font library
            // under the SIL OFL. Before this the role fell to the Heavy substitute, so four themes
            // asked for a serif and got a slab -- the warning said so, and nobody could act on it
            // because no face existed to point at.
            [KitFontRole.Serif] = "EBGaramond-Regular.ttf",
            // Blackletter and Handwritten remain absent. See fonts/LICENSE.txt.
        };

        /// <summary>
        /// Nearest shipped face for a role with no licence-clear font of its own.
        ///
        /// Returning null for these meant falling through to the THEME DEFAULT — i.e. the same
        /// face every other theme uses — so the 4 themes asking for serif/blackletter lost the
        /// font axis entirely and were tellable apart only by shape and material. A substitute in
        /// roughly the right weight keeps the axis alive.
        ///
        /// This is explicitly NOT a claim that Kenney_Thick is a serif. It is not, and the warning
        /// still fires naming the substitution, because a developer shipping a fantasy RPG needs
        /// to know the storybook face they asked for is not what is on screen.
        /// </summary>
        private static readonly Dictionary<KitFontRole, KitFontRole> Substitute = new()
        {
            [KitFontRole.Blackletter] = KitFontRole.Heavy,    // gothic display -> heaviest shipped
            [KitFontRole.Handwritten] = KitFontRole.Rounded,  // soft marker -> soft display
        };

        private static readonly Dictionary<KitFontRole, Font?> _cache = new();
        private static readonly HashSet<KitFontRole> _warned = new();
        private static Font? _bundledFallback;

        /// <summary>True when a role has a shipped face. Lets the gate assert coverage without
        /// triggering the warning.</summary>
        public static bool HasFace(KitFontRole role) => Files.ContainsKey(role);

        /// <summary>The file a role maps to, or null. For the gate.</summary>
        public static string? PathFor(KitFontRole role)
            => Files.TryGetValue(role, out string? f) ? Dir + f : null;

        /// <summary>
        /// The font for a role, or null to mean "use the theme default".
        ///
        /// Missing files warn because they are package defects; known substitute roles are silent
        /// because they are deliberate fallbacks.
        /// </summary>
        public static Font? Resolve(KitFontRole role)
        {
            if (role == KitFontRole.Default) return null;
            if (_cache.TryGetValue(role, out var cached)) return cached;

            Font? font = null;
            if (!Files.TryGetValue(role, out string? file))
            {
                // Substitute rather than fall through to the theme default: the default is what
                // every other theme already uses, so returning null erased the font axis for this
                // theme instead of merely approximating it.
                if (Substitute.TryGetValue(role, out var stand) && Files.TryGetValue(stand, out string? sf))
                {
                    file = sf;
                }
                else if (_warned.Add(role))
                    GD.PushWarning(
                        $"[KitFonts] role '{role}' has no CC0 face in this addon, so text falls "
                        + "back to the theme's default font and this theme will look like every "
                        + "other one. See addons/beep_game_builder_cs/fonts/LICENSE.txt.");
            }

            if (file != null)
            {
                string path = Dir + file;
                font = ResourceLoader.Exists(path) || FileAccess.FileExists(path)
                    ? GD.Load<Font>(path)
                    : null;
                if (font == null && _warned.Add(role))
                    GD.PushWarning(
                        $"[KitFonts] role '{role}' maps to {path}, which is missing. Text falls "
                        + "back to the theme default — visually identical to having no font "
                        + "system. Re-copy the fonts folder.");
            }

            _cache[role] = font;
            return font;
        }

        private static Font? BundledFallback()
        {
            if (_bundledFallback != null) return _bundledFallback;
            string path = Dir + BundledFallbackFile;
            _bundledFallback = ResourceLoader.Exists(path) ? GD.Load<Font>(path) : null;
            return _bundledFallback;
        }

        /// <summary>
        /// Resolve the kit role, then the active Godot theme, then Godot's fallback font.
        /// Text rendering is a hot path and Godot's low-level text API logs loudly when asked to
        /// measure or draw with a null font, so all drawn kit chrome should enter through here.
        /// </summary>
        public static Font? Fallback(Godot.Control? control, KitFontRole role = KitFontRole.Default)
            => Resolve(role)
            ?? BundledFallback()
            ?? control?.GetThemeDefaultFont()
            ?? ThemeDB.FallbackFont
            ?? null;
    }
}

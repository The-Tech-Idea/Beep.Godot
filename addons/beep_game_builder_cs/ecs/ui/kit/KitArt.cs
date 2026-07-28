using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// Resolves a widget's LAYER ART, so a kit widget renders from a 9-patch when one exists and
    /// falls back to its procedural draw when one does not.
    ///
    /// This is PLAN.md phase E's consumption half. The plan's finding is that the two reference
    /// families cannot both be reached the same way: the casual/mobile register is "reproducible
    /// procedurally", while the painted fantasy register "needs 9-patch art sliced from the
    /// sheets; NOT reachable procedurally". A widget therefore has to be able to do both, and
    /// choose per slot rather than per widget.
    ///
    /// WHERE THE ART LIVES, and why the addon ships none of it
    /// ------------------------------------------------------
    /// `Example_Art/` is REFERENCE, not stock: the audit recorded gameui2/3/7 as watermarked
    /// comps (Dreamstime, Game Art Partners, Envato) that are "style reference only — not
    /// shippable art", and the standing rule is "shipped art stays CC0 Kenney or authored".
    /// Slicing those sheets into the addon would ship someone else's pixels.
    ///
    /// So this follows the pattern the HUD textures already settled (docs/HUD_TEXTURE_SYSTEM.md):
    /// the ADDON ships nothing third-party, and a developer points <see cref="Root"/> at their
    /// own sliced art inside their own project. `beep/ui/kit_art_root` in ProjectSettings sets it
    /// once for a game; an empty root means every widget draws procedurally, which is a complete,
    /// working look and not a degraded one.
    ///
    /// Path convention: <c>&lt;root&gt;/&lt;genre&gt;/&lt;widget&gt;_&lt;slot&gt;.png</c>, e.g.
    /// <c>res://ui_art/kit/rpg/button_base.png</c>. A missing genre falls back to a shared
    /// <c>_common</c> folder before giving up, so one authored set can dress every genre.
    /// </summary>
    public static class KitArt
    {
        public const string RootSetting = "beep/ui/kit_art_root";

        /// <summary>Where sliced kit art lives. Empty = procedural everywhere.</summary>
        public static string Root
        {
            get
            {
                if (_root != null) return _root;
                _root = ProjectSettings.HasSetting(RootSetting)
                    ? ProjectSettings.GetSetting(RootSetting).AsString() ?? ""
                    : "";
                return _root;
            }
            set { _root = value ?? ""; _cache.Clear(); _warned.Clear(); }
        }
        private static string? _root;

        private static readonly Dictionary<string, Texture2D?> _cache = new();
        private static readonly HashSet<string> _warned = new();

        /// <summary>Margins for a slot, as left/top/right/bottom. Read from a sibling
        /// <c>.margins</c> file when present ("12 12 12 12"), else a proportional default —
        /// because a 9-patch whose margins are wrong slices the corner artwork, which is the
        /// single most visible way textured chrome goes wrong.</summary>
        public static Vector4 Margins(Texture2D tex, string key)
        {
            if (_margins.TryGetValue(key, out var m)) return m;
            var path = _cachePath.TryGetValue(key, out var p) ? p : null;
            Vector4 v;
            if (path != null && FileAccess.FileExists(path + ".margins"))
            {
                using var f = FileAccess.Open(path + ".margins", FileAccess.ModeFlags.Read);
                string[] parts = (f?.GetAsText() ?? "").Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
                v = parts.Length >= 4
                    ? new Vector4(parts[0].ToFloat(), parts[1].ToFloat(), parts[2].ToFloat(), parts[3].ToFloat())
                    : Default(tex);
            }
            else v = Default(tex);
            _margins[key] = v;
            return v;

            // A quarter of the shorter side clears any plausible corner radius without eating
            // the centre, which is what a stretched 9-patch needs.
            static Vector4 Default(Texture2D t)
            {
                float m = Mathf.Max(2f, Mathf.Min(t.GetWidth(), t.GetHeight()) * 0.25f);
                return new Vector4(m, m, m, m);
            }
        }
        private static readonly Dictionary<string, Vector4> _margins = new();
        private static readonly Dictionary<string, string> _cachePath = new();

        /// <summary>The texture for a widget's layer, or null to draw procedurally.</summary>
        public static Texture2D? Resolve(string? genre, string widget, string slot)
        {
            if (string.IsNullOrEmpty(Root)) return null;
            string key = $"{genre}/{widget}_{slot}";
            if (_cache.TryGetValue(key, out var hit)) return hit;

            Texture2D? tex = null;
            foreach (string g in new[] { genre ?? "", "_common" })
            {
                if (string.IsNullOrEmpty(g)) continue;
                string path = $"{Root.TrimEnd('/')}/{g}/{widget}_{slot}.png";
                if (!ResourceLoader.Exists(path)) continue;
                tex = GD.Load<Texture2D>(path);
                if (tex != null) { _cachePath[key] = path; break; }
            }

            // Say so ONCE per slot when a root is configured but a slot is missing: a widget
            // silently falling back looks identical to a widget whose art failed to import.
            if (tex == null && _warned.Add(key))
                GD.Print($"[KitArt] no art for '{key}' under '{Root}' — drawing it procedurally.");

            _cache[key] = tex;
            return tex;
        }

        /// <summary>Drop caches after art is added or the root changes.</summary>
        public static void Reload()
        {
            _cache.Clear(); _margins.Clear(); _cachePath.Clear(); _warned.Clear(); _root = null;
        }
    }
}

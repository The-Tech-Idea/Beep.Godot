using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// Lets a `theme.json` declare the kit's style axes, so a new look costs **no C#**.
    ///
    /// Every axis built in Phases A–F — shadow, outline polarity, corner per widget class, shear,
    /// wobble, font, tracking, selection cues, edge run — lives in `KitGeometry`, a **static C#
    /// table keyed by genre**. That table is why the kit could only ever have one look per genre,
    /// and shipping "two or three themes per genre" from it would mean editing C# for every one:
    /// exactly what `docs/SKIN_SYSTEM.md` promises the skin system never requires
    /// ("Zero C# changes needed to add content").
    ///
    /// So a theme may carry a `kit` block. Keys are snake_case like the rest of the schema, every
    /// key is optional, and anything absent keeps the genre's built-in value — a theme overrides
    /// what it cares about and inherits the rest.
    ///
    /// <code>
    /// "kit": {
    ///   "outline_shade": 1.85,
    ///   "shadow": "soft",
    ///   "corner_panel": 0.28,
    ///   "corner_bar":   0.50,
    ///   "shear": 0.16,
    ///   "font": "pixel",
    ///   "upper_case": true,
    ///   "tracking": 0.10,
    ///   "select_slot": "border|glow"
    /// }
    /// </code>
    /// </summary>
    public static class KitStyleJson
    {
        /// <summary>Overrides in force, keyed by genre id. Set when a skin is applied.</summary>
        private static readonly Dictionary<string, Godot.Collections.Dictionary> _byGenre = new();

        private static readonly HashSet<string> _warned = new();

        /// <summary>Register (or clear) a genre's `kit` block. Called by the skin catalog when a
        /// theme loads; clearing is what makes switching themes actually switch styles.</summary>
        public static void Set(string genre, Godot.Collections.Dictionary? kit)
        {
            if (string.IsNullOrEmpty(genre)) return;
            if (kit == null) _byGenre.Remove(genre);
            else _byGenre[genre] = kit;
            // The merged geometry is cached; without this a theme switch would keep drawing the
            // previous theme's style, which is the kind of bug that looks like "the theme didn't
            // apply" and sends you hunting in the wrong file.
            KitGeometry.InvalidateMerged(genre);
        }

        public static void Clear()
        {
            _byGenre.Clear();
            KitGeometry.InvalidateAllMerged();
        }

        public static bool Has(string genre) => _byGenre.ContainsKey(genre);

        /// <summary>Apply a genre's declared overrides onto a geometry, in place.</summary>
        public static void Apply(string genre, KitGeometry g)
        {
            if (!_byGenre.TryGetValue(genre, out var k) || k == null) return;

            g.OutlineShade = F(k, "outline_shade", g.OutlineShade);
            g.Corner = F(k, "corner", g.Corner);
            g.CornerPanel = F(k, "corner_panel", g.CornerPanel);
            g.CornerSlot = F(k, "corner_slot", g.CornerSlot);
            g.CornerBar = F(k, "corner_bar", g.CornerBar);
            g.CornerChip = F(k, "corner_chip", g.CornerChip);
            g.Shear = F(k, "shear", g.Shear);
            g.Wobble = F(k, "wobble", g.Wobble);
            g.Tracking = F(k, "tracking", g.Tracking);
            g.UpperCase = B(k, "upper_case", g.UpperCase);
            g.GrainPattern = k.ContainsKey("grain") ? k["grain"].AsString() : g.GrainPattern;
            g.GrainAmount = F(k, "grain_amount", g.GrainAmount);
            g.GrainTiles = k.ContainsKey("grain_tiles") ? (int)k["grain_tiles"].AsDouble() : g.GrainTiles;

            g.PixelSize = F(k, "pixel_size", g.PixelSize);
            if (k.ContainsKey("register"))
                g.Register = Enum<KitRegister>(k["register"].AsString(), genre, "register") ?? g.Register;
            if (k.ContainsKey("edge_run")) g.EdgeRun = EdgeRunFrom(k["edge_run"], genre);
            if (k.ContainsKey("gloss_style"))
                g.GlossStyle = Enum<KitGloss>(k["gloss_style"].AsString(), genre, "gloss_style") ?? g.GlossStyle;
            if (k.ContainsKey("shadow"))
                g.Shadow = ShadowFrom(k["shadow"].AsString(), genre) ?? g.Shadow;
            if (k.ContainsKey("font"))
                g.Font = Enum<KitFontRole>(k["font"].AsString(), genre, "font") ?? g.Font;

            g.SelectButton = Cues(k, "select_button", genre, g.SelectButton);
            g.SelectPanel = Cues(k, "select_panel", genre, g.SelectPanel);
            g.SelectSlot = Cues(k, "select_slot", genre, g.SelectSlot);
            g.SelectBar = Cues(k, "select_bar", genre, g.SelectBar);
            g.SelectChip = Cues(k, "select_chip", genre, g.SelectChip);

            WarnUnknownKeys(k, genre);
        }

        /// <summary>Every key the `kit` block understands.</summary>
        private static readonly HashSet<string> Known = new()
        {
            "outline_shade", "corner", "corner_panel", "corner_slot", "corner_bar", "corner_chip",
            "shear", "wobble", "tracking", "upper_case", "shadow", "font",
            "select_button", "select_panel", "select_slot", "select_bar", "select_chip",
            "grain", "grain_amount", "grain_tiles", "register", "pixel_size", "gloss_style", "edge_run",
        };

        /// <summary>
        /// Warn about keys the block does not understand.
        ///
        /// An unknown VALUE was already reported; an unknown KEY was not, and that is the more
        /// likely mistake: `corner_pannel` parses as valid JSON, sets nothing, and leaves the
        /// theme author looking at a corner that will not change. Silence there is
        /// indistinguishable from the feature being broken.
        /// </summary>
        private static void WarnUnknownKeys(Godot.Collections.Dictionary k, string genre)
        {
            foreach (var key in k.Keys)
            {
                string name = key.AsString();
                if (Known.Contains(name)) continue;
                if (_warned.Add($"{genre}/key/{name}"))
                    GD.PushWarning($"[KitStyleJson] genre '{genre}' theme declares kit.{name}, "
                                 + "which is not a key this block understands, so it is doing "
                                 + "nothing. Known keys: " + string.Join(", ", Known));
            }
        }

        private static float F(Godot.Collections.Dictionary k, string key, float fallback)
            => k.ContainsKey(key) ? (float)k[key].AsDouble() : fallback;

        private static bool B(Godot.Collections.Dictionary k, string key, bool fallback)
            => k.ContainsKey(key) ? k[key].AsBool() : fallback;

        /// <summary>
        /// `edge_run` — the last axis that was C#-only.
        ///
        /// Two forms, because they answer different questions:
        ///   "edge_run": "scifi"   the built-in run read off art-pass files 14 and 43
        ///   "edge_run": "none"    explicitly no run, so a theme can REMOVE the genre's
        ///   "edge_run": { "top": [ { "start": 0, "length": 0.34, "weight": 2.6,
        ///                            "fill": "block" }, ... ], "right": [...] }
        ///
        /// An omitted edge inherits nothing and simply has no run, which is the degenerate case
        /// the renderer already treats as a plain rectangle. Positions are FRACTIONS of the edge's
        /// length, so a run is independent of widget size — the same reason everything else moved
        /// onto the unit.
        /// </summary>
        private static KitEdgeRun? EdgeRunFrom(Variant v, string genre)
        {
            if (v.VariantType == Variant.Type.String)
            {
                string name = v.AsString().ToLowerInvariant();
                if (name is "none" or "") return null;
                if (name is "scifi" or "sci_fi" or "sci-fi") return KitEdgeRun.SciFi();
                return Unknown<KitEdgeRun>(v.AsString(), genre, "edge_run");
            }
            if (v.VariantType != Variant.Type.Dictionary)
                return Unknown<KitEdgeRun>(v.ToString(), genre, "edge_run");

            var d = v.AsGodotDictionary();
            var run = new KitEdgeRun
            {
                Top = Segs(d, "top", genre),
                Right = Segs(d, "right", genre),
                Bottom = Segs(d, "bottom", genre),
                Left = Segs(d, "left", genre),
            };
            foreach (var key in d.Keys)
            {
                string name = key.AsString();
                if (name is "top" or "right" or "bottom" or "left") continue;
                if (_warned.Add($"{genre}/edge_run/{name}"))
                    GD.PushWarning($"[KitStyleJson] genre '{genre}' declares edge_run.{name}, "
                                 + "which is not an edge. Known: top, right, bottom, left.");
            }
            // A run with nothing drawn on any edge is almost certainly a mistake -- and it renders
            // identically to declaring no run at all, which is exactly the silence this repo keeps
            // paying for.
            if (run.SegmentCount == 0 && _warned.Add($"{genre}/edge_run/empty"))
                GD.PushWarning($"[KitStyleJson] genre '{genre}' declares an edge_run with no "
                             + "segments on any edge, so it draws nothing -- identical to having "
                             + "declared none. Did the edge keys get misspelled?");
            return run;
        }

        private static KitEdgeSeg[] Segs(Godot.Collections.Dictionary d, string edge, string genre)
        {
            if (!d.ContainsKey(edge)) return System.Array.Empty<KitEdgeSeg>();
            var arr = d[edge].AsGodotArray();
            var list = new List<KitEdgeSeg>();
            foreach (var item in arr)
            {
                if (item.VariantType != Variant.Type.Dictionary)
                {
                    Unknown<object>(item.ToString(), genre, $"edge_run.{edge}");
                    continue;
                }
                var seg = item.AsGodotDictionary();
                var fill = KitSegFill.Solid;
                if (seg.ContainsKey("fill"))
                    fill = Enum<KitSegFill>(seg["fill"].AsString(), genre, $"edge_run.{edge}.fill")
                           ?? KitSegFill.Solid;
                list.Add(new KitEdgeSeg(
                    F(seg, "start", 0f), F(seg, "length", 1f), F(seg, "weight", 1f), fill));
            }
            return list.ToArray();
        }

        private static KitShadowDef? ShadowFrom(string s, string genre) => s.ToLowerInvariant() switch
        {
            "none" => KitShadowDef.None,
            "hard" => KitShadowDef.Hard(),
            "soft" => KitShadowDef.Soft(),
            "glow" => KitShadowDef.Glow(),
            "extrude" => KitShadowDef.Extrude(),
            _ => Unknown<KitShadowDef>(s, genre, "shadow"),
        };

        private static T? Enum<T>(string s, string genre, string key) where T : struct
            => System.Enum.TryParse<T>(s, true, out var v) ? v : Unknown<T?>(s, genre, key);

        /// <summary>`"border|glow"` → a flags set. Unknown names WARN rather than being skipped:
        /// a typo that silently yields `None` renders as "selection does nothing", which looks
        /// like a bug in the kit rather than a bug in the theme.</summary>
        private static KitSelectCue Cues(Godot.Collections.Dictionary k, string key, string genre,
                                         KitSelectCue fallback)
        {
            if (!k.ContainsKey(key)) return fallback;
            var cue = KitSelectCue.None;
            foreach (string part in k[key].AsString().Split('|'))
            {
                string t = part.Trim();
                if (t.Length == 0) continue;
                if (System.Enum.TryParse<KitSelectCue>(t, true, out var one)) cue |= one;
                else Unknown<object>(t, genre, key);
            }
            return cue;
        }

        /// <summary>A bad value must SAY so. Silently keeping the built-in default would make a
        /// misspelled theme key indistinguishable from a key that is working.</summary>
        private static T? Unknown<T>(string value, string genre, string key)
        {
            if (_warned.Add($"{genre}/{key}/{value}"))
                GD.PushWarning($"[KitStyleJson] genre '{genre}' theme declares kit.{key} = "
                             + $"'{value}', which is not a recognised value. The genre's built-in "
                             + "setting is kept, so this key is doing nothing.");
            return default;
        }
    }
}

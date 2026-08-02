using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// The single answer to "what colour is the surface I am drawing on, and what ink outlines
    /// it" — for the components that DRAW themselves (PanelFrameComponent, badges, meters)
    /// rather than hand a StyleBox to Godot.
    ///
    /// It exists because every such component was resolving that itself, and each one got it
    /// slightly differently. PanelFrameComponent did:
    ///
    ///     GetThemeStylebox("panel", "PanelContainer") as StyleBoxFlat
    ///
    /// which is null whenever the panel resolves to a TEXTURE or to StyleBoxEmpty — so it fell
    /// through to a hardcoded brown, and every framed screen drew a wood frame around pale
    /// blue-grey contents no matter which of the 50 skins was active. The frame was not a
    /// design choice, it was a fallback firing on every screen.
    ///
    /// Anything that needs a palette colour outside the generated Theme goes through here, so a
    /// drawn frame and a themed Button beside it are the same colour from the same source.
    /// </summary>
    public static class UiSurface
    {
        /// <summary>Theme type the palette's meaning colours are published under. Not a real
        /// Godot control type — a namespace for colours every component can query by role.</summary>
        public const string SemanticType = "BeepSemantic";

        /// <summary>What a colour MEANS, so a scene declares intent and the palette decides the
        /// value. A scene that stores Color(0.30, 0.66, 0.90) has pinned a palette into a file
        /// no skin can reach; a scene that stores Role.Info has not.</summary>
        public enum Role { Neutral, Accent, Accent2, Success, Warning, Danger, Info }

        /// <summary>The active palette's colour for a role. Falls back to the accent rather than
        /// to a literal, so an incomplete theme still yields a palette colour.</summary>
        public static Color Semantic(Godot.Control ctl, Role role)
        {
            string key = role switch
            {
                Role.Success => "success",
                Role.Warning => "warning",
                Role.Danger => "danger",
                Role.Info => "info",
                Role.Accent2 => "accent2",
                Role.Neutral => "neutral",
                _ => "accent",
            };
            if (ctl.HasThemeColor(key, SemanticType)) return ctl.GetThemeColor(key, SemanticType);
            if (ctl.HasThemeColor("accent", SemanticType)) return ctl.GetThemeColor("accent", SemanticType);

            // Neither the role NOR the accent is registered. GetThemeColor would hand back
            // BLACK here, silently — a meter drew as a solid black bar with a black track and no
            // hint as to why. Derive something visible from the surface instead, and say so
            // once: a component that cannot resolve its palette is our bug, not the caller's.
            if (!_warnedNoSemantic)
            {
                _warnedNoSemantic = true;
                GD.PushWarning(
                    $"UiSurface.Semantic: no '{key}' or 'accent' colour registered under theme type "
                    + $"'{SemanticType}' for {ctl.GetPath()}. Returning a surface-derived colour. "
                    + "A skinned scene gets these from ThemePresetComponent — if this fires at "
                    + "runtime, that component is missing or has not applied yet.");
            }
            Color surface = Of(ctl);
            return Luminance(surface) > 0.5f
                ? new Color(surface.R * 0.45f, surface.G * 0.45f, surface.B * 0.50f, 1f)
                : new Color(Mathf.Min(1f, surface.R * 2.2f + 0.18f),
                            Mathf.Min(1f, surface.G * 2.2f + 0.18f),
                            Mathf.Min(1f, surface.B * 2.2f + 0.22f), 1f);
        }

        private static bool _warnedNoSemantic;

        /// <summary>The Control a Node should read its theme from.
        ///
        /// Most UI components here are <c>UIComponent : EntityComponent</c> — Nodes, not
        /// Controls — so they have no GetThemeColor of their own. They are always either
        /// parented under a Control or building Control children, so the theme is one hop away;
        /// this finds it. Ancestors first (that is the Control whose surface the component is
        /// actually drawn on), then owned descendants.</summary>
        public static Godot.Control? NearestControl(Node? n)
        {
            for (Node? p = n; p != null; p = p.GetParent())
                if (p is Godot.Control c) return c;
            return n == null ? null : FirstControlChild(n);
        }

        private static Godot.Control? FirstControlChild(Node n)
        {
            foreach (var child in n.GetChildren())
            {
                if (child is Godot.Control c) return c;
                if (FirstControlChild(child) is { } deep) return deep;
            }
            return null;
        }

        /// <summary>Nodes already warned about, so a per-frame draw cannot spam the log.</summary>
        private static readonly System.Collections.Generic.HashSet<string> _warned = new();

        /// <summary>Policy when a component has no Control anywhere: WARN and return
        /// transparent — never a literal.
        ///
        /// A literal fallback is exactly the defect this class exists to remove: it would look
        /// plausible and silently pin a colour outside the palette. A component with no Control
        /// in its ancestry or children is not in a UI tree and is drawing nothing, so a
        /// transparent result is inert; the warning names the node so the real problem (a
        /// misplaced component) is fixable rather than hidden behind a colour that looks fine.</summary>
        private static Godot.Control? Host(Node n)
        {
            if (NearestControl(n) is { } c) return c;
            string key = n.GetPath();
            if (_warned.Add(key))
                GD.PushWarning($"[UiSurface] '{key}' ({n.GetType().Name}) has no Control in its "
                             + "ancestry or children, so it has no theme to read. Its colours "
                             + "will be transparent until it is placed under a Control.");
            return null;
        }

        /// <summary>Role colour for a Node-based component, via its nearest Control.</summary>
        public static Color Semantic(Node n, Role role)
            => Host(n) is { } c ? Semantic(c, role) : default;

        /// <summary>Surface colour for a Node-based component, via its nearest Control.</summary>
        public static Color Of(Node n)
            => Host(n) is { } c ? Of(c) : default;

        /// <summary>The theme's body text colour, for knobs and marks that must read against
        /// whatever surface they sit on.</summary>
        public static Color Text(Node n)
            => Host(n) is { } c ? c.GetThemeColor("font_color", "Label") : default;

        /// <summary>The theme's body font size, optionally scaled for a role.
        ///
        /// Components were hardcoding this — 11, 12, 13, 17, 18, 36 — while the themes declare
        /// anything from 14 to 24 (puzzle runs at 24, platformer at 20). A badge sized for 17pt
        /// text renders 24pt text straight out of its own plate, which is why several components
        /// look far too small for what is written in them.
        ///
        /// Anything that draws text, or sizes a box AROUND text, asks here.</summary>
        public static int FontSize(Node n, float scale = 1f, int min = 8)
        {
            var c = NearestControl(n);
            int b = c?.GetThemeFontSize("font_size", "Label") ?? 14;
            if (b <= 0) b = 14;
            return Mathf.Max(min, Mathf.RoundToInt(b * scale));
        }

        /// <summary>
        /// Named steps on the type scale, for the widgets that DRAW their own text.
        ///
        /// A Label in a container gets its hierarchy from a `theme_type_variation`
        /// (`BeepTitle`/`BeepSubtitle`/`BeepValue`/`BeepCaption`, registered by
        /// ThemePresetComponent). A drawn widget cannot use those — it has no Label — so it had
        /// nothing but a bare `FontSize(this)` and every string in the kit came out one size.
        ///
        /// These multipliers are deliberately the SAME numbers the Label variations use, so a
        /// drawn card title and a `BeepTitle` Label beside it agree, and changing the scale in
        /// one place changes both.
        /// </summary>
        public enum TextRole
        {
            /// <summary>Screen and card titles. 1.6x -- matches `BeepTitle`.</summary>
            Title,
            /// <summary>Section headings, banner text. 1.22x -- matches `BeepSubtitle`.</summary>
            Subtitle,
            /// <summary>A number that carries the meaning. 1.18x -- matches `BeepValue`.</summary>
            Value,
            /// <summary>Default running text. 1.0x.</summary>
            Body,
            /// <summary>Stat labels, hints, footers. 0.9x -- matches `BeepCaption`.</summary>
            Caption,
            /// <summary>Count badges and corner overlays, where the box is genuinely tiny.
            /// 0.76x -- no Label variation equivalent; drawn widgets need a step below Caption.</summary>
            Small,
        }

        public static float Multiplier(TextRole role) => role switch
        {
            TextRole.Title => 1.60f,
            TextRole.Subtitle => 1.22f,
            TextRole.Value => 1.18f,
            TextRole.Caption => 0.90f,
            TextRole.Small => 0.76f,
            _ => 1.00f,
        };

        /// <summary>The theme's size for a named role, ignoring any box. Use when the widget
        /// grows to fit its text rather than the other way round.</summary>
        public static int FontSize(Node n, TextRole role, int min = 7)
            => FontSize(n, Multiplier(role), min);

        /// <summary>
        /// A named role, SHRUNK to fit the box it is drawn in.
        ///
        /// This is the one most kit widgets want: the role sets the intent, the box sets the
        /// ceiling. A Title in a big card renders large; the same Title in a small chip shrinks
        /// rather than overflowing, and never below <paramref name="min"/>.
        /// </summary>
        public static int FitRole(Node n, TextRole role, Vector2 box, string? text = null,
                                  Font? font = null, int min = 7)
        {
            int want = FontSize(n, role, min);
            // The box's height still bounds it: a Title cannot be 1.9x the theme if the box it
            // is drawn in is only 14px tall.
            int fs = Mathf.Clamp(Mathf.FloorToInt(box.Y * 0.72f), min, want);
            if (font == null || string.IsNullOrEmpty(text) || box.X <= 1f) return fs;
            while (fs > min &&
                   font.GetStringSize(text, HorizontalAlignment.Left, -1, fs).X > box.X)
                fs--;
            return fs;
        }

        /// <summary>
        /// The largest font size at which <paramref name="text"/> FITS inside
        /// <paramref name="box"/> — bounded by the box's height and, if the text is measured,
        /// by its width too.
        ///
        /// WHY THIS EXISTS
        /// ---------------
        /// <see cref="FontSize"/> returns the theme's body size and knows nothing about the
        /// widget drawing with it. That is right for a Label inside a container, which grows to
        /// fit its text — and wrong for every box a widget draws for ITSELF. 79 of the kit's 86
        /// font-size call sites were a bare <c>FontSize(this)</c>, so a 24px count badge and a
        /// 200px card title rendered at exactly the same size: banners read as tiny captions on
        /// a large panel, and slot badges ("12", "Lv 12") were barely legible at any theme size.
        ///
        /// A drawn box should scale its type with itself. `heightRatio` is the share of the box
        /// height one line of text may occupy — 0.5 is a comfortable default for a single line
        /// in a tight box; a banner wants less, a big numeral more.
        ///
        /// Passing <paramref name="text"/> and <paramref name="font"/> also shrinks to fit the
        /// WIDTH, which is what stops a long title overflowing a narrow card. Without them only
        /// the height bound applies.
        /// </summary>
        public static int FitText(Node n, Vector2 box, float heightRatio = 0.5f,
                                  string? text = null, Font? font = null,
                                  int min = 7, float themeMax = 2.2f)
        {
            int theme = FontSize(n);
            // Never larger than a sane multiple of the theme's own size: a huge box should not
            // produce absurd type just because it has room, and the theme still sets the tone.
            int cap = Mathf.Max(min, Mathf.RoundToInt(theme * themeMax));
            int fs = Mathf.Clamp(Mathf.FloorToInt(box.Y * heightRatio), min, cap);

            if (font == null || string.IsNullOrEmpty(text) || box.X <= 1f) return fs;

            // Shrink until it fits the width. Linear from the height-derived size rather than a
            // binary search: the range is small and this always lands on the largest fitting
            // size rather than near it.
            while (fs > min &&
                   font.GetStringSize(text, HorizontalAlignment.Left, -1, fs).X > box.X)
                fs--;
            return fs;
        }

        /// <summary>Nominal mid-tone of the shipped 9-patch art, measured across the set:
        /// button_normal averages (204,210,214) = 0.82, panel (190,200,205) = 0.78. A textured
        /// box carries the palette in its modulate PRE-multiplied by this, so dividing it back
        /// out recovers the colour the control actually renders as.</summary>
        public const float ArtNominalLuminance = 0.80f;

        /// <summary>The colour a box renders as, whatever kind of box it is. False when the
        /// box carries no colour of its own (StyleBoxEmpty, StyleBoxLine, null).</summary>
        public static bool TryColorOf(StyleBox? sb, out Color color)
        {
            switch (sb)
            {
                case StyleBoxFlat flat:
                    color = flat.BgColor;
                    return color.A > 0.02f;
                case StyleBoxTexture tex:
                    // modulate = surface / ArtNominalLuminance, so undo that to get the surface.
                    var m = tex.ModulateColor;
                    color = new Color(m.R * ArtNominalLuminance,
                                      m.G * ArtNominalLuminance,
                                      m.B * ArtNominalLuminance, m.A);
                    return color.A > 0.02f;
                default:
                    color = default;
                    return false;
            }
        }

        /// <summary>The surface colour in effect for a control, tried in the order a control
        /// actually inherits from. Falls back to the theme's Label colour inverted, which is
        /// always defined, rather than to a literal — a literal is what produced the brown.</summary>
        public static Color Of(Godot.Control ctl)
        {
            // The palette's OWN surface, published by ThemePresetComponent.ThemeSemantics as the
            // "neutral" role, is asked FIRST — it is the authoritative value and, unlike a
            // StyleBox, nothing can blank it.
            //
            // This matters because the kit's drop-ins deliberately override their panel/normal
            // StyleBox with an empty one to suppress the stock chrome. That destroyed the very
            // source the lookups below read, so every swept widget fell through to the last-resort
            // branch and rendered the same brown: topdown's cream #F5E6C7 surface drew as #413627,
            // one shade off rpg's #3F3629, and platformer's orange #FFB800 drew as grey #3C3D3D.
            // Ten genres collapsed into one palette on screen while their theme.json files were
            // entirely correct.
            if (ctl.HasThemeColor("neutral", SemanticType))
            {
                Color n = ctl.GetThemeColor("neutral", SemanticType);
                if (n.A > 0.02f) return n;
            }

            if (TryColorOf(ctl.GetThemeStylebox("panel", "PanelContainer"), out var c)) return c;
            if (TryColorOf(ctl.GetThemeStylebox("panel", "Panel"), out c)) return c;
            if (TryColorOf(ctl.GetThemeStylebox("normal", "Button"), out c)) return c;

            // Last resort derived from the palette rather than invented: a theme always defines
            // a Label colour, and the surface it is meant to be read against is its opposite.
            Color text = ctl.GetThemeColor("font_color", "Label");
            return Luminance(text) > 0.5f
                ? new Color(text.R * 0.22f, text.G * 0.24f, text.B * 0.28f, 1f)
                : new Color(1f - text.R * 0.35f, 1f - text.G * 0.32f, 1f - text.B * 0.30f, 1f);
        }

        /// <summary>The outline colour for a surface. Same formula the generated theme stamps
        /// onto every StyleBoxFlat, so a drawn outline and a themed control's border match.</summary>
        public static Color Ink(Color surface) =>
            new(surface.R * 0.22f, surface.G * 0.24f, surface.B * 0.28f, 1f);

        public static float Luminance(Color c) => 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B;
    }
}

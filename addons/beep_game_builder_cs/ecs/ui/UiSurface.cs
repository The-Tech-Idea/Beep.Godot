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

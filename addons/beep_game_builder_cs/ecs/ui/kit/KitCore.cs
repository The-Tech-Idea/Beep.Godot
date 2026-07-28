using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// The silhouette a widget is cut to. GENRE owns this — see plans/game-ui-kit/PLAN.md §2.
    ///
    /// The first ten are the registers already generated and verified by
    /// tools/genre_shapes/; the last six come from the golden-kit reference, which uses them
    /// for navigation, status chips and domed headers.
    /// </summary>
    public enum KitShape
    {
        Rect, Round, Chamfer, Clip, Notch, Speed, Ribbon, Shield, Octagon, Ellipse,
        Arch, Pill, Arrow, Chevron, Parallelogram, Pentagon,
    }

    /// <summary>Where a widget sits in the visual hierarchy. Drives which palette role its
    /// base layer takes, so a raised control and a recessed well are not the same flat plate
    /// in two colours.</summary>
    public enum KitElevation { Recessed, Flush, Raised }

    /// <summary>Interaction state. Each is a distinct SCULPT, not an alpha change — a pressed
    /// plate is redrawn sunken and a disabled one redrawn de-sculpted. Fading a control is the
    /// single clearest tell that a UI is a themed form rather than a game.</summary>
    public enum KitState { Normal, Hover, Pressed, Disabled, Focus, Locked, Selected }

    /// <summary>
    /// The named layer stack a GENRE defines once and every widget inherits.
    ///
    /// This is a primitive rather than a per-widget list because the golden-kit reference makes
    /// the rule obvious: one gold material carries a dozen different silhouettes unchanged. A
    /// per-widget stack would let each widget invent its own bevel and drift apart, which is the
    /// same failure mode that put 51 colour literals across 26 components.
    /// </summary>
    public sealed class KitMaterial
    {
        /// <summary>Fill. Takes the surface for the widget's elevation.</summary>
        public bool Base = true;
        /// <summary>Inner bevel: light along the top-left, dark along the bottom-right —
        /// inverted when the widget is recessed or pressed.</summary>
        public float Bevel = 1f;
        /// <summary>Diagonal sheen across the upper face. 0 disables (matte genres).</summary>
        public float Gloss = 0.5f;
        /// <summary>Outer rim line, in palette-derived ink.</summary>
        public float Rim = 1f;
        /// <summary>Corner sparkle accents. 0 for anything not gem/metal.</summary>
        public float Sparkle;

        /// <summary>Per-genre materials. Registered here rather than in each widget so a genre
        /// reads as one family; a genre with no entry gets the neutral default.</summary>
        private static readonly Dictionary<string, KitMaterial> _byGenre = new()
        {
            ["rpg"] = new() { Bevel = 1.2f, Gloss = 0.55f, Rim = 1.3f, Sparkle = 0.35f },
            ["cardgame"] = new() { Bevel = 0.9f, Gloss = 0.7f, Rim = 1.0f, Sparkle = 0.5f },
            ["survival"] = new() { Bevel = 1.1f, Gloss = 0.2f, Rim = 1.3f },
            ["strategy"] = new() { Bevel = 0.8f, Gloss = 0.25f, Rim = 1.2f },
            ["shooter"] = new() { Bevel = 0.6f, Gloss = 0.35f, Rim = 1.1f },
            ["racing"] = new() { Bevel = 0.7f, Gloss = 0.85f, Rim = 1.0f, Sparkle = 0.25f },
            ["citybuilder"] = new() { Bevel = 0.7f, Gloss = 0.3f, Rim = 1.0f },
            ["platformer"] = new() { Bevel = 1.3f, Gloss = 0.8f, Rim = 1.2f },
            ["puzzle"] = new() { Bevel = 1.2f, Gloss = 0.9f, Rim = 1.0f, Sparkle = 0.4f },
            ["topdown"] = new() { Bevel = 1.0f, Gloss = 0.4f, Rim = 1.1f },
        };

        private static readonly KitMaterial _default = new();

        public static KitMaterial ForGenre(string? genre)
            => genre != null && _byGenre.TryGetValue(genre.ToLowerInvariant(), out var m) ? m : _default;

        /// <summary>Silhouette per genre. Mirrors tools/genre_shapes/gen_all_genres.py so the
        /// drawn widgets and the generated 9-patch art cut to the same outline.</summary>
        public static KitShape ShapeForGenre(string? genre) => genre?.ToLowerInvariant() switch
        {
            "rpg" => KitShape.Chamfer,
            "survival" => KitShape.Notch,
            "shooter" => KitShape.Clip,
            "strategy" or "citybuilder" => KitShape.Rect,
            "racing" => KitShape.Speed,
            "platformer" => KitShape.Pill,
            "cardgame" or "puzzle" or "topdown" => KitShape.Round,
            _ => KitShape.Round,
        };
    }

    /// <summary>
    /// How a genre builds its frame. INDEX.md: "The frame formula does not generalise. Two
    /// regimes: structural (3.5px + 0.07 x height, carved/wood families) and hairline (constant
    /// 1-3px regardless of size). Needs a mode flag, not tuned constants."
    /// </summary>
    public enum KitFrameMode
    {
        /// <summary>A bare plate with no separate frame.</summary>
        None,
        /// <summary>A constant thin keyline that does NOT scale with the widget — measured on
        /// rpgui1, racing4 and rpgui2, where a 30px chip and a 300px panel carry the same 1-3px
        /// line. Technical/flat registers.</summary>
        Hairline,
        /// <summary>Carved frame that grows with the widget: 3.5px floor + 0.07 x height,
        /// linear-fit on citybuilder5 (35px capsule -> 6px, 107px tile -> 11px).</summary>
        Structural,
    }

    /// <summary>
    /// Which reference family a genre is drawn from. PLAN.md 34: Example_Art/ holds "TWO style
    /// families that must not be averaged", and averaging them is the documented root error of
    /// the earlier phase-A attempts.
    ///
    /// This drives the MATERIAL, not just the frame: the two families differ in how depth is
    /// expressed, and using one family's depth cue on the other is what made every casual genre
    /// measure as painted.
    /// </summary>
    public enum KitRegister
    {
        /// <summary>Carved/painted: frame around a separate plate, bevel raked across the face,
        /// bright rim. rpgui, Upgrades, citybuilder5.</summary>
        Carved,
        /// <summary>Casual/mobile: ONE flat saturated plate, a discrete top band, a thick dark
        /// outline, large radius. ui1/ui2/skilltree1/store. Depth comes from the outline and the
        /// band - NOT from a shadow raked across the plate, which reads as painted.</summary>
        Casual,
        /// <summary>Technical: hairline keyline, thin light rim, minimal sculpt.
        /// rpgui1/racing4/rpgui2.</summary>
        Technical,
    }

    /// <summary>
    /// The genre's PROPORTIONS - how a widget is built, independent of its colour.
    ///
    /// Exists because the first phase-A proof rendered five genres as the same brown plate: the
    /// metrics lived as constants on KitControl, so every genre inherited one build and only the
    /// palette moved. A genre must be recognisable with colour removed (PLAN.md 4.1); these are
    /// the numbers that make that true. Colour is deliberately NOT a field here.
    /// </summary>
    public sealed class KitGeometry
    {
        /// <summary>Corner cut/radius as a fraction of the shorter side.</summary>
        public float Corner = 0.18f;
        /// <summary>Height as a multiple of the theme font, so proportion survives a type change
        /// instead of pinning a pixel height.</summary>
        public float HeightRatio = 2.6f;
        public float PadRatio = 1.6f;
        /// <summary>Rim weight in px at 14pt, scaled with the font.</summary>
        public float Rim = 2.0f;
        public float Bevel = 1.0f;
        public float Gloss = 0.4f;
        public float Sparkle;
        /// <summary>Which reference family this genre is drawn from. See <see cref="KitRegister"/>.</summary>
        public KitRegister Register = KitRegister.Carved;

        /// <summary>Which frame regime this genre uses. See <see cref="KitFrameMode"/>.</summary>
        public KitFrameMode FrameMode = KitFrameMode.Structural;
        /// <summary>Constant thickness for <see cref="KitFrameMode.Hairline"/>, in px at 14pt.</summary>
        public float HairlinePx = 2f;

        /// <summary>
        /// Frame thickness in px for a widget of this height.
        ///
        /// Replaces the old `FrameRatio` fraction, which could not fit both ends of the measured
        /// range: citybuilder5's 35px capsule carries a 6px frame (0.17) and its 107px tile an
        /// 11px one (0.10). A single ratio produces one or the other, never both — at 0.10 a
        /// 30px chip gets 3px, under the ~3.5px floor, and reads as a hairline border instead of
        /// carving. The linear fit holds across both: 3.5 + 0.07 x height.
        /// </summary>
        public float FramePx(float height) => FrameMode switch
        {
            KitFrameMode.None => 0f,
            KitFrameMode.Hairline => HairlinePx,
            _ => 3.5f + 0.07f * height,
        };

        /// <summary>
        /// How much darker the inner plate is than the frame, BY ELEVATION.
        ///
        /// This is one number per elevation rather than one per genre because citybuilder5
        /// measures both on the same screen, in the same material, 7x apart: the raised
        /// ActionTile's plate sits at 0.42/0.48 = 0.875 of its frame, while the recessed
        /// StoneCapsule readout sits at 0.09/0.77 = 0.12. INDEX.md summarises that second figure
        /// as "PlateShade 0.88 -> 0.12", which over-generalises a recessed READOUT into a global
        /// constant — applying it to everything would render every button's plate near-black.
        /// The split tracks elevation, which the kit already models.
        /// </summary>
        public float PlateShadeFor(KitElevation e) => e switch
        {
            KitElevation.Recessed => 0.12f,
            KitElevation.Flush => 0.55f,
            _ => 0.88f,
        };

        /// <summary>
        /// Recess for a large CONTENT well — a panel body, an inventory slot — as a multiple of
        /// its host.
        ///
        /// Deliberately NOT <see cref="PlateShadeFor"/>'s 0.12. That figure is measured on
        /// citybuilder5's StoneCapsule, a small readout sunk into a pale frame, and applying it
        /// to a panel body renders the whole panel as a black hole (seen, not theorised). The
        /// value for a content well is the "subtle inset" ratio that citybuilder3's tiles and
        /// gameui1's parchment slots produced INDEPENDENTLY at <b>0.79-0.80 x</b> the host, and
        /// it agrees with the slot interiors measured elsewhere (gameui9 L=0.42 against a
        /// brighter surround, rpg3's available slots at L≈0.67-0.72).
        ///
        /// Same lesson as the plate-shade correction: a lightness ratio is conditional on the
        /// WIDGET CLASS it was measured on. Check what was under the ruler before reusing it.
        /// </summary>
        public float WellShade = 0.79f;

        /// <summary>
        /// Glyph size as a fraction of an icon button, measured per family:
        /// <b>0.40 carved</b>, <b>0.55 flat</b> (citybuilder1 vs citybuilder2) and
        /// <b>0.60</b> on gameui3's kit. A carved plate spends its area on the frame, so its
        /// glyph is proportionally smaller; a flat plate gives the area to the icon.
        ///
        /// Defaults follow <see cref="Register"/> rather than being restated per genre, which is
        /// the point of having a register at all.
        /// </summary>
        public float GlyphRatio => Register switch
        {
            KitRegister.Carved => 0.40f,
            KitRegister.Casual => 0.55f,
            _ => 0.60f,
        };

        /// <summary>
        /// Outer rim lightness as a multiple of the plate. Above 1 is a BRIGHT carved rim
        /// (citybuilder5 measures 2.05x); below 1 is the thick dark outline of the casual/mobile
        /// register. Both appear in the reference set and the two families must not be averaged.
        ///
        /// Exists because the gate measured rim:body at 0.16 for ALL TEN genres — an identical
        /// dark line everywhere, contributing nothing to genre identity while the references use
        /// rim polarity as one of their loudest tells.
        /// </summary>
        public float RimBrightness = 0.24f;
        /// <summary>Corner studs/rivets. 0 = none.</summary>
        public int Studs;
        public float Overhang = 0.5f;

        // Proportions from PLAN.md 4.2; frame regime and rim polarity from the measured art
        // documents in plans/game-ui-kit/art/.
        //
        // THREE REGISTERS, deliberately not averaged (PLAN.md 34: the two style families in
        // Example_Art/ "must not be averaged"):
        //
        //   CARVED     structural frame + BRIGHT rim   rpg survival strategy citybuilder
        //              rpgui/Upgrades/citybuilder5. Frame grows with the widget; the outer rim
        //              is lighter than the plate (2.05x on citybuilder5, 1.78x on Upgrades).
        //   CASUAL     no frame + thick DARK outline   platformer puzzle cardgame topdown
        //              ui1/ui2/skilltree1/store. One flat plate, large radius, heavy dark
        //              keyline. This is the family the tracker names as procedurally reachable
        //              and says to target first, so the outline IS the edge treatment and there
        //              is no separate frame to carve.
        //   TECHNICAL  hairline frame + thin light rim shooter racing
        //              rpgui1/racing4/rpgui2, where a chip and a panel carry the same 1-3px line.
        private static readonly Dictionary<string, KitGeometry> _byGenre = new()
        {
            ["rpg"]         = new() { Register = KitRegister.Carved, Corner = .16f, HeightRatio = 2.9f, PadRatio = 1.9f, Rim = 3.0f, Bevel = 1.2f, Gloss = .55f, Sparkle = .35f, Studs = 1, FrameMode = KitFrameMode.Structural,  RimBrightness = 1.90f },
            ["survival"]    = new() { Register = KitRegister.Carved, Corner = .12f, HeightRatio = 2.7f, PadRatio = 1.7f, Rim = 3.0f, Bevel = 1.1f, Gloss = .20f, Studs = 1, FrameMode = KitFrameMode.Structural,  RimBrightness = 1.80f },
            ["strategy"]    = new() { Register = KitRegister.Carved, Corner = .04f, HeightRatio = 2.4f, PadRatio = 1.5f, Rim = 2.5f, Bevel = 0.8f, Gloss = .25f, Studs = 2, FrameMode = KitFrameMode.Structural,  RimBrightness = 2.05f },
            ["citybuilder"] = new() { Register = KitRegister.Carved, Corner = .06f, HeightRatio = 2.5f, PadRatio = 1.6f, Rim = 2.0f, Bevel = 0.7f, Gloss = .30f,            FrameMode = KitFrameMode.Structural,  RimBrightness = 2.05f },

            ["platformer"]  = new() { Register = KitRegister.Casual, Corner = .45f, HeightRatio = 3.1f, PadRatio = 2.1f, Rim = 3.5f, Bevel = 1.3f, Gloss = .80f,            FrameMode = KitFrameMode.None,        RimBrightness = 0.18f },
            ["puzzle"]      = new() { Register = KitRegister.Casual, Corner = .30f, HeightRatio = 3.0f, PadRatio = 2.0f, Rim = 2.5f, Bevel = 1.2f, Gloss = .90f, Sparkle = .40f, FrameMode = KitFrameMode.None,   RimBrightness = 0.18f },
            ["cardgame"]    = new() { Register = KitRegister.Casual, Corner = .22f, HeightRatio = 2.8f, PadRatio = 1.8f, Rim = 2.0f, Bevel = 0.9f, Gloss = .70f, Sparkle = .50f, FrameMode = KitFrameMode.None,   RimBrightness = 0.20f },
            ["topdown"]     = new() { Register = KitRegister.Casual, Corner = .18f, HeightRatio = 2.6f, PadRatio = 1.7f, Rim = 2.0f, Bevel = 1.0f, Gloss = .40f,            FrameMode = KitFrameMode.None,        RimBrightness = 0.22f },

            ["shooter"]     = new() { Register = KitRegister.Technical, Corner = .10f, HeightRatio = 2.3f, PadRatio = 1.5f, Rim = 1.5f, Bevel = 0.6f, Gloss = .35f,            FrameMode = KitFrameMode.Hairline, HairlinePx = 2.0f, RimBrightness = 1.35f },
            ["racing"]      = new() { Register = KitRegister.Technical, Corner = .08f, HeightRatio = 2.2f, PadRatio = 1.4f, Rim = 1.5f, Bevel = 0.7f, Gloss = .85f, Sparkle = .25f, FrameMode = KitFrameMode.Hairline, HairlinePx = 1.5f, RimBrightness = 1.45f },
        };

        private static readonly KitGeometry _default = new();

        public static KitGeometry ForGenre(string? genre)
            => genre != null && _byGenre.TryGetValue(genre.ToLowerInvariant(), out var g) ? g : _default;
    }

    /// <summary>Anchor for a sub-element, including positions OUTSIDE the host.
    ///
    /// This is the primitive Godot has no answer for. A banner that overhangs its frame and a
    /// cost badge pinned across a node's corner are the two most repeated moves in the whole
    /// reference set, and both are impossible with containers alone — a child is clipped to or
    /// laid out inside its parent. Attachments are drawn by the HOST, after its own layers, so
    /// they can cross its edge.</summary>
    public enum KitAnchor
    {
        TopLeft, TopCentre, TopRight,
        MiddleLeft, Centre, MiddleRight,
        BottomLeft, BottomCentre, BottomRight,
        Above, Below,
    }

    /// <summary>A sub-element pinned to one of its host's anchors, free to overhang it.</summary>
    public sealed class KitAttach
    {
        public KitAnchor Anchor = KitAnchor.TopCentre;
        public Vector2 Size = new(24, 24);
        /// <summary>Extra nudge after anchoring, in pixels.</summary>
        public Vector2 Offset = Vector2.Zero;
        /// <summary>How far past the host edge it sits, 0..1 of its own size. 0.5 straddles.</summary>
        public float Overhang = 0.5f;
        public KitShape Shape = KitShape.Round;
        public UiSurface.Role Role = UiSurface.Role.Accent;
        public Texture2D? Icon;
        public string Text = "";

        /// <summary>Rect in the HOST's local space. May fall outside the host — that is the
        /// entire point.</summary>
        public Rect2 Resolve(Vector2 hostSize)
        {
            float x = Anchor switch
            {
                KitAnchor.TopLeft or KitAnchor.MiddleLeft or KitAnchor.BottomLeft => 0f,
                KitAnchor.TopRight or KitAnchor.MiddleRight or KitAnchor.BottomRight => hostSize.X,
                _ => hostSize.X * 0.5f,
            };
            float y = Anchor switch
            {
                KitAnchor.TopLeft or KitAnchor.TopCentre or KitAnchor.TopRight or KitAnchor.Above => 0f,
                KitAnchor.BottomLeft or KitAnchor.BottomCentre or KitAnchor.BottomRight or KitAnchor.Below => hostSize.Y,
                _ => hostSize.Y * 0.5f,
            };

            // Centre on the anchor, then push out by Overhang so it crosses the edge.
            var pos = new Vector2(x - Size.X * 0.5f, y - Size.Y * 0.5f) + Offset;
            float push = Size.Y * Overhang;
            if (Anchor is KitAnchor.Above) pos.Y -= push;
            else if (Anchor is KitAnchor.Below) pos.Y += push;
            return new Rect2(pos, Size);
        }
    }
}

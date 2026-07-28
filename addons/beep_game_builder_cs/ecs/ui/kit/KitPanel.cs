using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A panel: frame, a RECESSED inner well, and an optional overhanging title banner.
    ///
    /// The structure is the one PLAN.md 4.2a extracted from the reference sheets — "a game
    /// control is a FRAME around an INNER PLATE, two nested shapes, not one plate with a bevel"
    /// — plus the banner, which the art pass counts as the most repeated element in the folder.
    /// A Godot `PanelContainer` can express none of it: one StyleBox, one rectangle, and a title
    /// that must sit inside the box rather than across its edge.
    ///
    /// The well is inset to <b>0.79-0.80 x</b> the host, a ratio two unrelated families produced
    /// independently (citybuilder3's tiles and gameui1's parchment slots), and is drawn at the
    /// RECESSED plate shade so it reads as carved into the frame rather than laid on top of it.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitPanel : KitControl
    {
        [Export] public string Title { get => _title; set { _title = value ?? ""; QueueRedraw(); } }
        private string _title = "";

        /// <summary>Banner silhouette. Plaque/Ribbon/Shield/Ellipse are the four the reference
        /// kits use; the genre picks one unless this is overridden.</summary>
        [Export] public bool OverrideBannerShape { get; set; }
        [Export] public KitShape BannerShape { get; set; } = KitShape.Rect;

        /// <summary>Banner lightness as a multiple of the frame. 0.44 (gameui2) reads recessed;
        /// values above 1 give gameui4's white plate. Polarity is per-family, so it is exposed.</summary>
        [Export(PropertyHint.Range, "0.1,1.6,0.01")] public float BannerShade { get; set; } = 0.44f;

        /// <summary>Draw the inner well. Off gives a plain framed plate.</summary>
        [Export] public bool ShowWell { get; set; } = true;

        public override void _Ready()
        {
            base._Ready();
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                CustomMinimumSize = new Vector2(fs * 16f, fs * 9f);
            }
        }

        /// <summary>Ribbon for the wood/adventure genres, ellipse for the candy ones, plaque
        /// otherwise — the same register mapping PanelFrameComponent settled on in Stage 32c,
        /// kept identical so a kit panel and a framed legacy screen agree.</summary>
        private KitShape ResolvedBannerShape()
        {
            if (OverrideBannerShape) return BannerShape;
            return Geo.Register switch
            {
                KitRegister.Carved => KitShape.Ribbon,
                KitRegister.Casual => KitShape.Ellipse,
                _ => KitShape.Rect,
            };
        }

        /// <summary>The content rect a caller should lay children out inside — the well, minus
        /// the banner's intrusion. Public so a screen does not have to re-derive the insets and
        /// drift from them.</summary>
        public Rect2 ContentRect()
        {
            Rect2 body = BodyRect();
            float ft = Geo.FramePx(body.Size.Y);
            return new Rect2(body.Position + new Vector2(ft, ft),
                             new Vector2(Mathf.Max(0f, body.Size.X - ft * 2f),
                                         Mathf.Max(0f, body.Size.Y - ft * 2f)));
        }

        /// <summary>Half the banner's height — the amount it hangs above the frame.</summary>
        private float BannerOverhang()
            => string.IsNullOrEmpty(_title)
                ? 0f
                : Mathf.Max(UiSurface.FontSize(this) * 1.5f, Size.Y * 0.14f) * 0.5f;

        /// <summary>
        /// The frame, inset from the top by the banner's overhang.
        ///
        /// The banner straddles the FRAME's edge — the measured behaviour — but the whole widget
        /// stays inside its own rect, because a Container reserves space from the control's size
        /// and knows nothing about anything drawn outside it. Drawing the banner at a negative y
        /// instead put it on top of whatever sat above: in kit_gallery.tscn the EQUIPMENT banner
        /// covered the COMBO stat row in the HBox above it.
        /// </summary>
        private Rect2 BodyRect()
        {
            float o = BannerOverhang();
            return new Rect2(0f, o, Size.X, Mathf.Max(4f, Size.Y - o));
        }

        /// <summary>Containers size from here, so the banner's headroom is part of the ask
        /// rather than something the panel silently borrows from its neighbour.</summary>
        public override Vector2 _GetMinimumSize()
        {
            var b = base._GetMinimumSize();
            return new Vector2(b.X, b.Y + BannerOverhang());
        }

        public override void _Draw()
        {
            if (Size.X <= 8 || Size.Y <= 8) return;

            var g = Geo;
            Rect2 body = BodyRect();
            Color face = FaceColor();
            Color ink = InkColor();
            float fs = UiSurface.FontSize(this);
            float rimPx = Mathf.Max(1f, g.Rim * (fs / 14f));

            // Frame.
            DrawShape(body, ActiveShape, face, RimColor(), rimPx);

            if (ShowWell)
            {
                // 0.79-0.80 x host, measured independently by two families. Derived from the
                // frame thickness where that is larger, so a carved genre's well clears its frame.
                float ft = Mathf.Max(g.FramePx(body.Size.Y), Mathf.Min(body.Size.X, body.Size.Y) * 0.10f);
                var well = new Rect2(body.Position + new Vector2(ft, ft),
                                     body.Size - new Vector2(ft * 2f, ft * 2f));
                if (well.Size.X > 4 && well.Size.Y > 4)
                {
                    float ps = g.WellShade;
                    var sunk = new Color(face.R * ps, face.G * ps, face.B * ps, face.A);
                    DrawShape(well, ActiveShape, sunk, ink, Mathf.Max(1f, rimPx * 0.5f));
                }
            }

            // Banner last so it draws OVER the frame it straddles.
            DrawBanner(body, _title, ResolvedBannerShape(), shade: BannerShade);

            DrawAttachments();
        }
    }
}

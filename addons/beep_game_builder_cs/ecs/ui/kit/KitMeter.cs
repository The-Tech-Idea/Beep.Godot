using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A progress/resource meter that is SEGMENTED by default.
    ///
    /// "**Segmented progress is the default, continuous is the exception**" is one of the art
    /// pass's settled rules, measured across seven independent references (gameui1-4, rpg1, rpg2,
    /// rpgui1). Every meter this framework shipped before was a continuous bar, which is the
    /// exception being used as the rule.
    ///
    /// Two more settled rules are built in rather than left to the caller:
    ///  - "**Empty/track = a dark tint of the surface's own HUE, never grey**" (4 references).
    ///    A grey track is the single clearest tell of a themed form; the track must carry the
    ///    same hue as the fill so the meter reads as one object.
    ///  - "**The palette goes on ONE element**, the other stays neutral" (5 references) — so the
    ///    fill takes the role colour and the track is derived from it, not separately themed.
    ///
    /// The optional end CAP comes from gameui6 and rpgui.md's finding that on that sheet
    /// "variation lives in the END CAPS, not the body — six bars, one track". The cap is a
    /// KitAttach so it can overhang the bar, which is how every reference draws it.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitMeter : ProgressBar
    {
        /// <summary>A bar: takes the theme's bar corner, which the
        /// references vary independently of the button corner.</summary>
        // A BAR takes the theme's bar corner, which the references vary independently of the
        // button corner. KitControl exposed this as an override; deriving from ProgressBar means
        // stating it directly instead.
        private const KitWidgetClass Class = KitWidgetClass.Bar;

        /// <summary>Overhanging sub-elements. KitControl kept this list for its subclasses;
        /// deriving from ProgressBar means owning it here and drawing it through KitChrome, which
        /// is the same resolve either way.</summary>
        private readonly System.Collections.Generic.List<KitAttach> Attachments = new();


        /// <summary>Number of segments. 0 makes the meter continuous — deliberately available,
        /// but deliberately NOT the default.</summary>
        [Export(PropertyHint.Range, "0,40,1")]
        public int Segments { get => _segments; set { _segments = Mathf.Max(0, value); QueueRedraw(); } }
        private int _segments = 10;

        [Export] public UiSurface.Role Fill { get; set; } = UiSurface.Role.Success;

        /// <summary>Icon pinned over the bar's leading end, overhanging it. Optional.</summary>
        [Export] public Texture2D? CapIcon { get => _cap; set { _cap = value; Rebuild(); } }
        private Texture2D? _cap;

        /// <summary>
        /// End caps as a PAIR, per tier — art-pass file 11.
        ///
        /// A tiered meter (health that upgrades, a boss bar with phases) marks its ends: a socket
        /// at the left the fill grows out of, and a terminal at the right that says where full
        /// IS. Without them a bar at 100% and a bar whose maximum has been upgraded look the
        /// same, which is precisely what a tier is supposed to communicate.
        /// </summary>
        [Export] public int Tier { get => _tier; set { _tier = Mathf.Max(0, value); Rebuild(); } }
        private int _tier;

        /// <summary>Draw the right-hand terminal as well as the left socket. Off for a plain
        /// resource bar, on for anything with a ceiling worth naming.</summary>
        [Export] public bool EndCaps { get => _caps; set { _caps = value; Rebuild(); } }
        private bool _caps;

        private string _genre = "";
        private KitGeometry Geo => KitGeometry.ForGenre(_genre);

        public override void _Ready()
        {
            _genre = KitChrome.GenreOf(this);
            // 0..1, like KitSlider: every existing `Value = 0.72` keeps meaning 72%, and Range
            // still offers a real domain to anyone who wants one. ProgressBar's own 0..100
            // default would have silently reinterpreted every shipped scene as "1 percent".
            MinValue = 0.0; MaxValue = 1.0; Step = 0.001;
            ShowPercentage = false;      // the kit draws its own readout
            foreach (string sb in new[] { "background", "fill" })
                AddThemeStyleboxOverride(sb, new StyleBoxEmpty());
            ValueChanged += _ => QueueRedraw();
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                // HUD rail height : text cap-height = 2.6 (citybuilder1), and the rail itself is
                // ~3% of screen height across five references. Sized off the type so it holds.
                CustomMinimumSize = new Vector2(fs * 10f, fs * 1.15f);
            }
            Rebuild();
        }

        private void Rebuild()
        {
            Attachments.Clear();
            if (_cap != null)
            {
                int fs = UiSurface.FontSize(this);
                Attachments.Add(new KitAttach
                {
                    Anchor = KitAnchor.MiddleLeft,
                    Size = new Vector2(fs * 1.6f, fs * 1.6f),
                    Shape = KitShape.Round,
                    Role = Fill,
                    Icon = _cap,
                    Overhang = 0.5f,
                });
            }
            if (_caps)
            {
                int fs = UiSurface.FontSize(this);
                float d = fs * 1.35f;
                // The socket the fill grows OUT of, and the terminal it grows TOWARD. Both
                // straddle their end, because a cap that sits inside the bar reads as part of
                // the fill rather than as its boundary.
                Attachments.Add(new KitAttach
                {
                    Anchor = KitAnchor.MiddleLeft, Size = new Vector2(d, d),
                    Shape = KitShape.Round, Role = Fill, Overhang = 0.5f,
                });
                Attachments.Add(new KitAttach
                {
                    Anchor = KitAnchor.MiddleRight, Size = new Vector2(d, d),
                    // The terminal carries the TIER as a numeral when there is one -- that is the
                    // whole reason the pair exists rather than a single decorative cap.
                    Shape = _tier > 0 ? KitShape.Round : KitShape.Pill,
                    Role = _tier > 0 ? UiSurface.Role.Warning : Fill,
                    Text = _tier > 0 ? _tier.ToString() : "",
                    Overhang = 0.5f,
                });
            }
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (Size.X <= 4 || Size.Y <= 4) return;

            var g = Geo;
            Color ink = UiSurface.Ink(UiSurface.Of(this));
            Color fill = UiSurface.Semantic(this, Fill);

            // The track is the fill's own hue driven dark — never a neutral grey.
            Color track = new Color(fill.R * 0.26f, fill.G * 0.26f, fill.B * 0.30f, 1f);

            // The cap overhangs, so the bar is inset to leave it room.
            float inset = _cap != null ? UiSurface.FontSize(this) * 0.55f : 0f;
            var bar = new Rect2(inset, 0, Size.X - inset, Size.Y);
            if (bar.Size.X <= 2) return;

            float rimPx = Mathf.Max(1f, g.Rim * 0.6f * (UiSurface.FontSize(this) / 14f));
            KitChrome.DrawShape(this, _genre, bar, KitChrome.Shape(_genre), track, ink, rimPx);

            if ((float)Value > 0f)
            {
                if (_segments <= 0)
                {
                    var f = new Rect2(bar.Position, new Vector2(bar.Size.X * (float)Value, bar.Size.Y));
                    if (f.Size.X > 1) KitChrome.DrawShape(this, _genre, f, KitChrome.Shape(_genre), fill, ink, 0f);
                }
                else
                {
                    // Gap scales with the bar so segments stay legible at any size; a fixed pixel
                    // gap disappears on a HUD rail and gapes on a full-width bar.
                    float gap = Mathf.Max(1f, bar.Size.Y * 0.14f);
                    float segW = (bar.Size.X - gap * (_segments - 1)) / _segments;
                    if (segW > 0.5f)
                    {
                        float lit = (float)Value * _segments;
                        for (int i = 0; i < _segments; i++)
                        {
                            float amount = Mathf.Clamp(lit - i, 0f, 1f);
                            if (amount <= 0.001f) break;
                            var s = new Rect2(bar.Position.X + i * (segW + gap), bar.Position.Y,
                                              segW * amount, bar.Size.Y);
                            if (s.Size.X > 0.5f) KitChrome.DrawShape(this, _genre, s, KitChrome.Shape(_genre), fill, ink, 0f);
                        }
                    }
                }
            }

            KitChrome.DrawAttachments(this, _genre, Attachments);
        }
    }
}

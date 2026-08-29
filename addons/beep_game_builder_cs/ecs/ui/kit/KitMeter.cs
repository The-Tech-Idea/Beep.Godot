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
        public int Segments
        {
            get => _segments;
            set
            {
                int next = Mathf.Max(0, value);
                if (_segments == next) return;
                _segments = next;
                RefreshMinimumAndRedraw();
            }
        }
        private int _segments = 0;

        [Export]
        public UiSurface.Role Fill
        {
            get => _fill;
            set { if (_fill == value) return; _fill = value; Rebuild(); }
        }
        private UiSurface.Role _fill = UiSurface.Role.Success;

        /// <summary>Optional value printed inside the rail. HUD binders set this when they have
        /// an exact value; decorative meters can leave it empty and stay purely visual.</summary>
        [Export]
        public string Readout
        {
            get => _readout;
            set
            {
                string next = value ?? "";
                if (_readout == next) return;
                _readout = next;
                RefreshMinimumAndRedraw();
            }
        }
        private string _readout = "";

        /// <summary>Icon pinned over the bar's leading end, overhanging it. Optional.</summary>
        [Export]
        public Texture2D? CapIcon
        {
            get => _cap;
            set
            {
                if (_cap == value) return;
                _cap = value;
                Rebuild();
            }
        }
        private Texture2D? _cap;

        /// <summary>
        /// End caps as a PAIR, per tier — art-pass file 11.
        ///
        /// A tiered meter (health that upgrades, a boss bar with phases) marks its ends: a socket
        /// at the left the fill grows out of, and a terminal at the right that says where full
        /// IS. Without them a bar at 100% and a bar whose maximum has been upgraded look the
        /// same, which is precisely what a tier is supposed to communicate.
        /// </summary>
        [Export]
        public int Tier
        {
            get => _tier;
            set
            {
                int next = Mathf.Max(0, value);
                if (_tier == next) return;
                _tier = next;
                Rebuild();
            }
        }
        private int _tier;

        /// <summary>Draw the right-hand terminal as well as the left socket. Off for a plain
        /// resource bar, on for anything with a ceiling worth naming.</summary>
        [Export]
        public bool EndCaps
        {
            get => _caps;
            set
            {
                if (_caps == value) return;
                _caps = value;
                Rebuild();
            }
        }
        private bool _caps;

        private string _genre = "";
        private KitGeometry Geo => KitGeometry.ForGenre(_genre);
        private bool _eventsHooked;

        public KitMeter()
        {
            MinValue = 0.0;
            MaxValue = 1.0;
            Step = 0.001;
        }

        public override void _Ready()
        {
            base._Ready();
            _genre = KitChrome.GenreOf(this);
            ShowPercentage = false;      // the kit draws its own readout
            SuppressNativeStyles();
            if (!_eventsHooked)
            {
                ValueChanged += _ => QueueRedraw();
                _eventsHooked = true;
            }
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
            Rebuild();
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what != NotificationThemeChanged) return;

            _genre = KitChrome.GenreOf(this);
            SuppressNativeStyles();
            Rebuild();
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            float h = Mathf.Clamp(fs * 1.25f, 14f, 22f);
            float gap = Mathf.Max(1f, h * 0.14f);
            float w = fs * 10f;
            if (_segments > 0)
                w = Mathf.Max(w, _segments * Mathf.Max(fs * 0.62f, h * 0.50f) + gap * (_segments - 1));

            if (!string.IsNullOrEmpty(_readout))
                w = Mathf.Max(w, TextWidth(_readout, UiSurface.TextRole.Caption) + fs * 3.2f);

            if (_cap != null)
                w += fs * 1.15f;
            if (_caps)
                w += fs * 1.35f;

            return new Vector2(w, h);
        }

        private void RefreshMinimumAndRedraw()
        {
            KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
            UpdateMinimumSize();
            QueueRedraw();
        }

        private float TextWidth(string text, UiSurface.TextRole role)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            string genre = string.IsNullOrEmpty(_genre) ? KitChrome.GenreOf(this) : _genre;
            Font? font = KitChrome.Font(this, genre);
            int fs = UiSurface.FontSize(this, role);
            string draw = KitChrome.Case(text, genre);
            return font?.GetStringSize(draw, HorizontalAlignment.Left, -1, fs).X ?? draw.Length * fs * 0.56f;
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
            RefreshMinimumAndRedraw();
        }

        private void SuppressNativeStyles()
        {
            foreach (string sb in new[] { "background", "fill" })
                KitChrome.SetEmptyStyleboxOverride(this, sb);
        }

        public override void _Draw()
        {
            if (Size.X <= 4 || Size.Y <= 4) return;

            var g = Geo;
            Color ink = UiSurface.Ink(UiSurface.Of(this));
            Color fill = UiSurface.SemanticOrDerived(this, Fill);

            // The track is the fill's own hue driven dark — never a neutral grey.
            Color track = new Color(fill.R * 0.26f, fill.G * 0.26f, fill.B * 0.30f, 1f);

            // The cap overhangs, so the bar is inset to leave it room.
            float inset = _cap != null ? UiSurface.FontSize(this) * 0.55f : 0f;
            var bar = new Rect2(inset, 0, Size.X - inset, Size.Y);
            if (bar.Size.X <= 2) return;

            float rimPx = Mathf.Max(1f, g.Rim * 0.6f * (UiSurface.FontSize(this) / 14f));
            KitChrome.DrawShape(this, _genre, bar, KitChrome.Shape(_genre, Class), track, ink, rimPx, Class);

            if ((float)Value > 0f)
            {
                if (_segments <= 0)
                {
                    var f = new Rect2(bar.Position, new Vector2(bar.Size.X * (float)Value, bar.Size.Y));
                    if (f.Size.X > 1) KitChrome.DrawShape(this, _genre, f, KitChrome.Shape(_genre, Class), fill, ink, 0f, Class);
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
                            if (s.Size.X > 0.5f) KitChrome.DrawShape(this, _genre, s, KitChrome.Shape(_genre, Class), fill, ink, 0f, Class);
                        }
                    }
                }
            }

            DrawReadout(bar);
            KitChrome.DrawAttachments(this, _genre, Attachments);
        }

        private void DrawReadout(Rect2 bar)
        {
            if (string.IsNullOrEmpty(_readout)) return;
            var font = KitChrome.Font(this, _genre);
            if (font == null) return;

            string readout = KitChrome.Case(_readout, _genre);
            int fs = UiSurface.FitText(this, bar.Size - new Vector2(UiSurface.FontSize(this) * 1.1f, 0f),
                                       0.62f, readout, font, min: 7, themeMax: 0.9f);
            readout = KitChrome.EllipsizeText(font, readout, fs, bar.Size.X - UiSurface.FontSize(this) * 1.1f);
            if (string.IsNullOrEmpty(readout)) return;
            Vector2 size = font.GetStringSize(readout, HorizontalAlignment.Left, -1, fs);
            var p = new Vector2(bar.Position.X + (bar.Size.X - size.X) * 0.5f,
                                bar.Position.Y + (bar.Size.Y + size.Y * 0.62f) * 0.5f);
            // Routed, not hand-shadowed: the 1px black copy this used to draw was a fifth text
            // treatment the theme never asked for, and it doubled up with Engraved/Extruded.
            KitChrome.DrawText(this, _genre, font, p, readout, fs, UiSurface.Text(this));
        }
    }
}

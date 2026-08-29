using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A panel plus a chevron handle that STRADDLES its leading edge.
    ///
    /// Measured from Example_Art/ui8.png (plans/game-ui-kit/art/ui8.md, widget 1):
    ///
    /// | part           | measured                                              |
    /// |----------------|-------------------------------------------------------|
    /// | handle         | **33px** tall, dark plate `#4D4136` **L=0.26**         |
    /// | handle glyph   | `v` in **pure white L=1.00** — maximum contrast        |
    /// | position       | **overhanging the panel's edge**, centred             |
    /// | panel interior | warm cream **L=0.82**                                 |
    ///
    /// The art document's conclusion: *"the affordance lives OUTSIDE the panel, on the edge it
    /// moves along, and it is a chevron"* — and it names this "the widget the kit was originally
    /// asked for". The handle carries the highest-contrast glyph on the screen because it is the
    /// only control whose STATE the player must read at a glance.
    ///
    /// This differs from the existing <c>CollapsiblePanelComponent</c> in kind, not degree: that
    /// one is a Node that drives a separate host Control and tweens its height, with the chevron
    /// pinned per-frame from the panel's rect. This is a drawn kit widget that owns its own
    /// plate, well and handle as one silhouette — so the handle cannot drift from the panel and
    /// there is no host-type constraint to warn about.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitCollapsiblePanel : KitControl
    {
        /// <summary>A panel: takes the theme's panel corner, which the
        /// references vary independently of the button corner.</summary>
        protected override KitWidgetClass WidgetClass => KitWidgetClass.Panel;

        /// <summary>Which edge the handle straddles, and therefore which way the panel folds.</summary>
        public enum Edge { Top, Bottom, Left, Right }

        [Export] public Edge HandleEdge { get => _edge; set { if (_edge == value) return; _edge = value; RefreshMinimumAndRedraw(); } }
        private Edge _edge = Edge.Top;

        [Export] public bool Collapsed
        {
            get => _collapsed;
            set
            {
                if (_collapsed == value) return;
                _collapsed = value;
                RefreshVisualAndRedraw();
                if (IsInsideTree())
                    EmitSignal(SignalName.Toggled, value);
            }
        }
        private bool _collapsed;

        [Export]
        public string Title
        {
            get => _title;
            set
            {
                string next = value ?? "";
                if (_title == next) return;
                _title = next;
                RefreshMinimumAndRedraw();
            }
        }
        private string _title = "";
        [Export] public KitPanelHeaderStyle HeaderStyle { get => _headerStyle; set { if (_headerStyle == value) return; _headerStyle = value; RefreshMinimumAndRedraw(); } }
        private KitPanelHeaderStyle _headerStyle = KitPanelHeaderStyle.UtilityStrip;
        [Export(PropertyHint.Range, "0.45,1.4,0.01")]
        public float TitleFontScale { get => _titleFontScale; set { if (Mathf.IsEqualApprox(_titleFontScale, value)) return; _titleFontScale = value; RefreshMinimumAndRedraw(); } }
        private float _titleFontScale = 0.90f;
        [Export(PropertyHint.Range, "0.1,1.6,0.01")]
        public float BannerShade
        {
            get => _bannerShade;
            set
            {
                if (Mathf.Abs(_bannerShade - value) < 0.001f) return;
                _bannerShade = value;
                RefreshVisualAndRedraw();
            }
        }
        private float _bannerShade = 0.44f;
        private bool _hoverHandle;
        private bool _eventsHooked;

        [Signal] public delegate void ToggledEventHandler(bool collapsed);

        /// <summary>33px at the reference's scale. Expressed against the type so it holds at any
        /// resolution rather than pinning a pixel height the way the measurement does.</summary>
        private float HandleSize => Mathf.Clamp(UiSurface.FontSize(this) * 1.45f, 18f, 28f);

        public override void _Ready()
        {
            base._Ready();
            ApplyInputDefaults(MouseFilterEnum.Stop, FocusModeEnum.All);
            if (!_eventsHooked)
            {
                MouseExited += ClearHandleHover;
                _eventsHooked = true;
            }
            RefreshKitMinimumSize();
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (KitChrome.ShouldClearPointerState(this, what))
                ClearHandleHover();
        }

        private void RefreshKitMinimumSize()
        {
            KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
        }

        private void RefreshMinimumAndRedraw()
        {
            RefreshKitMinimumSize();
            UpdateMinimumSize();
            QueueRedraw();
        }

        private void RefreshVisualAndRedraw()
        {
            QueueRedraw();
        }

        private void ClearHandleHover()
        {
            if (!_hoverHandle) return;
            _hoverHandle = false;
            QueueRedraw();
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            string genre = KitChrome.GenreOf(this);
            float handlePad = HandleSize * 0.5f;
            float width = fs * 14f;
            float height = fs * 8f;
            if (_edge is Edge.Left or Edge.Right)
                width += handlePad;
            else
                height += handlePad;

            if (!string.IsNullOrEmpty(_title) && HeaderStyle != KitPanelHeaderStyle.None)
            {
                Font? font = KitFont();
                int titleFs = UiSurface.FontSize(this, TitleFontScale, min: 8);
                float titleW = TextWidth(font, KitCase(_title), titleFs);
                width = Mathf.Max(width, titleW + titleFs * 3.2f);
                height += KitChrome.PanelHeaderRoom(this, genre, _title, HeaderStyle, TitleFontScale, height);
            }

            return new Vector2(width, height);
        }

        private static float TextWidth(Font? font, string text, int fs)
            => string.IsNullOrEmpty(text)
                ? 0f
                : font?.GetStringSize(text, HorizontalAlignment.Left, -1, fs).X ?? text.Length * fs * 0.56f;

        /// <summary>The handle's rect in local space. It deliberately falls OUTSIDE the panel
        /// body — that is the measured behaviour and the reason this is a drawn widget rather
        /// than a container with a header child.</summary>
        private Rect2 HandleRect()
        {
            float s = HandleSize;
            return _edge switch
            {
                Edge.Bottom => new Rect2((Size.X - s * 1.25f) * 0.5f, Size.Y - s * 0.42f, s * 1.25f, s),
                Edge.Left => new Rect2(-s * 0.42f, (Size.Y - s * 1.25f) * 0.5f, s, s * 1.25f),
                Edge.Right => new Rect2(Size.X - s * 0.58f, (Size.Y - s * 1.25f) * 0.5f, s, s * 1.25f),
                _ => new Rect2((Size.X - s * 1.25f) * 0.5f, -s * 0.42f, s * 1.25f, s),
            };
        }

        /// <summary>The panel body, inset on the handle's edge so the handle straddles it.</summary>
        private Rect2 BodyRect()
        {
            float pad = HandleSize * 0.5f;
            return _edge switch
            {
                Edge.Bottom => new Rect2(0, 0, Size.X, Mathf.Max(2f, Size.Y - pad)),
                Edge.Left => new Rect2(pad, 0, Mathf.Max(2f, Size.X - pad), Size.Y),
                Edge.Right => new Rect2(0, 0, Mathf.Max(2f, Size.X - pad), Size.Y),
                _ => new Rect2(0, pad, Size.X, Mathf.Max(2f, Size.Y - pad)),
            };
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventKey key && KitChrome.IsConfirmKey(key))
            {
                Collapsed = !Collapsed;
                AcceptEvent();
                return;
            }

            if (@event is InputEventMouseMotion mm)
            {
                bool next = HandleRect().HasPoint(mm.Position);
                if (next != _hoverHandle)
                {
                    _hoverHandle = next;
                    QueueRedraw();
                }
                return;
            }

            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb
                && HandleRect().HasPoint(mb.Position))
            {
                GrabFocus();
                Collapsed = !Collapsed;
                AcceptEvent();
            }
        }

        public override void _Draw()
        {
            if (Size.X <= 6 || Size.Y <= 6) return;

            Color ink = InkColor();
            int fs = UiSurface.FontSize(this);

            if (!_collapsed)
            {
                Rect2 body = BodyRect();

                // Frame + recessed inner well: two nested shapes, the structure every reference
                // panel is built from. The well is Recessed, so it takes the deeply sunken plate
                // shade rather than the raised one.
                DrawMaterial(body, ActiveShape);

                string genre = KitChrome.GenreOf(this);
                KitChrome.DrawPanelHeader(this, genre, body, _title, HeaderStyle,
                                          KitChrome.PanelHeaderShape(genre), BannerShade,
                                          TitleFontScale);
            }

            // ── the handle, drawn LAST so it sits over the panel edge it straddles ──
            Rect2 h = HandleRect();
            Color face = FaceColor();

            // Dark plate at the measured L=0.26 relative to the surface, so it stays the darkest
            // element whatever the skin does.
            Color plate = new Color(face.R * 0.30f, face.G * 0.30f, face.B * 0.32f, 1f);
            if (_hoverHandle)
            {
                Color info = UiSurface.Semantic(this, UiSurface.Role.Info);
                plate = new Color(Mathf.Lerp(plate.R, info.R, 0.32f),
                                  Mathf.Lerp(plate.G, info.G, 0.32f),
                                  Mathf.Lerp(plate.B, info.B, 0.32f), 1f);
            }
            DrawShape(h, KitShape.Round, plate, ink, Mathf.Max(1f, Geo.Rim * 0.6f * (fs / 14f)));
            KitChrome.DrawFocusRing(this, KitChrome.GenreOf(this), h, KitShape.Round, 0.8f);

            DrawChevron(h);
        }

        /// <summary>Pure white, per the measurement — this is the one glyph in the reference that
        /// is held at maximum contrast regardless of skin, because it is the only state the
        /// player must read at a glance.</summary>
        private void DrawChevron(Rect2 h)
        {
            var c = h.Position + h.Size * 0.5f;
            float r = Mathf.Min(h.Size.X, h.Size.Y) * 0.26f;
            float w = Mathf.Max(2f, r * 0.42f);
            var white = new Color(1, 1, 1, 1);

            // Points along the fold direction; flips when collapsed so the glyph states which
            // way the panel will move.
            bool pointsPositive = _edge is Edge.Top or Edge.Left ? !_collapsed : _collapsed;
            bool vertical = _edge is Edge.Top or Edge.Bottom;

            Vector2 a, b, d;
            if (vertical)
            {
                float dy = pointsPositive ? r : -r;
                a = c + new Vector2(-r, -dy * 0.6f);
                b = c + new Vector2(0, dy * 0.6f);
                d = c + new Vector2(r, -dy * 0.6f);
            }
            else
            {
                float dx = pointsPositive ? r : -r;
                a = c + new Vector2(-dx * 0.6f, -r);
                b = c + new Vector2(dx * 0.6f, 0);
                d = c + new Vector2(-dx * 0.6f, r);
            }
            DrawLine(a, b, white, w);
            DrawLine(b, d, white, w);
        }
    }
}

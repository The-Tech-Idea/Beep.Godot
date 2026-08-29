using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A star rating — CATALOGUE-FROM-ART.md F.2 (`StarRating`), and the score readout every
    /// level-complete and level-select screen in the puzzle/platformer families uses.
    ///
    /// The framework already ships star art in `level_complete`, `level_results` and
    /// `level_select`, drawn per scene; this is the widget those screens should share so three
    /// stars mean the same thing and are lit the same way everywhere.
    ///
    /// An unearned star DRAINS SATURATION rather than vanishing (the 7x settled rule): the
    /// player must be able to see how many stars a level HAS, not just how many they earned, or
    /// the readout says nothing about what is left to do.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitStarRating : Godot.Range
    {
        [Export]
        public bool AutoInputDefaults
        {
            get => _autoInputDefaults;
            set { if (_autoInputDefaults == value) return; _autoInputDefaults = value; }
        }
        private bool _autoInputDefaults = true;

        /// <summary>A chip: takes the theme's chip corner, which the references vary
        /// independently of the button corner.</summary>
        private const KitWidgetClass Class = KitWidgetClass.Chip;

        /// <summary>How many stars there are. This is Range's MaxValue — a star rating is a
        /// value within a range, which is exactly what Range models, so it gets Range's
        /// MinValue/MaxValue/Step/Value and its ValueChanged signal instead of a private pair of
        /// ints nothing else can read.</summary>
        [Export(PropertyHint.Range, "1,10,1")]
        public int Total
        {
            get => Mathf.Clamp((int)MaxValue, 1, 10);
            set
            {
                int next = Mathf.Clamp(value, 1, 10);
                if (Mathf.IsEqualApprox((float)MaxValue, next)) return;
                _totalExplicitlySet = true;
                MaxValue = next;
                Value = Mathf.Clamp(Value, MinValue, MaxValue);
                RefreshMinimumAndRedraw();
            }
        }

        /// <summary>How many are filled. This is Range's Value.</summary>
        [Export(PropertyHint.Range, "0,10,1")]
        public int Earned
        {
            get => (int)Value;
            set
            {
                int next = Mathf.Clamp(value, 0, (int)MaxValue);
                if (Mathf.IsEqualApprox((float)Value, next)) return;
                Value = next;
                RefreshVisualAndRedraw();
            }
        }

        private string _genre = "";
        private KitGeometry Geo => KitGeometry.ForGenre(_genre);
        private int _hover = -1;
        private bool _eventsHooked;
        private bool _totalExplicitlySet;

        [Export]
        public UiSurface.Role Role
        {
            get => _role;
            set { if (_role == value) return; _role = value; RefreshVisualAndRedraw(); }
        }
        private UiSurface.Role _role = UiSurface.Role.Warning;

        [Export]
        public bool Editable
        {
            get => _editable;
            set
            {
                if (_editable == value) return;
                _editable = value;
                if (!_editable) ClearHover();
                RefreshVisualAndRedraw();
            }
        }
        private bool _editable = true;

        public override void _Ready()
        {
            base._Ready();
            RefreshGenre();
            KitChrome.ApplyInputDefaults(this, AutoInputDefaults, MouseFilterEnum.Stop, FocusModeEnum.All);
            // Range has NO theme art of its own -- no stylebox, no icon -- so unlike Slider and
            // ProgressBar there is nothing to blank and nothing whose minimum size vanishes with
            // it. That is what makes it the right base here rather than a convenient one.
            MinValue = 0; Step = 1;
            if (!_totalExplicitlySet && Mathf.IsEqualApprox((float)MaxValue, 100f))
                MaxValue = 5;
            else
                MaxValue = Mathf.Clamp((float)MaxValue, 1f, 10f);
            if (Value < MinValue) Value = MinValue;
            if (Value > MaxValue) Value = MaxValue;
            if (!_eventsHooked)
            {
                ValueChanged += _ => QueueRedraw();
                MouseExited += ClearHover;
                _eventsHooked = true;
            }
            ApplyInitialMinimumSize();
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (KitChrome.ShouldClearPointerState(this, what))
                ClearHover();
            if (what == NotificationThemeChanged)
            {
                RefreshGenre();
                RefreshMinimumAndRedraw();
            }
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (!Editable)
            {
                ClearHover();
                return;
            }

            if (@event is InputEventKey key)
            {
                Vector2I dir = KitChrome.DirectionFromKey(key);
                if (dir.X <= -9999) { Earned = 0; AcceptEvent(); }
                else if (dir.X >= 9999) { Earned = Total; AcceptEvent(); }
                else if (dir.X < 0) { Earned = Mathf.Max(0, Earned - 1); AcceptEvent(); }
                else if (dir.X > 0) { Earned = Mathf.Min(Total, Earned + 1); AcceptEvent(); }
                return;
            }

            if (@event is InputEventMouseMotion mm)
            {
                int next = HitStar(mm.Position);
                if (next != _hover)
                {
                    _hover = next;
                    QueueRedraw();
                }
                return;
            }

            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
            {
                int hit = HitStar(mb.Position);
                if (hit < 0) return;
                GrabFocus();
                Earned = hit + 1;
                AcceptEvent();
            }
        }

        private void ClearHover()
        {
            if (_hover < 0) return;
            _hover = -1;
            QueueRedraw();
        }

        private int HitStar(Vector2 p)
        {
            int total = Total;
            if (total <= 0 || Size.X <= 1f) return -1;
            float width = Mathf.Max(Size.X, 1f);
            int i = Mathf.FloorToInt(p.X / (width / total));
            return i >= 0 && i < total && p.Y >= 0f && p.Y <= Size.Y ? i : -1;
        }

        private void ApplyInitialMinimumSize()
        {
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
        }

        private void RefreshMinimumAndRedraw()
        {
            KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
            UpdateMinimumSize();
            QueueRedraw();
        }

        private void RefreshVisualAndRedraw()
        {
            QueueRedraw();
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            return new Vector2(fs * 1.9f * Total, fs * 2f);
        }

        private void RefreshGenre()
        {
            _genre = KitChrome.GenreOf(this);
        }

        public override void _Draw()
        {
            if (Size.X < 8f || Size.Y < 6f) return;

            KitState state = Editable ? KitState.Normal : KitState.Disabled;
            Color lit = KitChrome.StateFace(UiSurface.SemanticOrDerived(this, Role), state);
            float l = UiSurface.Luminance(lit);
            // Unearned: same colour, saturation drained. Not hidden, not a different hue.
            Color dim = new(Mathf.Lerp(lit.R, l, 0.92f) * 0.6f,
                            Mathf.Lerp(lit.G, l, 0.92f) * 0.6f,
                            Mathf.Lerp(lit.B, l, 0.92f) * 0.6f, 1f);
            Color ink = KitChrome.StateFace(UiSurface.Ink(UiSurface.Of(this)), state);

            float pitch = Size.X / Total;
            float r = Mathf.Min(pitch, Size.Y) * 0.42f;

            for (int i = 0; i < Total; i++)
            {
                var c = new Vector2(pitch * (i + 0.5f), Size.Y * 0.5f);
                // Earned stars sit slightly higher — the reference screens lift them so the row
                // reads even in a thumbnail.
                if (i < Earned) c.Y -= Size.Y * 0.06f;
                if (i == _hover) c.Y -= Size.Y * 0.04f;
                DrawStar(c, r, i < Earned ? lit : dim, ink);
                if (Editable && i == _hover)
                    DrawArc(c, r * 1.08f, 0f, Mathf.Tau, 24,
                            UiSurface.SemanticOrDerived(this, UiSurface.Role.Info), Mathf.Max(1.2f, r * 0.08f));
            }

            if (Editable)
                KitChrome.DrawFocusRing(this, _genre, new Rect2(Vector2.Zero, Size),
                                        KitMaterial.WidgetShapeForGenre(_genre, Class), 0.8f);
        }

        private void DrawStar(Vector2 c, float r, Color fill, Color ink)
        {
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float rad = (i % 2 == 0) ? r : r * 0.44f;
                float ang = -Mathf.Pi * 0.5f + i * Mathf.Pi / 5f;
                pts[i] = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
            }
            DrawColoredPolygon(pts, fill);
            var closed = new Vector2[11];
            pts.CopyTo(closed, 0);
            closed[10] = pts[0];
            DrawPolyline(closed, ink, Mathf.Max(1.5f, r * 0.12f));
        }
    }
}

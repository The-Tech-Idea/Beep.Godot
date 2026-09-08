using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// Fixed-square world/level selector button with lock and star state.
    /// Based on the repeated level-node buttons in Example_Art/gameui4.png and mobile UI sheets.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitLevelButton : KitControl
    {
        protected override KitWidgetClass WidgetClass => KitWidgetClass.Button;

        [Export] public string LevelText { get => _levelText; set { SetText(ref _levelText, value); } }
        private string _levelText = "1";

        [Export(PropertyHint.Range, "0,3,1")] public int Stars { get => _stars; set { int next = Mathf.Clamp(value, 0, 3); if (_stars == next) return; _stars = next; RefreshContentAndRedraw(); } }
        private int _stars = 3;

        [Export] public bool Locked { get => _locked; set { if (_locked == value) return; _locked = value; SetState(value ? KitState.Locked : KitState.Normal); RefreshContentAndRedraw(); } }
        private bool _locked;

        [Export] public UiSurface.Role Accent { get => _accent; set { if (_accent == value) return; _accent = value; RefreshContentAndRedraw(); } }
        private UiSurface.Role _accent = UiSurface.Role.Warning;
        private bool _eventsHooked;

        [Signal] public delegate void PressedEventHandler();

        public override void _Ready()
        {
            base._Ready();
            ApplyInputDefaults(MouseFilterEnum.Stop, FocusModeEnum.All);
            if (!_eventsHooked)
            {
                MouseEntered += () => { if (!_locked) SetState(KitState.Hover); };
                MouseExited += () => { if (!_locked) SetState(KitState.Normal); };
                _eventsHooked = true;
            }
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (KitChrome.ShouldClearPointerState(this, what))
                ClearPointerState();
        }

        private void SetText(ref string target, string? value)
        {
            string next = value ?? "";
            if (target == next) return;
            target = next;
            RefreshContentAndRedraw();
        }

        private void RefreshContentAndRedraw()
        {
            KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
            UpdateMinimumSize();
            QueueRedraw();
        }

        private void ClearPointerState()
        {
            if (_locked || State == KitState.Normal) return;
            SetState(KitState.Normal);
        }

        public override void _GuiInput(InputEvent @event)
        {
            KitChrome.ActivateOnClickOrConfirm(this, @event,
                () => EmitSignal(SignalName.Pressed),
                interactive: !_locked);
        }

        public override Vector2 _GetMinimumSize()
        {
            float s = Mathf.Clamp(UiSurface.FontSize(this) * 3.65f, 46f, 68f);
            return new Vector2(s, s);
        }

        public override void _Draw()
        {
            if (Size.X <= 8 || Size.Y <= 8) return;

            int fs = UiSurface.FontSize(this);
            float rim = Mathf.Max(1.5f, Geo.Rim * (fs / 14f));
            Rect2 body = new(0, 0, Size.X, Size.Y * 0.82f);
            Color face = _locked ? Desaturate(FaceColor(), 0.90f) : UiSurface.Semantic(this, _accent);
            if (face.A < 0.02f) face = FaceColor();

            DrawPlate(body, ActiveShape, face, RimColor(), rim);
            KitChrome.DrawFocusRing(this, Genre, body, ActiveShape, 0.8f);

            Font? font = KitFont();
            if (font != null)
            {
                string text = KitCase(_locked ? "LOCK" : _levelText);
                Rect2 textBox = body.Grow(-Mathf.Clamp(Size.X * 0.16f, 6f, 12f));
                float textWidth = textBox.Size.X * 0.66f;
                int tf = UiSurface.FitRole(this, _locked ? UiSurface.TextRole.Small : UiSurface.TextRole.Value,
                                           new Vector2(textWidth, textBox.Size.Y * 0.62f), text, font, min: 8);
                text = KitChrome.EllipsizeText(font, text, tf, textWidth);
                if (string.IsNullOrEmpty(text)) return;
                Vector2 m = font.GetStringSize(text, HorizontalAlignment.Left, -1, tf);
                Color ink = UiSurface.Luminance(face) > 0.52f ? new Color(0.10f, 0.08f, 0.06f) : new Color(0.98f, 0.96f, 0.92f);
                DrawText(font, textBox.Position + new Vector2((textBox.Size.X - m.X) * 0.5f, (textBox.Size.Y + m.Y * 0.62f) * 0.5f), text, tf, ink);
            }

            DrawStars();
        }

        /// <summary>
        /// The earned score, as stars.
        ///
        /// It was `new string('*', _stars)` typed through the theme font: three asterisks, which a
        /// text face renders as small raised marks near the cap height and a pixel face renders as
        /// a 3x3 blob. It also dropped the kit's own drained-not-hidden rule -- unearned stars were
        /// simply absent, so a one-star level and a three-star level differed by a gap rather than
        /// by two dim stars, and a player could not see what a level was worth.
        ///
        /// Drawn through the shared star, so this widget, KitStarRating and KitLevelPath agree
        /// about what a star is. Outside the font branch as well, because it does not need one --
        /// it used to sit after an early return that skipped the stars whenever the level's own
        /// label ellipsized away to nothing.
        /// </summary>
        private void DrawStars()
        {
            if (_locked || Size.X < 12f || Size.Y < 12f) return;

            Color lit = UiSurface.Semantic(this, UiSurface.Role.Warning);
            float l = UiSurface.Luminance(lit);
            Color dim = new(Mathf.Lerp(lit.R, l, 0.9f) * 0.6f, Mathf.Lerp(lit.G, l, 0.9f) * 0.6f,
                            Mathf.Lerp(lit.B, l, 0.9f) * 0.6f, 1f);
            Color ink = InkColor();

            // The plate stops at 82% of the height and the remaining band exists for exactly this,
            // so the stars are centred in that band rather than laid over the plate's own rim and
            // (on a genre with artwork) its raised lip, where they read as a rendering fault.
            const float PlateBottom = 0.82f;
            float band = Size.Y * (1f - PlateBottom);
            float r = Mathf.Min(Size.X * 0.10f, band * 0.42f);
            float y = Size.Y * PlateBottom + band * 0.5f;
            for (int i = 0; i < 3; i++)
                KitChrome.DrawStar(this, new Vector2(Size.X * 0.5f + (i - 1) * r * 2.4f, y), r,
                                   i < _stars ? lit : dim, ink);
        }

        private static Color Desaturate(Color c, float amount)
        {
            float l = UiSurface.Luminance(c);
            return new Color(Mathf.Lerp(c.R, l, amount), Mathf.Lerp(c.G, l, amount), Mathf.Lerp(c.B, l, amount), c.A);
        }
    }
}

using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A loading indicator — CATALOGUE-FROM-ART.md F.2's `LoadingIndicator`.
    ///
    /// Three forms, because the references use different ones in different places and they are
    /// not interchangeable: a <b>ring</b> for a wait of unknown length, <b>dots</b> for an
    /// inline "working" cue inside a row or button, and a <b>bar</b> for a wait whose progress is
    /// actually known. Using the ring for a known-length wait throws away information the player
    /// could have had.
    ///
    /// Animation runs off <see cref="Node.GetProcessDeltaTime"/> accumulated locally rather than
    /// a Tween, so the widget spins correctly when the SceneTree is PAUSED — a loading indicator
    /// that freezes during a pause is the one moment it must not.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitSpinner : KitControl
    {
        public enum SpinnerKind { Ring, Dots, Bar }

        [Export] public SpinnerKind Kind { get => _kind; set { if (_kind == value) return; _kind = value; UpdateProcessing(); RefreshMinimumAndRedraw(); } }
        private SpinnerKind _kind = SpinnerKind.Ring;

        [Export]
        public UiSurface.Role Role
        {
            get => _role;
            set { if (_role == value) return; _role = value; RefreshVisualAndRedraw(); }
        }
        private UiSurface.Role _role = UiSurface.Role.Accent;

        /// <summary>Known progress 0..1 for <see cref="SpinnerKind.Bar"/>. Negative = unknown,
        /// which makes the bar sweep instead of fill.</summary>
        [Export(PropertyHint.Range, "-1.0,1.0,0.01")]
        public float Progress
        {
            get => _progress;
            set
            {
                float next = Mathf.Clamp(value, -1f, 1f);
                if (Mathf.IsEqualApprox(_progress, next)) return;
                _progress = next;
                UpdateProcessing();
                RefreshVisualAndRedraw();
            }
        }
        private float _progress = -1f;

        [Export(PropertyHint.Range, "0.1,4.0,0.05")]
        public float Speed
        {
            get => _speed;
            set
            {
                float next = Mathf.Clamp(value, 0.1f, 4f);
                if (Mathf.IsEqualApprox(_speed, next)) return;
                _speed = next;
                RefreshVisualAndRedraw();
            }
        }
        private float _speed = 1.1f;

        private float _t;

        public override void _Ready()
        {
            base._Ready();
            ProcessMode = ProcessModeEnum.Always;   // must keep moving while the tree is paused
            ApplyInputDefaults(MouseFilterEnum.Ignore);
            UpdateProcessing();
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            return Kind == SpinnerKind.Bar
                ? new Vector2(fs * 10f, fs * 0.9f)
                : new Vector2(fs * 3f, fs * 3f);
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what == NotificationVisibilityChanged)
                UpdateProcessing();
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

        private void UpdateProcessing()
        {
            SetProcess(IsVisibleInTree() && ShouldAnimate());
        }

        private bool ShouldAnimate()
            => Kind != SpinnerKind.Bar || Progress < 0f;

        public override void _Process(double delta)
        {
            _t += (float)delta * Speed;
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (Size.X < 6f || Size.Y < 4f) return;
            Color c = UiSurface.Semantic(this, Role);
            Color track = new(c.R * 0.26f, c.G * 0.26f, c.B * 0.30f, 1f);

            switch (Kind)
            {
                case SpinnerKind.Dots: Dots(c); break;
                case SpinnerKind.Bar: Bar(c, track); break;
                default: Ring(c, track); break;
            }
        }

        private void Ring(Color c, Color track)
        {
            float d = Mathf.Min(Size.X, Size.Y);
            var ctr = Size * 0.5f;
            float outer = d * 0.46f;
            float r = d * 0.34f;
            float w = Mathf.Max(2.5f, r * 0.30f);
            DrawCircle(ctr, outer, new Color(track.R, track.G, track.B, 0.26f));
            DrawArc(ctr, outer, 0f, Mathf.Tau, 40, InkColor() with { A = 0.42f }, Mathf.Max(1f, w * 0.45f));
            DrawArc(ctr, r, 0f, Mathf.Tau, 48, track, w);
            float start = _t * Mathf.Tau;
            DrawArc(ctr, r, start, start + Mathf.Tau * 0.30f, 24, c, w);
            DrawCircle(ctr, Mathf.Max(2f, w * 0.55f), c with { A = 0.82f });
        }

        private void Dots(Color c)
        {
            const int n = 3;
            float pitch = Size.X / n;
            float r = Mathf.Min(pitch, Size.Y) * 0.22f;
            for (int i = 0; i < n; i++)
            {
                // Each dot leads the next by a third of a cycle.
                float phase = _t * Mathf.Tau - i * (Mathf.Tau / n);
                float lift = (Mathf.Sin(phase) + 1f) * 0.5f;
                var p = new Vector2(pitch * (i + 0.5f), Size.Y * 0.5f - lift * Size.Y * 0.16f);
                DrawCircle(p + new Vector2(0f, r * 0.45f), r * (0.85f + lift * 0.25f), InkColor() with { A = 0.24f });
                DrawCircle(p, r * (0.75f + lift * 0.35f), c with { A = 0.55f + lift * 0.45f });
                DrawCircle(p + new Vector2(-r * 0.25f, -r * 0.25f), r * 0.25f, new Color(1, 1, 1, 0.22f));
            }
        }

        private void Bar(Color c, Color track)
        {
            var r = new Rect2(Vector2.Zero, Size);
            DrawShape(r, KitShape.Pill, track, InkColor(), 0f);
            DrawLine(new Vector2(r.Position.X + Size.Y * 0.45f, r.Position.Y + Size.Y * 0.28f),
                     new Vector2(r.End.X - Size.Y * 0.45f, r.Position.Y + Size.Y * 0.28f),
                     new Color(1, 1, 1, 0.10f), Mathf.Max(1f, Size.Y * 0.12f));
            if (Progress >= 0f)
            {
                var f = new Rect2(0f, 0f, Size.X * Mathf.Clamp(Progress, 0f, 1f), Size.Y);
                if (f.Size.X > 1f) DrawShape(f, KitShape.Pill, c, InkColor(), 0f);
                return;
            }
            // Unknown length: a sweeping block, so it cannot be mistaken for real progress.
            float w = Size.X * 0.3f;
            float x = (Mathf.Sin(_t * 2f) * 0.5f + 0.5f) * (Size.X - w);
            DrawShape(new Rect2(x, 0f, w, Size.Y), KitShape.Pill, c, InkColor(), 0f);
            DrawLine(new Vector2(x + w * 0.20f, Size.Y * 0.28f),
                     new Vector2(x + w * 0.80f, Size.Y * 0.28f),
                     new Color(1, 1, 1, 0.18f), Mathf.Max(1f, Size.Y * 0.10f));
        }
    }
}

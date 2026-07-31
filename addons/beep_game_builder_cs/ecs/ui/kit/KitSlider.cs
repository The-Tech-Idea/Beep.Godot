using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A slider with a chunky bar knob — CATALOGUE-FROM-ART.md sections C, D and E all list one,
    /// and `settings1.png` specifies the game form's version: a <b>vertical bar knob</b>, not the
    /// circular grabber a desktop toolkit draws.
    ///
    /// Two rules carried over from the theme engine, each a defect already paid for:
    ///  - the <b>track is a dark tint of the fill's own hue</b>, never a neutral grey (4x rule);
    ///  - the knob does not change HUE on focus. Stage 28 found the settings slider rendering
    ///    green while the two beneath it stayed blue, because grabber and grabber_highlight came
    ///    from different palette roles. Here the highlight is a LIGHTENED fill, same hue.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitSlider : HSlider
    {
        [Export] public UiSurface.Role Fill { get; set; } = UiSurface.Role.Accent;

        private string _genre = "";
        private KitGeometry Geo => KitGeometry.ForGenre(_genre);
        private bool _suppressing;

        /// <summary>Grab state for the pressed sculpt. Slider emits DragStarted/DragEnded; there
        /// is no public 'is being dragged' getter, so it is tracked here rather than guessed at.</summary>
        private bool _dragging;

        public override void _Ready()
        {
            _genre = KitChrome.GenreOf(this);
            // Kept 0..1 so every existing `Value = 0.62f` still means "62%". Range gives real
            // MinValue/MaxValue/Step to anyone who wants a different domain.
            MinValue = 0.0;
            MaxValue = 1.0;
            Step = 0.001;
            Suppress();
            DragStarted += () => { _dragging = true; QueueRedraw(); };
            DragEnded += _ => { _dragging = false; QueueRedraw(); };
            ValueChanged += _ => QueueRedraw();
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what != NotificationThemeChanged) return;
            _genre = KitChrome.GenreOf(this);
            Suppress();
            QueueRedraw();
        }

        /// <summary>Slider's grabber is an ICON, not a StyleBox — blanking only the styleboxes
        /// leaves Godot's knob drawn on top of ours. And blanking BOTH collapses the control's
        /// minimum size to about a pixel, whereupon _Draw hits its own size guard and the slider
        /// vanishes silently. Anything that blanks a control's theme art must restate the size
        /// that art was providing.</summary>
        private void Suppress()
        {
            if (_suppressing) return;
            _suppressing = true;
            foreach (string sb in new[] { "slider", "grabber_area", "grabber_area_highlight" })
                AddThemeStyleboxOverride(sb, new StyleBoxEmpty());
            foreach (string ic in new[] { "grabber", "grabber_highlight", "grabber_disabled", "tick" })
                AddThemeIconOverride(ic, KitChrome.Blank);
            int fs = UiSurface.FontSize(this);
            CustomMinimumSize = new Vector2(Mathf.Max(CustomMinimumSize.X, fs * 10f),
                                            Mathf.Max(fs * 1.9f, 22f));
            _suppressing = false;
        }

        private float KnobW => Mathf.Max(6f, Size.Y * 0.38f);

        public override void _Draw()
        {
            if (Size.X <= 6 || Size.Y <= 4) return;

            var g = Geo;
            Color fill = UiSurface.Semantic(this, Fill);
            Color ink = UiSurface.Ink(UiSurface.Of(this));
            int fs = UiSurface.FontSize(this);
            float rimPx = Mathf.Max(1f, g.Rim * 0.6f * (fs / 14f));

            // Track: the fill's own hue driven dark. A grey track is the clearest tell of a
            // themed form rather than a game.
            float th = Mathf.Max(4f, Size.Y * 0.34f);
            var track = new Rect2(0f, (Size.Y - th) * 0.5f, Size.X, th);
            KitChrome.DrawShape(this, _genre, track, KitShape.Pill,
                      new Color(fill.R * 0.26f, fill.G * 0.26f, fill.B * 0.30f, 1f), ink, rimPx);

            float half = KnobW * 0.5f;
            float span = Mathf.Max(1f, Size.X - KnobW);
            float kx = half + span * (float)Value;

            if (kx - half > 1f)
            {
                var done = new Rect2(track.Position, new Vector2(kx, track.Size.Y));
                KitChrome.DrawShape(this, _genre, done, KitShape.Pill, fill, ink, 0f);
            }

            // The bar knob: a chunky vertical plate, the game form's grabber.
            var knob = new Rect2(kx - half, 0f, KnobW, Size.Y);
            // Slider tracks its own grab state, so the pressed sculpt comes from Godot rather
            // than from a KitControl field this class no longer has.
            Color kc = _dragging
                ? new Color(Mathf.Lerp(fill.R, 1f, 0.28f), Mathf.Lerp(fill.G, 1f, 0.28f),
                            Mathf.Lerp(fill.B, 1f, 0.28f), 1f)   // lightened, SAME hue
                : fill;
            KitChrome.DrawShape(this, _genre, knob, KitChrome.Shape(_genre), kc, ink, Mathf.Max(1.5f, rimPx));
        }
    }
}

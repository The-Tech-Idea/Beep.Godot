using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// `&lt; Option &gt;` — the game form's replacement for a dropdown.
    ///
    /// CATALOGUE-FROM-ART.md section D lists `ArrowSelector` from `settings1.png`, and records a
    /// correction worth keeping: <b>dropdowns appear in NONE of the 43 reference images</b>. Game
    /// UIs page through options with arrows instead, because a dropdown needs a popup layer, a
    /// pointer, and a list that does not fit a controller. This is the widget a settings screen
    /// actually wants for resolution, language and difficulty.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitArrowSelector : KitControl
    {
        public readonly List<string> Options = new();

        [Export] public int Current
        {
            get => _current;
            set
            {
                if (Options.Count == 0) { _current = 0; return; }
                int v = Mathf.PosMod(value, Options.Count);
                if (v == _current) return;
                _current = v; QueueRedraw(); EmitSignal(SignalName.OptionChanged, v);
            }
        }
        private int _current;

        /// <summary>Stop at the ends instead of cycling. Off by default: the references page
        /// round, which avoids a dead-looking arrow at either end.</summary>
        [Export] public bool Clamp { get; set; }

        [Signal] public delegate void OptionChangedEventHandler(int index);

        public override void _Ready()
        {
            base._Ready();
            if (Options.Count == 0) Options.AddRange(new[] { "Low", "Medium", "High" });
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                CustomMinimumSize = new Vector2(fs * 9f, fs * 2.1f);
            }
        }

        private float ArrowW => Mathf.Max(14f, Size.Y * 0.8f);

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
                return;
            if (mb.Position.X < ArrowW) Step(-1);
            else if (mb.Position.X > Size.X - ArrowW) Step(1);
            else return;
            AcceptEvent();
        }

        private void Step(int d)
        {
            if (Options.Count == 0) return;
            int next = _current + d;
            if (Clamp) next = Mathf.Clamp(next, 0, Options.Count - 1);
            Current = next;
        }

        private bool CanStep(int d)
            => !Clamp || (_current + d >= 0 && _current + d < Options.Count);

        public override void _Draw()
        {
            if (Size.X < 12f || Size.Y < 6f) return;

            var g = Geo;
            Color face = FaceColor();
            Color ink = InkColor();
            var font = GetThemeDefaultFont();
            int fs = UiSurface.FontSize(this);
            float rimPx = Mathf.Max(1f, g.Rim * 0.7f * (fs / 14f));

            // The value plate is recessed — it is a readout, not a button.
            var r = new Rect2(Vector2.Zero, Size);
            float ps = g.WellShade;
            DrawShape(r, ActiveShape, new Color(face.R * ps, face.G * ps, face.B * ps, 1f), ink, rimPx);

            float aw = ArrowW;
            DrawArrow(new Rect2(0f, 0f, aw, Size.Y), -1, ink, CanStep(-1));
            DrawArrow(new Rect2(Size.X - aw, 0f, aw, Size.Y), 1, ink, CanStep(1));

            if (font == null || Options.Count == 0) return;
            string txt = Options[Mathf.Clamp(_current, 0, Options.Count - 1)];
            Vector2 m = font.GetStringSize(txt, HorizontalAlignment.Left, -1, fs);
            DrawString(font,
                       new Vector2((Size.X - m.X) * 0.5f, (Size.Y + m.Y * 0.6f) * 0.5f),
                       txt, HorizontalAlignment.Left, -1, fs, UiSurface.Text(this));
        }

        /// <summary>An arrow that cannot be taken drains saturation rather than disappearing —
        /// a missing control is harder to read than a muted one.</summary>
        private void DrawArrow(Rect2 box, int dir, Color ink, bool enabled)
        {
            var c = box.Position + box.Size * 0.5f;
            float a = Mathf.Min(box.Size.X, box.Size.Y) * 0.22f;
            float w = Mathf.Max(2f, a * 0.5f);
            Color col = UiSurface.Text(this);
            if (!enabled) col = col with { A = 0.28f };
            var tip = c + new Vector2(a * dir, 0f);
            DrawLine(c + new Vector2(-a * dir, -a), tip, col, w);
            DrawLine(c + new Vector2(-a * dir, a), tip, col, w);
        }
    }
}

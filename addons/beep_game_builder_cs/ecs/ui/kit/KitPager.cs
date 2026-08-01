using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// Page controls — CATALOGUE-FROM-ART.md section C's `PagerArrow`, plus the correction
    /// `ui8.md` records: "<b>Add jump-to-end pagers alongside step pagers</b>", and its note that
    /// "step and jump paging can be separate control pairs".
    ///
    /// So this is not one arrow: it is the pair (or two pairs), with a page indicator between
    /// them. Dots are used up to <see cref="MaxDots"/> and a "3 / 12" readout beyond that, which
    /// is what the references do rather than drawing forty dots.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitPager : KitControl
    {
        [Export(PropertyHint.Range, "1,999,1")]
        public int PageCount { get => _count; set { _count = Mathf.Max(1, value); QueueRedraw(); } }
        private int _count = 5;

        [Export] public int Page
        {
            get => _page;
            set
            {
                int v = Mathf.Clamp(value, 0, _count - 1);
                if (v == _page) return;
                _page = v; QueueRedraw(); EmitSignal(SignalName.PageChanged, v);
            }
        }
        private int _page;

        /// <summary>Show the outer jump-to-end pair as well as the step pair.</summary>
        [Export] public bool ShowJump { get; set; } = true;

        /// <summary>Beyond this many pages, dots become a "n / total" readout.</summary>
        [Export(PropertyHint.Range, "3,20,1")] public int MaxDots { get; set; } = 8;

        [Signal] public delegate void PageChangedEventHandler(int page);

        public override void _Ready()
        {
            base._Ready();
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                CustomMinimumSize = new Vector2(fs * (ShowJump ? 12f : 9f), fs * 2.2f);
            }
        }

        private float BtnW => Mathf.Max(16f, Size.Y * 0.9f);

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
                return;
            float w = BtnW;
            float x = mb.Position.X;
            if (ShowJump && x < w) Page = 0;
            else if (x < w * (ShowJump ? 2f : 1f)) Page = _page - 1;
            else if (ShowJump && x > Size.X - w) Page = _count - 1;
            else if (x > Size.X - w * (ShowJump ? 2f : 1f)) Page = _page + 1;
            else return;
            AcceptEvent();
        }

        public override void _Draw()
        {
            if (Size.X < 40f || Size.Y < 10f) return;

            Color ink = InkColor();
            var font = KitFont();
            int fs = UiSurface.FontSize(this);
            float w = BtnW;
            float inner = ShowJump ? w * 2f : w;

            if (ShowJump)
            {
                Arrow(new Rect2(0f, 0f, w, Size.Y), -1, true, _page > 0);
                Arrow(new Rect2(Size.X - w, 0f, w, Size.Y), 1, true, _page < _count - 1);
            }
            Arrow(new Rect2(ShowJump ? w : 0f, 0f, w, Size.Y), -1, false, _page > 0);
            Arrow(new Rect2(Size.X - inner, 0f, w, Size.Y), 1, false, _page < _count - 1);

            var mid = new Rect2(inner, 0f, Size.X - inner * 2f, Size.Y);
            if (mid.Size.X < 8f) return;

            if (_count <= MaxDots)
            {
                float pitch = mid.Size.X / _count;
                float r = Mathf.Min(pitch, Size.Y) * 0.16f;
                for (int i = 0; i < _count; i++)
                {
                    var c = new Vector2(mid.Position.X + pitch * (i + 0.5f), Size.Y * 0.5f);
                    if (i == _page) DrawCircle(c, r * 1.5f, UiSurface.Semantic(this, UiSurface.Role.Accent));
                    else DrawCircle(c, r, new Color(ink.R, ink.G, ink.B, 0.55f));
                }
            }
            else if (font != null)
            {
                string t = $"{_page + 1} / {_count}";
                Vector2 m = font.GetStringSize(t, HorizontalAlignment.Left, -1, fs);
                DrawText(font, new Vector2(mid.Position.X + (mid.Size.X - m.X) * 0.5f, (Size.Y + m.Y * 0.6f) * 0.5f),
                           t, fs, UiSurface.Text(this));
            }
        }

        /// <summary>A jump arrow is a step arrow with a bar against it — the standard idiom, and
        /// it keeps the two pairs distinguishable at a glance.</summary>
        private void Arrow(Rect2 box, int dir, bool jump, bool enabled)
        {
            var c = box.Position + box.Size * 0.5f;
            float a = Mathf.Min(box.Size.X, box.Size.Y) * 0.20f;
            float w = Mathf.Max(2f, a * 0.5f);
            Color col = UiSurface.Text(this);
            if (!enabled) col = col with { A = 0.25f };
            var tip = c + new Vector2(a * dir, 0f);
            DrawLine(c + new Vector2(-a * dir, -a), tip, col, w);
            DrawLine(c + new Vector2(-a * dir, a), tip, col, w);
            if (jump)
                DrawLine(tip + new Vector2(w * dir, -a), tip + new Vector2(w * dir, a), col, w);
        }
    }
}

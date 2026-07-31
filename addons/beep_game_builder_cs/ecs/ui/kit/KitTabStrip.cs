using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A row of tabs, welded to the panel below them.
    ///
    /// CATALOGUE-FROM-ART.md section A ranks this in the top build tier — it "appears in nearly
    /// every picture". The art pass measured SEVENTEEN distinct selection mechanisms across the
    /// folder and concluded the choice follows widget CLASS, with a convention per class:
    /// <b>tab strips use fill and elevation</b> (gameui8: "a filled pill appears behind the tab";
    /// gameui9: "raise the selected tab"), while card carousels use an outline. So
    /// <see cref="Selection"/> offers exactly the tab-appropriate mechanisms rather than a
    /// generic "selected" look shared with every other widget.
    ///
    /// The selected tab is painted in the PANEL's colour so it welds to the content area, and
    /// carries no bottom border — the lesson Stage 28 paid for on the settings screen, where a
    /// generic surface box gave every tab a drop shadow that fell across its neighbours.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitTabStrip : KitControl
    {
        public enum SelectionStyle
        {
            /// <summary>Selected tab takes the panel colour and welds to it (gameui8).</summary>
            Weld,
            /// <summary>A filled pill appears behind the selected tab (gameui8).</summary>
            Pill,
            /// <summary>The selected tab is raised above the strip (gameui9).</summary>
            Elevate,
        }

        public sealed class Tab
        {
            public string Text = "Tab";
            public Texture2D? Icon;
            /// <summary>Corner flash badge — section A names this on the tab strip specifically.
            /// 0 hides it.</summary>
            public int Badge;
        }

        public readonly List<Tab> Tabs = new();

        [Export] public SelectionStyle Selection { get; set; } = SelectionStyle.Weld;

        [Export] public int Current
        {
            get => _current;
            set { if (_current == value) return; _current = value; QueueRedraw(); EmitSignal(SignalName.TabChanged, value); }
        }
        private int _current;

        [Signal] public delegate void TabChangedEventHandler(int index);

        public override void _Ready()
        {
            base._Ready();
            if (Tabs.Count == 0)
                Tabs.AddRange(new[] { new Tab { Text = "One" }, new Tab { Text = "Two" }, new Tab { Text = "Three" } });
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                CustomMinimumSize = new Vector2(fs * 5.5f * Tabs.Count, fs * 2.3f);
            }
        }

        private Rect2 TabRect(int i)
        {
            float w = Size.X / Mathf.Max(1, Tabs.Count);
            // 6px separation at 14pt — tabs are near-touching, so the gap is deliberate and small.
            float sep = Mathf.Max(2f, UiSurface.FontSize(this) * 0.42f) * 0.5f;
            float raise = Selection == SelectionStyle.Elevate && i == _current ? 0f : Size.Y * 0.12f;
            return new Rect2(i * w + sep, raise, w - sep * 2f, Size.Y - raise);
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
                return;
            for (int i = 0; i < Tabs.Count; i++)
            {
                if (!TabRect(i).HasPoint(mb.Position)) continue;
                Current = i;
                AcceptEvent();
                return;
            }
        }

        public override void _Draw()
        {
            if (Size.X <= 8 || Size.Y <= 6 || Tabs.Count == 0) return;

            var g = Geo;
            Color face = FaceColor();
            Color ink = InkColor();
            var font = KitFont();
            int fs = UiSurface.FontSize(this);
            float rimPx = Mathf.Max(1f, g.Rim * 0.7f * (fs / 14f));

            for (int i = 0; i < Tabs.Count; i++)
            {
                Rect2 r = TabRect(i);
                if (r.Size.X < 3f) continue;
                bool sel = i == _current;

                // Unselected is the pressed surface, selected the panel's own colour: the pair
                // must be clearly different, not two near-identical greys.
                Color plate = sel
                    ? face
                    : new Color(face.R * 0.72f, face.G * 0.72f, face.B * 0.76f, 1f);

                if (sel && Selection == SelectionStyle.Pill)
                {
                    // The pill sits BEHIND the tab and is the only accented element.
                    Color acc = UiSurface.Semantic(this, UiSurface.Role.Accent);
                    DrawShape(r, KitShape.Pill, acc, ink, rimPx);
                    plate = new Color(acc.R, acc.G, acc.B, 1f);
                }
                else
                {
                    DrawShape(r, ActiveShape, plate, sel ? RimColor() : ink, rimPx);
                }

                if (font != null && !string.IsNullOrEmpty(Tabs[i].Text))
                {
                    // An unselected tab is a place you CAN go: normal text at reduced alpha, not
                    // the disabled colour. Reading it as unavailable was a real Stage 28 defect.
                    Color txt = UiSurface.Text(this);
                    if (!sel) txt = txt with { A = 0.78f };
                    // A tab's width is the strip divided by the tab count, so a long title has
                    // to shrink to its own tab rather than run into the next one.
                    int tf = UiSurface.FitRole(this, UiSurface.TextRole.Body,
                                               new Vector2(r.Size.X * 0.86f, r.Size.Y * 0.62f),
                                               Tabs[i].Text, font);
                    Vector2 m = font.GetStringSize(Tabs[i].Text, HorizontalAlignment.Left, -1, tf);
                    DrawString(font,
                               new Vector2(r.Position.X + (r.Size.X - m.X) * 0.5f,
                                           r.Position.Y + (r.Size.Y + m.Y * 0.6f) * 0.5f),
                               Tabs[i].Text, HorizontalAlignment.Left, -1, tf, txt);
                }

                // Corner flash badge, straddling the tab's top-right — the attention anchor the
                // art pass measured eight independent times.
                if (Tabs[i].Badge > 0 && font != null)
                {
                    string b = Tabs[i].Badge.ToString();
                    int small = Mathf.Max(8, Mathf.RoundToInt(fs * 0.7f));
                    Vector2 m = font.GetStringSize(b, HorizontalAlignment.Left, -1, small);
                    float bw = Mathf.Max(m.X + small * 0.7f, small * 1.4f), bh = small * 1.2f;
                    var br = new Rect2(r.End.X - bw * 0.6f, r.Position.Y - bh * 0.35f, bw, bh);
                    DrawShape(br, KitShape.Pill, UiSurface.Semantic(this, UiSurface.Role.Danger), ink, 1.5f);
                    DrawString(font, new Vector2(br.Position.X + (br.Size.X - m.X) * 0.5f,
                                                 br.Position.Y + (br.Size.Y + m.Y * 0.6f) * 0.5f),
                               b, HorizontalAlignment.Left, -1, small, new Color(0.98f, 0.96f, 0.92f));
                }
            }

            DrawAttachments();
        }
    }
}

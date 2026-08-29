using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// An inventory / hotbar / recipe slot grid.
    ///
    /// Measured from Example_Art/gameui9.png, rpg3.png, gameui2.png and gameui8.png:
    ///
    ///  - <b>interior : pitch = 0.58</b> (gameui9: 49px interior on an ~85px pitch), so the gap
    ///    between slots is a real part of the design rather than a layout leftover.
    ///  - <b>Selection is a 3px pure-white rectangle drawn OUTSIDE the slot</b> (gameui9) — not
    ///    a fill change, not a glow. It survives greyscale and works over any slot contents,
    ///    which is exactly why that sheet uses it.
    ///  - <b>Empty slots desaturate rather than darken</b>: rpg3 measures an available slot at
    ///    L≈0.67-0.72 S=0.65-0.72 and an empty one at L=0.26 <b>S=0.05</b>. The settled 7x rule
    ///    is that unavailable drains SATURATION; lightness may even rise.
    ///  - <b>Locked states state their requirement in words</b> (5 references), so a locked slot
    ///    carries a reason string and not just a padlock glyph.
    ///  - Count badges sit at the <b>bottom-right corner, straddling it</b> (gameui8).
    ///
    /// KitState.Empty has three distinct meanings on one screen in ui3.png — blank, an invite
    /// "+", and locked-with-a-requirement — so <see cref="SlotKind"/> models all three rather
    /// than collapsing them into "not filled".
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitSlotGrid : KitControl
    {
        /// <summary>A slot: takes the theme's slot corner, which the
        /// references vary independently of the button corner.</summary>
        protected override KitWidgetClass WidgetClass => KitWidgetClass.Slot;

        public enum SlotKind { Filled, Blank, Invite, Locked }

        public sealed class Slot
        {
            public SlotKind Kind = SlotKind.Blank;
            public Texture2D? Icon;
            /// <summary>Stack count. Drawn as a corner badge when above 1.</summary>
            public int Count;
            /// <summary>Shown for <see cref="SlotKind.Locked"/> — in words, per the 5x rule.</summary>
            public string Requirement = "";
            /// <summary>Rarity/quality tint on the slot background (gameui8). Neutral = none.</summary>
            public UiSurface.Role Tint = UiSurface.Role.Neutral;
        }

        [Export(PropertyHint.Range, "1,12,1")]
        public int Columns
        {
            get => _cols;
            set
            {
                int next = Mathf.Max(1, value);
                if (_cols == next) return;
                _cols = next;
                NormalizeSelectionToGrid();
                RefreshMinimumAndRedraw();
            }
        }
        private int _cols = 4;

        [Export(PropertyHint.Range, "1,12,1")]
        public int Rows
        {
            get => _rows;
            set
            {
                int next = Mathf.Max(1, value);
                if (_rows == next) return;
                _rows = next;
                NormalizeSelectionToGrid();
                RefreshMinimumAndRedraw();
            }
        }
        private int _rows = 3;

        /// <summary>Selected index, or -1. Drawn as an outline OUTSIDE the slot.</summary>
        [Export] public int Selected
        {
            get => _sel;
            set
            {
                int next = Mathf.Clamp(value, -1, TotalSlots - 1);
                if (_sel == next) return;
                _sel = next;
                RefreshVisualAndRedraw();
            }
        }
        private int _sel = -1;

        /// <summary>Interior as a fraction of pitch. 0.58 measured; lower widens the gutters.</summary>
        [Export(PropertyHint.Range, "0.3,1.0,0.01")]
        public float InteriorRatio
        {
            get => _interiorRatio;
            set
            {
                float next = Mathf.Clamp(value, 0.3f, 1f);
                if (Mathf.IsEqualApprox(_interiorRatio, next)) return;
                _interiorRatio = next;
                RefreshVisualAndRedraw();
            }
        }
        private float _interiorRatio = 0.58f;

        public readonly List<Slot> Slots = new();
        private int _hover = -1;

        [Export]
        public int[] SlotKinds
        {
            get
            {
                var kinds = new int[Slots.Count];
                for (int i = 0; i < Slots.Count; i++)
                    kinds[i] = (int)Slots[i].Kind;
                return kinds;
            }
            set => SetSlotKinds(value);
        }

        [Export]
        public int[] SlotCounts
        {
            get
            {
                var counts = new int[Slots.Count];
                for (int i = 0; i < Slots.Count; i++)
                    counts[i] = Slots[i].Count;
                return counts;
            }
            set => SetSlotCounts(value);
        }

        [Export]
        public Texture2D[] SlotIcons
        {
            get
            {
                var icons = new Texture2D[Slots.Count];
                for (int i = 0; i < Slots.Count; i++)
                    icons[i] = Slots[i].Icon!;
                return icons;
            }
            set => SetSlotIcons(value);
        }

        [Export]
        public string[] SlotRequirements
        {
            get
            {
                var requirements = new string[Slots.Count];
                for (int i = 0; i < Slots.Count; i++)
                    requirements[i] = Slots[i].Requirement;
                return requirements;
            }
            set => SetSlotRequirements(value);
        }

        [Export]
        public int[] SlotTintRoles
        {
            get
            {
                var tints = new int[Slots.Count];
                for (int i = 0; i < Slots.Count; i++)
                    tints[i] = (int)Slots[i].Tint;
                return tints;
            }
            set => SetSlotTintRoles(value);
        }

        public void SetSlots(IEnumerable<Slot>? slots)
        {
            List<Slot> next = NormalizeSlots(slots);
            if (SameSlots(Slots, next)) return;
            Slots.Clear();
            Slots.AddRange(next);
            RefreshSlots();
        }

        public void SetSlotKinds(int[]? kinds)
        {
            int count = kinds?.Length ?? 0;
            bool changed = Slots.Count != count;
            while (Slots.Count > count)
                Slots.RemoveAt(Slots.Count - 1);
            for (int i = 0; i < count; i++)
            {
                EnsureSlot(i);
                SlotKind next = SlotKindFromOrdinal(kinds![i]);
                if (Slots[i].Kind == next) continue;
                Slots[i].Kind = next;
                changed = true;
            }
            if (!changed) return;
            RefreshSlots();
        }

        public void SetSlotCounts(int[]? counts)
        {
            if (counts == null)
            {
                bool changed = false;
                for (int i = 0; i < Slots.Count; i++)
                {
                    if (Slots[i].Count == 0) continue;
                    Slots[i].Count = 0;
                    changed = true;
                }
                if (!changed) return;
                RefreshSlots();
                return;
            }

            bool updated = false;
            for (int i = 0; i < counts.Length; i++)
            {
                EnsureSlot(i);
                int next = Mathf.Max(0, counts[i]);
                if (Slots[i].Count == next) continue;
                Slots[i].Count = next;
                updated = true;
            }
            for (int i = counts.Length; i < Slots.Count; i++)
            {
                if (Slots[i].Count == 0) continue;
                Slots[i].Count = 0;
                updated = true;
            }
            if (!updated) return;
            RefreshSlots();
        }

        public void SetSlotIcons(Texture2D[]? icons)
        {
            if (icons == null)
            {
                bool changed = false;
                for (int i = 0; i < Slots.Count; i++)
                {
                    if (Slots[i].Icon == null) continue;
                    Slots[i].Icon = null;
                    changed = true;
                }
                if (!changed) return;
                RefreshSlots();
                return;
            }

            bool updated = false;
            for (int i = 0; i < icons.Length; i++)
            {
                EnsureSlot(i);
                if (Slots[i].Icon == icons[i]) continue;
                Slots[i].Icon = icons[i];
                updated = true;
            }
            for (int i = icons.Length; i < Slots.Count; i++)
            {
                if (Slots[i].Icon == null) continue;
                Slots[i].Icon = null;
                updated = true;
            }
            if (!updated) return;
            RefreshSlots();
        }

        public void SetSlotRequirements(string[]? requirements)
        {
            if (requirements == null)
            {
                bool changed = false;
                for (int i = 0; i < Slots.Count; i++)
                {
                    if (Slots[i].Requirement == "") continue;
                    Slots[i].Requirement = "";
                    changed = true;
                }
                if (!changed) return;
                RefreshSlots();
                return;
            }

            bool updated = false;
            for (int i = 0; i < requirements.Length; i++)
            {
                EnsureSlot(i);
                string next = requirements[i] ?? "";
                if (Slots[i].Requirement == next) continue;
                Slots[i].Requirement = next;
                updated = true;
            }
            for (int i = requirements.Length; i < Slots.Count; i++)
            {
                if (Slots[i].Requirement == "") continue;
                Slots[i].Requirement = "";
                updated = true;
            }
            if (!updated) return;
            RefreshSlots();
        }

        public void SetSlotTintRoles(int[]? tints)
        {
            if (tints == null)
            {
                bool changed = false;
                for (int i = 0; i < Slots.Count; i++)
                {
                    if (Slots[i].Tint == UiSurface.Role.Neutral) continue;
                    Slots[i].Tint = UiSurface.Role.Neutral;
                    changed = true;
                }
                if (!changed) return;
                RefreshSlots();
                return;
            }

            bool updated = false;
            for (int i = 0; i < tints.Length; i++)
            {
                EnsureSlot(i);
                UiSurface.Role next = RoleFromOrdinal(tints[i]);
                if (Slots[i].Tint == next) continue;
                Slots[i].Tint = next;
                updated = true;
            }
            for (int i = tints.Length; i < Slots.Count; i++)
            {
                if (Slots[i].Tint == UiSurface.Role.Neutral) continue;
                Slots[i].Tint = UiSurface.Role.Neutral;
                updated = true;
            }
            if (!updated) return;
            RefreshSlots();
        }

        public void AddSlot(SlotKind kind, Texture2D? icon = null, int count = 0,
                            string requirement = "", UiSurface.Role tint = UiSurface.Role.Neutral)
        {
            Slots.Add(new Slot
            {
                Kind = kind,
                Icon = icon,
                Count = Mathf.Max(0, count),
                Requirement = requirement ?? "",
                Tint = tint,
            });
            RefreshSlots();
        }

        public bool RemoveSlot(int index)
        {
            if (index < 0 || index >= Slots.Count)
                return false;

            Slots.RemoveAt(index);
            RefreshSlots();
            return true;
        }

        public void ClearSlots()
        {
            if (Slots.Count == 0)
                return;

            Slots.Clear();
            RefreshSlots();
        }

        public void RefreshSlots()
        {
            NormalizeSelectionToGrid();
            RefreshMinimumAndRedraw();
        }

        private void EnsureSlot(int index)
        {
            while (Slots.Count <= index)
                Slots.Add(new Slot());
        }

        private static List<Slot> NormalizeSlots(IEnumerable<Slot>? slots)
        {
            var next = new List<Slot>();
            if (slots == null)
                return next;

            foreach (Slot? slot in slots)
            {
                next.Add(new Slot
                {
                    Kind = SlotKindFromOrdinal((int)(slot?.Kind ?? SlotKind.Blank)),
                    Icon = slot?.Icon,
                    Count = Mathf.Max(0, slot?.Count ?? 0),
                    Requirement = slot?.Requirement ?? "",
                    Tint = RoleFromOrdinal((int)(slot?.Tint ?? UiSurface.Role.Neutral)),
                });
            }
            return next;
        }

        private static bool SameSlots(IReadOnlyList<Slot> left, IReadOnlyList<Slot> right)
        {
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
            {
                if (SlotKindFromOrdinal((int)left[i].Kind) != right[i].Kind) return false;
                if (!ReferenceEquals(left[i].Icon, right[i].Icon)) return false;
                if (Mathf.Max(0, left[i].Count) != right[i].Count) return false;
                if ((left[i].Requirement ?? "") != right[i].Requirement) return false;
                if (RoleFromOrdinal((int)left[i].Tint) != right[i].Tint) return false;
            }
            return true;
        }

        private static SlotKind SlotKindFromOrdinal(int value)
            => (SlotKind)Mathf.Clamp(value, (int)SlotKind.Filled, (int)SlotKind.Locked);

        private static UiSurface.Role RoleFromOrdinal(int value)
            => (UiSurface.Role)Mathf.Clamp(value, (int)UiSurface.Role.Neutral, (int)UiSurface.Role.Info);

        private int TotalSlots => _cols * _rows;
        private bool _eventsHooked;

        private void NormalizeSelectionToGrid()
        {
            int total = TotalSlots;
            _sel = Mathf.Clamp(_sel, -1, total - 1);
            if (_hover >= total || _hover < -1)
                _hover = -1;
        }

        [Signal] public delegate void SlotActivatedEventHandler(int index);

        public override void _Ready()
        {
            base._Ready();
            ApplyInputDefaults(MouseFilterEnum.Stop, FocusModeEnum.All);
            if (!_eventsHooked)
            {
                MouseExited += ClearHover;
                _eventsHooked = true;
            }
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (KitChrome.ShouldClearPointerState(this, what))
                ClearHover();
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            float pitch = fs * 3.2f;
            return new Vector2(pitch * _cols, pitch * _rows);
        }

        private void RefreshMinimumAndRedraw()
        {
            if (IsInsideTree())
            {
                KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
                UpdateMinimumSize();
            }
            QueueRedraw();
        }

        private void RefreshVisualAndRedraw()
        {
            QueueRedraw();
        }

        private float Pitch() => Mathf.Min(Size.X / _cols, Size.Y / _rows);

        private Rect2 SlotRect(int i)
        {
            float pitch = Pitch();
            float interior = pitch * InteriorRatio;
            float pad = (pitch - interior) * 0.5f;
            int cx = i % _cols, cy = i / _cols;
            return new Rect2(cx * pitch + pad, cy * pitch + pad, interior, interior);
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventKey key)
            {
                Vector2I dir = KitChrome.DirectionFromKey(key);
                if (dir != Vector2I.Zero)
                {
                    MoveSelection(dir);
                    AcceptEvent();
                    return;
                }
                if (KitChrome.IsConfirmKey(key) && _sel >= 0 && _sel < TotalSlots)
                {
                    EmitSignal(SignalName.SlotActivated, _sel);
                    AcceptEvent();
                    return;
                }
            }

            if (@event is InputEventMouseMotion mm)
            {
                int next = HitSlot(mm.Position);
                if (next != _hover)
                {
                    _hover = next;
                    QueueRedraw();
                }
                return;
            }

            if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
                return;
            int hit = HitSlot(mb.Position);
            if (hit >= 0)
            {
                Selected = hit;
                GrabFocus();
                EmitSignal(SignalName.SlotActivated, hit);
                AcceptEvent();
            }
        }

        private void MoveSelection(Vector2I dir)
        {
            int total = TotalSlots;
            if (total <= 0) return;
            int next = _sel < 0 ? 0 : _sel;
            if (dir.X <= -9999) next = 0;
            else if (dir.X >= 9999) next = total - 1;
            else next += dir.X + dir.Y * _cols;
            Selected = Mathf.Clamp(next, 0, total - 1);
        }

        private int HitSlot(Vector2 p)
        {
            for (int i = 0; i < TotalSlots; i++)
                if (SlotRect(i).HasPoint(p)) return i;
            return -1;
        }

        private void ClearHover()
        {
            if (_hover < 0) return;
            _hover = -1;
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (Size.X <= 8 || Size.Y <= 8) return;

            var g = Geo;
            Color face = FaceColor();
            Color ink = InkColor();
            var font = KitFont();
            int fs = UiSurface.FontSize(this);
            float rimPx = Mathf.Max(1f, g.Rim * 0.6f * (fs / 14f));
            int total = TotalSlots;

            for (int i = 0; i < total; i++)
            {
                Rect2 r = SlotRect(i);
                if (r.Size.X < 2f) continue;
                Slot s = i < Slots.Count ? Slots[i] : new Slot();

                // Slots are content wells, so they take WellShade — not the readout recess,
                // which renders a grid of black holes.
                float ps = g.WellShade;
                Color plate = new Color(face.R * ps, face.G * ps, face.B * ps, 1f);

                if (s.Tint != UiSurface.Role.Neutral && s.Kind == SlotKind.Filled)
                {
                    Color t = UiSurface.Semantic(this, s.Tint);
                    plate = new Color(Mathf.Lerp(plate.R, t.R, 0.45f),
                                      Mathf.Lerp(plate.G, t.G, 0.45f),
                                      Mathf.Lerp(plate.B, t.B, 0.45f), 1f);
                }

                if (s.Kind is SlotKind.Blank or SlotKind.Locked or SlotKind.Invite)
                {
                    // Drain saturation, do not merely darken (7x settled rule).
                    float l = UiSurface.Luminance(plate);
                    plate = new Color(Mathf.Lerp(plate.R, l, 0.92f), Mathf.Lerp(plate.G, l, 0.92f),
                                      Mathf.Lerp(plate.B, l, 0.92f), 1f);
                }

                DrawShape(r, ActiveShape, plate, ink, rimPx);
                DrawSlotInset(r, plate);

                switch (s.Kind)
                {
                    case SlotKind.Filled when s.Icon != null:
                        DrawTextureRect(s.Icon, r.Grow(-r.Size.X * 0.14f), false);
                        break;
                    case SlotKind.Invite:
                        DrawPlus(r, ink);
                        break;
                    case SlotKind.Locked:
                        DrawCross(r);
                        break;
                }

                if (s.Kind == SlotKind.Filled && s.Count > 1 && font != null)
                    DrawCountBadge(r, s.Count, font, fs, ink);

                // Locked slots say WHY, in words. A padlock alone is the thing the references
                // consistently do NOT do.
                if (s.Kind == SlotKind.Locked && !string.IsNullOrEmpty(s.Requirement) && font != null)
                {
                    string req = KitCase(s.Requirement);
                    float textWidth = r.Size.X * 1.3f;
                    int small = UiSurface.FitRole(this, UiSurface.TextRole.Small,
                                                  new Vector2(textWidth, r.Size.Y * 0.30f),
                                                  req, font, min: 8);
                    req = KitChrome.EllipsizeText(font, req, small, textWidth);
                    if (string.IsNullOrEmpty(req)) continue;
                    Vector2 m = font.GetStringSize(req, HorizontalAlignment.Left, -1, small);
                    DrawText(font, new Vector2(r.Position.X + (r.Size.X - m.X) * 0.5f, r.End.Y - small * 0.15f),
                               req, small, UiSurface.Text(this));
                }

                if (_hover == i && _sel != i)
                    KitSelect.Draw(this, Geo.SelectFor(WidgetClass),
                                   KitChrome.Poly(ActiveShape, r, Geo), r,
                                   UiSurface.Semantic(this, UiSurface.Role.Info),
                                   Mathf.Max(1.5f, 2f * (fs / 14f)));
            }

            // Selection LAST and OUTSIDE the slot, so it reads over any contents and does not
            // restyle the slot itself — and from the THEME's declared cues rather than a
            // hardcoded cream rectangle. citybuilder and strategy add a glow here, cardgame a
            // lift; racing3 proves one cue per widget cannot be right.
            if (_sel >= 0 && _sel < total)
            {
                Rect2 sel = SlotRect(_sel);
                KitSelect.Draw(this, Geo.SelectFor(WidgetClass),
                               KitChrome.Poly(ActiveShape, sel, Geo), sel,
                               UiSurface.Semantic(this, UiSurface.Role.Accent),
                               Mathf.Max(2f, 3f * (fs / 14f)));
            }

            KitChrome.DrawFocusRing(this, KitChrome.GenreOf(this), new Rect2(Vector2.Zero, Size),
                                    ActiveShape, 0.8f);

            DrawAttachments();
        }

        private void DrawPlus(Rect2 r, Color ink)
        {
            var c = r.Position + r.Size * 0.5f;
            float a = r.Size.X * 0.20f, w = Mathf.Max(2f, r.Size.X * 0.07f);
            var col = new Color(ink.R, ink.G, ink.B, 0.75f);
            DrawLine(c - new Vector2(a, 0), c + new Vector2(a, 0), col, w);
            DrawLine(c - new Vector2(0, a), c + new Vector2(0, a), col, w);
        }

        private void DrawSlotInset(Rect2 r, Color plate)
        {
            float w = Mathf.Max(1f, r.Size.X * 0.035f);
            Color light = new(Mathf.Min(1f, plate.R * 1.35f), Mathf.Min(1f, plate.G * 1.35f), Mathf.Min(1f, plate.B * 1.35f), 0.45f);
            Color shade = new(plate.R * 0.45f, plate.G * 0.45f, plate.B * 0.45f, 0.45f);
            DrawLine(r.Position + new Vector2(w, w), new Vector2(r.End.X - w, r.Position.Y + w), light, w);
            DrawLine(r.Position + new Vector2(w, w), new Vector2(r.Position.X + w, r.End.Y - w), light, w);
            DrawLine(new Vector2(r.Position.X + w, r.End.Y - w), r.End - new Vector2(w, w), shade, w);
            DrawLine(new Vector2(r.End.X - w, r.Position.Y + w), r.End - new Vector2(w, w), shade, w);
        }

        private void DrawCross(Rect2 r)
        {
            var c = r.Position + r.Size * 0.5f;
            float a = r.Size.X * 0.18f, w = Mathf.Max(2f, r.Size.X * 0.07f);
            var col = new Color(0.85f, 0.85f, 0.87f, 0.55f);
            DrawLine(c - new Vector2(a, a), c + new Vector2(a, a), col, w);
            DrawLine(c - new Vector2(a, -a), c + new Vector2(a, -a), col, w);
        }

        /// <summary>Bottom-right, straddling the corner (gameui8).</summary>
        private void DrawCountBadge(Rect2 r, int count, Font font, int fs, Color ink)
        {
            string txt = count.ToString();
            // Sized off the SLOT, not the theme. At a flat 0.72x body size a count badge was
            // barely legible on a large slot and identical on a small one -- the badge is drawn
            // inside a box this widget controls, so the box sets the type.
            int small = UiSurface.FitRole(this, UiSurface.TextRole.Small,
                                          new Vector2(r.Size.X * 0.55f, r.Size.Y * 0.42f),
                                          txt, font, min: 9);
            txt = KitChrome.EllipsizeText(font, txt, small, r.Size.X * 0.55f);
            if (string.IsNullOrEmpty(txt)) return;
            Vector2 m = font.GetStringSize(txt, HorizontalAlignment.Left, -1, small);
            float w = Mathf.Max(m.X + small * 0.7f, small * 1.4f), h = small * 1.25f;
            var b = new Rect2(r.End.X - w * 0.55f, r.End.Y - h * 0.55f, w, h);
            DrawShape(b, KitShape.Pill, UiSurface.Semantic(this, UiSurface.Role.Warning), ink, 1.5f);
            DrawText(font, new Vector2(b.Position.X + (b.Size.X - m.X) * 0.5f, b.Position.Y + (b.Size.Y + m.Y * 0.6f) * 0.5f),
                       txt, small, new Color(0.10f, 0.09f, 0.08f));
        }
    }
}

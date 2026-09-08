using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A right-click menu drawn in the kit's own chrome: a plate with one row per item, the row
    /// under the pointer highlighted.
    ///
    /// It is <c>TopLevel</c>, so it is positioned in screen space rather than inside whatever
    /// container happens to own it, and <see cref="PopupAt"/> clamps it back inside the viewport —
    /// a menu opened near the right edge would otherwise draw half off-screen, which is the failure
    /// every popup has the first time.
    ///
    /// Closes on <c>ui_cancel</c> or a click outside itself, and consumes that outside click so it
    /// does not also press whatever was underneath.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitContextMenu : KitControl
    {
        /// <summary>A message surface, not a pressable. It had been falling through to the base's
        /// Button default, which decided its corner radius and its selection cue as well as -- once
        /// the genre had artwork -- which sprite it was cut from, and it rendered as a glossy button
        /// with a raised lip.</summary>
        protected override KitWidgetClass WidgetClass => KitWidgetClass.Panel;

        [Export]
        public string[] Items
        {
            get => _items;
            set
            {
                string[] next = NormalizeStrings(value);
                if (SameStrings(_items, next)) return;
                _items = next;
                NormalizeHover();
                ResizeToItems();
                QueueRedraw();
            }
        }
        private string[] _items = System.Array.Empty<string>();

        [Signal] public delegate void ItemSelectedEventHandler(int index, string label);

        private int _hover = -1;
        private bool _eventsHooked;

        public override void _Ready()
        {
            base._Ready();
            TopLevel = true;
            Visible = false;
            ApplyInputDefaults(MouseFilterEnum.Stop, FocusModeEnum.All);
            if (!_eventsHooked)
            {
                MouseExited += ClearHover;
                _eventsHooked = true;
            }
            ResizeToItems();
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (KitChrome.ShouldClearPointerState(this, what))
                ClearHover();
        }

        public void PopupAt(Vector2 globalPosition)
        {
            Visible = true;
            _hover = _items.Length > 0 ? 0 : -1;
            ResizeToItems();
            GlobalPosition = ClampedPopupPosition(globalPosition);
            GrabFocus();
            QueueRedraw();
        }

        private Vector2 ClampedPopupPosition(Vector2 requestedGlobal)
        {
            Rect2 visible = PopupVisibleRect();
            Vector2 min = visible.Position + new Vector2(6f, 6f);
            Vector2 max = visible.End - Size - new Vector2(6f, 6f);
            if (max.X < min.X) max.X = min.X;
            if (max.Y < min.Y) max.Y = min.Y;
            return new Vector2(Mathf.Clamp(requestedGlobal.X, min.X, max.X),
                               Mathf.Clamp(requestedGlobal.Y, min.Y, max.Y));
        }

        public void SetItems(string[]? items)
        {
            Items = NormalizeStrings(items);
        }

        public void AddItem(string item)
        {
            string[] next = new string[_items.Length + 1];
            _items.CopyTo(next, 0);
            next[^1] = item ?? "";
            Items = next;
        }

        /// <summary>Drop one item, reporting whether the index named one. The highlight follows the
        /// removal so it never points past the end of a shortened menu.</summary>
        public bool RemoveItem(int index)
        {
            if (index < 0 || index >= _items.Length) return false;

            string[] next = new string[_items.Length - 1];
            for (int i = 0, w = 0; i < _items.Length; i++)
                if (i != index) next[w++] = _items[i];

            if (index <= _hover) _hover = Mathf.Max(-1, _hover - 1);
            Items = next;
            return true;
        }

        public void ClearItems()
        {
            Items = System.Array.Empty<string>();
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (KitChrome.IsCancel(@event))
            {
                Visible = false;
                _hover = -1;
                AcceptEvent();
                return;
            }

            if (KitChrome.NavigateOrRelease(this, @event, dir => dir.Y != 0 && MoveHover(dir.Y)))
                return;

            if (KitChrome.IsConfirm(@event) && _hover >= 0 && _hover < _items.Length)
            {
                Select(_hover);
                AcceptEvent();
                return;
            }

            if (@event is InputEventMouseMotion mm)
            {
                int hit = Hit(mm.Position);
                if (_hover != hit) { _hover = hit; QueueRedraw(); }
                return;
            }

            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
            {
                int hit = Hit(mb.Position);
                if (hit >= 0 && hit < _items.Length)
                    Select(hit);
                AcceptEvent();
            }
        }

        private void Select(int index)
        {
            if (index < 0 || index >= _items.Length) return;
            EmitSignal(SignalName.ItemSelected, index, _items[index]);
            Visible = false;
            _hover = -1;
        }

        /// <summary>Move the highlight, reporting whether it moved. At the first or last item the
        /// key is released; the menu still closes on ui_cancel, so nothing is stranded.</summary>
        private bool MoveHover(int delta)
        {
            if (_items.Length == 0) return false;
            int next = Mathf.Clamp(_hover < 0 ? 0 : _hover + delta, 0, _items.Length - 1);
            if (next == _hover) return false;
            _hover = next;
            QueueRedraw();
            return true;
        }

        private void NormalizeHover()
        {
            _hover = _items.Length == 0 ? -1 : Mathf.Clamp(_hover, 0, _items.Length - 1);
        }

        private void ClearHover()
        {
            if (_hover < 0) return;
            _hover = -1;
            QueueRedraw();
        }

        public override void _Input(InputEvent @event)
        {
            if (!Visible) return;
            if (@event is InputEventMouseButton { Pressed: true } mb && !GetGlobalRect().HasPoint(mb.GlobalPosition))
            {
                Visible = false;
                _hover = -1;
                GetViewport()?.SetInputAsHandled();
                QueueRedraw();
            }
        }

        public override void _Draw()
        {
            if (Size.X < 8f || Size.Y < 8f) return;

            // With no items this drew an empty plate, which in the editor is indistinguishable
            // from a broken widget. The preview says which array to fill and, like every other
            // one in the kit, draws only under the editor and mutates nothing.
            if (_items.Length == 0)
            {
                KitChrome.DrawEmptyPreview(this, Genre, new Rect2(Vector2.Zero, Size),
                                           ActiveShape, "Items");
                return;
            }

            DrawMaterial(new Rect2(Vector2.Zero, Size), ActiveShape);
            KitChrome.DrawFocusRing(this, Genre, new Rect2(Vector2.Zero, Size),
                                    ActiveShape, 0.75f);

            var font = KitFont();
            if (font == null) return;
            int fs = UiSurface.FontSize(this, UiSurface.TextRole.Caption);
            float rowH = RowHeight();
            Color ink = UiSurface.Text(this);
            Color accent = UiSurface.Semantic(this, UiSurface.Role.Accent);
            float pad = Mathf.Max(8f, fs * 0.8f);

            for (int i = 0; i < _items.Length; i++)
            {
                var row = new Rect2(pad * 0.55f, pad * 0.45f + i * rowH, Size.X - pad * 1.1f, rowH - 2f);
                if (i == _hover)
                    DrawShape(row, KitShape.Pill, accent with { A = 0.26f }, UiSurface.Ink(accent) with { A = 0.50f }, 1f);

                string text = KitCase(_items[i]);
                int fit = UiSurface.FitRole(this, UiSurface.TextRole.Caption, row.Size - new Vector2(pad, 0), text, font, min: 8);
                text = KitChrome.EllipsizeText(font, text, fit, row.Size.X - pad * 0.9f);
                Vector2 m = font.GetStringSize(text, HorizontalAlignment.Left, -1, fit);
                DrawText(font, new Vector2(row.Position.X + pad * 0.45f, row.Position.Y + (row.Size.Y + m.Y * 0.60f) * 0.5f),
                         text, fit, ink);
            }
        }

        private int Hit(Vector2 p)
        {
            float pad = Mathf.Max(8f, UiSurface.FontSize(this) * 0.8f);
            int i = Mathf.FloorToInt((p.Y - pad * 0.45f) / RowHeight());
            return i >= 0 && i < _items.Length ? i : -1;
        }

        private float RowHeight() => Mathf.Max(24f, UiSurface.FontSize(this) * 1.9f);

        public override Vector2 _GetMinimumSize()
            => PopupSizeForViewport(NaturalMinimumSize());

        private Vector2 NaturalMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            float width = fs * 11f;
            var font = KitFont();
            if (font != null)
                foreach (string item in _items)
                    width = Mathf.Max(width, font.GetStringSize(KitCase(item), HorizontalAlignment.Left, -1, fs).X + fs * 3f);
            return new Vector2(width, Mathf.Max(RowHeight() + fs, RowHeight() * Mathf.Max(1, _items.Length) + fs));
        }

        private void ResizeToItems()
        {
            Vector2 wanted = _GetMinimumSize();
            Size = CustomMinimumSize = wanted;
            UpdateMinimumSize();
        }

        private Vector2 PopupSizeForViewport(Vector2 natural)
        {
            if (!IsInsideTree())
                return natural;

            Rect2 visible = PopupVisibleRect();
            if (visible.Size.X <= 0f || visible.Size.Y <= 0f)
                return natural;

            const float margin = 6f;
            float maxWidth = Mathf.Max(96f, visible.Size.X - margin * 2f);
            return new Vector2(Mathf.Min(natural.X, maxWidth), natural.Y);
        }

        private Rect2 PopupVisibleRect()
        {
            Rect2 visible = GetViewport()?.GetVisibleRect() ?? new Rect2(Vector2.Zero, Size);
            if (TopLevel)
                return visible;

            Transform2D viewportToCanvas = GetCanvasTransform().AffineInverse();
            Vector2 a = viewportToCanvas * visible.Position;
            Vector2 b = viewportToCanvas * visible.End;
            Vector2 pos = new(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y));
            Vector2 size = new(Mathf.Abs(b.X - a.X), Mathf.Abs(b.Y - a.Y));
            return new Rect2(pos, size);
        }

        private static string[] NormalizeStrings(string[]? values)
        {
            if (values == null || values.Length == 0)
                return System.Array.Empty<string>();

            var next = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                next[i] = values[i] ?? "";
            return next;
        }

        private static bool SameStrings(string[] a, string[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if ((a[i] ?? "") != (b[i] ?? ""))
                    return false;
            return true;
        }
    }
}

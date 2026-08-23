using Godot;

namespace Beep.ECS.UI.Kit
{
    [Tool]
    [GlobalClass]
    public partial class KitContextMenu : KitControl
    {
        public string[] Items { get => _items; set { _items = value ?? System.Array.Empty<string>(); ResizeToItems(); QueueRedraw(); } }
        private string[] _items = System.Array.Empty<string>();

        [Signal] public delegate void ItemSelectedEventHandler(int index, string label);

        private int _hover = -1;

        public override void _Ready()
        {
            base._Ready();
            TopLevel = true;
            Visible = false;
            MouseFilter = MouseFilterEnum.Stop;
            FocusMode = FocusModeEnum.All;
            ResizeToItems();
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
            Rect2 visible = GetViewport()?.GetVisibleRect() ?? new Rect2(Vector2.Zero, Size);
            Vector2 min = visible.Position + new Vector2(6f, 6f);
            Vector2 max = visible.End - Size - new Vector2(6f, 6f);
            if (max.X < min.X) max.X = min.X;
            if (max.Y < min.Y) max.Y = min.Y;
            return new Vector2(Mathf.Clamp(requestedGlobal.X, min.X, max.X),
                               Mathf.Clamp(requestedGlobal.Y, min.Y, max.Y));
        }

        public void SetItems(string[] items)
        {
            Items = items;
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventKey key)
            {
                if (KitChrome.IsCancelKey(key))
                {
                    Visible = false;
                    _hover = -1;
                    AcceptEvent();
                    return;
                }
                Vector2I dir = KitChrome.DirectionFromKey(key);
                if (dir.Y != 0)
                {
                    MoveHover(dir.Y);
                    AcceptEvent();
                    return;
                }
                if (KitChrome.IsConfirmKey(key) && _hover >= 0 && _hover < _items.Length)
                {
                    Select(_hover);
                    AcceptEvent();
                    return;
                }
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

        private void MoveHover(int delta)
        {
            if (_items.Length == 0) return;
            int next = _hover < 0 ? 0 : _hover + delta;
            _hover = Mathf.Clamp(next, 0, _items.Length - 1);
            QueueRedraw();
        }

        public override void _Input(InputEvent @event)
        {
            if (!Visible) return;
            if (@event is InputEventMouseButton { Pressed: true } mb && !GetGlobalRect().HasPoint(mb.GlobalPosition))
            {
                Visible = false;
                _hover = -1;
            }
        }

        public override void _Draw()
        {
            if (Size.X < 8f || Size.Y < 8f) return;
            DrawMaterial(new Rect2(Vector2.Zero, Size), ActiveShape);
            KitChrome.DrawFocusRing(this, KitChrome.GenreOf(this), new Rect2(Vector2.Zero, Size),
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
        {
            int fs = UiSurface.FontSize(this);
            float width = fs * 11f;
            var font = KitFont();
            if (font != null)
                foreach (string item in _items)
                    width = Mathf.Max(width, font.GetStringSize(item, HorizontalAlignment.Left, -1, fs).X + fs * 3f);
            return new Vector2(width, Mathf.Max(RowHeight() + fs, RowHeight() * Mathf.Max(1, _items.Length) + fs));
        }

        private void ResizeToItems()
        {
            Size = CustomMinimumSize = _GetMinimumSize();
        }
    }
}

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using SizeFlags = Godot.Control.SizeFlags;
namespace Beep.ECS.UI
{
    /// <summary>
    /// Data table component. Attach to a VBoxContainer. Creates a sortable table
    /// with alternating row colors and click-to-sort column headers.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TableComponent : UIComponent
    {
        [Export] public string[] ColumnHeaders { get; set; } = System.Array.Empty<string>();
        [Export] public int[] ColumnWidths { get; set; } = System.Array.Empty<int>();
        // Palette-derived, not literals — see UiSurface. Computed, so a skin change is
        // picked up with no invalidation step.
        /// <summary>Multiply a surface toward black, for row banding.</summary>
        private static Color Shade(Color c, float k) => new(c.R * k, c.G * k, c.B * k, c.A);

        public Color HeaderBg => UiSurface.Ink(UiSurface.Of(this));
        public Color RowEven => UiSurface.Of(this);
        public Color RowOdd => Shade(UiSurface.Of(this), 0.94f);
        public Color HoverColor => UiSurface.Semantic(this, UiSurface.Role.Accent) with { A = 0.28f };
        public Color BorderAccent => UiSurface.Semantic(this, UiSurface.Role.Accent);
        public Color TextAccent => UiSurface.Semantic(this, UiSurface.Role.Accent);
        public Color TextPrimary => UiSurface.Text(this);
        /// <summary>Row height as a multiple of the theme's body font — a 32px row clips 24pt.</summary>
        [Export(PropertyHint.Range, "1.0,5.0,0.05")] public float RowHeightScale { get; set; } = 2.3f;
        private int RowHeight => Mathf.RoundToInt(UiSurface.FontSize(this) * RowHeightScale);
        // Scale of the theme's body font, not a fixed size. The themes run 14-24, so a
        // literal renders a genre's larger type out of a control built for 14.
        [Export(PropertyHint.Range, "0.3,6.0,0.05")] public float FontScale { get; set; } = 1.0f;
        private int FontSize => UiSurface.FontSize(this, FontScale);

        [Signal] public delegate void ColumnClickedEventHandler(int columnIndex, string columnName);
        [Signal] public delegate void RowClickedEventHandler(int rowIndex, string[] values);

        private VBoxContainer? _container;
        private HBoxContainer? _headerRow;
        private readonly List<PanelContainer> _rows = new();
        private readonly List<string[]> _data = new();
        private readonly List<Button> _headerButtons = new();
        private readonly Dictionary<Button, Action> _headerHandlers = new();
        private int _sortColumn = -1;
        private bool _sortAsc = true;

        public override void _Ready()
        {
            base._Ready();
            _container = GetParent() as VBoxContainer;
            if (_container == null)
            {
                GD.PushWarning($"[{Name}] TableComponent needs a VBoxContainer parent to build header + rows; got '{GetParent()?.GetType().Name ?? "null"}'. Parent it to a VBoxContainer.");
                return;
            }
            BuildHeader();
        }

        private void BuildHeader()
        {
            if (Engine.IsEditorHint()) return;
            if (_container == null) return;
            _headerRow = new HBoxContainer { CustomMinimumSize = new Vector2(0, RowHeight) };
            _headerRow.AddThemeConstantOverride("separation", 0);

            for (int i = 0; i < ColumnHeaders.Length; i++)
            {
                var btn = new Button();
                btn.Text = ColumnHeaders[i];
                btn.Flat = true;
                btn.Alignment = HorizontalAlignment.Left;
                btn.CustomMinimumSize = new Vector2(i < ColumnWidths.Length ? ColumnWidths[i] : 100, RowHeight);
                btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                Action handler = () => OnHeaderButtonPressed(btn);
                _headerHandlers[btn] = handler;
                btn.Pressed += handler;
                _headerButtons.Add(btn);
                StyleHeaderButton(btn);
                _headerRow.AddChild(btn);
            }
            _container.AddChild(_headerRow);
        }

        private void OnHeaderButtonPressed(Button btn)
        {
            int col = _headerButtons.IndexOf(btn);
            if (col >= 0) SortByColumn(col);
        }

        private void StyleHeaderButton(Button btn)
        {
            var sb = new StyleBoxFlat { BgColor = HeaderBg };
            sb.SetCornerRadiusAll(0);
            sb.BorderWidthBottom = 2;
            sb.BorderColor = BorderAccent;
            btn.AddThemeStyleboxOverride("normal", sb);
            btn.AddThemeStyleboxOverride("hover", sb);
            btn.AddThemeColorOverride("font_color", TextAccent);
            btn.AddThemeFontSizeOverride("font_size", FontSize);
        }

        public void Clear()
        {
            foreach (var row in _rows) row.QueueFree();  // frees the panel and its subtree
            _rows.Clear();
            _data.Clear();
        }

        public void AddRow(params string[] values)
        {
            _data.Add(values);
            RenderRow(values, _rows.Count);
        }

        public void SetData(List<string[]> data)
        {
            Clear();
            foreach (var row in data) AddRow(row);
        }

        private void RenderRow(string[] values, int index)
        {
            if (_container == null) return;

            // The row background is the row's own PanelContainer, not a loose Panel: the old code
            // built a Panel, styled it, and never added it to the tree, so zebra striping and hover
            // never rendered and UpdateRowBg found no Panel to recolor. A PanelContainer draws its
            // "panel" stylebox behind whatever it wraps, which is exactly the colored-row idiom.
            Color bg = index % 2 == 0 ? RowEven : RowOdd;
            var rowPanel = new PanelContainer { CustomMinimumSize = new Vector2(0, RowHeight) };
            rowPanel.MouseFilter = Godot.Control.MouseFilterEnum.Stop;
            ApplyRowBg(rowPanel, bg);

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 0);
            row.MouseFilter = Godot.Control.MouseFilterEnum.Ignore;  // let the panel receive hover/click
            rowPanel.AddChild(row);

            for (int i = 0; i < values.Length; i++)
            {
                var label = new Label { Text = values[i], VerticalAlignment = VerticalAlignment.Center };
                label.CustomMinimumSize = new Vector2(i < ColumnWidths.Length ? ColumnWidths[i] : 100, RowHeight);
                label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                label.AddThemeFontSizeOverride("font_size", FontSize);
                label.AddThemeColorOverride("font_color", TextPrimary);
                row.AddChild(label);
            }

            int rowIdx = index;
            rowPanel.GuiInput += e => OnRowGuiInput(e, rowIdx, values);
            rowPanel.MouseEntered += () => ApplyRowBg(rowPanel, HoverColor);
            rowPanel.MouseExited += () => ApplyRowBg(rowPanel, bg);

            _rows.Add(rowPanel);
            _container.AddChild(rowPanel);
        }

        private void OnRowGuiInput(InputEvent e, int rowIdx, string[] values)
        {
            if (e is InputEventMouseButton mb && mb.Pressed)
                EmitSignal(SignalName.RowClicked, rowIdx, values);
        }

        private static void ApplyRowBg(PanelContainer row, Color color)
        {
            var sb = new StyleBoxFlat { BgColor = color };
            sb.SetCornerRadiusAll(0);
            row.AddThemeStyleboxOverride("panel", sb);
        }

        public void SortByColumn(int column)
        {
            if (_sortColumn == column) _sortAsc = !_sortAsc;
            else { _sortColumn = column; _sortAsc = true; }

            var sorted = _sortAsc
                ? _data.OrderBy(r => r.Length > column ? r[column] : "").ToList()
                : _data.OrderByDescending(r => r.Length > column ? r[column] : "").ToList();

            _data.Clear();
            _data.AddRange(sorted);

            // Rebuild rows
            foreach (var row in _rows) row.QueueFree();  // frees the panel and its subtree
            _rows.Clear();

            for (int i = 0; i < _data.Count; i++) RenderRow(_data[i], i);

            EmitSignal(SignalName.ColumnClicked, column, ColumnHeaders.Length > column ? ColumnHeaders[column] : "");
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            foreach (var kv in _headerHandlers)
                if (GodotObject.IsInstanceValid(kv.Key))
                    kv.Key.Pressed -= kv.Value;
            _headerHandlers.Clear();
            _headerButtons.Clear();
        }
    }
}

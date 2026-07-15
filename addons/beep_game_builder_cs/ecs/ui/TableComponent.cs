using Godot;
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
        [Export] public Color HeaderBg { get; set; } = new(0.1f, 0.15f, 0.25f, 1f);
        [Export] public Color RowEven { get; set; } = new(0.15f, 0.2f, 0.3f, 1f);
        [Export] public Color RowOdd { get; set; } = new(0.18f, 0.22f, 0.33f, 1f);
        [Export] public Color HoverColor { get; set; } = new(0.25f, 0.3f, 0.45f, 1f);
        [Export] public Color BorderAccent { get; set; } = new(0.35f, 0.5f, 0.7f, 1f);
        [Export] public Color TextAccent { get; set; } = new(0.7f, 0.8f, 1f, 1f);
        [Export] public Color TextPrimary { get; set; } = new(0.9f, 0.92f, 0.95f, 1f);
        [Export] public int RowHeight { get; set; } = 32;
        [Export] public int FontSize { get; set; } = 14;

        [Signal] public delegate void ColumnClickedEventHandler(int columnIndex, string columnName);
        [Signal] public delegate void RowClickedEventHandler(int rowIndex, string[] values);

        private VBoxContainer? _container;
        private HBoxContainer? _headerRow;
        private readonly List<HBoxContainer> _rows = new();
        private readonly List<string[]> _data = new();
        private readonly List<Button> _headerButtons = new();
        private int _sortColumn = -1;
        private bool _sortAsc = true;

        public override void _Ready()
        {
            base._Ready();
            _container = GetParent() as VBoxContainer;
            if (_container == null) return;
            BuildHeader();
        }

        private void BuildHeader()
        {
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
                btn.Pressed += () => OnHeaderButtonPressed(btn);
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
            foreach (var row in _rows) { foreach (var c in row.GetChildren()) (c as Godot.Control)?.QueueFree(); row.QueueFree(); }
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
            var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, RowHeight) };
            row.AddThemeConstantOverride("separation", 0);
            row.MouseFilter = Godot.Control.MouseFilterEnum.Stop;

            Color bg = index % 2 == 0 ? RowEven : RowOdd;
            var sb = new StyleBoxFlat { BgColor = bg };
            sb.SetCornerRadiusAll(0);

            var panel = new Panel();
            panel.AddThemeStyleboxOverride("panel", sb);
            panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);

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
            row.GuiInput += e => OnRowGuiInput(e, rowIdx, values);
            row.MouseEntered += () => OnRowMouseEntered(row);
            row.MouseExited += () => OnRowMouseExited(row, bg);

            _rows.Add(row);
            _container.AddChild(row);
        }

        private void OnRowGuiInput(InputEvent e, int rowIdx, string[] values)
        {
            if (e is InputEventMouseButton mb && mb.Pressed)
                EmitSignal(SignalName.RowClicked, rowIdx, values);
        }

        private void OnRowMouseEntered(HBoxContainer row) => UpdateRowBg(row, HoverColor);
        private void OnRowMouseExited(HBoxContainer row, Color bg) => UpdateRowBg(row, bg);

        private static void UpdateRowBg(HBoxContainer row, Color color)
        {
            var sb = new StyleBoxFlat { BgColor = color };
            sb.SetCornerRadiusAll(0);
            foreach (var child in row.GetChildren())
                if (child is Panel p) { p.AddThemeStyleboxOverride("panel", sb); break; }
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
            foreach (var row in _rows) { foreach (var c in row.GetChildren()) (c as Godot.Control)?.QueueFree(); row.QueueFree(); }
            _rows.Clear();

            for (int i = 0; i < _data.Count; i++) RenderRow(_data[i], i);

            EmitSignal(SignalName.ColumnClicked, column, ColumnHeaders.Length > column ? ColumnHeaders[column] : "");
        }

        public override void _ExitTree()
        {
            foreach (var btn in _headerButtons)
                if (GodotObject.IsInstanceValid(btn))
                    btn.Pressed -= OnHeaderButtonPressed;
            _headerButtons.Clear();
        }
    }
}

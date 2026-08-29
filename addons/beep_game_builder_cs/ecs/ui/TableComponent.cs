using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Beep.ECS.UI.Kit;
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
        [Export] public NodePath HeaderRowPath { get; set; } = new("");
        [Export] public NodePath RowsContainerPath { get; set; } = new("");
        [Export] public bool BuildInEditor { get; set; } = true;
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = false;

        [Signal] public delegate void ColumnClickedEventHandler(int columnIndex, string columnName);
        [Signal] public delegate void RowClickedEventHandler(int rowIndex, string[] values);

        private VBoxContainer? _container;
        private HBoxContainer? _headerRow;
        private bool _createdHeaderRow;
        private readonly List<KitPanelContainer> _rows = new();
        private readonly List<string[]> _data = new();
        private readonly List<Button> _headerButtons = new();
        private readonly Dictionary<Button, Action> _headerHandlers = new();
        private int _sortColumn = -1;
        private bool _sortAsc = true;

        public override void _Ready()
        {
            base._Ready();
            if (!Engine.IsEditorHint() || BuildInEditor)
                CallDeferred(nameof(Setup));
            UpdateConfigurationWarnings();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (!GenerateControlsWhenPathsEmpty && FindHeaderRow() == null)
                return new[] { "Set HeaderRowPath to an authored HBoxContainer, add a sibling HeaderRow HBoxContainer, or enable GenerateControlsWhenPathsEmpty." };
            return System.Array.Empty<string>();
        }

        private void Setup()
        {
            if (!BindExistingControls())
            {
                if (!GenerateControlsWhenPathsEmpty)
                    return;

                BuildGeneratedHeaderRow();
            }

            if (_container == null || _headerRow == null)
                return;

            BuildHeader();
        }

        private void BuildHeader()
        {
            if (_headerRow == null) return;
            foreach (var kv in _headerHandlers)
                if (GodotObject.IsInstanceValid(kv.Key))
                    kv.Key.Pressed -= kv.Value;
            _headerHandlers.Clear();
            _headerButtons.Clear();

            KitChrome.SetConstantOverrideIfChanged(_headerRow, "separation", 0);

            var existingButtons = _headerRow.GetChildren().OfType<Button>().ToList();
            for (int i = 0; i < ColumnHeaders.Length; i++)
            {
                Button btn;
                if (i < existingButtons.Count)
                {
                    btn = existingButtons[i];
                }
                else
                {
                    if (!GenerateControlsWhenPathsEmpty)
                        break;

                    btn = new KitPushButton();
                    _headerRow.AddChild(btn);
                    SetEditedOwner(btn);
                }

                btn.Text = ColumnHeaders[i];
                btn.Flat = true;
                btn.Alignment = HorizontalAlignment.Left;
                btn.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                btn.CustomMinimumSize = new Vector2(i < ColumnWidths.Length ? ColumnWidths[i] : 100, RowHeight);
                btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                if (btn is KitPushButton kitButton)
                    kitButton.Accent = UiSurface.Role.Neutral;

                Action handler = () => OnHeaderButtonPressed(btn);
                _headerHandlers[btn] = handler;
                btn.Pressed += handler;
                _headerButtons.Add(btn);
                StyleHeaderButton(btn);
            }
        }

        private bool BindExistingControls()
        {
            _createdHeaderRow = false;
            _container = FindRowsContainer();
            _headerRow = FindHeaderRow();

            return _container != null && _headerRow != null;
        }

        private void BuildGeneratedHeaderRow()
        {
            _container = FindRowsContainer();
            if (_container == null)
            {
                GD.PushWarning($"[{Name}] TableComponent needs a VBoxContainer parent or RowsContainerPath to place table rows; got '{GetParent()?.GetType().Name ?? "null"}'.");
                return;
            }

            _createdHeaderRow = true;
            _headerRow = new HBoxContainer
            {
                Name = "HeaderRow",
                CustomMinimumSize = new Vector2(0, RowHeight)
            };
            _container.AddChild(_headerRow);
            _container.MoveChild(_headerRow, 0);
            SetEditedOwner(_headerRow);
        }

        public bool UsesSceneControls()
            => FindHeaderRow() != null || FindRowsContainer() != null;

        private VBoxContainer? FindRowsContainer()
        {
            if (!RowsContainerPath.IsEmpty && GetNodeOrNull<VBoxContainer>(RowsContainerPath) is { } pathRows)
                return pathRows;

            if (FindChild("Rows", recursive: true, owned: false) is VBoxContainer childRows)
                return childRows;

            if (GetParent()?.FindChild("Rows", recursive: true, owned: false) is VBoxContainer parentRows)
                return parentRows;

            return GetParent() as VBoxContainer;
        }

        private HBoxContainer? FindHeaderRow()
        {
            if (!HeaderRowPath.IsEmpty && GetNodeOrNull<HBoxContainer>(HeaderRowPath) is { } pathHeader)
                return pathHeader;

            if (FindChild("HeaderRow", recursive: true, owned: false) is HBoxContainer childHeader)
                return childHeader;

            if (GetParent()?.FindChild("HeaderRow", recursive: true, owned: false) is HBoxContainer parentHeader)
                return parentHeader;

            return null;
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
            KitChrome.SetStyleboxOverrideIfChanged(btn, "normal", sb);
            KitChrome.SetStyleboxOverrideIfChanged(btn, "hover", sb);
            KitChrome.SetColorOverrideIfChanged(btn, "font_color", TextAccent);
            KitChrome.SetFontSizeOverrideIfChanged(btn, "font_size", FontSize);
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
            var rowPanel = new KitPanelContainer
            {
                CustomMinimumSize = new Vector2(0, RowHeight),
                ShowWell = false,
                ExtraPadding = Vector2.Zero,
                FocusMode = Control.FocusModeEnum.All
            };
            rowPanel.MouseFilter = Godot.Control.MouseFilterEnum.Stop;
            ApplyRowBg(rowPanel, bg);

            var row = new HBoxContainer();
            KitChrome.SetConstantOverrideIfChanged(row, "separation", 0);
            row.MouseFilter = Godot.Control.MouseFilterEnum.Ignore;  // let the panel receive hover/click
            rowPanel.AddChild(row);

            for (int i = 0; i < values.Length; i++)
            {
                var label = new KitTableCell
                {
                    CellText = values[i],
                    MouseFilter = Godot.Control.MouseFilterEnum.Ignore
                };
                label.CustomMinimumSize = new Vector2(i < ColumnWidths.Length ? ColumnWidths[i] : 100, RowHeight);
                label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                row.AddChild(label);
            }

            int rowIdx = index;
            bool hovered = false;
            void RefreshInteractiveBg()
                => ApplyRowBg(rowPanel, hovered || rowPanel.HasFocus() ? HoverColor : bg);

            rowPanel.GuiInput += e => OnRowGuiInput(rowPanel, e, rowIdx, values);
            rowPanel.MouseEntered += () => { hovered = true; RefreshInteractiveBg(); };
            rowPanel.MouseExited += () => { hovered = false; RefreshInteractiveBg(); };
            rowPanel.FocusEntered += RefreshInteractiveBg;
            rowPanel.FocusExited += RefreshInteractiveBg;

            _rows.Add(rowPanel);
            _container.AddChild(rowPanel);
            SetEditedOwner(rowPanel);
        }

        private void OnRowGuiInput(Control row, InputEvent e, int rowIdx, string[] values)
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                row.GrabFocus();
                EmitSignal(SignalName.RowClicked, rowIdx, values);
                row.AcceptEvent();
                return;
            }

            if (e is InputEventKey key && KitChrome.IsConfirmKey(key))
            {
                EmitSignal(SignalName.RowClicked, rowIdx, values);
                row.AcceptEvent();
            }
        }

        private static void ApplyRowBg(PanelContainer row, Color color)
        {
            var sb = new StyleBoxFlat { BgColor = color };
            sb.SetCornerRadiusAll(0);
            KitChrome.SetStyleboxOverrideIfChanged(row, "panel", sb);
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
            if (_createdHeaderRow && _headerRow != null && GodotObject.IsInstanceValid(_headerRow))
                _headerRow.QueueFree();
            _headerRow = null;
        }

        private void SetEditedOwner(Node node)
        {
            if (!Engine.IsEditorHint())
                return;

            node.Owner = GetTree()?.EditedSceneRoot;
        }
    }
}

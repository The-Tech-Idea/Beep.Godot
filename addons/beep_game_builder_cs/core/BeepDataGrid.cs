using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Beep.GameBuilder;

/// <summary>
/// Auto-generates a GridContainer from any C# collection.
/// Column headers from property names, rows from items.
/// </summary>
[Tool]
[GlobalClass]
public partial class BeepDataGrid : VBoxContainer
{
    private HBoxContainer _headerRow;
    private VBoxContainer _bodyContainer;
    private ScrollContainer _scroll;
    private List<PropertyInfo> _columns = new();
    private object _dataSource;
    private string _sourcePropName;
    private Color _headerColor = new(0.15f, 0.15f, 0.2f);
    private Color _evenRowColor = new(0.1f, 0.1f, 0.15f);
    private Color _oddRowColor = new(0.12f, 0.12f, 0.18f);
    private Color _selectedColor = new(0.2f, 0.3f, 0.5f);
    private int _selectedRow = -1;

    public event Action<int, object> RowSelected;
    public event Action<int, object> RowDoubleClicked;

    [Export] public string HeaderFontSize { get; set; } = "16";
    [Export] public string RowFontSize { get; set; } = "14";
    [Export] public bool ShowHeader { get; set; } = true;
    [Export] public bool Selectable { get; set; } = true;

    public override void _Ready()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        _scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(_scroll);

        var inner = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _scroll.AddChild(inner);

        if (ShowHeader)
        {
            _headerRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            inner.AddChild(_headerRow);
        }

        _bodyContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        inner.AddChild(_bodyContainer);
    }

    /// <summary>Bind to a List&lt;T&gt; property on a source object.</summary>
    public void SetDataSource<T>(object source, string listPropertyName)
    {
        _dataSource = source;
        _sourcePropName = listPropertyName;
        var prop = source.GetType().GetProperty(listPropertyName);
        if (prop == null) return;

        var list = prop.GetValue(source) as IList<T>;
        if (list == null) return;

        // Discover columns from T's public properties
        _columns.Clear();
        foreach (var p in typeof(T).GetProperties())
        {
            if (p.CanRead && (p.PropertyType == typeof(string) || p.PropertyType == typeof(int) ||
                p.PropertyType == typeof(float) || p.PropertyType == typeof(double) ||
                p.PropertyType == typeof(bool) || p.PropertyType.IsEnum ||
                p.PropertyType == typeof(Color) || p.PropertyType == typeof(Vector2)))
                _columns.Add(p);
        }

        BuildRows(list);
    }

    /// <summary>Set data directly from a List.</summary>
    public void SetData<T>(IList<T> data)
    {
        _columns.Clear();
        foreach (var p in typeof(T).GetProperties())
        {
            if (p.CanRead && (p.PropertyType == typeof(string) || p.PropertyType == typeof(int) ||
                p.PropertyType == typeof(float) || p.PropertyType == typeof(double) ||
                p.PropertyType == typeof(bool) || p.PropertyType.IsEnum))
                _columns.Add(p);
        }
        BuildRows(data);
    }

    /// <summary>Refresh from the original data source.</summary>
    public void Refresh()
    {
        if (_dataSource == null || string.IsNullOrEmpty(_sourcePropName)) return;
        var prop = _dataSource.GetType().GetProperty(_sourcePropName);
        var list = prop?.GetValue(_dataSource);
        if (list == null) return;
        BuildRowsDynamic(list);
    }

    private void BuildRows<T>(IList<T> data)
    {
        // Clear existing
        if (_headerRow != null) ClearChildren(_headerRow);
        ClearChildren(_bodyContainer);

        // Build headers
        if (_headerRow != null)
        {
            foreach (var col in _columns)
            {
                var hdr = new Label
                {
                    Text = col.Name,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill
                };
                hdr.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.9f));
                hdr.AddThemeFontSizeOverride("font_size", int.Parse(HeaderFontSize));
                _headerRow.AddChild(hdr);
            }
        }

        // Build rows
        for (int i = 0; i < data.Count; i++)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var bgColor = i % 2 == 0 ? _evenRowColor : _oddRowColor;

            var bg = new ColorRect { Color = bgColor, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
            row.AddChild(bg); bg.MoveToFront();

            int idx = i; // capture for closure
            var item = data[i];

            foreach (var col in _columns)
            {
                var val = col.GetValue(item)?.ToString() ?? "";
                var cell = new Label
                {
                    Text = val,
                    HorizontalAlignment = col.PropertyType == typeof(int) || col.PropertyType == typeof(float) || col.PropertyType == typeof(double)
                        ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    ClipText = true
                };
                cell.AddThemeFontSizeOverride("font_size", int.Parse(RowFontSize));
                row.AddChild(cell);
            }

            if (Selectable)
            {
                var btn = new Button { Flat = true, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
                btn.Modulate = new Color(1, 1, 1, 0);
                btn.Pressed += () => SelectRow(idx, item);
                row.AddChild(btn);
            }

            _bodyContainer.AddChild(row);
        }
    }

    private void BuildRowsDynamic(object listObj)
    {
        var listType = listObj.GetType();
        var count = (int)listType.GetProperty("Count")?.GetValue(listObj);
        var indexer = listType.GetProperty("Item");

        ClearChildren(_bodyContainer);
        for (int i = 0; i < count; i++)
        {
            var item = indexer?.GetValue(listObj, new object[] { i });
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            foreach (var col in _columns)
            {
                var val = col.GetValue(item)?.ToString() ?? "";
                row.AddChild(new Label { Text = val, SizeFlagsHorizontal = SizeFlags.ExpandFill });
            }
            _bodyContainer.AddChild(row);
        }
    }

    private void SelectRow(int index, object item)
    {
        _selectedRow = index;
        RowSelected?.Invoke(index, item);
        // Highlight
        int childIdx = 0;
        foreach (var child in _bodyContainer.GetChildren())
        {
            if (child is HBoxContainer row && row.GetChildCount() > 0 && row.GetChild(0) is ColorRect bg)
            {
                bg.Color = childIdx == index ? _selectedColor : (childIdx % 2 == 0 ? _evenRowColor : _oddRowColor);
            }
            childIdx++;
        }
    }

    private static void ClearChildren(Node parent)
    {
        foreach (var child in parent.GetChildren()) child.QueueFree();
    }
}

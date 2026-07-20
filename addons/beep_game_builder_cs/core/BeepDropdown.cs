using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Beep.GameBuilder;

/// <summary>
/// Searchable dropdown with filter-as-you-type.
/// </summary>
[Tool]
[GlobalClass]
public partial class BeepDropdown : Button
{
    private PopupMenu _popup;
    private LineEdit _searchBox;
    private VBoxContainer _popupContent;
    private List<string> _allItems = new();
    private List<string> _filteredItems = new();
    private ItemList _itemList;

    [Export] public string Placeholder { get; set; } = "Search...";
    public string SelectedItem { get; private set; }
    public event Action<string> ItemSelected;

    public override void _Ready()
    {
        Text = Placeholder;
        Alignment = HorizontalAlignment.Left;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        // Build popup: a search box over the item list. Without the LineEdit the "filter-as-you-type"
        // claim was unreachable — the popup only ever showed the full list and Filter() had no UI caller.
        _popup = new PopupMenu();

        _popupContent = new VBoxContainer();
        _popupContent.SetAnchorsPreset(LayoutPreset.FullRect);

        _searchBox = new LineEdit { PlaceholderText = Placeholder, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _searchBox.TextChanged += t => Filter(t);
        _popupContent.AddChild(_searchBox);

        _itemList = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _itemList.ItemSelected += OnItemClicked;
        _popupContent.AddChild(_itemList);

        _popup.AddChild(_popupContent);
        AddChild(_popup);

        Pressed += () =>
        {
            _popup.Position = (Vector2I)GlobalPosition + new Vector2I(0, (int)Size.Y);
            _popup.Size = new Vector2I((int)Size.X, 220);
            _popup.Popup();
            _searchBox.Clear();      // start unfiltered each open
            RefreshList("");
            _searchBox.GrabFocus();
        };
    }

    /// <summary>Set available items.</summary>
    public void SetItems(IEnumerable<string> items)
    {
        _allItems = items.ToList();
        _filteredItems = new List<string>(_allItems);
        RefreshList("");
    }

    /// <summary>Filter items by search text.</summary>
    public void Filter(string search)
    {
        var lower = search.ToLower();
        _filteredItems = _allItems.Where(i => i.ToLower().Contains(lower)).ToList();
        RefreshListInternal();
    }

    private void RefreshList(string filter) { Filter(filter); }

    private void RefreshListInternal()
    {
        _itemList.Clear();
        foreach (var item in _filteredItems)
            _itemList.AddItem(item);
    }

    private void OnItemClicked(long index)
    {
        if (index >= 0 && index < _filteredItems.Count)
        {
            SelectedItem = _filteredItems[(int)index];
            Text = SelectedItem;
            ItemSelected?.Invoke(SelectedItem);
        }
        _popup.Hide();
    }

    /// <summary>Get or set the selected value.</summary>
    public string Value
    {
        get => SelectedItem;
        set { SelectedItem = value; Text = value; }
    }
}

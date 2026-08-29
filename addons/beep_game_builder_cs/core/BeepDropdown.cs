using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Beep.ECS.UI.Kit;

namespace Beep.GameBuilder;

/// <summary>
/// Searchable dropdown with filter-as-you-type.
/// </summary>
[Tool]
[GlobalClass]
public partial class BeepDropdown : KitPushButton
{
    private List<string> _allItems = new();
    private List<string> _filteredItems = new();
    private KitContextMenu? _menu;
    private bool _pressedHooked;

    [Export] public string Placeholder { get; set; } = "Search...";
    public string SelectedItem { get; private set; } = "";
    public event Action<string>? ItemSelected;

    public override void _Ready()
    {
        Text = Placeholder;
        Alignment = HorizontalAlignment.Left;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        base._Ready();

        if (_menu == null)
        {
            _menu = new KitContextMenu();
            _menu.ItemSelected += OnMenuItemSelected;
            AddChild(_menu);
        }

        if (!_pressedHooked)
        {
            Pressed += OpenMenu;
            _pressedHooked = true;
        }
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
        _menu?.SetItems(_filteredItems.ToArray());
    }

    private void OnMenuItemSelected(int index, string label)
    {
        if (index >= 0 && index < _filteredItems.Count)
        {
            SelectedItem = label;
            Text = SelectedItem;
            ItemSelected?.Invoke(SelectedItem);
        }
    }

    private void OpenMenu()
    {
        RefreshList("");
        _menu?.PopupAt(GlobalPosition + new Vector2(0, Size.Y));
    }

    /// <summary>Get or set the selected value.</summary>
    public string Value
    {
        get => SelectedItem;
        set { SelectedItem = value; Text = value; }
    }
}

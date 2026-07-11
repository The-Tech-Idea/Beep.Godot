using Godot;
using System;
using System.Collections.Generic;

namespace Beep.GameBuilder;

/// <summary>
/// Wraps Godot's Tree with data-binding helpers.
/// Build a tree from hierarchical data or add rows programmatically.
/// </summary>
[Tool]
[GlobalClass]
public partial class BeepTreeView : Tree
{
    [Export] public bool ShowRoot { get; set; } = true;
    [Export] public bool Selectable { get; set; } = true;

    private TreeItem _root;
    private Dictionary<string, TreeItem> _lookup = new();

    public event Action<TreeItem, object> ItemSelected;
    public event Action<TreeItem, object> ItemDoubleClicked;
    public event Action<TreeItem, object> ItemActivated;

    public override void _Ready()
    {
        Columns = 1;
        HideRoot = !ShowRoot;
        _root = CreateItem();
        _root.SetText(0, "Root");
        if (Selectable) SelectMode = SelectModeEnum.Single;

        ItemActivated += (item, column) =>
        {
            var data = item.GetMetadata(0).AsGodotObject();
            if (data != null) ItemActivated?.Invoke(item, data);
        };
        CellSelected += () =>
        {
            var item = GetSelected();
            if (item == null) return;
            var data = item.GetMetadata(0).AsGodotObject();
            if (data != null) ItemSelected?.Invoke(item, data);
        };
    }

    /// <summary>Build tree from a recursive data structure.</summary>
    public void BuildTree<T>(IEnumerable<T> items, Func<T, string> textSelector,
        Func<T, IEnumerable<T>> childrenSelector = null, Func<T, Texture2D> iconSelector = null)
    {
        Clear();
        _root = CreateItem();
        _lookup.Clear();

        foreach (var item in items)
            BuildNode(_root, item, textSelector, childrenSelector, iconSelector);
    }

    /// <summary>Add a leaf node with metadata.</summary>
    public TreeItem AddNode(TreeItem parent, string text, object data = null, Texture2D icon = null)
    {
        var item = parent.CreateChild();
        item.SetText(0, text);
        if (icon != null) item.SetIcon(0, icon);
        if (data != null) item.SetMetadata(0, Variant.From(data));
        return item;
    }

    /// <summary>Add a branch node.</summary>
    public TreeItem AddBranch(TreeItem parent, string text, bool collapsed = false)
    {
        var item = parent.CreateChild();
        item.SetText(0, text);
        item.Collapsed = collapsed;
        return item;
    }

    /// <summary>Get selected data object.</summary>
    public T GetSelectedData<[MustBeVariant] T>() where T : class
    {
        var item = GetSelected();
        return item?.GetMetadata(0).As<T>();
    }

    /// <summary>Clear all items.</summary>
    public new void Clear()
    {
        base.Clear();
        _root = CreateItem();
        _lookup.Clear();
    }

    private void BuildNode<T>(TreeItem parent, T item, Func<T, string> textSelector,
        Func<T, IEnumerable<T>> childrenSelector, Func<T, Texture2D> iconSelector)
    {
        var node = parent.CreateChild();
        node.SetText(0, textSelector(item));
        if (iconSelector != null) { var icon = iconSelector(item); if (icon != null) node.SetIcon(0, icon); }
        node.SetMetadata(0, Variant.From(item));

        if (childrenSelector != null)
        {
            var children = childrenSelector(item);
            if (children != null)
                foreach (var child in children)
                    BuildNode(node, child, textSelector, childrenSelector, iconSelector);
        }
    }
}

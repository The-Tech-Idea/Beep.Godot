using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Beep.GameBuilder;

/// <summary>
/// Binds C# object properties to Godot UI elements using reflection.
/// Supports automatic updates via polling or manual refresh.
/// </summary>
public static class BeepDataBinder
{
    internal static List<Binding> _bindings = new();
    private static double _pollTimer = 0;
    private const double PollInterval = 0.1;

    /// <summary>Bind a single object property to a UI node's property.</summary>
    public static Binding Bind(object source, string sourceProp, Node target, string targetProp, BindingMode mode = BindingMode.OneWay)
    {
        var b = new Binding { Source = source, SourceProp = sourceProp, Target = target, TargetProp = targetProp, Mode = mode };
        _bindings.Add(b);
        b.Refresh();
        return b;
    }

    /// <summary>Bind an object property to a Label's Text.</summary>
    public static Binding BindLabel(object source, string prop, Label label, string format = "{0}")
    {
        return Bind(source, prop, label, "Text", BindingMode.OneWay)
            .WithFormatter(v => string.Format(format, v));
    }

    /// <summary>Bind an object property to a ProgressBar's Value.</summary>
    public static Binding BindProgress(object source, string prop, ProgressBar bar)
        => Bind(source, prop, bar, "Value", BindingMode.OneWay);

    /// <summary>Bind an object property to a TextureProgressBar's Value.</summary>
    public static Binding BindTextureProgress(object source, string prop, TextureProgressBar bar)
        => Bind(source, prop, bar, "Value", BindingMode.OneWay);

    /// <summary>Bind a string property to a RichTextLabel's Text (BBCode).</summary>
    public static Binding BindRichLabel(object source, string prop, RichTextLabel label)
        => Bind(source, prop, label, "Text", BindingMode.OneWay);

    /// <summary>Bind a List/array to an ItemList.</summary>
    public static Binding BindItemList<T>(IList<T> source, ItemList list, Func<T, string> displaySelector)
    {
        var b = new ListBinding<T> { SourceList = source, Target = list, DisplaySelector = displaySelector };
        b.Refresh();
        return b;
    }

    /// <summary>Bind a List/array to an OptionButton.</summary>
    public static Binding BindOptionButton<T>(IList<T> source, OptionButton btn, Func<T, string> displaySelector)
    {
        var b = new OptionBinding<T> { SourceList = source, Target = btn, DisplaySelector = displaySelector };
        b.Refresh();
        return b;
    }

    /// <summary>Bind a boolean property to a CheckBox/CheckButton.</summary>
    public static Binding BindCheckBox(object source, string prop, CheckBox check, BindingMode mode = BindingMode.TwoWay)
        => Bind(source, prop, check, "ButtonPressed", mode);

    /// <summary>Bind a boolean to a node's Visible property for show/hide.</summary>
    public static Binding BindVisible(object source, string prop, CanvasItem target)
        => Bind(source, prop, target, "Visible", BindingMode.OneWay);

    /// <summary>Bind a Color to a ColorPicker or ColorRect.</summary>
    public static Binding BindColor(object source, string prop, ColorRect rect)
        => Bind(source, prop, rect, "Color", BindingMode.OneWay);

    /// <summary>Refresh all one-way bindings (call from _Process).</summary>
    public static void RefreshAll(double delta)
    {
        _pollTimer += delta;
        if (_pollTimer < PollInterval) return;
        _pollTimer = 0;
        foreach (var b in _bindings)
            if (b.Mode == BindingMode.OneWay || b.Mode == BindingMode.OneWayToSource)
                b.Refresh();
    }

    /// <summary>Remove all bindings for a given source object.</summary>
    public static void Unbind(object source)
    {
        _bindings.RemoveAll(b => b.Source == source);
    }

    /// <summary>Clear all bindings.</summary>
    public static void Clear() => _bindings.Clear();
}

public enum BindingMode { OneWay, TwoWay, OneWayToSource }

public class Binding
{
    public object Source;
    public string SourceProp;
    public Node Target;
    public string TargetProp;
    public BindingMode Mode;
    public Func<object, object> Formatter;

    public Binding WithFormatter(Func<object, object> f) { Formatter = f; return this; }

    public virtual void Refresh()
    {
        if (Source == null || Target == null) return;
        try
        {
            var val = Source.GetType().GetProperty(SourceProp)?.GetValue(Source);
            if (Formatter != null) val = Formatter(val);
            Target.Set(TargetProp, Variant.From(val));
        }
        catch { /* silent fail on missing property */ }
    }

    public void RefreshTwoWay()
    {
        if (Mode == BindingMode.TwoWay)
        {
            var val = Target.Get(TargetProp);
            Source.GetType().GetProperty(SourceProp)?.SetValue(Source, val.Obj);
        }
    }

    public void Unbind() => BeepDataBinder._bindings.Remove(this);
}

public class ListBinding<T> : Binding
{
    public IList<T> SourceList;
    public new ItemList Target;
    public Func<T, string> DisplaySelector;

    public override void Refresh()
    {
        if (SourceList == null || Target == null) return;
        Target.Clear();
        foreach (var item in SourceList)
            Target.AddItem(DisplaySelector?.Invoke(item) ?? item?.ToString() ?? "");
    }
}

public class OptionBinding<T> : Binding
{
    public IList<T> SourceList;
    public new OptionButton Target;
    public Func<T, string> DisplaySelector;

    public override void Refresh()
    {
        if (SourceList == null || Target == null) return;
        Target.Clear();
        foreach (var item in SourceList)
            Target.AddItem(DisplaySelector?.Invoke(item) ?? item?.ToString() ?? "");
    }
}

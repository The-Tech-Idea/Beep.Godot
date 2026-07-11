using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace Beep.GameBuilder;

/// <summary>
/// Auto-generates a form from any C# class properties with labels, inputs, and validation.
/// </summary>
[Tool]
[GlobalClass]
public partial class BeepFormBuilder : VBoxContainer
{
    private object _dataObject;
    private List<FieldBinding> _fields = new();
    private Button _submitBtn;

    [Export] public string SubmitText { get; set; } = "Save";
    [Export] public int LabelWidth { get; set; } = 120;
    [Export] public int FieldFontSize { get; set; } = 14;

    public event Action<object> Submitted;
    public event Action<object, string, string> ValidationFailed;

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 8);
        _submitBtn = new Button { Text = SubmitText, SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
        _submitBtn.Pressed += OnSubmit;
    }

    /// <summary>Build form fields from an object's properties.</summary>
    public void BuildForm(object obj, string[] excludeProps = null)
    {
        _dataObject = obj;
        ClearChildren(this);
        _fields.Clear();

        var exclude = excludeProps ?? Array.Empty<string>();
        var props = obj.GetType().GetProperties()
            .Where(p => p.CanRead && p.CanWrite && !exclude.Contains(p.Name));

        foreach (var prop in props)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", 8);

            // Label
            var label = new Label
            {
                Text = prop.Name.PrettyName(),
                CustomMinimumSize = new Vector2(LabelWidth, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            label.AddThemeFontSizeOverride("font_size", FieldFontSize);
            row.AddChild(label);

            // Input based on type
            Godot.Control input = CreateInputForType(prop.PropertyType, prop.GetValue(obj), (v) =>
            {
                try { prop.SetValue(obj, Convert.ChangeType(v, prop.PropertyType)); }
                catch { }
            });

            input.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(input);
            AddChild(row);

            _fields.Add(new FieldBinding { Property = prop, Input = input });
        }

        AddChild(_submitBtn);
    }

    private Godot.Control CreateInputForType(Type type, object currentValue, Action<object> onChanged)
    {
        if (type == typeof(string))
        {
            var le = new LineEdit { Text = currentValue?.ToString() ?? "" };
            le.TextChanged += t => onChanged(t);
            le.AddThemeFontSizeOverride("font_size", FieldFontSize);
            return le;
        }
        if (type == typeof(int) || type == typeof(float) || type == typeof(double))
        {
            var sb = new SpinBox { Value = Convert.ToDouble(currentValue ?? 0), AllowGreater = true, AllowLesser = true };
            if (type == typeof(int)) { sb.Step = 1; sb.Rounded = true; }
            else sb.Step = 0.1;
            sb.ValueChanged += v => onChanged(type == typeof(int) ? (int)v : v);
            return sb;
        }
        if (type == typeof(bool))
        {
            var cb = new CheckBox { ButtonPressed = (bool)(currentValue ?? false) };
            cb.Toggled += v => onChanged(v);
            return cb;
        }
        if (type.IsEnum)
        {
            var ob = new OptionButton();
            foreach (var name in Enum.GetNames(type)) ob.AddItem(name);
            ob.ItemSelected += i => onChanged(Enum.GetValues(type).GetValue(i));
            return ob;
        }
        if (type == typeof(Color))
        {
            var cp = new ColorPickerButton { Color = (Color)(currentValue ?? Colors.White) };
            cp.ColorChanged += c => onChanged(c);
            return cp;
        }
        if (type == typeof(Vector2))
        {
            var v2h = new HBoxContainer();
            var x = new SpinBox { Value = ((Vector2)(currentValue ?? Vector2.Zero)).X, AllowGreater = true, AllowLesser = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var y = new SpinBox { Value = ((Vector2)(currentValue ?? Vector2.Zero)).Y, AllowGreater = true, AllowLesser = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            x.ValueChanged += v => onChanged(new Vector2((float)v, ((Vector2)GetFormValue()).Y));
            y.ValueChanged += v => onChanged(new Vector2(((Vector2)GetFormValue()).X, (float)v));
            v2h.AddChild(x); v2h.AddChild(y);
            return v2h;
        }
        // fallback
        var fallback = new LineEdit { Text = currentValue?.ToString() ?? "" };
        fallback.TextChanged += t => onChanged(t);
        return fallback;
    }

    private object GetFormValue() => _dataObject;

    private void OnSubmit()
    {
        // Basic validation: check required string fields aren't empty
        foreach (var f in _fields)
        {
            if (f.Property.PropertyType == typeof(string))
            {
                var val = f.Property.GetValue(_dataObject) as string;
                if (string.IsNullOrWhiteSpace(val))
                {
                    ValidationFailed?.Invoke(_dataObject, f.Property.Name, $"{f.Property.Name.PrettyName()} is required.");
                    return;
                }
            }
        }
        Submitted?.Invoke(_dataObject);
    }

    private class FieldBinding
    {
        public PropertyInfo Property;
        public Godot.Control Input;
    }

    private static void ClearChildren(Node parent)
    {
        foreach (var c in parent.GetChildren()) c.QueueFree();
    }
}

public static class StringExtensions
{
    public static string PrettyName(this string pascal) =>
        System.Text.RegularExpressions.Regex.Replace(pascal, "(\\B[A-Z])", " $1");
}

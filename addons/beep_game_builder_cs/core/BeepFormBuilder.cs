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
            var capturedProp = prop;
            Godot.Control input = CreateInputForType(prop.PropertyType, prop.GetValue(obj), (v) =>
            {
                try { capturedProp.SetValue(obj, v is IConvertible ? Convert.ChangeType(v, capturedProp.PropertyType) : v); }
                catch (Exception e)
                {
                    GD.PushWarning($"[BeepFormBuilder] could not set '{capturedProp.Name}' ({capturedProp.PropertyType.Name}) from '{v}': {e.Message}");
                }
            });

            input.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(input);
            AddChild(row);

            _fields.Add(new FieldBinding { Property = prop, Input = input });
        }

        // Recreate the submit button every build: ClearChildren above QueueFree'd the previous one, and
        // re-adding a node already flagged for deletion makes it vanish at frame end (taking its handler).
        _submitBtn = new Button { Text = SubmitText, SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
        _submitBtn.Pressed += OnSubmit;
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
            // Track the live vector in a captured local — GetFormValue() returns the whole form object,
            // not this field's Vector2, so casting it here threw InvalidCastException on every edit.
            var vec = (Vector2)(currentValue ?? Vector2.Zero);
            var x = new SpinBox { Value = vec.X, AllowGreater = true, AllowLesser = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var y = new SpinBox { Value = vec.Y, AllowGreater = true, AllowLesser = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            x.ValueChanged += v => { vec.X = (float)v; onChanged(vec); };
            y.ValueChanged += v => { vec.Y = (float)v; onChanged(vec); };
            v2h.AddChild(x); v2h.AddChild(y);
            return v2h;
        }
        // fallback
        var fallback = new LineEdit { Text = currentValue?.ToString() ?? "" };
        fallback.TextChanged += t => onChanged(t);
        return fallback;
    }

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

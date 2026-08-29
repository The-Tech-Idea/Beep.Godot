using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Data binder component for two-way UI ↔ data synchronization.
    /// Manages bindings between C# object properties and Godot UI nodes.
    /// Supports formatters, two-way sync, and per-instance binding management.
    ///
    /// Example:
    /// var binder = GetNode&lt;DataBinderHostComponent&gt;("DataBinder");
    /// binder.BindLabel(player, nameof(player.Health), healthLabel, "HP: {0}");
    /// binder.BindProgress(player, nameof(player.Health), healthBar);
    /// binder.BindCheckBox(settings, nameof(settings.SoundEnabled), soundCheckbox);
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class DataBinderHostComponent : UIComponent, ISaveable
    {
        [Export] public bool AutoRefresh { get => _autoRefresh; set { if (_autoRefresh == value) return; _autoRefresh = value; UpdateProcessing(); } }
        private bool _autoRefresh = true;
        [Export] public double PollInterval { get; set; } = 0.1;

        /// <summary>Include this binder's state in saves. Off by default: GameStateData holds
        /// one slot per feature, so several participating binders would overwrite each other.</summary>
        [Export] public bool ParticipatesInSave { get; set; } = false;

        [Signal] public delegate void BindingCreatedEventHandler(string sourceProperty);
        [Signal] public delegate void BindingRemovedEventHandler(string sourceProperty);

        private class Binding
        {
            public object Source = null!;
            public string SourceProp = "";
            public Node Target = null!;
            public string TargetProp = "";
            public BindingMode Mode;
            public Func<object?, object?>? Formatter;

            // Latch so a broken binding reports itself once instead of either spamming every
            // frame or (as before) failing completely silently.
            private bool _warned;

            // Last raw value pushed, so Refresh only re-pushes on an actual change.
            private object? _lastRaw;
            private bool _hasLast;

            private void WarnOnce(string direction, System.Exception ex)
            {
                if (_warned) return;
                _warned = true;
                GD.PushWarning($"[DataBinder] {direction} binding {SourceProp} <-> {TargetProp} failed and is now inert: {ex.Message}");
            }

            /// <summary>Push source→target only when the value changed, so a poll that finds no
            /// change does nothing (no redundant Target.Set every tick).</summary>
            public void Refresh()
            {
                if (Source == null || Target == null) return;
                try
                {
                    var prop = Source.GetType().GetProperty(SourceProp);
                    if (prop == null) return;
                    var val = prop.GetValue(Source);
                    if (Formatter != null) val = Formatter(val);
                    if (_hasLast && Equals(val, _lastRaw)) return;
                    _lastRaw = val;
                    _hasLast = true;
                    Target.Set(TargetProp, ToVariant(val));
                }
                catch (System.Exception ex) { WarnOnce("Source→Target", ex); }
            }

            public void RefreshToSource()
            {
                if (Mode != BindingMode.TwoWay && Mode != BindingMode.OneWayToSource) return;
                if (Source == null || Target == null) return;
                try
                {
                    var prop = Source.GetType().GetProperty(SourceProp);
                    if (prop == null || !prop.CanWrite) return;
                    object? raw = Target.Get(TargetProp).Obj;
                    if (raw != null && raw is IConvertible && prop.PropertyType != raw.GetType())
                        raw = Convert.ChangeType(raw, prop.PropertyType);
                    prop.SetValue(Source, raw);
                }
                catch (System.Exception ex) { WarnOnce("Target→Source", ex); }
            }
        }

        private readonly List<Binding> _bindings = new();
        private double _pollTimer = 0;

        private static Variant ToVariant(object? value)
        {
            return value switch
            {
                null => default,
                Variant variant => variant,
                string text => Variant.From(text),
                bool flag => Variant.From(flag),
                int number => Variant.From(number),
                long number => Variant.From(number),
                float number => Variant.From(number),
                double number => Variant.From(number),
                Color color => Variant.From(color),
                Vector2 vector => Variant.From(vector),
                Vector2I vector => Variant.From(vector),
                Vector3 vector => Variant.From(vector),
                Vector3I vector => Variant.From(vector),
                Rect2 rect => Variant.From(rect),
                Rect2I rect => Variant.From(rect),
                NodePath path => Variant.From(path),
                StringName name => Variant.From(name),
                GodotObject godotObject => Variant.From(godotObject),
                Enum enumValue => Variant.From(Convert.ToInt64(enumValue)),
                IConvertible convertible => Variant.From(convertible.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                _ => Variant.From(value.ToString() ?? string.Empty)
            };
        }

        private static string NormalizeTargetProperty(string property)
        {
            return property switch
            {
                "Text" => "text",
                "Value" => "value",
                "ButtonPressed" => "button_pressed",
                "Visible" => "visible",
                "Color" => "color",
                _ => property
            };
        }

        public override void _Ready()
        {
            base._Ready();
            SetProcess(false);
            if (ParticipatesInSave) AddToGroup(SaveableHelper.Group);
            _pollTimer = 0;
            UpdateProcessing();
        }

        public override void _Process(double delta)
        {
            if (!IsActive || !AutoRefresh || Engine.IsEditorHint() || _bindings.Count == 0)
            {
                UpdateProcessing();
                return;
            }

            _pollTimer += delta;
            if (_pollTimer < PollInterval) return;
            _pollTimer = 0;

            RefreshAll();      // one-way: source → target
            RefreshTwoWay();   // two-way: push UI edits back to the source (e.g. BindCheckBox);
                               // without this, TwoWay bindings never wrote back unless a caller
                               // manually invoked RefreshTwoWay().
        }

        public override void _ExitTree()
        {
            _bindings.Clear();
            SetProcess(false);
            base._ExitTree();
        }

        /// <summary>Create a data binding between a source property and target UI property.</summary>
        public void Bind(object source, string sourceProp, Node target, string targetProp,
            BindingMode mode = BindingMode.OneWay, Func<object?, object?>? formatter = null)
        {
            if (source == null || target == null)
            {
                GD.PushWarning($"[{Name}] Bind ignored: {(source == null ? "source" : "target")} is null (property '{sourceProp}' → '{targetProp}'). Nothing was bound.");
                return;
            }

            var binding = new Binding
            {
                Source = source,
                SourceProp = sourceProp,
                Target = target,
                TargetProp = NormalizeTargetProperty(targetProp),
                Mode = mode,
                Formatter = formatter
            };

            _bindings.Add(binding);
            if (mode == BindingMode.OneWayToSource)
                binding.RefreshToSource();
            else
                binding.Refresh();
            EmitSignal(SignalName.BindingCreated, sourceProp);
            UpdateProcessing();
        }

        /// <summary>Convenience: bind a property to a Label's text.</summary>
        public void BindLabel(object source, string sourceProp, Label label,
            string format = "{0}", BindingMode mode = BindingMode.OneWay)
        {
            Bind(source, sourceProp, label, "Text", mode, v => string.Format(format, v));
        }

        /// <summary>Convenience: bind a numeric property to a ProgressBar's value.</summary>
        public void BindProgress(object source, string sourceProp, ProgressBar bar,
            BindingMode mode = BindingMode.OneWay)
        {
            Bind(source, sourceProp, bar, "Value", mode);
        }

        /// <summary>Convenience: bind a numeric property to a TextureProgressBar's value.</summary>
        public void BindTextureProgress(object source, string sourceProp, TextureProgressBar bar,
            BindingMode mode = BindingMode.OneWay)
        {
            Bind(source, sourceProp, bar, "Value", mode);
        }

        /// <summary>Convenience: bind a string property to a RichTextLabel's text.</summary>
        public void BindRichLabel(object source, string sourceProp, RichTextLabel label,
            BindingMode mode = BindingMode.OneWay)
        {
            Bind(source, sourceProp, label, "Text", mode);
        }

        /// <summary>Convenience: bind a boolean property to a CheckBox/CheckButton.</summary>
        public void BindCheckBox(object source, string sourceProp, CheckBox check,
            BindingMode mode = BindingMode.TwoWay)
        {
            Bind(source, sourceProp, check, "ButtonPressed", mode);
        }

        /// <summary>Convenience: bind a boolean property to a node's Visible property.</summary>
        public void BindVisible(object source, string sourceProp, CanvasItem target,
            BindingMode mode = BindingMode.OneWay)
        {
            Bind(source, sourceProp, target, "Visible", mode);
        }

        /// <summary>Convenience: bind a Color property to a ColorRect or ColorPicker.</summary>
        public void BindColor(object source, string sourceProp, CanvasItem target,
            BindingMode mode = BindingMode.OneWay)
        {
            Bind(source, sourceProp, target, "Color", mode);
        }

        /// <summary>Refresh all one-way bindings immediately.</summary>
        public void RefreshAll()
        {
            foreach (var binding in _bindings)
            {
                if (binding.Mode == BindingMode.OneWay || binding.Mode == BindingMode.TwoWay)
                    binding.Refresh();
            }
        }

        /// <summary>Refresh target-to-source bindings.</summary>
        public void RefreshTwoWay()
        {
            foreach (var binding in _bindings)
            {
                if (binding.Mode == BindingMode.TwoWay || binding.Mode == BindingMode.OneWayToSource)
                    binding.RefreshToSource();
            }
        }

        /// <summary>Force refresh a specific source property across all its bindings.</summary>
        public void RefreshProperty(string sourceProp)
        {
            foreach (var binding in _bindings)
            {
                if (binding.SourceProp == sourceProp && binding.Mode == BindingMode.OneWay)
                    binding.Refresh();
            }
        }

        /// <summary>Remove all bindings for a given source object.</summary>
        public void Unbind(object source)
        {
            // Emit BindingRemoved per removed binding — the event surface is uniform across
            // Unbind(source), Unbind(source, prop) and Clear().
            for (int i = _bindings.Count - 1; i >= 0; i--)
            {
                if (_bindings[i].Source == source)
                {
                    string prop = _bindings[i].SourceProp;
                    _bindings.RemoveAt(i);
                    EmitSignal(SignalName.BindingRemoved, prop);
                }
            }
            UpdateProcessing();
        }

        /// <summary>Remove a specific binding.</summary>
        public void Unbind(object source, string sourceProp)
        {
            int removed = _bindings.RemoveAll(b => b.Source == source && b.SourceProp == sourceProp);
            if (removed > 0) EmitSignal(SignalName.BindingRemoved, sourceProp);
            UpdateProcessing();
        }

        /// <summary>Get the number of active bindings.</summary>
        public int BindingCount => _bindings.Count;

        /// <summary>Clear all bindings.</summary>
        public void Clear()
        {
            foreach (var b in _bindings) EmitSignal(SignalName.BindingRemoved, b.SourceProp);
            _bindings.Clear();
            UpdateProcessing();
        }

        private void UpdateProcessing()
            => SetProcess(!Engine.IsEditorHint() && IsActive && AutoRefresh && _bindings.Count > 0);

        // ── ISaveable Implementation ──
        // Note: Bindings themselves are not persisted (they're UI infrastructure).
        // However, the bound data persists through other save/load mechanisms.
        public void Save(GameBuilder.GameStateData state)
        {
            // Bindings are UI setup, not game state — don't serialize them
        }

        public void Load(GameBuilder.GameStateData state)
        {
            // Rebind after load (UI state is re-established)
            RefreshAll();
        }
    }

    public enum BindingMode { OneWay, TwoWay, OneWayToSource }
}

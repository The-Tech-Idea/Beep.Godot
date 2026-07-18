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
        [Export] public bool AutoRefresh { get; set; } = true;
        [Export] public double PollInterval { get; set; } = 0.1;

        /// <summary>Include this binder's state in saves. Off by default: GameStateData holds
        /// one slot per feature, so several participating binders would overwrite each other.</summary>
        [Export] public bool ParticipatesInSave { get; set; } = false;

        [Signal] public delegate void BindingRefreshedEventHandler(string sourceProperty, Variant newValue);
        [Signal] public delegate void BindingCreatedEventHandler(string sourceProperty);
        [Signal] public delegate void BindingRemovedEventHandler(string sourceProperty);

        private class Binding
        {
            public object Source;
            public string SourceProp;
            public Node Target;
            public string TargetProp;
            public BindingMode Mode;
            public Func<object, object> Formatter;

            // Latch so a broken binding reports itself once instead of either spamming every
            // frame or (as before) failing completely silently.
            private bool _warned;

            private void WarnOnce(string direction, System.Exception ex)
            {
                if (_warned) return;
                _warned = true;
                GD.PushWarning($"[DataBinder] {direction} binding {SourceProp} <-> {TargetProp} failed and is now inert: {ex.Message}");
            }

            public void Refresh()
            {
                if (Source == null || Target == null) return;
                try
                {
                    var prop = Source.GetType().GetProperty(SourceProp);
                    if (prop == null) return;
                    var val = prop.GetValue(Source);
                    if (Formatter != null) val = Formatter(val);
                    Target.Set(TargetProp, Variant.From(val));
                }
                catch (System.Exception ex) { WarnOnce("Source→Target", ex); }
            }

            public void RefreshTwoWay()
            {
                if (Mode != BindingMode.TwoWay) return;
                if (Source == null || Target == null) return;
                try
                {
                    var val = Target.Get(TargetProp);
                    Source.GetType().GetProperty(SourceProp)?.SetValue(Source, val.Obj);
                }
                catch (System.Exception ex) { WarnOnce("Target→Source", ex); }
            }
        }

        private readonly List<Binding> _bindings = new();
        private double _pollTimer = 0;

        public override void _Ready()
        {
            base._Ready();
            if (ParticipatesInSave) AddToGroup(SaveableHelper.Group);
            _pollTimer = 0;
        }

        public override void _Process(double delta)
        {
            if (!IsActive || !AutoRefresh || Engine.IsEditorHint()) return;

            _pollTimer += delta;
            if (_pollTimer < PollInterval) return;
            _pollTimer = 0;

            RefreshAll();
        }

        public override void _ExitTree()
        {
            _bindings.Clear();
            base._ExitTree();
        }

        /// <summary>Create a data binding between a source property and target UI property.</summary>
        public void Bind(object source, string sourceProp, Node target, string targetProp,
            BindingMode mode = BindingMode.OneWay, Func<object, object> formatter = null)
        {
            if (source == null || target == null) return;

            var binding = new Binding
            {
                Source = source,
                SourceProp = sourceProp,
                Target = target,
                TargetProp = targetProp,
                Mode = mode,
                Formatter = formatter
            };

            _bindings.Add(binding);
            binding.Refresh();
            EmitSignal(SignalName.BindingCreated, sourceProp);
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
                if (binding.Mode == BindingMode.OneWay || binding.Mode == BindingMode.OneWayToSource)
                {
                    binding.Refresh();
                }
            }
        }

        /// <summary>Refresh two-way bindings (push UI changes back to source).</summary>
        public void RefreshTwoWay()
        {
            foreach (var binding in _bindings)
            {
                if (binding.Mode == BindingMode.TwoWay)
                    binding.RefreshTwoWay();
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
            _bindings.RemoveAll(b => b.Source == source);
        }

        /// <summary>Remove a specific binding.</summary>
        public void Unbind(object source, string sourceProp)
        {
            _bindings.RemoveAll(b => b.Source == source && b.SourceProp == sourceProp);
            EmitSignal(SignalName.BindingRemoved, sourceProp);
        }

        /// <summary>Get the number of active bindings.</summary>
        public int BindingCount => _bindings.Count;

        /// <summary>Clear all bindings.</summary>
        public void Clear()
        {
            _bindings.Clear();
        }

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

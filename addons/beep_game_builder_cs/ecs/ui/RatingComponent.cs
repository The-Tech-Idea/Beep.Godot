using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Star rating component. Attach to any Container to display 1-5 stars.
    /// Blind — works for reviews, player ratings, difficulty, quality.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class RatingComponent : UIComponent
    {
        [Export] public int MaxStars { get; set; } = 5;
        [Export] public float Value { get; set; } = 3.5f;
        [Export] public float StarSize { get; set; } = 24f;
        [Export] public Color FilledColor { get; set; } = new(1f, 0.84f, 0f, 1f);
        [Export] public Color EmptyColor { get; set; } = new(0.3f, 0.3f, 0.3f, 1f);
        [Export] public bool Interactive { get; set; } = false;

        [Signal] public delegate void RatingChangedEventHandler(float newValue);

        private Container? _container;
        private readonly List<Godot.Control> _starLabels = new();
        private readonly List<Action> _starDisconnectors = new();
        // The committed rating. Value is only the DISPLAYED value and shows a preview while hovering;
        // _committed is the truth, so moving the mouse away restores it instead of keeping the preview.
        private float _committed;

        public override void _Ready()
        {
            base._Ready();
            _container = GetParent() as Container;
            if (_container == null)
            {
                GD.PushWarning($"[{Name}] parent is not a Container — the star row cannot be built.");
                return;
            }
            _committed = Value;
            // Focusable when interactive, so a keyboard/gamepad player can adjust it (ui_left/right).
            if (Interactive) _container.FocusMode = Godot.Control.FocusModeEnum.All;
            BuildStars();
            UpdateDisplay();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!Interactive || _container == null || !_container.HasFocus()) return;
            if (@event.IsActionPressed("ui_right")) { SetValue(Mathf.Min(_committed + 1, MaxStars)); GetViewport().SetInputAsHandled(); }
            else if (@event.IsActionPressed("ui_left")) { SetValue(Mathf.Max(_committed - 1, 0)); GetViewport().SetInputAsHandled(); }
        }

        private void BuildStars()
        {
            if (Engine.IsEditorHint()) return;
            for (int i = 0; i < MaxStars; i++)
            {
                var label = new Label { Text = "★", HorizontalAlignment = HorizontalAlignment.Center };
                label.AddThemeFontSizeOverride("font_size", (int)StarSize);
                label.CustomMinimumSize = new Vector2(StarSize + 4, StarSize + 4);

                if (Interactive)
                {
                    int idx = i;
                    label.MouseFilter = Godot.Control.MouseFilterEnum.Stop;
                    // Named handlers stored for disconnection — they capture this and are attached to
                    // labels that live under the parent Container, so a component freed alone would
                    // otherwise fire them on a freed instance.
                    Godot.Control.GuiInputEventHandler onGui = e =>
                    {
                        if (e is InputEventMouseButton mb && mb.Pressed)
                        {
                            _committed = idx + 1;        // commit the click
                            Value = _committed;
                            UpdateDisplay();
                            EmitSignal(SignalName.RatingChanged, Value);
                        }
                    };
                    Action onEnter = () => { Value = idx + 0.8f; UpdateDisplay(); };   // preview only
                    Action onExit = () => { Value = _committed; UpdateDisplay(); };     // restore committed
                    label.GuiInput += onGui;
                    label.MouseEntered += onEnter;
                    label.MouseExited += onExit;
                    var lbl = label;
                    _starDisconnectors.Add(() =>
                    {
                        if (!GodotObject.IsInstanceValid(lbl)) return;
                        lbl.GuiInput -= onGui;
                        lbl.MouseEntered -= onEnter;
                        lbl.MouseExited -= onExit;
                    });
                }

                _starLabels.Add(label);
                _container?.AddChild(label);
            }
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            foreach (var disconnect in _starDisconnectors) disconnect();
            _starDisconnectors.Clear();
            // The stars are AddChild'd to the parent Container — free the ones we created.
            foreach (var s in _starLabels) if (GodotObject.IsInstanceValid(s)) s.QueueFree();
            _starLabels.Clear();
        }

        public void UpdateDisplay()
        {
            if (_container == null) return;
            var children = _container.GetChildren();
            for (int i = 0; i < children.Count && i < MaxStars; i++)
            {
                if (children[i] is Label label)
                {
                    float fill = Mathf.Clamp(Value - i, 0f, 1f);
                    Color color = fill >= 1f ? FilledColor :
                        fill > 0f ? FilledColor.Lerp(EmptyColor, 1f - fill) : EmptyColor;
                    label.AddThemeColorOverride("font_color", color);
                }
            }
        }

        public void SetValue(float value)
        {
            Value = value;
            _committed = value;
            UpdateDisplay();
            // Emit for programmatic changes too — an interactive click already emits (BuildStars),
            // so a listener saw user clicks but not code-driven updates.
            EmitSignal(SignalName.RatingChanged, Value);
        }
    }
}

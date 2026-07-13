using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Tweened integer roll for a child or sibling Label. SetValue animates the
    /// displayed number from the current value to the target. Use for score,
    /// currency, combo counters — anywhere a number should "count up" rather than snap.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class AnimatedNumberComponent : UIComponent
    {
        [Export] public int CurrentValue { get; set; } = 0;
        [Export] public float RollDuration { get; set; } = 0.5f;
        [Export] public NodePath LabelPath { get; set; } = new("");

        private Label? _label;
        private Tween? _roll;

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(ResolveLabel));
            UpdateText(CurrentValue);
        }

        private void ResolveLabel()
        {
            if (GetParent() is Label sibling) { _label = sibling; return; }
            if (!LabelPath.IsEmpty) _label = GetNodeOrNull<Label>(LabelPath);
            if (_label == null && GetParent() is Node p)
            {
                _label = new Label { Name = "NumberLabel", Text = "0" };
                p.AddChild(_label);
                if (p.IsInsideTree()) _label.Owner = p.Owner;
            }
        }

        /// <summary>Animate from the current value to <paramref name="value"/>.</summary>
        public void SetValue(int value)
        {
            if (!IsActive) { CurrentValue = value; UpdateText(value); return; }
            int from = CurrentValue;
            CurrentValue = value;
            _roll?.Kill();
            _roll = CreateTween();
            _roll.TweenMethod(Callable.From<int>(UpdateText), from, value, RollDuration);
        }

        private void UpdateText(int v)
        {
            if (_label != null) _label.Text = v.ToString("N0");
        }
    }
}

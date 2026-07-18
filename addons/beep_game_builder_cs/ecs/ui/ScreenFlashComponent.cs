using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Full-screen color flash overlay. Creates a ColorRect on a CanvasLayer,
    /// tweens its alpha up then down for a brief flash. Good for damage taken,
    /// healing received, level-up, explosions.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ScreenFlashComponent : UIComponent
    {
        [Export] public Color FlashColor { get; set; } = new(1, 0, 0, 0.4f);
        [Export] public float Duration { get; set; } = 0.2f;
        [Export] public int CanvasLayer { get; set; } = 50;

        [Signal] public delegate void FlashCompleteEventHandler();

        private ColorRect? _rect;
        private CanvasLayer? _layer;
        private Tween? _tween;

        public override void _Ready()
        {
            base._Ready();
            // Runtime only: SetupOverlay adds a CanvasLayer to the scene root.
            if (Engine.IsEditorHint()) return;
            CallDeferred(nameof(SetupOverlay));
        }

        private void SetupOverlay()
        {
            _layer = new CanvasLayer { Name = "ScreenFlash", Layer = CanvasLayer };
            _rect = new ColorRect
            {
                Name = "FlashRect",
                Color = new Color(0, 0, 0, 0),
                MouseFilter = Godot.Control.MouseFilterEnum.Ignore
            };
            _rect.SetAnchorsPreset(Godot.Control.LayoutPreset.FullRect);
            _layer.AddChild(_rect);
            GetTree().Root.AddChild(_layer);
        }

        /// <summary>Trigger a flash with the default color.</summary>
        public void Flash() => FlashWith(FlashColor);

        /// <summary>Trigger a flash with a custom color.</summary>
        public void FlashWith(Color color)
        {
            if (!IsActive || _rect == null) return;
            _tween?.Kill();
            _rect.Color = new Color(color.R, color.G, color.B, 0);
            _tween = CreateTween();
            _tween.TweenProperty(_rect, "color:a", color.A, Duration * 0.4f);
            _tween.TweenProperty(_rect, "color:a", 0f, Duration * 0.6f);
            _tween.Finished += () => EmitSignal(SignalName.FlashComplete);
        }

        public override void _ExitTree()
        {
            // The overlay is parented to /root, not to this component, so it must be freed here or a
            // dead "ScreenFlash" CanvasLayer accumulates on root every scene change.
            _tween?.Kill();
            if (_layer != null && GodotObject.IsInstanceValid(_layer)) _layer.QueueFree();
            base._ExitTree();
        }
    }
}

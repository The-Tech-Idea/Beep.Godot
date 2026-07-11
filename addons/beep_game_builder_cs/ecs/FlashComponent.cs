using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Damage flash component. Blind — flashes any CanvasItem white/red on trigger.
    /// Works for players, enemies, destructible objects, UI elements.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class FlashComponent : EntityComponent
    {
        [Export] public Color FlashColor { get; set; } = Colors.White;
        [Export] public float FlashDuration { get; set; } = 0.1f;
        [Export] public int FlashCount { get; set; } = 2;
        [Export] public bool FlashOnDamage { get; set; } = true;

        [Signal] public delegate void FlashedEventHandler();

        private CanvasItem? _canvas;
        private ShaderMaterial? _flashMaterial;
        private Tween? _tween;

        public override void _Ready()
        {
            base._Ready();
            _canvas = GetParent<CanvasItem>();
            if (_canvas != null && _canvas.Material is ShaderMaterial sm)
                _flashMaterial = sm;
        }

        public void Flash()
        {
            if (_canvas == null || !IsActive) return;
            _tween?.Kill();

            _tween = _canvas.CreateTween();
            for (int i = 0; i < FlashCount; i++)
            {
                _tween.TweenProperty(_canvas, "modulate", FlashColor, FlashDuration * 0.5f);
                _tween.TweenProperty(_canvas, "modulate", Colors.White, FlashDuration * 0.5f);
            }
            _tween.Finished += () => EmitSignal(SignalName.Flashed);
        }
    }
}

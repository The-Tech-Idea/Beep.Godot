using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Damage flash component. Blind — flashes any CanvasItem white/red on trigger.
    /// Works for players, enemies, destructible objects, UI elements.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class FlashComponent : GameplayComponent
    {
        [Export] public Color FlashColor { get; set; } = Colors.White;
        [Export] public float FlashDuration { get; set; } = 0.1f;
        [Export] public int FlashCount { get; set; } = 2;
        [Export] public bool FlashOnDamage { get; set; } = true;

        [Signal] public delegate void FlashedEventHandler();

        private CanvasItem? _canvas;
        private ShaderMaterial? _flashMaterial;
        private Tween? _tween;
        private HealthComponent? _health;
        // Held so _ExitTree can actually detach it — a fresh `-= (a,h)=>Flash()` lambda is a
        // different delegate instance and would not remove the one subscribed in _Ready.
        private HealthComponent.DamagedEventHandler? _damagedHandler;

        public override void _Ready()
        {
            base._Ready();
            _canvas = GetParent() as CanvasItem;
            if (_canvas != null && _canvas.Material is ShaderMaterial sm)
                _flashMaterial = sm;

            _health = GetSiblingComponent<HealthComponent>();
            if (FlashOnDamage && _health != null)
            {
                _damagedHandler = (amount, health) => Flash();
                _health.Damaged += _damagedHandler;
            }
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
            _tween.Finished += OnTweenFinished;
        }

        private void OnTweenFinished()
        {
            EmitSignal(SignalName.Flashed);
        }

        public override void _ExitTree()
        {
            _tween?.Kill();
            if (_health != null && _damagedHandler != null)
                _health.Damaged -= _damagedHandler;
        }
    }
}

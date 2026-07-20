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
        private Tween? _tween;
        private Color _resting = Colors.White;
        private HealthComponent? _health;
        // Held so _ExitTree can actually detach it — a fresh `-= (a,h)=>Flash()` lambda is a
        // different delegate instance and would not remove the one subscribed in _Ready.
        private HealthComponent.DamagedEventHandler? _damagedHandler;

        public override void _Ready()
        {
            base._Ready();
            _canvas = GetParent() as CanvasItem;
            if (_canvas != null) _resting = _canvas.Modulate;   // the tint/alpha to return to after a flash
            else GD.PushWarning($"[{Name}] parent is not a CanvasItem — Flash() can never run. Parent this to the Sprite2D/Control it should flash.");

            _health = GetSiblingComponent<HealthComponent>();
            if (FlashOnDamage && _health != null)
            {
                _damagedHandler = (amount, health) => Flash();
                _health.Damaged += _damagedHandler;
            }
            else if (FlashOnDamage && !Engine.IsEditorHint())
                // FlashOnDamage is on by default, so an entity with no Health sibling silently never
                // flashes on damage. Add a HealthComponent beside this, or turn FlashOnDamage off.
                GD.PushWarning($"[{Name}] FlashOnDamage is on but there is no sibling HealthComponent — it will never flash on damage. Add a HealthComponent or disable FlashOnDamage.");
        }

        public void Flash()
        {
            if (_canvas == null || !IsActive) return;
            _tween?.Kill();

            _tween = _canvas.CreateTween();
            for (int i = 0; i < FlashCount; i++)
            {
                _tween.TweenProperty(_canvas, "modulate", FlashColor, FlashDuration * 0.5f);
                _tween.TweenProperty(_canvas, "modulate", _resting, FlashDuration * 0.5f);
            }
            _tween.Finished += OnTweenFinished;
        }

        private void OnTweenFinished()
        {
            EmitSignal(SignalName.Flashed);
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            _tween?.Kill();
            if (_health != null && GodotObject.IsInstanceValid(_health) && _damagedHandler != null)
                _health.Damaged -= _damagedHandler;
        }
    }
}

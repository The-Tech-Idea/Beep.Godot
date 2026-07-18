using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Particle effect component. Attach to any Node to add a one-shot or looping particle burst.
    /// Blind — works for explosions, pickups, weather, UI feedback, anything.
    /// 98 presets available. Override the PackedScene path to use any particle .tscn.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ParticleComponent : WorldComponent
    {
        [Export] public PackedScene? ParticleScene { get; set; }
        [Export] public bool OneShot { get; set; } = true;
        [Export] public bool PlayOnStart { get; set; } = false;
        [Export] public bool AutoQueueFree { get; set; } = true;
        [Export] public bool FollowParent { get; set; } = false;
        [Export] public Vector2 Offset { get; set; } = Vector2.Zero;

        /// <summary>Burst automatically when a sibling <see cref="HealthComponent"/> dies — the death
        /// puff that <see cref="Burst"/> (0 callers before this) never had. The component owns its
        /// own wire, so HealthComponent stays unaware of it.</summary>
        [Export] public bool BurstOnDeath { get; set; } = false;

        [Signal] public delegate void BurstPlayedEventHandler();
        [Signal] public delegate void FinishedEventHandler();

        private GpuParticles2D? _particles;
        private HealthComponent? _health;

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(SetupParticles));
            if (BurstOnDeath && !Engine.IsEditorHint())
            {
                _health = GetSiblingComponent<HealthComponent>();
                if (_health != null) _health.Died += Burst;
                else GD.PushWarning($"[{Name}] BurstOnDeath is on but there is no sibling HealthComponent — nothing will trigger the burst.");
            }
        }

        private void SetupParticles()
        {
            if (ParticleScene != null)
            {
                var inst = ParticleScene.Instantiate<GpuParticles2D>();
                if (inst != null)
                {
                    _particles = inst;
                    _particles.OneShot = OneShot;
                    _particles.Emitting = PlayOnStart;
                    _particles.Position = Offset;
                    _particles.Finished += OnParticlesFinished;
                    AddChild(_particles);
                }
            }
            if (PlayOnStart) EmitSignal(SignalName.BurstPlayed);
        }

        private void OnParticlesFinished()
        {
            EmitSignal(SignalName.Finished);
            if (AutoQueueFree) _particles?.QueueFree();
        }

        public override void _Process(double delta)
        {
            if (FollowParent && _particles != null && GodotObject.IsInstanceValid(_particles) && GetParent() is Node2D parent)
                _particles.GlobalPosition = parent.GlobalPosition + Offset;
        }

        public void Burst()
        {
            if (_particles == null || !GodotObject.IsInstanceValid(_particles) || !IsActive) return;
            _particles.Restart();
            _particles.Emitting = true;
            EmitSignal(SignalName.BurstPlayed);
        }

        public void Stop()
        {
            if (_particles != null && GodotObject.IsInstanceValid(_particles))
                _particles.Emitting = false;
        }

        public override void _ExitTree()
        {
            if (_health != null) _health.Died -= Burst;
            if (_particles != null && GodotObject.IsInstanceValid(_particles))
                _particles.QueueFree();
        }
    }
}

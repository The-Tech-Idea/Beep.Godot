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

        [Signal] public delegate void BurstPlayedEventHandler();
        [Signal] public delegate void FinishedEventHandler();

        private GpuParticles2D? _particles;

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(SetupParticles));
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
            if (FollowParent && _particles != null && GetParent() is Node2D parent)
                _particles.GlobalPosition = parent.GlobalPosition + Offset;
        }

        public void Burst()
        {
            if (_particles == null || !IsActive) return;
            _particles.Restart();
            _particles.Emitting = true;
            EmitSignal(SignalName.BurstPlayed);
        }

        public void Stop() { if (_particles != null) _particles.Emitting = false; }
    }
}

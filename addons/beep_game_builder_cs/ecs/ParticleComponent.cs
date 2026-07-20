using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Particle effect component. Attach to any Node to add a one-shot or looping particle burst.
    /// Blind — works for explosions, pickups, weather, UI feedback, anything.
    /// Leave ParticleScene unset to use the shipped default burst, or point it at any of the 9
    /// bundled particle scenes in templates/particles/ (or your own GPUParticles2D scene).
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ParticleComponent : WorldComponent
    {
        // Shipped fallback so an unconfigured component still emits instead of silently doing nothing.
        private const string DefaultParticleScenePath = "res://addons/beep_game_builder_cs/templates/particles/simple_burst_particles.tscn";

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
            // Don't build the runtime GpuParticles2D child at edit time — this [Tool] component
            // lives in scenes, and instantiating + AddChild-ing at design time littered the edited
            // scene with runtime-only nodes (the sibling BurstOnDeath wire was already guarded; this
            // deferred setup was not).
            if (Engine.IsEditorHint()) return;
            Callable.From(SetupParticles).CallDeferred();
            if (BurstOnDeath)
            {
                _health = GetSiblingComponent<HealthComponent>();
                if (_health != null) _health.Died += Burst;
                else GD.PushWarning($"[{Name}] BurstOnDeath is on but there is no sibling HealthComponent — nothing will trigger the burst.");
            }
        }

        private void SetupParticles()
        {
            var scene = ParticleScene;
            if (scene == null)
            {
                // No scene assigned → fall back to a shipped burst so the component works out of the
                // box rather than silently emitting nothing. Assign ParticleScene to override.
                scene = ResourceLoader.Exists(DefaultParticleScenePath)
                    ? ResourceLoader.Load<PackedScene>(DefaultParticleScenePath) : null;
                if (scene == null)
                {
                    GD.PushWarning($"[{Name}] ParticleComponent has no ParticleScene and the shipped default ('{DefaultParticleScenePath}') could not load — no particles will play.");
                    return;
                }
            }

            // InstantiateOrNull (not Instantiate<T>) so a non-GPUParticles scene warns instead of
            // throwing InvalidCastException — the doc comment invites "any particle .tscn".
            var inst = scene.InstantiateOrNull<GpuParticles2D>();
            if (inst == null)
            {
                GD.PushWarning($"[{Name}] ParticleComponent's scene '{scene.ResourcePath}' does not root a GpuParticles2D — cannot play it. Use a GPUParticles2D-rooted scene.");
                return;
            }
            _particles = inst;
            _particles.OneShot = OneShot;
            _particles.Emitting = PlayOnStart;
            _particles.Position = Offset;
            _particles.Finished += OnParticlesFinished;
            AddChild(_particles);

            if (PlayOnStart) EmitSignal(SignalName.BurstPlayed);
        }

        private void OnParticlesFinished()
        {
            EmitSignal(SignalName.Finished);
            if (AutoQueueFree) _particles?.QueueFree();
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint()) return;
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
            base._ExitTree();
            // _health is a sibling — its free order vs this node isn't guaranteed, so guard the -=.
            if (_health != null && GodotObject.IsInstanceValid(_health)) _health.Died -= Burst;
            if (_particles != null && GodotObject.IsInstanceValid(_particles))
                _particles.QueueFree();
        }
    }
}

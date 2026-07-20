using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// One-shot sprite-flipbook effect — the AnimatedSprite2D counterpart to <see cref="ParticleComponent"/>.
    /// Spawns an AnimatedSprite2D scene (an explosion, a poof, a slash) at the parent's position, plays
    /// it once, and frees it when the animation finishes. Good for the punchy frame-animated bursts a
    /// GPUParticles2D can't do.
    ///
    /// Ships playable out of the box: leave EffectScene unset and it uses the bundled explosion flipbook,
    /// so it works with zero wiring (a shipped feature shouldn't need an asset nobody supplies). Assign
    /// EffectScene to override. Optionally fires on a sibling <see cref="HealthComponent"/>'s death.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class SpriteEffectComponent : WorldComponent
    {
        // Shipped fallback so an unconfigured component still shows an effect instead of nothing.
        private const string DefaultEffectScenePath = "res://addons/beep_game_builder_cs/templates/particles/explosion_anim.tscn";

        [Export] public PackedScene? EffectScene { get; set; }
        [Export] public bool PlayOnStart { get; set; } = false;
        /// <summary>Play automatically when a sibling <see cref="HealthComponent"/> dies (a death explosion).</summary>
        [Export] public bool PlayOnDeath { get; set; } = false;
        [Export] public Vector2 Offset { get; set; } = Vector2.Zero;
        [Export(PropertyHint.Range, "0.1,8,0.1")] public float EffectScale { get; set; } = 1f;

        [Signal] public delegate void EffectPlayedEventHandler();
        [Signal] public delegate void EffectFinishedEventHandler();

        private PackedScene? _effectScene;
        private HealthComponent? _health;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;

            _effectScene = EffectScene
                ?? (ResourceLoader.Exists(DefaultEffectScenePath) ? ResourceLoader.Load<PackedScene>(DefaultEffectScenePath) : null);
            if (_effectScene == null)
                GD.PushWarning($"[{Name}] SpriteEffectComponent has no EffectScene and the shipped default could not load — nothing will play.");

            if (PlayOnDeath)
            {
                _health = GetSiblingComponent<HealthComponent>();
                if (_health != null) _health.Died += Play;
                else GD.PushWarning($"[{Name}] PlayOnDeath is on but there is no sibling HealthComponent — nothing will trigger the effect.");
            }

            if (PlayOnStart) Callable.From(Play).CallDeferred();
        }

        /// <summary>Spawn the flipbook in world space at the parent's position, play once, free on finish.</summary>
        public void Play()
        {
            if (!IsActive || _effectScene == null) return;
            if (GetParent() is not Node2D parent2D)
            {
                GD.PushWarning($"[{Name}] SpriteEffectComponent's parent is {GetParent()?.GetType().Name ?? "null"}, not a Node2D — cannot place the effect. Parent it under a Node2D.");
                return;
            }

            var sprite = _effectScene.InstantiateOrNull<AnimatedSprite2D>();
            if (sprite == null)
            {
                GD.PushWarning($"[{Name}] SpriteEffectComponent's EffectScene '{_effectScene.ResourcePath}' does not root an AnimatedSprite2D — cannot play it.");
                return;
            }

            // Parent into the world (the grandparent), not this entity — so a death explosion outlives
            // the entity that spawned it instead of being freed with it.
            (parent2D.GetParent() ?? parent2D).AddChild(sprite);
            sprite.GlobalPosition = parent2D.GlobalPosition + Offset;
            sprite.Scale = new Vector2(EffectScale, EffectScale);

            // Free when the (non-looping) animation ends. The component may be freed with a dying
            // entity before the animation completes, so guard `this` before emitting on it, and free
            // the sprite independently.
            sprite.AnimationFinished += () =>
            {
                if (GodotObject.IsInstanceValid(this)) EmitSignal(SignalName.EffectFinished);
                if (GodotObject.IsInstanceValid(sprite)) sprite.QueueFree();
            };
            sprite.Play();
            EmitSignal(SignalName.EffectPlayed);
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            // _health is a sibling — guard the -= against it being freed first.
            if (_health != null && GodotObject.IsInstanceValid(_health)) _health.Died -= Play;
        }
    }
}

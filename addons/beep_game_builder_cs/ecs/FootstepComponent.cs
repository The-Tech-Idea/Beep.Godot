using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Procedural footstep audio. Plays random footstep sounds from an array
    /// at a configurable interval while the entity is moving. Supports random
    /// pitch variation and minimum speed threshold.
    /// Attach to a CharacterBody2D (reads its velocity).
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class FootstepComponent : WorldComponent
    {
        [Export] public AudioStream[] Sounds { get; set; } = System.Array.Empty<AudioStream>();
        [Export] public float MinSpeed { get; set; } = 50f;
        [Export] public float StepInterval { get; set; } = 0.3f;
        [Export] public float PitchVariation { get; set; } = 0.1f;
        [Export] public string Bus { get; set; } = "Master";

        // The five bundled concrete-footstep clips, loaded if Sounds is left empty so a walking
        // entity is audible with no per-scene wiring. Override with a surface-appropriate set.
        private static readonly string[] _defaultPaths =
        {
            "res://addons/beep_game_builder_cs/audio/footsteps/footstep_concrete_000.ogg",
            "res://addons/beep_game_builder_cs/audio/footsteps/footstep_concrete_001.ogg",
            "res://addons/beep_game_builder_cs/audio/footsteps/footstep_concrete_002.ogg",
            "res://addons/beep_game_builder_cs/audio/footsteps/footstep_concrete_003.ogg",
            "res://addons/beep_game_builder_cs/audio/footsteps/footstep_concrete_004.ogg",
        };

        private CharacterBody2D? _body;
        private AudioStreamPlayer? _player;
        private float _stepTimer;

        public float EffectiveMinSpeed => NonNegativeFinite(MinSpeed);
        public float EffectiveStepInterval => float.IsFinite(StepInterval) && StepInterval > 0.05f ? StepInterval : 0.05f;
        public float EffectivePitchVariation => float.IsFinite(PitchVariation) ? Mathf.Clamp(PitchVariation, 0f, 0.99f) : 0f;
        public string EffectiveBus => string.IsNullOrWhiteSpace(Bus) ? "Master" : Bus;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            _body = GetParent() as CharacterBody2D;
            if (Sounds.Length == 0)
                Sounds = LoadDefaults();
            if (GetParent() is not CharacterBody2D)
                // Reads _body's velocity/IsOnFloor — a non-body parent silently never steps.
                GD.PushWarning($"[{Name}] FootstepComponent's parent is {GetParent()?.GetType().Name ?? "null"}, not a CharacterBody2D — no footsteps will play. Parent it under the moving body.");
            Callable.From(SetupPlayer).CallDeferred();
        }

        private static AudioStream[] LoadDefaults()
        {
            var list = new Godot.Collections.Array<AudioStream>();
            foreach (var path in _defaultPaths)
                if (ResourceLoader.Exists(path) && ResourceLoader.Load<AudioStream>(path) is { } s)
                    list.Add(s);
            return System.Linq.Enumerable.ToArray(list);
        }

        private void SetupPlayer()
        {
            if (_player != null && GodotObject.IsInstanceValid(_player)) return;
            _player = new AudioStreamPlayer { Name = "FootstepPlayer", Bus = EffectiveBus };
            AddChild(_player);
        }

        public override void _PhysicsProcess(double delta)
        {
            if (Engine.IsEditorHint() || _body == null || _player == null || !IsActive) return;
            if (Sounds.Length == 0 || !_body.IsOnFloor()) return;

            if (!float.IsFinite(_body.Velocity.X) || !float.IsFinite(_body.Velocity.Y))
                return;

            float speed = _body.Velocity.Length();
            if (speed < EffectiveMinSpeed) return;

            _stepTimer -= DeltaSeconds(delta);
            if (_stepTimer <= 0)
            {
                _stepTimer = EffectiveStepInterval;
                PlayStep();
            }
        }

        private void PlayStep()
        {
            if (Sounds.Length == 0 || _player == null) return;
            // GD.RandRange(int,int) is INCLUSIVE; the old (int)RandRange(0, Length-1) truncated the
            // DOUBLE overload's [0, Length-1) so the last sound never played (with 2 sounds, index 1
            // never). (There is no GD.RandiRange in the C# API.)
            var sound = Sounds[GD.RandRange(0, Sounds.Length - 1)];
            if (sound == null) return;
            _player.Stream = sound;
            _player.Bus = EffectiveBus;
            _player.PitchScale = 1f + (float)GD.RandRange(-EffectivePitchVariation, EffectivePitchVariation);
            _player.Play();
        }

        private static float DeltaSeconds(double delta)
            => double.IsFinite(delta) && delta > 0.0 ? (float)delta : 0f;

        private static float NonNegativeFinite(float value)
            => float.IsFinite(value) && value > 0f ? value : 0f;
    }
}

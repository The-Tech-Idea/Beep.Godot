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

        public override void _Ready()
        {
            base._Ready();
            _body = GetParent() as CharacterBody2D;
            if (!Engine.IsEditorHint() && Sounds.Length == 0)
                Sounds = LoadDefaults();
            if (!Engine.IsEditorHint() && GetParent() is not CharacterBody2D)
                // Reads _body's velocity/IsOnFloor — a non-body parent silently never steps.
                GD.PushWarning($"[{Name}] FootstepComponent's parent is {GetParent()?.GetType().Name ?? "null"}, not a CharacterBody2D — no footsteps will play. Parent it under the moving body.");
            CallDeferred(nameof(SetupPlayer));
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
            _player = new AudioStreamPlayer { Name = "FootstepPlayer", Bus = Bus };
            AddChild(_player);
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_body == null || _player == null || !IsActive) return;
            if (Sounds.Length == 0 || !_body.IsOnFloor()) return;

            float speed = Mathf.Abs(_body.Velocity.Length());
            if (speed < MinSpeed) return;

            _stepTimer -= (float)delta;
            if (_stepTimer <= 0)
            {
                _stepTimer = StepInterval;
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
            _player.Stream = sound;
            _player.PitchScale = 1f + (float)GD.RandRange(-PitchVariation, PitchVariation);
            _player.Play();
        }
    }
}

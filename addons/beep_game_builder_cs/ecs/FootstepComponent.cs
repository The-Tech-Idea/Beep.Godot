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

        private CharacterBody2D? _body;
        private AudioStreamPlayer? _player;
        private float _stepTimer;

        public override void _Ready()
        {
            base._Ready();
            _body = GetParent() as CharacterBody2D;
            CallDeferred(nameof(SetupPlayer));
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
            var sound = Sounds[(int)GD.RandRange(0, Sounds.Length - 1)];
            _player!.Stream = sound;
            _player.PitchScale = 1f + (float)GD.RandRange(-PitchVariation, PitchVariation);
            _player.Play();
        }
    }
}

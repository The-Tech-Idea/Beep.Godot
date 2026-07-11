using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Audio component. Blind — attach to any Node to give it sound.
    /// One-shot SFX or looping background music. Works for UI, entities, environments.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class AudioComponent : GameplayComponent
    {
        [Export] public AudioStream? Stream { get; set; }
        [Export] public float VolumeDb { get; set; } = 0f;
        [Export] public float PitchScale { get; set; } = 1f;
        [Export] public bool AutoPlay { get; set; } = false;
        [Export] public bool Loop { get; set; } = false;
        [Export] public string Bus { get; set; } = "Master";

        [Signal] public delegate void PlayedEventHandler();
        [Signal] public delegate void StoppedEventHandler();
        [Signal] public delegate void FinishedEventHandler();

        private AudioStreamPlayer? _player;

        public override void _Ready()
        {
            base._Ready();
            _player = new AudioStreamPlayer();
            _player.Stream = Stream;
            _player.VolumeDb = VolumeDb;
            _player.PitchScale = PitchScale;
            _player.Bus = Bus;
            _player.Finished += () => EmitSignal(SignalName.Finished);
            AddChild(_player);

            if (AutoPlay) Play();
        }

        public void Play()
        {
            if (_player == null || !IsActive) return;
            _player.Play();
            EmitSignal(SignalName.Played);
        }

        public void Stop()
        {
            _player?.Stop();
            EmitSignal(SignalName.Stopped);
        }

        public void PlayOneShot(AudioStream stream, float volume = 0f)
        {
            if (!IsActive || GetParent() == null) return;
            var p = new AudioStreamPlayer { Stream = stream, VolumeDb = volume, Bus = Bus, Autoplay = true };
            GetParent().AddChild(p);
            p.Finished += p.QueueFree;
        }
    }
}

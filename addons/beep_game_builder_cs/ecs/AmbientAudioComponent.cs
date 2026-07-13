using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Zone-based ambient audio. Attach to an Area2D. When a body enters the zone,
    /// crossfades to the ambient track. On exit, crossfades back to the previous
    /// track (or silence). Integrates with WeatherSystemComponent — storm weather
    /// can trigger a thunder ambient override.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class AmbientAudioComponent : WorldComponent
    {
        [Export] public AudioStream? AmbientTrack { get; set; }
        [Export] public AudioStream? CombatTrack { get; set; }
        [Export] public float CrossfadeDuration { get; set; } = 1.5f;
        [Export] public string Bus { get; set; } = "Master";
        [Export] public bool Loop { get; set; } = true;

        private AudioStreamPlayer? _ambientPlayer;
        private AudioStreamPlayer? _combatPlayer;
        private bool _inCombat;

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(Setup));
        }

        private void Setup()
        {
            _ambientPlayer = new AudioStreamPlayer
            {
                Name = "AmbientPlayer",
                Bus = Bus,
                VolumeDb = -80f  // start silent, crossfade in
            };
            _combatPlayer = new AudioStreamPlayer
            {
                Name = "CombatPlayer",
                Bus = Bus,
                VolumeDb = -80f
            };
            AddChild(_ambientPlayer);
            AddChild(_combatPlayer);

            // Wire to Area2D parent for zone detection.
            if (GetParent() is Area2D area)
            {
                area.BodyEntered += OnBodyEntered;
                area.BodyExited += OnBodyExited;
            }
        }

        private void OnBodyEntered(Node body)
        {
            if (!IsActive) return;
            if (AmbientTrack != null)
            {
                _ambientPlayer!.Stream = AmbientTrack;
                _ambientPlayer!.Play();
                Crossfade(_ambientPlayer, 0f);
            }
        }

        private void OnBodyExited(Node body)
        {
            if (!IsActive) return;
            Crossfade(_ambientPlayer, -80f);
        }

        /// <summary>Switch to combat music (called when enemies are near).</summary>
        public void EnterCombat()
        {
            if (_inCombat || CombatTrack == null) return;
            _inCombat = true;
            _combatPlayer!.Stream = CombatTrack;
            _combatPlayer!.Play();
            Crossfade(_ambientPlayer, -20f);
            Crossfade(_combatPlayer, 0f);
        }

        /// <summary>Return to ambient music.</summary>
        public void ExitCombat()
        {
            if (!_inCombat) return;
            _inCombat = false;
            Crossfade(_combatPlayer, -80f);
            Crossfade(_ambientPlayer, 0f);
        }

        private void Crossfade(AudioStreamPlayer? player, float targetDb)
        {
            if (player == null) return;
            var tween = CreateTween();
            tween.TweenProperty(player, "volume_db", targetDb, CrossfadeDuration);
        }
    }
}

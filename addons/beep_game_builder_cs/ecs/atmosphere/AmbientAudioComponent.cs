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
        [Export] public AudioStream? ThunderTrack { get; set; }
        [Export] public float CrossfadeDuration { get; set; } = 1.5f;
        [Export] public string Bus { get; set; } = "Master";
        [Export] public bool Loop { get; set; } = true;
        [Export] public NodePath? WeatherSystemPath { get; set; }

        private AudioStreamPlayer? _ambientPlayer;
        private AudioStreamPlayer? _combatPlayer;
        private AudioStreamPlayer? _thunderPlayer;
        private bool _inCombat;
        private WeatherSystemComponent? _weather;
        // One tween PER player: a single shared field killed the previous fade on every call, so
        // EnterCombat's two Crossfades (duck ambient, raise combat) cancelled each other — only the
        // last player ever faded.
        private readonly System.Collections.Generic.Dictionary<AudioStreamPlayer, Tween> _fades = new();

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
            _thunderPlayer = new AudioStreamPlayer
            {
                Name = "ThunderPlayer",
                Bus = Bus,
                VolumeDb = 0f
            };
            AddChild(_ambientPlayer);
            AddChild(_combatPlayer);
            AddChild(_thunderPlayer);

            // Wire to Area2D parent for zone detection.
            if (GetParent() is Area2D area)
            {
                area.BodyEntered += OnBodyEntered;
                area.BodyExited += OnBodyExited;
            }

            // Wire to WeatherSystemComponent for thunder on lightning strikes.
            if (WeatherSystemPath != null) _weather = GetNodeOrNull<WeatherSystemComponent>(WeatherSystemPath);
            if (_weather == null)
            {
                foreach (var n in GetTree().GetNodesInGroup("weather_system"))
                    if (n is WeatherSystemComponent w) { _weather = w; break; }
            }
            if (_weather != null) _weather.LightningStruck += OnLightningStruck;
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
            if (_fades.TryGetValue(player, out var old) && GodotObject.IsInstanceValid(old)) old.Kill();
            var tw = CreateTween();
            tw.TweenProperty(player, "volume_db", targetDb, CrossfadeDuration);
            _fades[player] = tw;
        }

        private void OnLightningStruck()
        {
            if (!IsActive || ThunderTrack == null || _thunderPlayer == null) return;
            _thunderPlayer.Stream = ThunderTrack;
            _thunderPlayer.Play();
        }

        public override void _ExitTree()
        {
            foreach (var t in _fades.Values) if (GodotObject.IsInstanceValid(t)) t.Kill();
            if (_ambientPlayer != null && _ambientPlayer.Playing) _ambientPlayer.Stop();
            if (_combatPlayer != null && _combatPlayer.Playing) _combatPlayer.Stop();
            if (_thunderPlayer != null && _thunderPlayer.Playing) _thunderPlayer.Stop();
            if (_weather != null) _weather.LightningStruck -= OnLightningStruck;
        }
    }
}

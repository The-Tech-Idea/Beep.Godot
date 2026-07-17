using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Layered weather audio system. Manages multiple audio tracks (rain, wind, distant thunder)
    /// on a dedicated audio bus with parameter-driven mixing.
    ///
    /// Integrates with WeatherSystemComponent: listens for weather changes and cross-fades
    /// audio tracks accordingly. Supports intensity-based volume control.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class WeatherAudioController : WorldComponent
    {
        [ExportGroup("Audio Tracks")]
        [Export] public AudioStream? RainLoop { get; set; }
        [Export] public AudioStream? WindLoop { get; set; }
        [Export] public AudioStream[]? ThunderVariants { get; set; }
        [Export] public AudioStream? AmbientLoop { get; set; }

        [ExportGroup("Audio Config")]
        [Export] public string BusName { get; set; } = "Weather";
        [Export] public float RainMaxVolume { get; set; } = -10f;  // dB
        [Export] public float WindMaxVolume { get; set; } = -6f;
        [Export] public float ThunderVolume { get; set; } = 0f;
        [Export] public float AmbientMaxVolume { get; set; } = -15f;
        [Export] public float CrossFadeDuration { get; set; } = 0.5f;
        [Export] public NodePath? WeatherSystemPath { get; set; }

        private AudioStreamPlayer? _rainPlayer;
        private AudioStreamPlayer? _windPlayer;
        private AudioStreamPlayer? _thunderPlayer;
        private AudioStreamPlayer? _ambientPlayer;
        private WeatherSystemComponent? _weather;
        private int _weatherBusIndex = -1;
        private Tween? _fadeTween;

        public override void _Ready()
        {
            base._Ready();
            // Don't run in the editor: this class is [Tool] and lives in every genre main
            // scene, and Setup() adds a bus to the AudioServer and spawns player nodes.
            // Without this, merely opening a main scene mutated the EDITOR's audio buses
            // and littered the scene with runtime-only children.
            if (Engine.IsEditorHint()) return;

            // The tracks are deliberately yours to supply — the addon ships no audio assets,
            // and there is no sensible default for "rain". But shipping silently meant this
            // built an audio bus and four players to mix silence in every weather-enabled
            // genre, looking for all the world like working weather audio. Say it once, up
            // front, rather than leaving it to be discovered by listening.
            if (RainLoop == null && WindLoop == null && AmbientLoop == null
                && (ThunderVariants == null || ThunderVariants.Length == 0))
            {
                GD.PushWarning($"[{Name}] No weather audio tracks assigned (RainLoop / WindLoop / ThunderVariants / AmbientLoop are all empty) — weather will be silent. These are yours to supply; the addon ships no audio.");
            }

            CallDeferred(nameof(Setup));
        }

        private void Setup()
        {
            // Create audio bus if it doesn't exist
            _weatherBusIndex = AudioServer.GetBusIndex(BusName);
            if (_weatherBusIndex == -1)
            {
                // Bus doesn't exist; create it as a child of Master
                var masterBus = AudioServer.GetBusIndex("Master");
                _weatherBusIndex = AudioServer.BusCount;
                AudioServer.AddBus(_weatherBusIndex);
                AudioServer.SetBusName(_weatherBusIndex, BusName);
                if (masterBus >= 0)
                    AudioServer.SetBusSend(_weatherBusIndex, "Master");
            }

            // Create audio players
            _rainPlayer = CreatePlayer("RainPlayer", RainLoop);
            _windPlayer = CreatePlayer("WindPlayer", WindLoop);
            _ambientPlayer = CreatePlayer("AmbientPlayer", AmbientLoop);

            _thunderPlayer = new AudioStreamPlayer
            {
                Name = "ThunderPlayer",
                Bus = BusName,
                VolumeDb = -80f
            };
            AddChild(_thunderPlayer);

            // Wire to WeatherSystemComponent for intensity-based mixing
            if (WeatherSystemPath != null)
                _weather = GetNodeOrNull<WeatherSystemComponent>(WeatherSystemPath);
            if (_weather == null)
            {
                foreach (var n in GetTree().GetNodesInGroup("weather_system"))
                    if (n is WeatherSystemComponent w) { _weather = w; break; }
            }
            if (_weather != null)
                _weather.WeatherChanged += OnWeatherChanged;
        }

        /// <summary>
        /// Set overall weather audio intensity (0 = silent, 1 = full volume).
        /// </summary>
        public void SetWeatherIntensity(float intensity)
        {
            if (!IsActive || _weatherBusIndex < 0) return;

            intensity = Mathf.Clamp(intensity, 0f, 1f);

            // Fade rain volume with intensity
            var rainTarget = intensity > 0.3f ? Mathf.Lerp(-80f, RainMaxVolume, intensity) : -80f;
            FadePlayerVolume(_rainPlayer, rainTarget);

            // Fade wind volume with intensity
            var windTarget = intensity > 0.2f ? Mathf.Lerp(-80f, WindMaxVolume, intensity) : -80f;
            FadePlayerVolume(_windPlayer, windTarget);

            // Fade ambient with inverse intensity (quiet when storm is loud)
            var ambientTarget = Mathf.Lerp(AmbientMaxVolume, -80f, intensity);
            FadePlayerVolume(_ambientPlayer, ambientTarget);
        }

        /// <summary>
        /// Play a random thunder sound from variants.
        /// </summary>
        public void PlayThunder()
        {
            if (!IsActive || _thunderPlayer == null || ThunderVariants == null || ThunderVariants.Length == 0)
                return;

            var variant = ThunderVariants[GD.Randi() % ThunderVariants.Length];
            _thunderPlayer.Stream = variant;
            _thunderPlayer.VolumeDb = ThunderVolume;
            _thunderPlayer.Play();
        }

        private AudioStreamPlayer CreatePlayer(string name, AudioStream? stream)
        {
            var player = new AudioStreamPlayer
            {
                Name = name,
                Bus = BusName,
                VolumeDb = -80f,
                Stream = stream
            };
            AddChild(player);
            if (stream != null) player.Play();
            return player;
        }

        private void FadePlayerVolume(AudioStreamPlayer? player, float targetDb)
        {
            if (player == null) return;
            _fadeTween?.Kill();
            _fadeTween = CreateTween();
            _fadeTween.TweenProperty(player, "volume_db", targetDb, CrossFadeDuration);
        }

        private void OnWeatherChanged(int weatherType)
        {
            if (!IsActive) return;
            // Weather intensity is handled by SetWeatherIntensity() calls from WeatherSystemComponent
            // This signal handler is reserved for future use (e.g., special sound cues)
        }

        public override void _ExitTree()
        {
            _fadeTween?.Kill();
            if (_rainPlayer != null && _rainPlayer.Playing) _rainPlayer.Stop();
            if (_windPlayer != null && _windPlayer.Playing) _windPlayer.Stop();
            if (_ambientPlayer != null && _ambientPlayer.Playing) _ambientPlayer.Stop();
            if (_thunderPlayer != null && _thunderPlayer.Playing) _thunderPlayer.Stop();
            if (_weather != null) _weather.WeatherChanged -= OnWeatherChanged;
        }
    }
}

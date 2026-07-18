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

        /// <summary>OPTIONAL heavier rain/wind loops. When assigned, the mix crosses over from the
        /// light loop to the heavy one as weather intensity climbs (drizzle → downpour), instead of
        /// just turning the single loop up. Leave null to keep the single-loop behaviour.</summary>
        [Export] public AudioStream? RainLoopHeavy { get; set; }
        [Export] public AudioStream? WindLoopHeavy { get; set; }

        [ExportGroup("Audio Config")]
        [Export] public string BusName { get; set; } = "Weather";
        [Export] public float RainMaxVolume { get; set; } = -10f;  // dB
        [Export] public float WindMaxVolume { get; set; } = -6f;
        [Export] public float ThunderVolume { get; set; } = 0f;
        [Export] public float AmbientMaxVolume { get; set; } = -15f;
        [Export] public float CrossFadeDuration { get; set; } = 0.5f;
        [Export] public NodePath? WeatherSystemPath { get; set; }

        private AudioStreamPlayer? _rainPlayer;
        private AudioStreamPlayer? _rainHeavyPlayer;
        private AudioStreamPlayer? _windPlayer;
        private AudioStreamPlayer? _windHeavyPlayer;
        private AudioStreamPlayer? _thunderPlayer;
        private AudioStreamPlayer? _ambientPlayer;
        private WeatherSystemComponent? _weather;
        private int _weatherBusIndex = -1;
        // Which precipitation layers the CURRENT weather type wants audible — so the rain loop
        // doesn't keep hissing through Snow/Sandstorm/Clear. Set by OnWeatherChanged, read by
        // SetWeatherIntensity. Default true so the mix behaves until the first WeatherChanged.
        private bool _rainWanted = true;
        private bool _windWanted = true;
        // One tween PER player: a single shared field killed sibling fades — SetWeatherIntensity's
        // three back-to-back Fades (rain, wind, ambient) all cancelled but the last.
        private readonly System.Collections.Generic.Dictionary<AudioStreamPlayer, Tween> _fades = new();

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
            // Heavy layers are optional; only spawn them when a heavy loop is supplied.
            if (RainLoopHeavy != null) _rainHeavyPlayer = CreatePlayer("RainHeavyPlayer", RainLoopHeavy);
            if (WindLoopHeavy != null) _windHeavyPlayer = CreatePlayer("WindHeavyPlayer", WindLoopHeavy);

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
            {
                _weather.WeatherChanged += OnWeatherChanged;
                OnWeatherChanged((int)_weather.CurrentWeather);   // seed rain/wind-wanted for the current type
                // Drive the audio mix from the weather's smooth intensity. Nothing called
                // SetWeatherIntensity before, so the rain/wind/ambient levels never moved and
                // the whole controller mixed at -80 dB forever. IntensityChanged is emitted
                // every time the weather eases toward its target.
                _weather.IntensityChanged += SetWeatherIntensity;
                SetWeatherIntensity(_weather.WeatherIntensity);   // seed to the current value
                // Play thunder on each lightning strike. PlayThunder had no caller — in the shipped
                // atmosphere.tscn (which has this controller but not AmbientAudioComponent) the
                // thunder path was dead. Now storms actually crack.
                _weather.LightningStruck += PlayThunder;
            }
        }

        /// <summary>
        /// Set overall weather audio intensity (0 = silent, 1 = full volume).
        /// </summary>
        public void SetWeatherIntensity(float intensity)
        {
            if (_weatherBusIndex < 0) return;
            if (!IsActive)
            {
                // Deactivated → fade everything to silence rather than stranding whatever level the
                // loops last faded to (the old early-return left them playing at their last volume).
                FadePlayerVolume(_rainPlayer, -80f);
                FadePlayerVolume(_rainHeavyPlayer, -80f);
                FadePlayerVolume(_windPlayer, -80f);
                FadePlayerVolume(_windHeavyPlayer, -80f);
                FadePlayerVolume(_ambientPlayer, -80f);
                return;
            }

            intensity = Mathf.Clamp(intensity, 0f, 1f);

            // Rain: ramps in above 0.3. When a heavy loop is present, the light loop attenuates as
            // the heavy loop crosses in over the upper band, so drizzle becomes a downpour rather
            // than just a louder drizzle. No heavy loop → heavyMix stays 0 and only the light plays.
            float rainBase = _rainWanted && intensity > 0.3f ? Mathf.Lerp(-80f, RainMaxVolume, Mathf.Clamp((intensity - 0.3f) / 0.4f, 0f, 1f)) : -80f;
            float rainHeavyMix = _rainWanted && _rainHeavyPlayer != null ? Mathf.Clamp((intensity - 0.55f) / 0.35f, 0f, 1f) : 0f;
            FadePlayerVolume(_rainPlayer, Mathf.Lerp(rainBase, -80f, rainHeavyMix));
            if (_rainHeavyPlayer != null)
                FadePlayerVolume(_rainHeavyPlayer, Mathf.Lerp(-80f, RainMaxVolume, rainHeavyMix));

            // Wind: same crossfade, starting a touch earlier (wind is audible before rain).
            float windBase = _windWanted && intensity > 0.2f ? Mathf.Lerp(-80f, WindMaxVolume, Mathf.Clamp((intensity - 0.2f) / 0.4f, 0f, 1f)) : -80f;
            float windHeavyMix = _windWanted && _windHeavyPlayer != null ? Mathf.Clamp((intensity - 0.5f) / 0.4f, 0f, 1f) : 0f;
            FadePlayerVolume(_windPlayer, Mathf.Lerp(windBase, -80f, windHeavyMix));
            if (_windHeavyPlayer != null)
                FadePlayerVolume(_windHeavyPlayer, Mathf.Lerp(-80f, WindMaxVolume, windHeavyMix));

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
            if (_fades.TryGetValue(player, out var old) && GodotObject.IsInstanceValid(old)) old.Kill();
            var tw = CreateTween();
            tw.TweenProperty(player, "volume_db", targetDb, CrossFadeDuration);
            _fades[player] = tw;
        }

        private void OnWeatherChanged(int weatherType)
        {
            if (!IsActive) return;
            // Gate the precipitation layers by weather TYPE (intensity drives their level). Rain hisses
            // only for Rain/Storm; wind for the windy types. Everything else mutes those loops so the
            // rain loop no longer plays generically through Snow/Sandstorm/Clear.
            var type = (WeatherSystemComponent.WeatherType)weatherType;
            _rainWanted = type is WeatherSystemComponent.WeatherType.Rain
                              or WeatherSystemComponent.WeatherType.Storm;
            _windWanted = type is WeatherSystemComponent.WeatherType.Storm
                              or WeatherSystemComponent.WeatherType.Sandstorm
                              or WeatherSystemComponent.WeatherType.Snow;
            // Re-apply so the layers mute/unmute immediately for the new type.
            if (_weather != null) SetWeatherIntensity(_weather.WeatherIntensity);
        }

        public override void _ExitTree()
        {
            foreach (var t in _fades.Values) if (GodotObject.IsInstanceValid(t)) t.Kill();
            if (_rainPlayer != null && _rainPlayer.Playing) _rainPlayer.Stop();
            if (_rainHeavyPlayer != null && _rainHeavyPlayer.Playing) _rainHeavyPlayer.Stop();
            if (_windPlayer != null && _windPlayer.Playing) _windPlayer.Stop();
            if (_windHeavyPlayer != null && _windHeavyPlayer.Playing) _windHeavyPlayer.Stop();
            if (_ambientPlayer != null && _ambientPlayer.Playing) _ambientPlayer.Stop();
            if (_thunderPlayer != null && _thunderPlayer.Playing) _thunderPlayer.Stop();
            if (_weather != null)
            {
                _weather.WeatherChanged -= OnWeatherChanged;
                _weather.IntensityChanged -= SetWeatherIntensity;
                _weather.LightningStruck -= PlayThunder;
            }
        }
    }
}

using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Comprehensive 2D weather system. Attach to a Node2D (or the world root).
    /// Supports 10 weather types, each with particle effects, ambient tinting,
    /// wind force, and lightning flashes. Fog/haze is rendered by the standalone
    /// DynamicFogLayer (which reads this system's WeatherIntensity/CurrentWeather).
    ///
    /// Weather types:
    ///   Clear, Cloudy, Rain, Snow, Storm, Fog, Sandstorm, Hail, LeafFall, Heatwave
    ///
    /// Features (grounded in Godot 2D best practices — shader fakes, not 3D volumetric):
    /// • Pixel-art sprite weather for rain/storm/snow/hail/sand plus CpuParticles2D leaves.
    /// • Ambient lighting via CanvasModulate (dark for storms, warm for heatwave, etc.).
    /// • Lightning flashes: random full-screen ColorRect brightness bursts during Storm.
    /// • Wind force (Vector2) that gameplay components can read for leaf-drift, projectile drift, etc.
    /// • AutoCycle mode: rotates through weather types on a configurable timer.
    /// • Smooth ambient-color transitions (tween) when switching weather.
    ///
    /// Sources: WeatherSystem2D asset pattern; community consensus that 2D uses
    /// shader fakes rather than 3D volumetric.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class WeatherSystemComponent : WorldComponent
    {
        public enum WeatherType
        {
            Clear,      // No particles, white ambient
            Cloudy,     // No particles, slightly dimmed
            Rain,       // Rain particles, cool tint
            Snow,       // Snow particles, cold tint
            Storm,      // Heavy rain + lightning flashes, dark tint
            Fog,        // Fog shader overlay, muted tint
            Sandstorm,  // Sand particles + orange fog overlay
            Hail,       // Fast hail particles, cold tint
            LeafFall,   // Falling leaf particles, autumn tint
            Heatwave    // Heat-distortion shader overlay, warm tint
        }

        // ── Configuration ──
        [ExportGroup("General")]
        [Export] public WeatherType CurrentWeather { get; set; } = WeatherType.Clear;
        [Export] public bool AutoCycle { get; set; } = false;
        [Export] public double CycleInterval { get; set; } = 60.0;
        [Export] public int ParticleCount { get; set; } = 600;
        [Export] public Material? ParticleMaterial { get; set; }

        // ── Particle sprites ──
        // Nothing ever assigned CpuParticles2D.Texture, so every weather type rendered as
        // Godot's default white square — the motion, colour and scale below were all tuned,
        // but rain didn't look like rain. These default to the bundled Kenney CC0 sprites
        // (see textures/particles/CREDITS.md); clear one to fall back to plain squares, or
        // point it at your own art.
        // Left null: assigning here would mean GD.Load in a field initializer, which runs
        // during construction — including when the editor probes the class and when
        // beep.component_info reflects over it — and loading resources that early is
        // fragile. The bundled sprite is resolved lazily in ConfigureParticles instead, so
        // leaving these empty still gives you textured weather out of the box.
        [ExportGroup("Particle Textures")]
        /// <summary>Rain and Storm. Empty = the bundled 2D droplet. Line-trail textures are not
        /// used as the default because they read as screen-space streak curtains.</summary>
        [Export] public Texture2D? RainTexture { get; set; }

        /// <summary>Snow. Empty = the bundled soft round flake.</summary>
        [Export] public Texture2D? SnowTexture { get; set; }

        /// <summary>Hail. Empty = the bundled hard circle (tighter than snow).</summary>
        [Export] public Texture2D? HailTexture { get; set; }

        /// <summary>Sandstorm. Empty = the bundled grit mote.</summary>
        [Export] public Texture2D? SandTexture { get; set; }

        /// <summary>LeafFall. No bundled default — the pack has no leaf sprite, and a circle
        /// reads as snow, not foliage. Supply your own, or leaves stay untextured.</summary>
        [Export] public Texture2D? LeafTexture { get; set; }

        /// <summary>Rain/Storm impact splashes. Empty = the bundled soft round dot.</summary>
        [Export] public Texture2D? SplashTexture { get; set; }

        /// <summary>Set false to use no sprite at all unless you assign one explicitly
        /// (particles fall back to Godot's default white point).</summary>
        [Export] public bool UseBundledParticleTextures { get; set; } = true;

        // Loaded on first use and shared by every instance — these are small CC0 sprites
        // (textures/particles/CREDITS.md) and Godot caches the resource anyway.
        private const string TexDir = "res://addons/beep_game_builder_cs/textures/particles/";
        private const string CloudTexDir = "res://addons/beep_game_builder_cs/textures/clouds/";
        private static readonly System.Collections.Generic.Dictionary<string, Texture2D?> _bundledCache = new();
        private static readonly System.Collections.Generic.List<Texture2D> _bundledLightning = new();

        private static Texture2D? Bundled(string file)
        {
            if (_bundledCache.TryGetValue(file, out var cached)) return cached;
            string path = TexDir + file;
            Texture2D? tex = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
            if (tex == null)
            {
                // New addon PNGs may not have a .import sidecar yet. Runtime loading keeps the
                // effect visible instead of silently falling back to no texture until the editor
                // import pass catches up.
                string global = ProjectSettings.GlobalizePath(path);
                if (FileAccess.FileExists(global))
                {
                    Image img = Image.LoadFromFile(global);
                    if (img != null && !img.IsEmpty())
                        tex = ImageTexture.CreateFromImage(img);
                }
            }
            // Cache the miss too, so a missing file warns once rather than every weather change.
            if (tex == null) GD.PushWarning($"[Weather] Bundled particle texture not found: {path}");
            _bundledCache[file] = tex;
            return tex;
        }

        private static Texture2D[] BundledLightningSprites()
        {
            if (_bundledLightning.Count > 0) return _bundledLightning.ToArray();
            for (int i = 1; i <= 5; i++)
            {
                string path = $"{CloudTexDir}lightning{i}.png";
                if (ResourceLoader.Exists(path) && GD.Load<Texture2D>(path) is { } tex)
                    _bundledLightning.Add(tex);
            }
            if (_bundledLightning.Count == 0)
                GD.PushWarning($"[Weather] Bundled lightning sprites not found under {CloudTexDir}");
            return _bundledLightning.ToArray();
        }

        // ── Public read-only state for HUDs / forecast UIs ──
        /// <summary>Seconds remaining before AutoCycle switches weather (0 if not cycling).</summary>
        public double TimeToNextWeather => AutoCycle && _currentWeatherDuration > 0
            ? System.Math.Max(0, _currentWeatherDuration - _cycleTimer) : 0;
        /// <summary>Human-readable name of the current weather type.</summary>
        public string CurrentWeatherName => CurrentWeather.ToString();

        /// <summary>
        /// How near the most recent strike was, 0.4–1.0. Rolled once per bolt, BEFORE
        /// <c>LightningStruck</c> fires, so every listener describes the same lightning: the flash
        /// scales its amplitude by it, and the audio controllers use it to pick crack-vs-rumble and
        /// to set the thunder delay. Read it in a <c>LightningStruck</c> handler.
        /// </summary>
        public float LastBoltStrength { get; private set; } = 1f;

        /// <summary>
        /// Set true while the player is under a roof — precipitation stops and weather audio
        /// muffles. This is YOURS to drive: an Area2D over each building, a tile check, whatever
        /// your level uses. The addon cannot know where your roofs are.
        /// </summary>
        [Export] public bool InsideShelter { get; set; }

        /// <summary>
        /// The eased 0–1 form of <see cref="InsideShelter"/>. Snapping precipitation off the frame
        /// the player crosses a threshold looks like a bug; this slides over ~0.3s. Components that
        /// react to being sheltered should read THIS, not the bool.
        /// </summary>
        public float ShelterFactor { get; private set; }

        [ExportGroup("Ambient Tints")]
        [Export] public Color ClearTint { get; set; } = new(1f, 1f, 1f, 1f);
        [Export] public Color CloudyTint { get; set; } = new(0.85f, 0.85f, 0.9f, 1f);
        [Export] public Color RainTint { get; set; } = new(0.65f, 0.7f, 0.8f, 1f);
        [Export] public Color SnowTint { get; set; } = new(0.8f, 0.85f, 0.95f, 1f);
        [Export] public Color StormTint { get; set; } = new(0.35f, 0.35f, 0.45f, 1f);
        [Export] public Color FogTint { get; set; } = new(0.8f, 0.8f, 0.8f, 1f);
        [Export] public Color SandstormTint { get; set; } = new(0.85f, 0.65f, 0.35f, 1f);
        [Export] public Color HailTint { get; set; } = new(0.75f, 0.78f, 0.85f, 1f);
        [Export] public Color LeafFallTint { get; set; } = new(0.9f, 0.75f, 0.5f, 1f);
        [Export] public Color HeatwaveTint { get; set; } = new(1f, 0.9f, 0.7f, 1f);

        [ExportGroup("Lightning")]
        [Export] public bool EnableLightning { get; set; } = true;
        [Export] public double LightningMinInterval { get; set; } = 3.0;
        [Export] public double LightningMaxInterval { get; set; } = 12.0;
        [Export] public Color LightningColor { get; set; } = new(0.9f, 0.9f, 1f, 1f);

        [ExportGroup("Lightning Bolts")]
        /// <summary>Spawn procedural Line2D bolts (in addition to the screen flash) on each strike.</summary>
        [Export] public bool EnableLightningBolts { get; set; } = true;
        public enum LightningVisual
        {
            Sprite,
            Line,
            Both,
        }
        /// <summary>Side-view 2D games read lightning best as a flat sky sprite flash.</summary>
        [Export] public LightningVisual LightningMode { get; set; } = LightningVisual.Sprite;
        /// <summary>Node to parent spawned bolts to. If null, uses the weather parent. Should be a world-space Node2D.</summary>
        [Export] public NodePath? BoltContainer { get; set; }
        /// <summary>Camera shake intensity on a strike (0 = no shake). Scaled by weather intensity.</summary>
        [Export] public float LightningShakeIntensity { get; set; } = 12f;
        /// <summary>Optional lightning sprites. Empty = bundled cloud-pack lightning PNGs.</summary>
        [Export] public Texture2D[] LightningSprites { get; set; } = System.Array.Empty<Texture2D>();

        [ExportGroup("Wind")]
        [Export] public bool EnableWind { get; set; } = true;
        [Export] public Vector2 WindForce { get; set; } = Vector2.Zero;
        [Export] public float WindChangeSpeed { get; set; } = 0.5f;
        [Export] public float MaxWindMagnitude { get; set; } = 3f;
        /// <summary>Base side-view wind direction. Weather strength scales this.</summary>
        [Export] public Vector2 PrevailingWind { get; set; } = new(1f, 0f);
        /// <summary>Extra intermittent wind for Storm/Snow/Sandstorm.</summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float GustStrength { get; set; } = 0.35f;

        [ExportGroup("Overlays")]
        /// <summary>
        /// CanvasLayer index for the screen-space overlay layer. High so fog/clouds/
        /// lightning draw above the world AND follow the camera (screen-space, not
        /// world-space). Canonical 2D-weather pattern from godotshaders.com.
        /// </summary>
        [Export] public int OverlayLayerIndex { get; set; } = 100;

        [ExportGroup("Clouds")]
        [Export] public bool CloudCoverageAutoDriven { get; set; } = true;
        /// <summary>Manual override; only used when CloudCoverageAutoDriven is false.</summary>
        [Export] public float CloudCoverage { get; set; } = 0.55f;
        [Export] public float CloudDriftSpeed { get; set; } = 0.04f;

        private bool _enableClouds = true;
        [Export]
        public bool EnableClouds
        {
            get => _enableClouds;
            set
            {
                bool turningOn = value && !_enableClouds;
                bool turningOff = !value && _enableClouds;
                _enableClouds = value;
                if (turningOn && _overlayLayer != null)
                {
                    EnsureCloudOverlays(_overlayLayer);
                    // Clouds are added after the flash — keep the full-screen lightning flash topmost
                    // (the build path does this too; the runtime toggle must repeat it).
                    if (_flashOverlay != null && GodotObject.IsInstanceValid(_flashOverlay))
                        _overlayLayer.MoveChild(_flashOverlay, -1);
                }
                if (turningOff)
                {
                    // Hide (not just fade) — ProcessClouds no longer runs to zero the alpha, and a
                    // Visible ColorRect keeps executing its fragment shader. Visible=false stops it.
                    if (_cloudOverlay != null) { _cloudOverlay.Modulate = new Color(1, 1, 1, 0); _cloudOverlay.Visible = false; }
                    if (_cloudShadowOverlay != null) { _cloudShadowOverlay.Modulate = new Color(1, 1, 1, 0); _cloudShadowOverlay.Visible = false; }
                }
            }
        }
        [Export] public Color CloudColor { get; set; } = new(1f, 1f, 1f, 1f);
        [Export] public Color CloudShadowColor { get; set; } = new(0f, 0f, 0f, 0.35f);
        /// <summary>Multiplier on cloud-shadow visibility (set 0 to disable dapple).</summary>
        [Export] public float CloudShadowStrength { get; set; } = 1f;

        /// <summary>
        /// Looking DOWN at the world rather than across it. Set from the genre.
        ///
        /// The same weather has to be drawn differently depending on the camera axis, and the kit
        /// drew both identically -- which is why a top-down game's rain looked like a
        /// side-scroller's. From above you look THROUGH falling rain: it foreshortens into short
        /// near-vertical streaks with barely any horizontal travel, and what actually sells the
        /// weather is the cloud SHADOW sweeping over terrain you can see all of. Across, rain
        /// crosses the frame at an angle and the clouds themselves are the backdrop.
        /// </summary>
        [Export] public bool TopDownView { get; set; }

        /// <summary>
        /// Genre-aware 2D weather projection. <see cref="TopDownView"/> is kept for existing
        /// scenes; when it is true and this remains Side, the component treats it as TopDown.
        /// </summary>
        public enum WeatherViewMode
        {
            Side,
            TopDown,
            RpgTopDown,
            Isometric,
            CityBuilder,
        }

        [Export] public WeatherViewMode ViewMode { get; set; } = WeatherViewMode.Side;

        /// <summary>How clouds are drawn. Three techniques, none strictly better.</summary>
        public enum CloudRender
        {
            /// <summary>The procedural density field. Costs nothing in assets and tiles forever,
            /// but noise has no SILHOUETTE — it reads as fog, and at low sample resolution it
            /// reads as blocky fog. Right for haze and overcast, wrong for distinct clouds.</summary>
            Procedural,
            /// <summary>Drifting sprite clouds (<see cref="CloudSpriteLayer"/>). Drawn art has the
            /// silhouette noise cannot fake, and parallax comes free from the size variants. Needs
            /// cloud textures; the addon ships a set under textures/clouds/.</summary>
            Sprites,
            /// <summary>No cloud layer at all. Weather still tints, and precipitation still
            /// falls — for genres where an overhead cloud plane just occludes the playfield.</summary>
            None,
        }

        /// <summary>
        /// Which cloud technique this scene uses.
        ///
        /// Exposed rather than chosen for you because the right answer depends on the game, not on
        /// the engine: a side-scroller wants sprite clouds against the sky, a top-down game may
        /// want only the shadow they cast, and a puzzle game wants neither. All three paths are
        /// supported and none is a fallback for another.
        /// </summary>
        [Export] public CloudRender CloudMode { get; set; } = CloudRender.Procedural;

        /// <summary>Cloud sprites for <see cref="CloudRender.Sprites"/>. The addon ships a set
        /// under textures/clouds/ in three tones and five sizes per shape.</summary>
        [Export] public Texture2D[] CloudSprites { get; set; } = System.Array.Empty<Texture2D>();

        [ExportGroup("Transitions")]
        /// <summary>Seconds to cross-fade between weathers. 0 = instant.</summary>
        [Export] public float TransitionDuration { get; set; } = 1.5f;

        [ExportGroup("Weather Sprite Rendering")]
        /// <summary>Nearest sampling is useful for pixel games, but painterly/top-down art should
        /// stay linear so rain/snow/sand do not read as chunky pixel art.</summary>
        [Export] public bool WeatherSpritePixelArtSampling { get; set; } = false;
        /// <summary>Snap sprite weather to whole pixels only when a pixel-art project wants that
        /// hard-grid look. Leave off for painterly or higher-resolution art.</summary>
        [Export] public bool WeatherSpriteSnapToPixelGrid { get; set; } = false;

        [Signal] public delegate void WeatherChangedEventHandler(int type);
        [Signal] public delegate void LightningStruckEventHandler();

        // ── Internal nodes ──
        private CanvasLayer? _overlayLayer;   // screen-space container for fog/clouds/flash
        private CpuParticles2D? _particles;
        // Weather no longer owns a CanvasModulate — it contributes its tint to the shared
        // AmbientController, which composes it with the day/night tint. See AmbientController.
        private AmbientController? _ambient;
        private const string AmbientKey = "weather";
        private CpuParticles2D? _splashes;
        private bool _splashWanted;
        private Vector2 _splashCameraOffset = Vector2.Zero;
        private WeatherSpriteLayer? _weatherSprites;
        private ColorRect? _flashOverlay;
        private Node2D? _boltContainer;  // cached container for lightning bolts
        private readonly System.Collections.Generic.List<Node> _activeLightningBolts = new();  // track active bolts for cleanup

        // ── Internal state ──
        private double _cycleTimer;
        private double _currentWeatherDuration;   // per-weather min/max duration for AutoCycle
        private Color _weatherTintCurrent = new(1f, 1f, 1f, 1f);  // eased weather tint (pre day/night multiply)
        private double _lightningTimer;
        private bool _lightningActive;
        private double _lightningFlashTime;
        private float _gustPhase;
        private bool _nodesReady;

        public double EffectiveCycleInterval => double.IsFinite(CycleInterval) ? System.Math.Max(0.1, CycleInterval) : 60.0;
        public int EffectiveParticleCount => Mathf.Clamp(ParticleCount, 1, 5000);
        public double EffectiveLightningMinInterval => System.Math.Min(SanitizedLightningMinInterval, SanitizedLightningMaxInterval);
        public double EffectiveLightningMaxInterval => System.Math.Max(SanitizedLightningMinInterval, SanitizedLightningMaxInterval);
        public float EffectiveLightningShakeIntensity => Mathf.Max(0f, float.IsFinite(LightningShakeIntensity) ? LightningShakeIntensity : 0f);
        public float EffectiveWindChangeSpeed => Mathf.Max(0f, float.IsFinite(WindChangeSpeed) ? WindChangeSpeed : 0f);
        public float EffectiveMaxWindMagnitude => Mathf.Max(0f, float.IsFinite(MaxWindMagnitude) ? MaxWindMagnitude : 0f);
        public float EffectiveGustStrength => Mathf.Clamp(float.IsFinite(GustStrength) ? GustStrength : 0f, 0f, 1f);
        public float EffectiveCloudCoverage => Mathf.Clamp(float.IsFinite(CloudCoverage) ? CloudCoverage : 0f, 0f, 1f);
        public float EffectiveCloudDriftSpeed => Mathf.Max(0f, float.IsFinite(CloudDriftSpeed) ? CloudDriftSpeed : 0f);
        public float EffectiveCloudShadowStrength => Mathf.Max(0f, float.IsFinite(CloudShadowStrength) ? CloudShadowStrength : 0f);
        public float EffectiveCloudTextureScale => Mathf.Clamp(float.IsFinite(CloudTextureScale) ? CloudTextureScale : 1f, 0.1f, 8f);
        public float EffectiveCloudParallax => Mathf.Clamp(float.IsFinite(CloudParallax) ? CloudParallax : 0f, 0f, 1f);
        public float EffectiveCloudShadowParallax => Mathf.Clamp(float.IsFinite(CloudShadowParallax) ? CloudShadowParallax : 1f, 0f, 1f);
        public float EffectiveTransitionDuration => Mathf.Max(0f, float.IsFinite(TransitionDuration) ? TransitionDuration : 0f);
        public float EffectiveTargetIntensity => Mathf.Clamp(float.IsFinite(TargetIntensity) ? TargetIntensity : 0f, 0f, 1f);
        public float EffectiveIntensityLerpSpeed => Mathf.Max(0f, float.IsFinite(IntensityLerpSpeed) ? IntensityLerpSpeed : 1.5f);

        private double SanitizedLightningMinInterval => double.IsFinite(LightningMinInterval) ? System.Math.Max(0.0, LightningMinInterval) : 3.0;
        private double SanitizedLightningMaxInterval => double.IsFinite(LightningMaxInterval) ? System.Math.Max(0.0, LightningMaxInterval) : 12.0;

        /// <summary>
        /// Frame-lerp rate for the weather→ambient transition. Higher = snappier.
        /// TransitionDuration is kept as a user-facing convenience; this is the
        /// per-frame implementation of "ease toward target" that also keeps working
        /// when the day/night tint is multiplying every frame.
        /// </summary>
        private const float TransitionLerpRate = 2.0f;

        public override void _Ready()
        {
            base._Ready();
            // Pull initial settings from GameInfo if present (same pattern as PlatformerController).
            var info = Beep.GameBuilder.GameInfo.Instance;
            if (info != null)
            {
                CurrentWeather = info.DefaultWeather;
                // AutoCycle from the genre too. Without this every genre started at its default
                // weather and STAYED there -- with all ten defaulting to Clear, the whole system
                // was enabled and produced nothing, which is what "weather isn't happening" means.
                AutoCycle = info.AutoCycleWeather;
                ViewMode = info.WeatherViewMode;
                TopDownView = info.TopDownView || ViewMode != WeatherViewMode.Side;
                IsActive = info.EnableWeather;
                // info.EnableDayNightCycle now configures the standalone DayNightCycleComponent,
                // not this one — day/night was moved out. See BeepGenreScene / DayNightCycleComponent.
            }
            // Register in the discovery group so WindFieldComponent and
            // WeatherHUDComponent can auto-find this system without a NodePath.
            if (!IsInGroup("weather_system")) AddToGroup("weather_system");
            CallDeferred(nameof(DeferredInit));
        }

        private void DeferredInit()
        {
            // Runtime only. EnsureNodes spawns WeatherParticles and the overlay layer into
            // the PARENT, so in the editor it injected runtime-only nodes into whatever
            // scene you opened (this component is in all ten genre main scenes) and warned
            // about a missing CanvasModulate every time. Only SetWeather was guarded.
            if (Engine.IsEditorHint()) return;
            if (!IsActive) return;

            if (!EnsureNodes()) return;
            _nodesReady = true;
            if (IsActive) SetWeather(CurrentWeather);
        }

        private bool EnsureNodes()
        {
            if (GetParent() is not Node parent) return false;
            if (parent is not Node2D)
                GD.PushWarning($"[Weather] Should be a child of a Node2D (world root) for particles/ambient to render correctly. Parent was {parent.GetType().Name}.");

            // Particle system (precipitation / leaves / sand).
            // Lives in WORLD space (parent), not the overlay layer, so rain falls
            // through actual level coordinates and "moves naturally as the camera
            // moves" via local coords — per the godotshaders.com guidance.
            _particles = parent.GetNodeOrNull<CpuParticles2D>("WeatherParticles");
            if (_particles == null)
            {
                _particles = new CpuParticles2D { Name = "WeatherParticles", Emitting = false };
                if (ParticleMaterial != null)
                    _particles.Material = ParticleMaterial;
                parent.AddChild(_particles);
            }
            ConfigureParticleEmitter();

            // Ambient tint goes through the shared AmbientController, which owns the one
            // CanvasModulate and composes weather with the day/night cycle. (Weather used to
            // grab its own CanvasModulate here and multiply in day/night itself, which is why
            // it fought the standalone day/night component over the single allowed modulate.)
            _ambient = AmbientController.ForTree(this);

            // ── Screen-space overlay layer ──
            // All full-screen overlays (fog, clouds, lightning flash) live inside a
            // CanvasLayer with a high layer index. This is the canonical 2D weather
            // pattern: the layer is camera-independent, so fog/clouds/flash cover the
            // viewport regardless of scroll, instead of scrolling with the world.
            _overlayLayer = parent.GetNodeOrNull<CanvasLayer>("WeatherOverlayLayer");
            if (_overlayLayer == null)
            {
                _overlayLayer = new CanvasLayer { Name = "WeatherOverlayLayer", Layer = OverlayLayerIndex };
                parent.AddChild(_overlayLayer);
            }
            else _overlayLayer.Layer = OverlayLayerIndex;
            Node overlayRoot = _overlayLayer;

            // Fog is drawn by DynamicFogLayer now, not here — it was rendered twice (weather
            // had its own overlay AND the scene had a Fog node). DynamicFogLayer reads this
            // system's WeatherIntensity/CurrentWeather.

            // Lightning flash overlay (full-screen ColorRect for brief brightness bursts).
            _flashOverlay = overlayRoot.GetNodeOrNull<ColorRect>("WeatherFlash");
            if (_flashOverlay == null)
            {
                _flashOverlay = new ColorRect
                {
                    Name = "WeatherFlash",
                    MouseFilter = Godot.Control.MouseFilterEnum.Ignore,
                    Color = new Color(0, 0, 0, 0)
                };
                _flashOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                overlayRoot.AddChild(_flashOverlay);
            }

            _lightningTimer = GD.RandRange(EffectiveLightningMinInterval, EffectiveLightningMaxInterval);

            // Cache the bolt container so we don't search every lightning strike.
            if (BoltContainer != null)
                _boltContainer = GetNodeOrNull<Node2D>(BoltContainer);
            _boltContainer ??= parent as Node2D;

            // Cloud + cloud-shadow overlays (built/owned by the Overlays partial).
            if (EnableClouds) EnsureCloudOverlays(overlayRoot);
            EnsureWeatherSpriteLayer(overlayRoot);

            // The lightning flash was added before the clouds, so it drew UNDER them — a full-screen
            // white-out must be topmost. Move it to the end after the cloud/rain overlays exist.
            if (_flashOverlay != null && GodotObject.IsInstanceValid(_flashOverlay))
                overlayRoot.MoveChild(_flashOverlay, -1);

            return _particles != null && _overlayLayer != null && _weatherSprites != null;
        }

        private void EnsureWeatherSpriteLayer(Node parent)
        {
            _weatherSprites = parent.GetNodeOrNull<WeatherSpriteLayer>("WeatherSprites");
            if (_weatherSprites == null)
            {
                _weatherSprites = new WeatherSpriteLayer { Name = "WeatherSprites", Visible = false };
                parent.AddChild(_weatherSprites);
            }
            ApplyWeatherSpriteTextures();
        }

        private void ApplyWeatherSpriteTextures()
        {
            if (_weatherSprites == null) return;
            _weatherSprites.RainTexture = RainTexture ?? (UseBundledParticleTextures ? Bundled("rain_drop_2d.png") : null);
            _weatherSprites.SplashTexture = SplashTexture ?? (UseBundledParticleTextures ? Bundled("rain_splash_2d.png") : null);
            _weatherSprites.SnowTexture = SnowTexture ?? (UseBundledParticleTextures ? Bundled("snow_flake_2d.png") : null);
            _weatherSprites.HailTexture = HailTexture ?? (UseBundledParticleTextures ? Bundled("hail_pellet_2d.png") : null);
            _weatherSprites.SandTexture = SandTexture ?? (UseBundledParticleTextures ? Bundled("sand_mote_2d.png") : null);
            _weatherSprites.UsePixelArtSampling = WeatherSpritePixelArtSampling;
            _weatherSprites.SnapToPixelGrid = WeatherSpriteSnapToPixelGrid;
        }

        /// <summary>
        /// Configure the CpuParticles2D emitter as a screen-spanning box so
        /// snow/hail/sand/leaves cover the viewport. Called once on build and again from
        /// _Process when the viewport resizes (early-outs if no change). Uses local
        /// coords so weather moves naturally with the camera.
        /// </summary>
        private Vector2 _lastEmitSize = Vector2.Zero;
        private void ConfigureParticleEmitter()
        {
            if (_particles == null) return;
            var vp = GetViewport();
            // GetVisibleRect().Size is a Vector2 in the Godot 4.7 C# binding.
            Vector2 size = vp != null ? vp.GetVisibleRect().Size : new Vector2(1280, 720);
            if (size.IsEqualApprox(_lastEmitSize)) return;
            _lastEmitSize = size;

            // Screen-spanning rectangle emission so particles are already in
            // flight across the full viewport width when they enter view.
            _particles.EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle;
            // A BOX OVER THE VIEW, not a 16px strip at the top.
            //
            // Godot's own guidance for 2D rain is an emission box slightly wider than the
            // viewport (godotengine.org GPUParticles article; the ParticleProcessMaterial rain
            // recipe). The strip meant every drop had to survive a full fall from above the
            // screen to cover it, so the picture was thin at the top, empty at the bottom, and
            // fell apart entirely the moment the fall was slowed for a top-down camera.
            //
            // Filling the box means drops exist THROUGHOUT the view from the first frame, which
            // is what real rain looks like and what removes the dependency on lifetime tuning.
            _particles.EmissionRectExtents = new Vector2(size.X * 0.6f, size.Y * 0.6f);

            // The splash band is sized off the same viewport, so re-place it whenever the
            // emission box moves rather than only on a weather change — otherwise a resize
            // leaves the splashes at the old ground line.
            ConfigureSplashes(CurrentWeather, size);

            // Local coords → emission is pinned to the emitter's transform; PositionEmitterAtCamera
            // moves the emitter to the camera each frame so the field tracks the view.
            _particles.LocalCoords = true;
        }

        /// <summary>Center the emission strip on the active Camera2D, at the top of the visible
        /// rect, so precipitation always fills the view as the camera scrolls. No-op when there is
        /// no active camera (a fixed-view scene keeps the emitter at its parent origin).</summary>
        private void PositionEmitterAtCamera()
        {
            if (GetViewport()?.GetCamera2D() is not Camera2D cam) return;
            Vector2 center = cam.GetScreenCenterPosition();
            if (_particles != null)
                _particles.GlobalPosition = new Vector2(center.X, center.Y - _lastEmitSize.Y * 0.5f);
            if (_splashes != null && _splashes.IsInsideTree())
                _splashes.GlobalPosition = center + _splashCameraOffset;
        }

        private void UpdateWeatherSpriteLayer()
        {
            if (_weatherSprites == null) return;

            bool wantsSprites = CurrentWeather is WeatherType.Rain or WeatherType.Storm
                or WeatherType.Snow or WeatherType.Hail or WeatherType.Sandstorm;
            WeatherViewMode view = EffectiveViewMode();
            Vector2 size = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1280, 720);
            Camera2D? camera = GetViewport()?.GetCamera2D();

            _weatherSprites.Field = size;
            _weatherSprites.ViewMode = view;
            _weatherSprites.Kind = CurrentWeather switch
            {
                WeatherType.Storm => WeatherSpriteLayer.PixelWeatherKind.Storm,
                WeatherType.Snow => WeatherSpriteLayer.PixelWeatherKind.Snow,
                WeatherType.Hail => WeatherSpriteLayer.PixelWeatherKind.Hail,
                WeatherType.Sandstorm => WeatherSpriteLayer.PixelWeatherKind.Sandstorm,
                _ => WeatherSpriteLayer.PixelWeatherKind.Rain,
            };
            _weatherSprites.Wind = WindForce;
            _weatherSprites.CameraCenter = camera?.GetScreenCenterPosition() ?? Vector2.Zero;
            _weatherSprites.CameraZoom = camera?.Zoom ?? Vector2.One;
            _weatherSprites.UsePixelArtSampling = WeatherSpritePixelArtSampling;
            _weatherSprites.SnapToPixelGrid = WeatherSpriteSnapToPixelGrid;
            _weatherSprites.Intensity = wantsSprites ? _intensityCurrent * (1f - ShelterFactor) : 0f;
            float spriteDensity = CurrentWeather switch
            {
                WeatherType.Storm => 0.55f,
                WeatherType.Rain => 0.42f,
                WeatherType.Snow => 0.24f,
                WeatherType.Hail => 0.22f,
                WeatherType.Sandstorm => 0.48f,
                _ => 0.25f,
            };
            _weatherSprites.MaxSprites = Mathf.Clamp((int)(EffectiveParticleCount * spriteDensity), 60, 360);
            ApplyWeatherSpriteTextures();
            _weatherSprites.Visible = wantsSprites && _weatherSprites.Intensity > 0.01f;
        }

        public override void _Process(double delta)
        {
            // [Tool] component: don't run the intensity engine in the editor — ProcessIntensity
            // registers RenderingServer global shader params and emits IntensityChanged at edit
            // time. DeferredInit is already editor-skipped, so the nodes it drives are null anyway.
            if (Engine.IsEditorHint() || !IsActive) return;
            if (!_nodesReady)
            {
                if (!EnsureNodes()) return;
                _nodesReady = true;
                SetWeather(CurrentWeather);
            }

            // (Day/night progression moved to DayNightCycleComponent.)
            float dt = DeltaSeconds(delta);

            // Intensity engine — scales particles/fog/wind, publishes global
            // shader uniforms. MUST run before the ambient tint below so the
            // intensity is current when we lerp clear→weather by it.
            ProcessIntensity(delta);

            // Combined ambient tint: lerp clear→weather by intensity (so a 30%
            // storm is only 30% as dark), then multiply by the day/night tint so
            // storms at night go properly dark. Per-frame because both factors move.
            if (_ambient != null)
            {
                Color weatherTarget = GetTintFor(CurrentWeather);
                _weatherTintCurrent = _weatherTintCurrent.Lerp(weatherTarget, Mathf.Clamp(dt * TransitionLerpRate, 0f, 1f));
                // Intensity gates how far toward the weather tint we go. The day/night
                // multiply is no longer done here — the AmbientController composes this
                // weather layer with the day/night layer, so a storm at midnight still
                // reads dark without the two systems fighting over the CanvasModulate.
                Color intensityTint = new Color(
                    Mathf.Lerp(ClearTint.R, _weatherTintCurrent.R, _intensityCurrent),
                    Mathf.Lerp(ClearTint.G, _weatherTintCurrent.G, _intensityCurrent),
                    Mathf.Lerp(ClearTint.B, _weatherTintCurrent.B, _intensityCurrent), 1f);
                _ambient.SetContribution(AmbientKey, intensityTint);
            }

            // Auto-cycle.
            if (AutoCycle)
            {
                _cycleTimer += dt;
                double duration = _currentWeatherDuration > 0 ? _currentWeatherDuration : EffectiveCycleInterval;
                if (_cycleTimer >= duration)
                {
                    _cycleTimer = 0;
                    SetWeather(PickWeightedWeather());
                }
            }

            // Wind drift — pushes particle gravity (clouds read WindForce in ProcessClouds).
            if (EnableWind)
            {
                UpdateWeatherWind(dt);
                ApplyWindToParticles();
            }

            UpdateWeatherSpriteLayer();

            // Re-fit the particle emitter if the viewport was resized.
            ConfigureParticleEmitter();

            // Follow the camera so precipitation covers the view in a scrolling level. LocalCoords
            // keeps emission pinned to the emitter's transform, so without this the emitter sat at
            // world origin and rain/snow/hail only fell near (0,0). Mirrors what the cloud/fog
            // overlays already do via the camera offset. Position the strip at the top of the view.
            PositionEmitterAtCamera();

            // Cloud drift + wind-direction sync.
            if (EnableClouds) ProcessClouds(dt);

            // Lightning (Storm only).
            ProcessLightning(dt);
        }

        private void UpdateWeatherWind(double delta)
        {
            WeatherViewMode view = EffectiveViewMode();
            Vector2 dir = float.IsFinite(PrevailingWind.X) && float.IsFinite(PrevailingWind.Y) && PrevailingWind.LengthSquared() > 0.0001f
                ? PrevailingWind.Normalized()
                : Vector2.Right;

            if (view == WeatherViewMode.Isometric)
                dir = new Vector2(dir.X >= 0f ? 0.86f : -0.86f, 0.5f).Normalized();
            else if (IsTopDownLike(view))
                dir = new Vector2(dir.X * 0.35f, 0.18f).Normalized();

            float weatherWind = CurrentWeather switch
            {
                WeatherType.Clear => 0.08f,
                WeatherType.Cloudy => 0.35f,
                WeatherType.Rain => 0.65f,
                WeatherType.Snow => 1.25f,
                WeatherType.Storm => 1.8f,
                WeatherType.Fog => 0.12f,
                WeatherType.Sandstorm => 1.65f,
                WeatherType.Hail => 1.1f,
                WeatherType.LeafFall => 0.55f,
                WeatherType.Heatwave => 0.08f,
                _ => 0.25f,
            };
            if (view == WeatherViewMode.CityBuilder)
                weatherWind *= 0.65f;
            else if (view == WeatherViewMode.RpgTopDown)
                weatherWind *= 0.85f;

            float gustStrength = EffectiveGustStrength;
            float gustWeight = CurrentWeather is WeatherType.Storm or WeatherType.Snow
                or WeatherType.Sandstorm or WeatherType.Hail ? gustStrength : gustStrength * 0.35f;
            float dt = DeltaSeconds(delta);
            _gustPhase += dt * Mathf.Lerp(0.5f, 1.6f, gustWeight);
            float gust = 1f + Mathf.Sin(_gustPhase * 1.7f) * gustWeight
                         + Mathf.Sin(_gustPhase * 3.9f + 1.4f) * gustWeight * 0.35f;

            Vector2 target = dir * Mathf.Min(EffectiveMaxWindMagnitude, weatherWind * Mathf.Max(0f, gust) * _intensityCurrent);
            WindForce = WindForce.Lerp(target, Mathf.Clamp(EffectiveWindChangeSpeed * dt, 0f, 1f));
        }

        private static float DeltaSeconds(double delta) =>
            double.IsFinite(delta) ? Mathf.Max(0f, (float)delta) : 0f;

        private void ApplyWindToParticles()
        {
            if (_particles == null) return;
            WeatherViewMode view = EffectiveViewMode();
            float x = WindForce.X * 120f;
            switch (CurrentWeather)
            {
                case WeatherType.Snow:
                    _particles.Gravity = new Vector2(x * 0.85f, _particles.Gravity.Y);
                    _particles.Direction = view == WeatherViewMode.Isometric
                        ? new Vector2(Mathf.Clamp(WindForce.X * 0.28f, -0.7f, 0.7f), 0.82f)
                        : new Vector2(Mathf.Clamp(WindForce.X * 0.45f, -0.9f, 0.9f), 1f);
                    break;
                case WeatherType.Storm:
                    _particles.Gravity = new Vector2(x * 0.65f, _particles.Gravity.Y);
                    _particles.Direction = view == WeatherViewMode.Isometric
                        ? new Vector2(Mathf.Clamp(0.55f + WindForce.X * 0.12f, -0.8f, 0.8f), 0.9f)
                        : new Vector2(Mathf.Clamp(0.25f + WindForce.X * 0.18f, -0.75f, 0.75f), 1f);
                    break;
                case WeatherType.Sandstorm:
                    _particles.Gravity = new Vector2(Mathf.Sign(WindForce.X == 0f ? 1f : WindForce.X) * 300f, 0);
                    break;
                default:
                    _particles.Gravity = new Vector2(x * 0.45f, _particles.Gravity.Y);
                    break;
            }
        }

        private WeatherViewMode EffectiveViewMode()
        {
            if (ViewMode == WeatherViewMode.Side && TopDownView) return WeatherViewMode.TopDown;
            return ViewMode;
        }

        private static bool IsTopDownLike(WeatherViewMode view) =>
            view is WeatherViewMode.TopDown or WeatherViewMode.RpgTopDown
                or WeatherViewMode.Isometric or WeatherViewMode.CityBuilder;

        // ════════════════════════════════════════════════════════════════
        // Weather switching
        // ════════════════════════════════════════════════════════════════

        public void SetWeather(WeatherType type)
        {
            CurrentWeather = type;

            // Snap intensity to full for direct SetWeather calls. TransitionTo
            // overrides this to fade in gradually.
            if (!_transitioning)
            {
                float defaultIntensity = DefaultIntensityFor(type);
                TargetIntensity = defaultIntensity;
                WeatherIntensity = defaultIntensity;
                _intensityCurrent = defaultIntensity;
            }

            // Per-weather AutoCycle duration: storms are brief, fog lingers.
            _currentWeatherDuration = (double)GD.RandRange(
                GetWeatherMinDuration(type), GetWeatherMaxDuration(type));
            _cycleTimer = 0;

            // Particle configuration.
            bool usesParticles = type is WeatherType.LeafFall;
            if (_particles != null)
            {
                _particles.Emitting = usesParticles;
                if (usesParticles)
                {
                _particles.Amount = EffectiveParticleCount;
                    _lastParticleAmount = -1;   // force ProcessIntensity to re-apply the scaled amount
                    ConfigureParticles(type);
                }
            }
            UpdateWeatherSpriteLayer();

            // The old particle splash emitter is kept dormant; rain/storm impacts now come from
            // WeatherSpriteLayer so all wet-weather pixels belong to one 2D renderer.
            {
                var vp = GetViewport();
                ConfigureSplashes(type, vp != null ? vp.GetVisibleRect().Size : new Vector2(1280, 720));
            }

            // Lightning enabled only for Storm.
            _lightningTimer = GD.RandRange(EffectiveLightningMinInterval, EffectiveLightningMaxInterval);
            _lightningActive = false;

            EmitSignal(SignalName.WeatherChanged, (int)type);
        }

        private static float DefaultIntensityFor(WeatherType type) => type switch
        {
            WeatherType.Clear => 0f,
            WeatherType.Cloudy => 0.55f,
            WeatherType.Rain => 0.78f,
            WeatherType.Snow => 0.68f,
            WeatherType.Storm => 1f,
            WeatherType.Fog => 0.65f,
            WeatherType.Sandstorm => 0.9f,
            WeatherType.Hail => 0.85f,
            WeatherType.LeafFall => 0.45f,
            WeatherType.Heatwave => 0.70f,
            _ => 0.5f,
        };

        /// <summary>
        /// Keep precipitation in one 2D visual layer.
        ///
        /// Earlier tuning varied opacity strongly per particle to fake near/far depth. That reads
        /// like a 3D volume camera, which is wrong for this addon. A narrow alpha ramp still breaks
        /// up mechanical repetition without turning rain into foreground/background streaks.
        /// </summary>
        private void ApplyFlatAlphaVariation(float lowAlpha, float highAlpha)
        {
            if (_particles == null) return;
            var g = new Gradient();
            g.SetOffset(0, 0f);
            g.SetOffset(1, 1f);
            g.SetColor(0, new Color(1f, 1f, 1f, lowAlpha));
            g.SetColor(1, new Color(1f, 1f, 1f, highAlpha));
            _particles.ColorInitialRamp = g;
        }

        /// <summary>
        /// Impact SPLASHES — the cue that separates rain from a screen of falling lines.
        ///
        /// Rain reads as rain because it LANDS. Ours never did: one emitter dropped streaks that
        /// vanished at the end of their lifetime with nothing to show they had arrived, so however
        /// well the drops themselves were tuned the picture stayed a moving texture rather than
        /// weather happening to a place.
        ///
        /// A second short-lived emitter does it. The splashes are NOT parented to the drops (no
        /// collision, no per-drop bookkeeping) — they are an independent scatter at the same
        /// density, which at rain speeds is indistinguishable and costs nothing.
        ///
        /// WHERE they land is the view axis. Side-on, water hits the ground: a strip along the
        /// bottom. Top-down, you are looking AT the ground, so it hits everywhere you can see.
        /// </summary>
        private void ConfigureSplashes(WeatherType type, Vector2 size)
        {
            bool wants = false;
            WeatherViewMode view = EffectiveViewMode();
            _splashes ??= new CpuParticles2D { Name = "WeatherSplashes", LocalCoords = true };

            // ADOPT, don't assume. The first version created the node with
            // `_particles?.GetParent()?.AddChild(_splashes)` — and when that ran before
            // EnsureNodes had made _particles, the `?.` turned AddChild into a NO-OP. The field
            // was non-null, so every later call skipped creation and configured an ORPHAN that
            // was never in the tree and never drew a pixel. Nothing logged; the splashes simply
            // did not exist. Re-checking parentage each time is what makes call order irrelevant.
            if (!_splashes.IsInsideTree())
            {
                var host = _particles?.GetParent() ?? GetParent();
                if (host == null)
                {
                    GD.PushWarning($"[{Name}] Rain splashes have nowhere to attach (no parent node) "
                                 + "— impacts will not render. Parent the weather system to a Node2D.");
                    return;
                }
                host.AddChild(_splashes);
            }

            _splashWanted = wants;
            if (!wants) { _splashes.Emitting = false; return; }


            bool storm = type == WeatherType.Storm;
            _splashes.Texture = SplashTexture ?? (UseBundledParticleTextures ? Bundled("rain_splash_2d.png") : null);
            _splashes.Amount = Mathf.Max(1, (int)(EffectiveParticleCount * (storm ? 0.22f : 0.15f)));
            _splashes.Lifetime = 0.18f;
            _splashes.EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle;

            if (IsTopDownLike(view))
            {
                // Looking down at the ground — impacts scatter across the whole visible field.
                _splashCameraOffset = Vector2.Zero;
                _splashes.Position = _splashCameraOffset;
                _splashes.EmissionRectExtents = view == WeatherViewMode.Isometric
                    ? new Vector2(size.X * 0.55f, size.Y * 0.36f)
                    : new Vector2(size.X * 0.55f, size.Y * (view == WeatherViewMode.CityBuilder ? 0.48f : 0.55f));
            }
            else
            {
                // Side-on — a band at the foot of the view, where the ground plane reads.
                _splashCameraOffset = new Vector2(0, size.Y * 0.42f);
                _splashes.Position = _splashCameraOffset;
                _splashes.EmissionRectExtents = new Vector2(size.X * 0.55f, size.Y * 0.06f);
            }

            // Flat impact flecks on the 2D playfield. No upward spray arc: that was the main cue
            // making the splash feel like a 3D camera looking across the ground.
            _splashes.Direction = view switch
            {
                WeatherViewMode.Isometric => new Vector2(0.8f, 0.28f),
                WeatherViewMode.Side => new Vector2(0, -0.15f),
                _ => Vector2.Right,
            };
            _splashes.Spread = IsTopDownLike(view) ? (view == WeatherViewMode.Isometric ? 95f : 180f) : 95f;
            _splashes.InitialVelocityMin = 4f;
            _splashes.InitialVelocityMax = storm ? 18f : 12f;
            _splashes.Gravity = Vector2.Zero;
            _splashes.ScaleAmountMin = 0.55f;
            _splashes.ScaleAmountMax = storm ? 1.05f : 0.85f;
            _splashes.Color = new Color(0.70f, 0.84f, 1f, storm ? 0.46f : 0.36f);

            // Fade out over the splash's life — a droplet that blinks out at full opacity is the
            // thing that makes cheap splash effects read as sparkles.
            var fade = new Gradient();
            fade.SetOffset(0, 0f);
            fade.SetOffset(1, 1f);
            fade.SetColor(0, new Color(1, 1, 1, 1));
            fade.SetColor(1, new Color(1, 1, 1, 0));
            _splashes.ColorRamp = fade;

            PositionEmitterAtCamera();
        }

        private void ConfigureParticles(WeatherType type)
        {
            if (_particles == null) return;

            // Cleared up front so a type with no depth call (Fog, LeafFall) does not inherit the
            // previous type's ramp. Weather changes at runtime, so a stale ramp would silently
            // recolour the next precipitation — the kind of leak that only shows after a cycle.
            _particles.ColorInitialRamp = null;

            // The sprite, alongside the motion/colour each case tunes below. Without this
            // every weather type drew as a white square no matter how well-tuned the rest
            // was. An explicit export always wins; otherwise fall back to the bundled
            // sprite (unless that's switched off).
            _particles.Texture = type switch
            {
                WeatherType.Rain or WeatherType.Storm => RainTexture ?? Fallback("rain_drop_2d.png"),
                WeatherType.Snow                      => SnowTexture ?? Fallback("snow_flake_2d.png"),
                WeatherType.Hail                      => HailTexture ?? Fallback("hail_pellet_2d.png"),
                WeatherType.Sandstorm                 => SandTexture ?? Fallback("sand_mote_2d.png"),
                // No bundled leaf sprite exists — a circle would read as snow.
                WeatherType.LeafFall                  => LeafTexture,
                _                                     => null
            };

            // LeafFall is the one type with no bundled fallback (a circle would read as snow), so an
            // unset LeafTexture leaves CpuParticles2D drawing plain squares. Cosmetic, but say so
            // rather than let it surprise — fired only on a weather change, so it isn't spammy.
            if (type == WeatherType.LeafFall && LeafTexture == null)
                GD.PushWarning($"[{Name}] LeafFall weather has no LeafTexture assigned — falling leaves render as plain squares (the addon ships no leaf sprite). Assign LeafTexture to fix.");

            Texture2D? Fallback(string file) => UseBundledParticleTextures ? Bundled(file) : null;

            // THE VIEW AXIS, applied after the per-type switch below sets the side-on values.
            // Foreshortening is the whole difference: from above, a drop travels mostly AWAY from
            // the camera, so its on-screen streak is short, slow and near-vertical however hard it
            // is actually falling. Leaving the side-on numbers made every top-down genre look like
            // a platformer in the rain.
            void ApplyViewAxis()
            {
                WeatherViewMode view = EffectiveViewMode();
                if (view == WeatherViewMode.Side) return;

                if (view == WeatherViewMode.Isometric)
                {
                    _particles.Direction = new Vector2(0.62f, 0.78f);
                    _particles.Spread = Mathf.Min(_particles.Spread, 7f);
                    _particles.Gravity = new Vector2(_particles.Gravity.Y * 0.28f, _particles.Gravity.Y * 0.45f);
                    _particles.InitialVelocityMin *= 0.55f;
                    _particles.InitialVelocityMax *= 0.55f;
                    _particles.ScaleAmountMin *= 0.9f;
                    _particles.ScaleAmountMax *= 0.9f;
                    _particles.Lifetime *= 1.7f;
                    return;
                }

                float cityMul = view == WeatherViewMode.CityBuilder ? 0.7f : 1f;
                _particles.Direction = new Vector2(_particles.Direction.X * 0.25f, 1f);
                _particles.Spread = Mathf.Min(_particles.Spread, view == WeatherViewMode.RpgTopDown ? 8f : 6f);
                _particles.Gravity = new Vector2(0, _particles.Gravity.Y * 0.35f * cityMul);
                _particles.InitialVelocityMin *= 0.45f * cityMul;
                _particles.InitialVelocityMax *= 0.45f * cityMul;
                _particles.ScaleAmountMin *= view == WeatherViewMode.RpgTopDown ? 0.95f : 0.8f;
                _particles.ScaleAmountMax *= view == WeatherViewMode.RpgTopDown ? 0.95f : 0.8f;
                _particles.Lifetime *= view == WeatherViewMode.CityBuilder ? 2.4f : 2.0f;
            }

            WeatherViewMode viewMode = EffectiveViewMode();
            switch (type)
            {
                case WeatherType.Rain:
                    _particles.Direction = new Vector2(0.18f, 1f);
                    _particles.Spread = 16f;
                    _particles.Gravity = new Vector2(0, 360);
                    _particles.InitialVelocityMax = 300;
                    _particles.InitialVelocityMin = 150;
                    _particles.ScaleAmountMin = 0.85f;
                    _particles.ScaleAmountMax = 1.25f;
                    _particles.Color = new Color(0.72f, 0.86f, 1f, 0.72f);
                    ApplyFlatAlphaVariation(0.7f, 1f);
                    break;
                case WeatherType.Snow:
                    bool topLike = IsTopDownLike(viewMode);
                    _particles.Direction = topLike ? new Vector2(0.05f, 1f) : new Vector2(0.32f, 1f);
                    _particles.Spread = topLike ? (viewMode == WeatherViewMode.RpgTopDown ? 34f : 26f) : 44f;
                    _particles.Gravity = new Vector2(topLike ? 0 : 70, topLike ? 28 : 46);
                    _particles.InitialVelocityMin = topLike ? 10 : 26;
                    _particles.InitialVelocityMax = topLike ? 42 : 82;
                    _particles.ScaleAmountMin = topLike ? 0.95f : 0.85f;
                    _particles.ScaleAmountMax = topLike ? 1.45f : 1.25f;
                    _particles.Color = new Color(0.92f, 0.98f, 1f, topLike ? 0.9f : 0.82f);
                    ApplyFlatAlphaVariation(0.82f, 1f);
                    break;
                case WeatherType.Storm:
                    _particles.Direction = new Vector2(0.32f, 1f);
                    _particles.Spread = 18f;
                    _particles.Gravity = new Vector2(0, 460);
                    _particles.InitialVelocityMax = 380;
                    _particles.InitialVelocityMin = 210;
                    _particles.ScaleAmountMin = 0.95f;
                    _particles.ScaleAmountMax = 1.38f;
                    _particles.Color = new Color(0.62f, 0.78f, 1f, 0.82f);
                    ApplyFlatAlphaVariation(0.76f, 1f);
                    break;
                case WeatherType.Sandstorm:
                    _particles.Direction = new Vector2(1f, 0.08f);
                    _particles.Spread = 22f;
                    _particles.Gravity = new Vector2(220, 18);
                    _particles.InitialVelocityMin = 90;
                    _particles.InitialVelocityMax = 260;
                    _particles.ScaleAmountMin = 0.9f;
                    _particles.ScaleAmountMax = 1.65f;
                    _particles.Color = new Color(0.88f, 0.68f, 0.40f, 0.68f);
                    ApplyFlatAlphaVariation(0.58f, 1f);
                    break;
                case WeatherType.Hail:
                    _particles.Direction = new Vector2(0.18f, 1f);
                    _particles.Spread = 7f;
                    _particles.Gravity = new Vector2(0, 760);
                    _particles.InitialVelocityMin = 260;
                    _particles.InitialVelocityMax = 560;
                    _particles.ScaleAmountMin = 1.0f;
                    _particles.ScaleAmountMax = 1.45f;
                    _particles.Color = new Color(0.88f, 0.92f, 1f, 0.95f);
                    ApplyFlatAlphaVariation(0.82f, 1f);
                    break;
                case WeatherType.LeafFall:
                    _particles.Direction = new Vector2(0.2f, 1f);
                    _particles.Spread = 45f;
                    _particles.Gravity = new Vector2(0, 30);
                    _particles.InitialVelocityMax = 40;
                    _particles.ScaleAmountMin = 1f;
                    _particles.ScaleAmountMax = 2f;
                    _particles.Color = new Color(0.8f, 0.5f, 0.2f, 0.8f);
                    // turbulence not available in this binding
                    break;
            }

            // Emission is a thin strip at the top of the view (PositionEmitterAtCamera), so each
            // particle must LIVE long enough to fall across the whole viewport — otherwise slow
            // types (snow/leaves) die near the top and only the top band is covered. Fast precip
            // crosses quickly; the horizontal sandstorm crosses the width. Amount is fixed, so a
            // longer lifetime just lowers density, which is correct for slow, sparse snow/leaves.
            _particles.Lifetime = type switch
            {
                WeatherType.Rain                      => 1.7f,
                WeatherType.Storm                     => 1.45f,
                WeatherType.Hail                      => 1.2f,
                WeatherType.Snow                      => IsTopDownLike(viewMode) ? 12f : 7.5f,
                WeatherType.LeafFall                  => 16f,
                WeatherType.Sandstorm                 => 4f,
                _                                     => 1f
            };

            // AFTER the switch and base lifetime: the cases above are authored side-on, and this
            // foreshortens them for a top-down camera.
            ApplyViewAxis();
        }

        // ════════════════════════════════════════════════════════════════
        // Lightning
        // ════════════════════════════════════════════════════════════════

        private void ProcessLightning(double delta)
        {
            float dt = DeltaSeconds(delta);
            if (!EnableLightning || CurrentWeather != WeatherType.Storm)
            {
                if (_flashOverlay != null && _flashOverlay.Color.A > 0)
                    _flashOverlay.Color = new Color(0, 0, 0, 0);
                return;
            }

            if (_lightningActive)
            {
                _lightningFlashTime += dt;
                // Scaled by THIS bolt's strength — see LastBoltStrength. A fixed-amplitude
                // envelope makes every strike the same distance away, which is the tell that
                // it is an effect on a timer rather than a storm.
                float intensity = FlashEnvelope((float)_lightningFlashTime) * LastBoltStrength;
                if (_lightningFlashTime >= FlashDuration)
                {
                    intensity = 0f;
                    _lightningActive = false;
                    _lightningTimer = GD.RandRange(EffectiveLightningMinInterval, EffectiveLightningMaxInterval);
                }
                if (_flashOverlay != null)
                    _flashOverlay.Color = new Color(LightningColor.R, LightningColor.G, LightningColor.B, intensity * 0.8f);
            }
            else
            {
                _lightningTimer -= dt;
                if (_lightningTimer <= 0)
                {
                    _lightningActive = true;
                    _lightningFlashTime = 0;
                    // Roll this strike's DISTANCE before announcing it, so every listener
                    // (thunder delay, sample choice, camera shake) reads the same bolt.
                    LastBoltStrength = (float)GD.RandRange(0.4, 1.0);
                    EmitSignal(SignalName.LightningStruck);

                    // The sprite path is the default for 2D platformer scenes: it uses the same
                    // authored weather art pack as the clouds. The Line2D bolt is still available.
                    if (EnableLightningBolts)
                    {
                        if (LightningMode is LightningVisual.Sprite or LightningVisual.Both)
                            SpawnLightningSprite();
                        if (LightningMode is LightningVisual.Line or LightningVisual.Both)
                            SpawnLightningBolt();
                    }
                    TriggerCameraShake();
                }
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  Lightning bolt + camera shake helpers
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Spawn a procedural Line2D lightning bolt from above the camera
        /// viewport down to a random ground point near the camera. Uses
        /// LightningBoltComponent (auto-frees after its Lifetime).
        /// </summary>
        /// <summary>
        /// The flash envelope, as (time, intensity) keys interpolated between.
        ///
        /// A real strike is a PRIMARY bolt followed by a return stroke down the same channel a few
        /// hundredths of a second later, and it is that second peak the eye reads as "lightning"
        /// rather than "something flickered". The previous envelope was a monotonic staircase
        /// (1.0 → 0.6 → 0.3 → 0) whose third step was even commented "secondary" — but a step that
        /// is DIMMER than the one before it is just decay. There was no return stroke, and holding
        /// each level flat made it four visible brightness plateaus at 60fps.
        ///
        /// So: it dips to 0.2 and climbs BACK to 0.8. The non-monotonicity is the whole signature.
        /// The long tail is the storm re-darkening, and is deliberately ~7× the rise.
        /// </summary>
        private static readonly (float T, float V)[] FlashKeys =
        {
            (0.00f, 0.00f),  // dark
            (0.04f, 1.00f),  // primary bolt — near-instant, blinding
            (0.10f, 0.20f),  // channel dims between strokes
            (0.13f, 0.80f),  // RETURN STROKE — the second peak
            (0.58f, 0.00f),  // long fade back into the storm
        };

        private const double FlashDuration = 0.58;

        private static float FlashEnvelope(float t)
        {
            for (int i = 1; i < FlashKeys.Length; i++)
            {
                var (t1, v1) = FlashKeys[i];
                if (t > t1) continue;
                var (t0, v0) = FlashKeys[i - 1];
                // Guard the zero-length span rather than trusting the table: a duplicated
                // timestamp would divide by zero and blow the whole overlay to NaN.
                float span = t1 - t0;
                return span <= 0f ? v1 : Mathf.Lerp(v0, v1, (t - t0) / span);
            }
            return 0f;
        }

        private void SpawnLightningBolt()
        {
            if (_boltContainer == null) return;

            // Cap active bolts (clean up old ones if stacking).
            if (_activeLightningBolts.Count > 15)
            {
                var oldBolt = _activeLightningBolts[0];
                _activeLightningBolts.RemoveAt(0);
                oldBolt.QueueFree();
            }

            Node2D parent2D = _boltContainer;

            // Pick a strike point relative to the camera so the bolt is on-screen.
            var cam = GetViewport()?.GetCamera2D();
            Vector2 camCenter = cam != null ? cam.GlobalPosition : parent2D.GlobalPosition;
            float strikeX = (float)GD.RandRange(-600, 600);
            Vector2 startSky = camCenter + new Vector2(strikeX - 200f, -500f);
            Vector2 endGround = camCenter + new Vector2(strikeX, (float)GD.RandRange(100, 300));

            var bolt = new LightningBoltComponent();
            parent2D.AddChild(bolt);
            bolt.GlobalPosition = Vector2.Zero; // bolts use global coords passed to Strike()
            bolt.Strike(startSky, endGround);
            _activeLightningBolts.Add(bolt);
        }

        private void SpawnLightningSprite()
        {
            if (_overlayLayer == null) return;
            Texture2D[] sprites = LightningSprites.Length > 0 ? LightningSprites : BundledLightningSprites();
            if (sprites.Length == 0) return;

            var vp = GetViewport();
            Vector2 size = vp != null ? vp.GetVisibleRect().Size : new Vector2(1280, 720);
            var tex = sprites[(int)(GD.Randi() % (uint)sprites.Length)];
            WeatherViewMode view = EffectiveViewMode();
            var sprite = new Sprite2D
            {
                Name = "LightningSprite",
                Texture = tex,
                Centered = true,
                Position = new Vector2(
                    (float)GD.RandRange(size.X * 0.18f, size.X * 0.82f),
                    (float)GD.RandRange(size.Y * 0.05f, size.Y * (IsTopDownLike(view) ? 0.55f : 0.38f))),
                Modulate = new Color(1f, 1f, 1f, 0.95f),
                ZIndex = 200,
            };
            float targetH = size.Y * (IsTopDownLike(view) ? 0.42f : 0.58f);
            float texH = Mathf.Max(1f, tex.GetHeight());
            float scale = targetH / texH * (float)GD.RandRange(0.75f, 1.15f);
            sprite.Scale = new Vector2(scale * (GD.Randf() > 0.5f ? -1f : 1f), scale);

            _overlayLayer.AddChild(sprite);
            _activeLightningBolts.Add(sprite);

            var tween = CreateTween();
            tween.TweenProperty(sprite, "modulate:a", 0.0f, 0.18f)
                 .SetTrans(Tween.TransitionType.Quad)
                 .SetEase(Tween.EaseType.Out);
            tween.TweenCallback(Callable.From(() =>
            {
                _activeLightningBolts.Remove(sprite);
                if (GodotObject.IsInstanceValid(sprite)) sprite.QueueFree();
            }));
        }

        /// <summary>
        /// Auto-discover a ScreenShakeComponent in the scene and trigger it.
        /// Searches the main scene tree (ScreenShakeComponent attaches to a
        /// Camera2D which is typically a root-level node). Scales by weather
        /// intensity so a weak storm barely shakes.
        /// </summary>
        private void TriggerCameraShake()
        {
            if (EffectiveLightningShakeIntensity <= 0) return;
            var tree = GetTree();
            if (tree == null) return;
            var shake = tree.Root.FindChild("ScreenShakeComponent", true, false) as ScreenShakeComponent;
            // Fall back to scanning for any ScreenShakeComponent in the tree.
            if (shake == null)
            {
                foreach (var node in tree.GetNodesInGroup("screen_shake"))
                {
                    if (node is ScreenShakeComponent s) { shake = s; break; }
                }
            }
            if (shake != null)
                shake.Shake(EffectiveLightningShakeIntensity * _intensityCurrent, 0.4f);
            else if (!_shakeMissWarned)
            {
                _shakeMissWarned = true;
                GD.PushWarning($"[{Name}] LightningShakeIntensity > 0 but no ScreenShakeComponent found (by node name or the 'screen_shake' group) — lightning won't shake the camera. Add a ScreenShakeComponent to the Camera2D (and put it in the 'screen_shake' group if you rename it).");
            }
        }

        private bool _shakeMissWarned;

        // ════════════════════════════════════════════════════════════════
        // Helpers
        // ════════════════════════════════════════════════════════════════

        private Color GetTintFor(WeatherType type) => type switch
        {
            WeatherType.Clear => ClearTint,
            WeatherType.Cloudy => CloudyTint,
            WeatherType.Rain => RainTint,
            WeatherType.Snow => SnowTint,
            WeatherType.Storm => StormTint,
            WeatherType.Fog => FogTint,
            WeatherType.Sandstorm => SandstormTint,
            WeatherType.Hail => HailTint,
            WeatherType.LeafFall => LeafFallTint,
            WeatherType.Heatwave => HeatwaveTint,
            _ => ClearTint
        };

        public override void _ExitTree()
        {
            base._ExitTree();
            foreach (var bolt in _activeLightningBolts)
                bolt?.QueueFree();
            _activeLightningBolts.Clear();
            // Withdraw the weather tint (DayNight and Seasonal both do this). Without it, removing
            // the weather node while the AmbientController persists leaves the last weather tint as a
            // permanent multiplicative dimming layer with nothing left to clear it.
            _ambient?.SetContribution(AmbientKey, null);
            // Reset the global shader params we published, so leaving a rainy scene doesn't strand
            // beep_puddle_depth / beep_snow_accumulation / etc. at their last value app-wide (the same
            // leak class DayNight's clear-colour restore addresses). Only if we ever registered them.
            if (_globalsRegistered)
                foreach (var name in new[]{ ParamWindStrength, ParamWindX, ParamPuddleDepth, ParamSnowAccumulation, ParamWeatherIntensity })
                    RenderingServer.GlobalShaderParameterSet(name, 0f);
        }
    }
}

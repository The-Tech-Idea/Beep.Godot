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
    /// • Precipitation via CpuParticles2D (rain/snow/hail/leaves).
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
        [Export] public int ParticleCount { get; set; } = 250;
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
        /// <summary>Rain and Storm. Empty = the bundled streak (a streak reads as falling
        /// water; a blob doesn't).</summary>
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

        /// <summary>Set false to use no sprite at all unless you assign one explicitly
        /// (particles fall back to Godot's default white point).</summary>
        [Export] public bool UseBundledParticleTextures { get; set; } = true;

        // Loaded on first use and shared by every instance — these are small CC0 sprites
        // (textures/particles/CREDITS.md) and Godot caches the resource anyway.
        private const string TexDir = "res://addons/beep_game_builder_cs/textures/particles/";
        private static readonly System.Collections.Generic.Dictionary<string, Texture2D?> _bundledCache = new();

        private static Texture2D? Bundled(string file)
        {
            if (_bundledCache.TryGetValue(file, out var cached)) return cached;
            string path = TexDir + file;
            // Cache the miss too, so a missing file warns once rather than every weather change.
            Texture2D? tex = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
            if (tex == null) GD.PushWarning($"[Weather] Bundled particle texture not found: {path}");
            _bundledCache[file] = tex;
            return tex;
        }

        // ── Public read-only state for HUDs / forecast UIs ──
        /// <summary>Seconds remaining before AutoCycle switches weather (0 if not cycling).</summary>
        public double TimeToNextWeather => AutoCycle && _currentWeatherDuration > 0
            ? System.Math.Max(0, _currentWeatherDuration - _cycleTimer) : 0;
        /// <summary>Human-readable name of the current weather type.</summary>
        public string CurrentWeatherName => CurrentWeather.ToString();

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
        /// <summary>Node to parent spawned bolts to. If null, uses the weather parent. Should be a world-space Node2D.</summary>
        [Export] public NodePath? BoltContainer { get; set; }
        /// <summary>Camera shake intensity on a strike (0 = no shake). Scaled by weather intensity.</summary>
        [Export] public float LightningShakeIntensity { get; set; } = 12f;

        [ExportGroup("Wind")]
        [Export] public bool EnableWind { get; set; } = true;
        [Export] public Vector2 WindForce { get; set; } = Vector2.Zero;
        [Export] public float WindChangeSpeed { get; set; } = 0.5f;
        [Export] public float MaxWindMagnitude { get; set; } = 3f;

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
                if (turningOn && _overlayLayer != null) EnsureCloudOverlays(_overlayLayer);
                if (turningOff)
                {
                    if (_cloudOverlay != null) _cloudOverlay.Modulate = new Color(1, 1, 1, 0);
                    if (_cloudShadowOverlay != null) _cloudShadowOverlay.Modulate = new Color(1, 1, 1, 0);
                }
            }
        }
        [Export] public Color CloudColor { get; set; } = new(1f, 1f, 1f, 1f);
        [Export] public Color CloudShadowColor { get; set; } = new(0f, 0f, 0f, 0.35f);
        /// <summary>Multiplier on cloud-shadow visibility (set 0 to disable dapple).</summary>
        [Export] public float CloudShadowStrength { get; set; } = 1f;

        [ExportGroup("Transitions")]
        /// <summary>Seconds to cross-fade between weathers. 0 = instant.</summary>
        [Export] public float TransitionDuration { get; set; } = 1.5f;

        [Signal] public delegate void WeatherChangedEventHandler(int type);
        [Signal] public delegate void LightningStruckEventHandler();

        // ── Internal nodes ──
        private CanvasLayer? _overlayLayer;   // screen-space container for fog/clouds/flash
        private CpuParticles2D? _particles;
        // Weather no longer owns a CanvasModulate — it contributes its tint to the shared
        // AmbientController, which composes it with the day/night tint. See AmbientController.
        private AmbientController? _ambient;
        private const string AmbientKey = "weather";
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
        private Tween? _weatherTransitionTween;

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

            EnsureNodes();
            if (IsActive) SetWeather(CurrentWeather);
        }

        private void EnsureNodes()
        {
            if (GetParent() is not Node parent) return;
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

            _lightningTimer = GD.RandRange(LightningMinInterval, LightningMaxInterval);

            // Cache the bolt container so we don't search every lightning strike.
            if (BoltContainer != null)
                _boltContainer = GetNodeOrNull<Node2D>(BoltContainer);
            _boltContainer ??= parent as Node2D;

            // Cloud + cloud-shadow overlays (built/owned by the Overlays partial).
            if (EnableClouds) EnsureCloudOverlays(overlayRoot);
        }

        /// <summary>
        /// Configure the CpuParticles2D emitter as a screen-spanning box so
        /// rain/snow/hail cover the viewport. Called once on build and again from
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
            _particles.EmissionRectExtents = new Vector2(size.X * 0.6f, 8f);

            // Local coords → the emitter stays at its parent's transform while
            // particles fall through world space; as the camera scrolls, fresh
            // rain keeps entering frame naturally.
            _particles.LocalCoords = true;
        }

        public override void _Process(double delta)
        {
            if (!IsActive) return;

            // (Day/night progression moved to DayNightCycleComponent.)

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
                _weatherTintCurrent = _weatherTintCurrent.Lerp(weatherTarget, (float)delta * TransitionLerpRate);
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
                _cycleTimer += delta;
                double duration = _currentWeatherDuration > 0 ? _currentWeatherDuration : CycleInterval;
                if (_cycleTimer >= duration)
                {
                    _cycleTimer = 0;
                    SetWeather(PickWeightedWeather());
                }
            }

            // Wind drift — pushes particle gravity (clouds read WindForce in ProcessClouds).
            if (EnableWind)
            {
                WindForce = new Vector2(
                    WindForce.X + (float)GD.RandRange(-WindChangeSpeed, WindChangeSpeed) * (float)delta * 60f,
                    WindForce.Y + (float)GD.RandRange(-WindChangeSpeed, WindChangeSpeed) * (float)delta * 60f
                );
                WindForce = WindForce.LimitLength(MaxWindMagnitude);
                if (_particles != null)
                    _particles.Gravity = new Vector2(WindForce.X * 100f, _particles.Gravity.Y);
            }

            // Re-fit the particle emitter if the viewport was resized.
            ConfigureParticleEmitter();

            // Cloud drift + wind-direction sync.
            if (EnableClouds) ProcessClouds(delta);

            // Lightning (Storm only).
            ProcessLightning(delta);
        }

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
                TargetIntensity = 1f;
                WeatherIntensity = 1f;
                _intensityCurrent = 1f;
            }

            // Per-weather AutoCycle duration: storms are brief, fog lingers.
            _currentWeatherDuration = (double)GD.RandRange(
                GetWeatherMinDuration(type), GetWeatherMaxDuration(type));
            _cycleTimer = 0;

            // Particle configuration.
            bool usesParticles = type is WeatherType.Rain or WeatherType.Snow or WeatherType.Storm
                or WeatherType.Sandstorm or WeatherType.Hail or WeatherType.LeafFall;
            if (_particles != null)
            {
                _particles.Emitting = usesParticles;
                if (usesParticles)
                {
                    _particles.Amount = ParticleCount;
                    ConfigureParticles(type);
                }
            }

            // Lightning enabled only for Storm.
            _lightningTimer = GD.RandRange(LightningMinInterval, LightningMaxInterval);
            _lightningActive = false;

            EmitSignal(SignalName.WeatherChanged, (int)type);
        }

        private void ConfigureParticles(WeatherType type)
        {
            if (_particles == null) return;

            // The sprite, alongside the motion/colour each case tunes below. Without this
            // every weather type drew as a white square no matter how well-tuned the rest
            // was. An explicit export always wins; otherwise fall back to the bundled
            // sprite (unless that's switched off).
            _particles.Texture = type switch
            {
                WeatherType.Rain or WeatherType.Storm => RainTexture ?? Fallback("trace_01.png"),
                WeatherType.Snow                      => SnowTexture ?? Fallback("circle_05.png"),
                WeatherType.Hail                      => HailTexture ?? Fallback("circle_02.png"),
                WeatherType.Sandstorm                 => SandTexture ?? Fallback("dirt_02.png"),
                // No bundled leaf sprite exists — a circle would read as snow.
                WeatherType.LeafFall                  => LeafTexture,
                _                                     => null
            };

            Texture2D? Fallback(string file) => UseBundledParticleTextures ? Bundled(file) : null;

            switch (type)
            {
                case WeatherType.Rain:
                    _particles.Direction = new Vector2(0.15f, 1f);
                    _particles.Spread = 10f;
                    _particles.Gravity = new Vector2(0, 600);
                    _particles.InitialVelocityMax = 500;
                    _particles.InitialVelocityMin = 300;
                    _particles.ScaleAmountMin = 0.2f;
                    _particles.ScaleAmountMax = 0.4f;
                    _particles.Color = new Color(0.6f, 0.7f, 0.9f, 0.6f);
                    break;
                case WeatherType.Snow:
                    _particles.Direction = new Vector2(0.05f, 1f);
                    _particles.Spread = 30f;
                    _particles.Gravity = new Vector2(0, 50);
                    _particles.InitialVelocityMax = 60;
                    _particles.ScaleAmountMin = 0.8f;
                    _particles.ScaleAmountMax = 1.5f;
                    _particles.Color = new Color(0.9f, 0.95f, 1f, 0.8f);
                    // turbulence not available in this binding
                    break;
                case WeatherType.Storm:
                    _particles.Direction = new Vector2(0.45f, 1f);
                    _particles.Spread = 20f;
                    _particles.Gravity = new Vector2(0, 900);
                    _particles.InitialVelocityMax = 700;
                    _particles.ScaleAmountMin = 0.15f;
                    _particles.ScaleAmountMax = 0.3f;
                    _particles.Color = new Color(0.5f, 0.55f, 0.7f, 0.5f);
                    break;
                case WeatherType.Sandstorm:
                    _particles.Direction = new Vector2(1f, 0.1f);
                    _particles.Spread = 15f;
                    _particles.Gravity = new Vector2(300, 0);
                    _particles.InitialVelocityMax = 400;
                    _particles.ScaleAmountMin = 0.3f;
                    _particles.ScaleAmountMax = 0.8f;
                    _particles.Color = new Color(0.85f, 0.65f, 0.35f, 0.5f);
                    break;
                case WeatherType.Hail:
                    _particles.Direction = new Vector2(0.1f, 1f);
                    _particles.Spread = 5f;
                    _particles.Gravity = new Vector2(0, 1200);
                    _particles.InitialVelocityMax = 800;
                    _particles.ScaleAmountMin = 0.5f;
                    _particles.ScaleAmountMax = 0.9f;
                    _particles.Color = new Color(0.85f, 0.88f, 0.92f, 0.9f);
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
        }

        // ════════════════════════════════════════════════════════════════
        // Lightning
        // ════════════════════════════════════════════════════════════════

        private void ProcessLightning(double delta)
        {
            if (!EnableLightning || CurrentWeather != WeatherType.Storm)
            {
                if (_flashOverlay != null && _flashOverlay.Color.A > 0)
                    _flashOverlay.Color = new Color(0, 0, 0, 0);
                return;
            }

            if (_lightningActive)
            {
                // Flash decay — quick bright burst then fade.
                _lightningFlashTime += delta;
                float intensity;
                if (_lightningFlashTime < 0.06) // initial bright flash
                    intensity = 1f;
                else if (_lightningFlashTime < 0.12) // brief flicker
                    intensity = 0.6f;
                else if (_lightningFlashTime < 0.2) // secondary
                    intensity = 0.3f;
                else // done
                {
                    intensity = 0f;
                    _lightningActive = false;
                    _lightningTimer = GD.RandRange(LightningMinInterval, LightningMaxInterval);
                }
                if (_flashOverlay != null)
                    _flashOverlay.Color = new Color(LightningColor.R, LightningColor.G, LightningColor.B, intensity * 0.8f);
            }
            else
            {
                _lightningTimer -= delta;
                if (_lightningTimer <= 0)
                {
                    _lightningActive = true;
                    _lightningFlashTime = 0;
                    EmitSignal(SignalName.LightningStruck);
                    // Spawn a procedural bolt + shake the camera. The bolt is a
                    // world-space Line2D so it renders against the level; the
                    // flash overlay (_flashOverlay) handled above does the screen white-out.
                    if (EnableLightningBolts) SpawnLightningBolt();
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

        /// <summary>
        /// Auto-discover a ScreenShakeComponent in the scene and trigger it.
        /// Searches the main scene tree (ScreenShakeComponent attaches to a
        /// Camera2D which is typically a root-level node). Scales by weather
        /// intensity so a weak storm barely shakes.
        /// </summary>
        private void TriggerCameraShake()
        {
            if (LightningShakeIntensity <= 0) return;
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
            shake?.Shake(LightningShakeIntensity * _intensityCurrent, 0.4f);
        }

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
            _weatherTransitionTween?.Kill();
            foreach (var bolt in _activeLightningBolts)
                bolt?.QueueFree();
            _activeLightningBolts.Clear();
        }
    }
}

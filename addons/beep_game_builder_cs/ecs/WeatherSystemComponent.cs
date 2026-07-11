using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Comprehensive 2D weather system. Attach to a Node2D (or the world root).
    /// Supports 10 weather types, each with particle effects, ambient tinting,
    /// a procedural fog overlay (shader-based), wind force, and lightning flashes.
    ///
    /// Weather types:
    ///   Clear, Cloudy, Rain, Snow, Storm, Fog, Sandstorm, Hail, LeafFall, Heatwave
    ///
    /// Features (grounded in Godot 2D best practices — shader fakes, not 3D volumetric):
    /// • Precipitation via CpuParticles2D (rain/snow/hail/leaves).
    /// • Procedural fog/sandstorm/heatwave overlay via a canvas_item shader.
    /// • Ambient lighting via CanvasModulate (dark for storms, warm for heatwave, etc.).
    /// • Lightning flashes: random full-screen ColorRect brightness bursts during Storm.
    /// • Wind force (Vector2) that gameplay components can read for leaf-drift, projectile drift, etc.
    /// • AutoCycle mode: rotates through weather types on a configurable timer.
    /// • Smooth ambient-color transitions (tween) when switching weather.
    ///
    /// Sources: WeatherSystem2D asset pattern; Godot 4.7 canvas_item shaders for fog;
    /// community consensus that 2D uses shader fakes rather than 3D volumetric.
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
        [Export] public Vector2 WindForce { get; set; } = Vector2.Zero;
        [Export] public float WindChangeSpeed { get; set; } = 0.5f;

        [ExportGroup("Fog Overlay")]
        [Export] public float FogDensity { get; set; } = 0.4f;
        [Export] public float FogScrollSpeed { get; set; } = 0.3f;
        /// <summary>FBM octave count for the fog shader. More = finer detail, higher cost.</summary>
        [Export] public int FogOctaves { get; set; } = 5;
        /// <summary>Fog drift direction + speed in UV space. Warm X = rightward wind.</summary>
        [Export] public Vector2 FogVelocity { get; set; } = new(0.15f, 0.05f);
        /// <summary>
        /// Optional noise texture (FastNoiseLite sampler2D). When set, the fog
        /// samples this instead of the in-shader hash — higher quality, matches
        /// the godotshaders.com 2D Fog Overlay pattern. Leave null for procedural.
        /// </summary>
        [Export] public Texture2D? NoiseTexture { get; set; }

        [ExportGroup("Overlays")]
        /// <summary>
        /// CanvasLayer index for the screen-space overlay layer. High so fog/clouds/
        /// lightning draw above the world AND follow the camera (screen-space, not
        /// world-space). Canonical 2D-weather pattern from godotshaders.com.
        /// </summary>
        [Export] public int OverlayLayerIndex { get; set; } = 100;

        [ExportGroup("Clouds")]
        [Export] public bool EnableClouds { get; set; } = true;
        /// <summary>0 clear .. 1 overcast. Auto-set per weather, override here.</summary>
        [Export] public float CloudCoverage { get; set; } = 0.55f;
        [Export] public float CloudDriftSpeed { get; set; } = 0.04f;
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
        private CanvasModulate? _ambient;
        private ColorRect? _fogOverlay;
        private ColorRect? _flashOverlay;
        private ShaderMaterial? _fogMat;

        // ── Internal state ──
        private double _cycleTimer;
        private double _currentWeatherDuration;   // per-weather min/max duration for AutoCycle
        private Color _weatherTintCurrent = new(1f, 1f, 1f, 1f);  // eased weather tint (pre day/night multiply)
        private double _lightningTimer;
        private bool _lightningActive;
        private double _lightningFlashTime;
        private float _fogScroll;

        /// <summary>
        /// Frame-lerp rate for the weather→ambient transition. Higher = snappier.
        /// TransitionDuration is kept as a user-facing convenience; this is the
        /// per-frame implementation of "ease toward target" that also keeps working
        /// when the day/night tint is multiplying every frame.
        /// </summary>
        private const float TransitionLerpRate = 2.0f;

        // ── Fog shader ──
        // FBM-based fog overlay. Supports an optional FastNoiseLite sampler2D
        // (godotshaders.com 2D Fog Overlay pattern); when null, falls back to an
        // in-shader simplex-ish noise so it still works with zero assets.
        // Domain warping + a vec2 velocity give the fog a swirling, self-churning
        // motion rather than a flat linear scroll.
        private const string FogShader = @"
shader_type canvas_item;
uniform float density   : hint_range(0.0, 1.0) = 0.4;
uniform vec4  tint      : source_color = vec4(0.8, 0.8, 0.8, 1.0);
uniform vec2  velocity  = vec2(0.15, 0.05);   // drift direction + speed (UV/s)
uniform float octaves   = 5.0;
uniform float time_v    = 0.0;
uniform float heat_distortion = 0.0;           // >0 for heatwave ripple
uniform sampler2D noise_tex : hint_default_white; // optional FastNoiseLite

// ---- Procedural value noise (used when noise_tex is the default white) ----
float hash21(vec2 p){
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}
float vnoise(vec2 p){
    vec2 i = floor(p); vec2 f = fract(p);
    float a = hash21(i);
    float b = hash21(i + vec2(1.0, 0.0));
    float c = hash21(i + vec2(0.0, 1.0));
    float d = hash21(i + vec2(1.0, 1.0));
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

// Fractal Brownian Motion — the core of naturalistic fog.
float fbm(vec2 p, float oct){
    float v = 0.0, a = 0.5;
    for (int i = 0; i < 8; i++) {
        if (float(i) >= oct) break;
        v += a * vnoise(p);
        p *= 2.02;
        a *= 0.5;
    }
    return v;
}

void fragment(){
    vec2 uv = UV;

    // Heatwave: warp UV vertically with a traveling sine to fake refraction.
    if (heat_distortion > 0.0)
        uv.y += sin(uv.x * 30.0 + time_v * 5.0) * heat_distortion * 0.01;

    // Flow: drift the sample point along the velocity vector, and warp it with
    // a second noise field so the fog churns on itself instead of sliding.
    vec2 flow = uv + velocity * time_v;
    vec2 warp = vec2(
        fbm(uv * 2.0 + velocity * 0.5 * time_v, 3.0),
        fbm(uv * 2.0 + vec2(5.2, 1.3) + velocity * 0.5 * time_v, 3.0)
    ) - 0.5;

    // Sample the optional noise texture (FastNoiseLite) at a scaled + warped UV;
    // default_white sampler means this returns 1.0 when no texture is assigned,
    // in which case the procedural fbm() below takes over via the mix weight.
    float tex_n = texture(noise_tex, (uv + warp * 0.4) * 4.0 + velocity * time_v).r;
    float proc_n = fbm((uv + warp * 0.4) * 4.0 + flow, octaves);
    // If a real texture is bound, it's almost always non-1.0 → bias toward it.
    float tex_weight = step(abs(tex_n - 1.0), 0.001) > 0.5 ? 0.0 : 0.7;
    float n = mix(proc_n, tex_n, tex_weight);

    float fog = smoothstep(1.0 - density, 1.0 - density * 0.3, n);
    COLOR = vec4(tint.rgb, fog * density);
}
";

        public override void _Ready()
        {
            base._Ready();
            // Register in the discovery group so WindFieldComponent and
            // WeatherHUDComponent can auto-find this system without a NodePath.
            if (!IsInGroup("weather_system")) AddToGroup("weather_system");
            EnsureNodes();
            if (!Engine.IsEditorHint()) SetWeather(CurrentWeather);
        }

        private void EnsureNodes()
        {
            if (GetParent() is not Node parent) return;

            // Particle system (precipitation / leaves / sand).
            // Lives in WORLD space (parent), not the overlay layer, so rain falls
            // through actual level coordinates and "moves naturally as the camera
            // moves" via local coords — per the godotshaders.com guidance.
            _particles = parent.GetNodeOrNull<CpuParticles2D>("WeatherParticles");
            if (_particles == null)
            {
                _particles = new CpuParticles2D { Name = "WeatherParticles", Emitting = false };
                parent.AddChild(_particles);
            }
            ConfigureParticleEmitter();

            // Ambient lighting modulator (world-space — tints everything below it).
            _ambient = parent.GetNodeOrNull<CanvasModulate>("WeatherAmbient");
            if (_ambient == null)
            {
                _ambient = new CanvasModulate { Name = "WeatherAmbient", Color = ClearTint };
                parent.AddChild(_ambient);
            }

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

            // Fog overlay (shader-based, full-screen ColorRect).
            _fogOverlay = overlayRoot.GetNodeOrNull<ColorRect>("WeatherFogOverlay");
            if (_fogOverlay == null)
            {
                _fogOverlay = new ColorRect
                {
                    Name = "WeatherFogOverlay",
                    MouseFilter = Godot.Control.MouseFilterEnum.Ignore,
                    Color = new Color(0, 0, 0, 0)
                };
                _fogOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                overlayRoot.AddChild(_fogOverlay);
            }
            var shader = new Shader { Code = FogShader };
            _fogMat = new ShaderMaterial { Shader = shader };
            _fogOverlay.Material = _fogMat;
            ApplyFogShaderParams();

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

        /// <summary>Push all fog-shader uniforms from the C# exports.</summary>
        private void ApplyFogShaderParams()
        {
            if (_fogMat == null) return;
            _fogMat.SetShaderParameter("density", FogDensity);
            _fogMat.SetShaderParameter("tint", GetFogTintFor(CurrentWeather));
            _fogMat.SetShaderParameter("velocity", FogVelocity);
            _fogMat.SetShaderParameter("octaves", Mathf.Clamp(FogOctaves, 1, 8));
            // noise_tex is a hint_default_white sampler. Passing a real texture
            // overrides the in-shader procedural noise; when null we pass nothing
            // and the default white (which the shader treats as "no texture bound")
            // stays in effect.
            if (NoiseTexture != null)
                _fogMat.SetShaderParameter("noise_tex", NoiseTexture);
        }

        public override void _Process(double delta)
        {
            if (!IsActive) return;

            // Day-night progression (also drives sky clear color + emits hour signal).
            ProcessDayNight(delta);

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
                // Intensity gates how far toward the weather tint we go.
                Color intensityTint = new Color(
                    Mathf.Lerp(ClearTint.R, _weatherTintCurrent.R, _intensityCurrent),
                    Mathf.Lerp(ClearTint.G, _weatherTintCurrent.G, _intensityCurrent),
                    Mathf.Lerp(ClearTint.B, _weatherTintCurrent.B, _intensityCurrent), 1f);
                Color dayNight = GetDayNightTint();
                _ambient.Color = new Color(
                    intensityTint.R * dayNight.R,
                    intensityTint.G * dayNight.G,
                    intensityTint.B * dayNight.B,
                    1f);
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

            // Wind drift — affects both particle gravity AND the fog velocity
            // so a strong wind visibly pushes the fog/clouds too.
            if (WindForce != Vector2.Zero)
            {
                WindForce = new Vector2(
                    WindForce.X + (float)GD.RandRange(-WindChangeSpeed, WindChangeSpeed) * (float)delta * 60f,
                    WindForce.Y + (float)GD.RandRange(-WindChangeSpeed, WindChangeSpeed) * (float)delta * 60f
                );
                if (_particles != null)
                    _particles.Gravity = new Vector2(WindForce.X * 100f, _particles.Gravity.Y);
                if (_fogMat != null)
                    _fogMat.SetShaderParameter("velocity", new Vector2(
                        FogVelocity.X + WindForce.X * 0.1f,
                        FogVelocity.Y + WindForce.Y * 0.05f));
            }

            // Fog shader animation — advance the time uniform used by the FBM.
            if (_fogMat != null && _fogOverlay != null && _fogOverlay.Visible)
            {
                _fogScroll += (float)delta;
                _fogMat.SetShaderParameter("time_v", _fogScroll);
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

            // Fog overlay — cross-fade rather than snap. We keep the overlay
            // always visible (alpha 0 when unused) and tween modulate so the
            // old weather's fog dissolves while the new one's fades in.
            bool usesFog = type is WeatherType.Fog or WeatherType.Sandstorm or WeatherType.Heatwave;
            if (_fogMat != null && usesFog)
            {
                _fogMat.SetShaderParameter("density", type == WeatherType.Heatwave ? FogDensity * 0.3f : FogDensity);
                _fogMat.SetShaderParameter("tint", GetFogTintFor(type));
                _fogMat.SetShaderParameter("velocity", FogVelocity);
                _fogMat.SetShaderParameter("heat_distortion", type == WeatherType.Heatwave ? 1.0f : 0f);
            }
            if (_fogOverlay != null)
            {
                _fogOverlay.Visible = true; // alpha tween handles hiding
                var tw = CreateTween();
                tw.TweenProperty(_fogOverlay, "modulate:a", usesFog ? 1f : 0f, TransitionDuration);
            }

            // Lightning enabled only for Storm.
            _lightningTimer = GD.RandRange(LightningMinInterval, LightningMaxInterval);
            _lightningActive = false;

            EmitSignal(SignalName.WeatherChanged, (int)type);
        }

        private void ConfigureParticles(WeatherType type)
        {
            if (_particles == null) return;
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
            // Resolve a world-space parent for the bolt.
            Node? container = null;
            if (BoltContainer != null) container = GetNodeOrNull<Node>(BoltContainer);
            container ??= GetParent();
            if (container is not Node2D parent2D) return;

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

        private Color GetFogTintFor(WeatherType type) => type switch
        {
            WeatherType.Fog => new Color(FogTint.R, FogTint.G, FogTint.B, 1f),
            WeatherType.Sandstorm => new Color(0.85f, 0.65f, 0.35f, 1f),
            WeatherType.Heatwave => new Color(1f, 0.85f, 0.6f, 1f),
            _ => new Color(0.8f, 0.8f, 0.8f, 1f)
        };
    }
}

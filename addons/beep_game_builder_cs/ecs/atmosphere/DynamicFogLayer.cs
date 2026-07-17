using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Dynamic 2D fog overlay that reduces visibility based on weather intensity.
    /// Attach to the scene root; creates its own CanvasLayer with high layer index.
    ///
    /// Uses a Canvas Item shader with animated noise texture for organic fog motion.
    /// Fog density and speed are controlled by weather intensity.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class DynamicFogLayer : WorldComponent
    {
        [ExportGroup("Fog Visual")]
        [Export] public Color FogColor { get; set; } = new(0.5f, 0.5f, 0.5f, 1f);
        [Export] public Texture2D? NoiseTexture { get; set; }
        [Export] public float MaxDensity { get; set; } = 0.5f;
        [Export] public Vector2 AnimationSpeed { get; set; } = new(0.02f, 0.01f);

        [ExportGroup("Fog Control")]
        [Export] public int CanvasLayerIndex { get; set; } = 101;
        [Export] public NodePath? WeatherSystemPath { get; set; }
        [Export] public bool EnableWeatherIntegration { get; set; } = true;

        private CanvasLayer? _layer;
        private ColorRect? _fogRect;
        private ShaderMaterial? _fogMat;
        private WeatherSystemComponent? _weather;
        private float _currentDensity = 0f;

        // Fog shader with animated noise
        private const string FogShaderCode = @"
shader_type canvas_item;

// filter_linear, not nearest: the noise is 256x256 stretched over the whole viewport, so at
// 1080p one texel covers ~7.5 px. Nearest gives hard-edged blocks — which contradicts the
// smooth simplex the component generates, and smoothstep softens the ramp but not the texel
// edges. Nobody saw this while the texture was unbound and the fog invisible; it only became
// a visible choice once fog rendered at all. A uniform's filter hint overrides the texture's.
uniform sampler2D noise_texture : repeat_enable, filter_linear;
uniform vec4 fog_color : source_color = vec4(0.5, 0.5, 0.5, 1.0);
uniform float density : hint_range(0.0, 1.0) = 0.3;
uniform vec2 animation_speed = vec2(0.02, 0.01);

void fragment() {
    vec2 uv = UV + TIME * animation_speed;
    float noise = texture(noise_texture, uv).r;
    float fog = smoothstep(0.3, 0.7, noise) * density;
    COLOR = vec4(fog_color.rgb, fog);
}
";

        public override void _Ready()
        {
            base._Ready();
            // DeferredInit spawns fog nodes. This is [Tool] and sits in every genre main
            // scene, so without the guard just opening one in the editor fills it with
            // runtime-only children.
            if (Engine.IsEditorHint()) return;
            // Fog is weather-driven, so it follows the weather enable flag.
            if (Beep.GameBuilder.GameInfo.Instance is { } info) IsActive = info.EnableWeather;
            if (!IsInGroup("fog_layer")) AddToGroup("fog_layer");
            CallDeferred(nameof(DeferredInit));
        }

        private void DeferredInit()
        {
            EnsureFogLayer();

            // Wire to weather system
            if (EnableWeatherIntegration)
            {
                if (WeatherSystemPath != null)
                    _weather = GetNodeOrNull<WeatherSystemComponent>(WeatherSystemPath);
                if (_weather == null)
                {
                    foreach (var n in GetTree().GetNodesInGroup("weather_system"))
                        if (n is WeatherSystemComponent w) { _weather = w; break; }
                }
            }
        }

        private void EnsureFogLayer()
        {
            // Create or find the fog canvas layer
            _layer = GetNodeOrNull<CanvasLayer>("FogCanvasLayer");
            if (_layer == null)
            {
                _layer = new CanvasLayer { Name = "FogCanvasLayer", Layer = CanvasLayerIndex };
                AddChild(_layer);
            }
            else
            {
                _layer.Layer = CanvasLayerIndex;
            }

            // Create the fog overlay ColorRect
            _fogRect = _layer.GetNodeOrNull<ColorRect>("FogOverlay");
            if (_fogRect == null)
            {
                _fogRect = new ColorRect
                {
                    Name = "FogOverlay",
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Color = new Color(0, 0, 0, 0)
                };
                _fogRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                _layer.AddChild(_fogRect);
            }

            // Create and assign shader material
            var shader = new Shader { Code = FogShaderCode };
            _fogMat = new ShaderMaterial { Shader = shader };
            _fogRect.Material = _fogMat;
            ApplyFogShaderParams();
        }

        public override void _Process(double delta)
        {
            if (!IsActive || _fogMat == null) return;

            // Drive density from the weather system. This is now the ONE fog: the weather
            // system used to also draw its own fog overlay, so fog rendered twice. Weather's
            // internal overlay was removed; this reads WeatherIntensity and only fogs for
            // fog-like weather. (Was a stub that hardcoded 0.2 regardless of weather.)
            if (_weather != null && EnableWeatherIntegration)
                SetFogDensity(FogWeightFor(_weather.CurrentWeather) * _weather.WeatherIntensity);
        }

        /// <summary>How foggy each weather type is, 0..1, before intensity scales it. Fog is
        /// full; storms and sandstorms haze; everything else is clear.</summary>
        private static float FogWeightFor(WeatherSystemComponent.WeatherType w) => w switch
        {
            WeatherSystemComponent.WeatherType.Fog => 1.0f,
            WeatherSystemComponent.WeatherType.Storm => 0.4f,
            WeatherSystemComponent.WeatherType.Sandstorm => 0.6f,
            _ => 0f
        };

        /// <summary>
        /// Set fog density based on weather intensity (0 = clear, 1 = maximum fog).
        /// </summary>
        public void SetFogDensity(float intensity)
        {
            intensity = Mathf.Clamp(intensity, 0f, 1f);
            _currentDensity = intensity * MaxDensity;

            if (_fogMat != null)
            {
                _fogMat.SetShaderParameter("density", _currentDensity);
                _fogMat.SetShaderParameter("fog_color", FogColor);
                _fogMat.SetShaderParameter("animation_speed", AnimationSpeed * (1f + intensity * 0.5f));
            }
        }

        /// <summary>Procedural fallback for <see cref="NoiseTexture"/>, built once on demand.
        ///
        /// The fog is entirely noise-driven — the shader does
        /// `smoothstep(0.3, 0.7, texture(noise_texture, uv).r)`. An unbound sampler2D samples
        /// black, so a null NoiseTexture meant noise = 0, smoothstep = 0, and fog that could
        /// never render at any density. Nothing in the repo ever set the texture, so the fog
        /// has always been invisible, silently, in all six weather-enabled genres. A shipped
        /// component's headline feature shouldn't depend on an asset nobody supplies.</summary>
        private static Texture2D DefaultNoise()
        {
            if (_defaultNoise != null) return _defaultNoise;
            _defaultNoise = new NoiseTexture2D
            {
                Width = 256,
                Height = 256,
                Seamless = true,          // fog scrolls and repeats; a seam would band across it
                Noise = new FastNoiseLite
                {
                    NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
                    Frequency = 0.008f,   // broad, slow drifts rather than static-like speckle
                    FractalOctaves = 3,
                },
            };
            return _defaultNoise;
        }
        private static Texture2D? _defaultNoise;

        private void ApplyFogShaderParams()
        {
            if (_fogMat == null) return;
            _fogMat.SetShaderParameter("fog_color", FogColor);
            _fogMat.SetShaderParameter("density", _currentDensity);
            _fogMat.SetShaderParameter("animation_speed", AnimationSpeed);

            // Always bind something: an authored texture when given, otherwise the procedural
            // default. Skipping the assignment is what made the failure silent.
            _fogMat.SetShaderParameter("noise_texture", NoiseTexture ?? DefaultNoise());
        }

        public override void _ExitTree()
        {
            base._ExitTree();
        }
    }
}

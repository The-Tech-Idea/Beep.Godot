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

uniform sampler2D noise_texture : repeat_enable, filter_nearest;
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

            // Update fog density based on weather intensity
            if (_weather != null && EnableWeatherIntegration)
            {
                // Assume weather has a WeatherIntensity property (0-1)
                // For now, use CurrentWeather as proxy (this will be wired up later)
                SetFogDensity(0.2f);  // Default density; adjust based on actual weather integration
            }
        }

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

        private void ApplyFogShaderParams()
        {
            if (_fogMat == null) return;
            _fogMat.SetShaderParameter("fog_color", FogColor);
            _fogMat.SetShaderParameter("density", _currentDensity);
            _fogMat.SetShaderParameter("animation_speed", AnimationSpeed);

            if (NoiseTexture != null)
                _fogMat.SetShaderParameter("noise_texture", NoiseTexture);
        }

        public override void _ExitTree()
        {
            base._ExitTree();
        }
    }
}

using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Vignette overlay. Applies a radial darkening shader to the parent Control
    /// (or CanvasLayer's first Control). Adjustable intensity and color. Creates
    /// the shader inline — no external .gdshader file needed.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class VignetteComponent : UIComponent
    {
        [Export] public float Intensity { get; set; } = 1.0f;
        [Export] public Color Tint { get; set; } = new(0, 0, 0, 1);
        [Export] public float Softness { get; set; } = 0.45f;
        [Export] public float Radius { get; set; } = 0.5f;

        private const string ShaderCode = @"
shader_type canvas_item;
uniform float intensity : hint_range(0.0, 4.0) = 1.0;
uniform vec4 tint : source_color = vec4(0.0, 0.0, 0.0, 1.0);
uniform float softness : hint_range(0.0, 1.0) = 0.45;
uniform float radius : hint_range(0.0, 1.0) = 0.5;

void fragment() {
    vec4 col = texture(TEXTURE, UV);
    float d = distance(UV, vec2(0.5));
    float v = smoothstep(radius, radius - softness, d);
    COLOR = mix(col, col * tint, intensity * (1.0 - v));
}
";

        private ShaderMaterial? _mat;

        public override void _Ready()
        {
            base._Ready();
            Apply();
        }

        public override void _Process(double delta)
        {
            // Push export values into the shader uniforms when they change (editor live-edit).
            if (_mat != null && Engine.IsEditorHint())
            {
                _mat.SetShaderParameter("intensity", Intensity);
                _mat.SetShaderParameter("tint", Tint);
                _mat.SetShaderParameter("softness", Softness);
                _mat.SetShaderParameter("radius", Radius);
            }
        }

        public void Apply()
        {
            if (GetParent() is not CanvasItem ci) return;
            var shader = new Shader { Code = ShaderCode };
            _mat = new ShaderMaterial { Shader = shader };
            _mat.SetShaderParameter("intensity", Intensity);
            _mat.SetShaderParameter("tint", Tint);
            _mat.SetShaderParameter("softness", Softness);
            _mat.SetShaderParameter("radius", Radius);
            ci.Material = _mat;
        }
    }
}

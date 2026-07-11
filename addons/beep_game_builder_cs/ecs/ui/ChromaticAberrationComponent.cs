using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Chromatic aberration / RGB-split overlay. Applies a shader to the parent
    /// Control (or CanvasLayer Control) that offsets the red and blue channels
    /// outward from center. Adjustable strength. Creates the shader inline.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ChromaticAberrationComponent : UIComponent
    {
        [Export] public float Strength { get; set; } = 0.004f;

        private const string ShaderCode = @"
shader_type canvas_item;
uniform float strength : hint_range(0.0, 0.05) = 0.004;

void fragment() {
    vec2 dir = UV - vec2(0.5);
    float r = texture(TEXTURE, UV - dir * strength).r;
    float g = texture(TEXTURE, UV).g;
    float b = texture(TEXTURE, UV + dir * strength).b;
    float a = texture(TEXTURE, UV).a;
    COLOR = vec4(r, g, b, a);
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
            if (_mat != null && Engine.IsEditorHint())
                _mat.SetShaderParameter("strength", Strength);
        }

        public void Apply()
        {
            if (GetParent() is not CanvasItem ci) return;
            var shader = new Shader { Code = ShaderCode };
            _mat = new ShaderMaterial { Shader = shader };
            _mat.SetShaderParameter("strength", Strength);
            ci.Material = _mat;
        }
    }
}

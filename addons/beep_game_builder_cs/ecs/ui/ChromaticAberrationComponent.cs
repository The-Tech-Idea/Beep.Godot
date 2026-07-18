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

        // Post-process overlay: split the SCREEN behind this Control, not the
        // Control's own blank TEXTURE. Sample via hint_screen_texture / SCREEN_UV
        // so a full-rect overlay aberrates the whole rendered scene.
        private const string ShaderCode = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform float strength : hint_range(0.0, 0.05) = 0.004;

void fragment() {
    vec2 dir = UV - vec2(0.5);
    float r = texture(screen_tex, SCREEN_UV - dir * strength).r;
    float g = texture(screen_tex, SCREEN_UV).g;
    float b = texture(screen_tex, SCREEN_UV + dir * strength).b;
    float a = texture(screen_tex, SCREEN_UV).a;
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
            if (GetParent() is not CanvasItem ci)
            {
                GD.PushWarning($"[{Name}] ChromaticAberrationComponent needs a CanvasItem parent (a full-rect Control/ColorRect) to apply the shader to; got '{GetParent()?.GetType().Name ?? "null"}'.");
                return;
            }
            var shader = new Shader { Code = ShaderCode };
            _mat = new ShaderMaterial { Shader = shader };
            _mat.SetShaderParameter("strength", Strength);
            ci.Material = _mat;
        }
    }
}

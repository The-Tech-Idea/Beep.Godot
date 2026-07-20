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

        // Post-process overlay: sample the SCREEN behind this Control, not the
        // Control's own (usually blank) TEXTURE. `texture(TEXTURE, UV)` darkened
        // nothing because a plain overlay Control has no texture of its own. The
        // node must cover the viewport (a full-rect ColorRect/Control) for the
        // vignette to frame the whole scene.
        private const string ShaderCode = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform float intensity : hint_range(0.0, 4.0) = 1.0;
uniform vec4 tint : source_color = vec4(0.0, 0.0, 0.0, 1.0);
uniform float softness : hint_range(0.0, 1.0) = 0.45;
uniform float radius : hint_range(0.0, 1.0) = 0.5;

void fragment() {
    vec4 col = texture(screen_tex, SCREEN_UV);
    float d = distance(UV, vec2(0.5));
    float v = smoothstep(radius, radius - softness, d);
    COLOR = mix(col, col * tint, intensity * (1.0 - v));
}
";

        private ShaderMaterial? _mat;
        // The parent CanvasItem's material before we overlaid the vignette, so _ExitTree restores
        // it instead of nulling out a material the node legitimately had. _replacedMaterial guards
        // the restore so we only touch it when Apply() actually swapped ours in.
        private Material? _priorMaterial;
        private bool _replacedMaterial;

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
            if (GetParent() is not CanvasItem ci)
            {
                GD.PushWarning($"[{Name}] VignetteComponent needs a CanvasItem parent (a full-rect Control/ColorRect) to apply the shader to; got '{GetParent()?.GetType().Name ?? "null"}'.");
                return;
            }
            var shader = new Shader { Code = ShaderCode };
            _mat = new ShaderMaterial { Shader = shader };
            _mat.SetShaderParameter("intensity", Intensity);
            _mat.SetShaderParameter("tint", Tint);
            _mat.SetShaderParameter("softness", Softness);
            _mat.SetShaderParameter("radius", Radius);
            if (!_replacedMaterial) _priorMaterial = ci.Material;   // remember what was there, once
            _replacedMaterial = true;
            ci.Material = _mat;
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            // Restore the parent's original material so we don't leave a pooled/reused Control
            // stuck with our vignette shader. Only if Apply() actually replaced it.
            if (_replacedMaterial && GetParent() is CanvasItem ci && GodotObject.IsInstanceValid(ci))
                ci.Material = _priorMaterial;
        }
    }
}

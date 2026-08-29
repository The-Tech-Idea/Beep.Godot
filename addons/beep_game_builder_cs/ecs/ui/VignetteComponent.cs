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
        [Export] public float Intensity { get => _intensity; set { _intensity = value; _mat?.SetShaderParameter("intensity", EffectiveIntensity); } }
        [Export] public Color Tint { get => _tint; set { _tint = value; _mat?.SetShaderParameter("tint", value); } }
        [Export] public float Softness { get => _softness; set { _softness = value; _mat?.SetShaderParameter("softness", EffectiveSoftness); } }
        [Export] public float Radius { get => _radius; set { _radius = value; _mat?.SetShaderParameter("radius", EffectiveRadius); } }

        public float EffectiveIntensity => Mathf.Clamp(float.IsFinite(Intensity) ? Intensity : 0f, 0f, 4f);
        public float EffectiveSoftness => Mathf.Clamp(float.IsFinite(Softness) ? Softness : 0.45f, 0.001f, 1f);
        public float EffectiveRadius => Mathf.Clamp(float.IsFinite(Radius) ? Radius : 0.5f, 0.001f, 1f);

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
        private float _intensity = 1.0f;
        private Color _tint = new(0, 0, 0, 1);
        private float _softness = 0.45f;
        private float _radius = 0.5f;
        // The parent CanvasItem's material before we overlaid the vignette, so _ExitTree restores
        // it instead of nulling out a material the node legitimately had. _replacedMaterial guards
        // the restore so we only touch it when Apply() actually swapped ours in.
        private Material? _priorMaterial;
        private bool _replacedMaterial;

        public override void _Ready()
        {
            base._Ready();
            SetProcess(Engine.IsEditorHint());
            Apply();
        }

        public override void _Process(double delta)
        {
            // Push export values into the shader uniforms when they change (editor live-edit).
            if (_mat != null && Engine.IsEditorHint())
            {
                _mat.SetShaderParameter("intensity", EffectiveIntensity);
                _mat.SetShaderParameter("tint", Tint);
                _mat.SetShaderParameter("softness", EffectiveSoftness);
                _mat.SetShaderParameter("radius", EffectiveRadius);
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
            _mat.SetShaderParameter("intensity", EffectiveIntensity);
            _mat.SetShaderParameter("tint", Tint);
            _mat.SetShaderParameter("softness", EffectiveSoftness);
            _mat.SetShaderParameter("radius", EffectiveRadius);
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
            SetProcess(false);
        }
    }
}

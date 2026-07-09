using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Shimmer loading placeholder. Attach to any Control to show animated loading skeleton.
    /// Blind — works for cards, text blocks, images, any placeholder while data loads.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class SkeletonLoaderComponent : EntityComponent
    {
        [Export] public float ShimmerSpeed { get; set; } = 2f;
        [Export] public Color BaseColor { get; set; } = new(0.18f, 0.18f, 0.22f, 1f);
        [Export] public Color ShimmerColor { get; set; } = new(0.25f, 0.25f, 0.3f, 1f);
        [Export] public bool AutoPlay { get; set; } = true;

        private Control? _control;
        private float _time;
        private ShaderMaterial? _shimmerMat;

        public override void _Ready()
        {
            base._Ready();
            _control = GetParent<Control>();
            if (_control == null) return;

            _shimmerMat = new ShaderMaterial();
            _shimmerMat.Shader = new Shader();
            _shimmerMat.Shader.Code = @"shader_type canvas_item;
uniform float time : hint_range(0,10) = 0;
uniform vec4 base_color : source_color;
uniform vec4 shimmer_color : source_color;
void fragment(){
    float shimmer = smoothstep(0.4, 0.6, sin(UV.x * 3.0 + time * 2.0) * 0.5 + 0.5);
    COLOR = mix(base_color, shimmer_color, shimmer);
}";
            _shimmerMat.SetShaderParameter("base_color", BaseColor);
            _shimmerMat.SetShaderParameter("shimmer_color", ShimmerColor);
            _control.Material = _shimmerMat;
        }

        public override void _Process(double delta)
        {
            if (!IsActive || !AutoPlay || _shimmerMat == null) return;
            _time += (float)delta * ShimmerSpeed;
            _shimmerMat.SetShaderParameter("time", _time % 10f);
        }

        public void Stop() { if (_control != null) _control.Material = null; }
    }
}

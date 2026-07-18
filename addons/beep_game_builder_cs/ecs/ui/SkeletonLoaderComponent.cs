using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Shimmer loading placeholder. Attach to any Control to show animated loading skeleton.
    /// Blind — works for cards, text blocks, images, any placeholder while data loads.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class SkeletonLoaderComponent : UIComponent
    {
        [Export] public float ShimmerSpeed { get; set; } = 2f;
        [Export] public Color BaseColor { get; set; } = new(0.18f, 0.18f, 0.22f, 1f);
        [Export] public Color ShimmerColor { get; set; } = new(0.25f, 0.25f, 0.3f, 1f);
        [Export] public bool AutoPlay { get; set; } = true;

        private Godot.Control? _control;
        private float _time;
        private ShaderMaterial? _shimmerMat;
        // The parent's material before we overlaid the shimmer, so Stop() restores it instead
        // of nulling out a material the Control legitimately had.
        private Material? _priorMaterial;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            _control = GetParent() as Godot.Control;
            if (_control == null)
            {
                GD.PushWarning($"[{Name}] SkeletonLoaderComponent needs a Control parent to overlay the shimmer on; got '{GetParent()?.GetType().Name ?? "null"}'. Parent it to the placeholder Control.");
                return;
            }

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
            _priorMaterial = _control.Material;   // remember what was there, if anything
            _control.Material = _shimmerMat;
        }

        public override void _Process(double delta)
        {
            if (!IsActive || !AutoPlay || _shimmerMat == null) return;
            _time += (float)delta * ShimmerSpeed;
            _shimmerMat.SetShaderParameter("time", _time % 10f);
        }

        public void Stop() { if (_control != null) _control.Material = _priorMaterial; }
    }
}

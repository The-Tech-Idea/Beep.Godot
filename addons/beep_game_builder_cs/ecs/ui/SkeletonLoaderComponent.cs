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
        // Palette-derived, not a literal. A colour baked into a component is a palette
        // pinned where no skin can reach it; these follow theme -> palette like every
        // other control. Computed, so a skin change is picked up with no invalidation.
        public Color BaseColor => UiSurface.Ink(UiSurface.Of(this));
        public Color ShimmerColor => UiSurface.Of(this);
        [Export]
        public bool AutoPlay
        {
            get => _autoPlay;
            set
            {
                if (_autoPlay == value) return;
                _autoPlay = value;
                if (!Engine.IsEditorHint() && IsInsideTree())
                {
                    if (_autoPlay) Start();
                    else Stop();
                }
            }
        }
        private bool _autoPlay = true;

        private Godot.Control? _control;
        private float _time;
        private ShaderMaterial? _shimmerMat;
        private bool _running;
        // The parent's material before we overlaid the shimmer, so Stop() restores it instead
        // of nulling out a material the Control legitimately had.
        private Material? _priorMaterial;

        public override void _Ready()
        {
            base._Ready();
            SetProcess(false);
            if (Engine.IsEditorHint()) return;
            _control = GetParent() as Godot.Control;
            if (_control == null)
            {
                GD.PushWarning($"[{Name}] SkeletonLoaderComponent needs a Control parent to overlay the shimmer on; got '{GetParent()?.GetType().Name ?? "null"}'. Parent it to the placeholder Control.");
                return;
            }

            EnsureMaterial();
            if (AutoPlay)
                Start();
        }

        private void EnsureMaterial()
        {
            if (_shimmerMat != null) return;

            _shimmerMat = new ShaderMaterial
            {
                Shader = new Shader
                {
                    Code = @"shader_type canvas_item;
uniform float time : hint_range(0,10) = 0;
uniform vec4 base_color : source_color;
uniform vec4 shimmer_color : source_color;
void fragment(){
    float shimmer = smoothstep(0.4, 0.6, sin(UV.x * 3.0 + time * 2.0) * 0.5 + 0.5);
    COLOR = mix(base_color, shimmer_color, shimmer);
}"
                }
            };
            RefreshMaterialColors();
        }

        private void RefreshMaterialColors()
        {
            if (_shimmerMat == null) return;
            _shimmerMat.SetShaderParameter("base_color", BaseColor);
            _shimmerMat.SetShaderParameter("shimmer_color", ShimmerColor);
        }

        public override void _Process(double delta)
        {
            if (!IsActive || !_running || _shimmerMat == null)
            {
                Stop();
                return;
            }
            _time += (float)delta * ShimmerSpeed;
            _shimmerMat.SetShaderParameter("time", _time % 10f);
        }

        public void Start()
        {
            if (!IsActive || _control == null) return;
            EnsureMaterial();
            if (_shimmerMat == null) return;
            RefreshMaterialColors();
            if (!_running)
                _priorMaterial = _control.Material;
            _control.Material = _shimmerMat;
            _running = true;
            SetProcess(true);
        }

        public void Stop()
        {
            if (_control != null && GodotObject.IsInstanceValid(_control) && _control.Material == _shimmerMat)
                _control.Material = _priorMaterial;
            _running = false;
            SetProcess(false);
        }

        public override void _Notification(int what)
        {
            if (what == Godot.Control.NotificationThemeChanged)
                RefreshMaterialColors();
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            // Restore the parent's original material — otherwise a loader freed on the normal path
            // (data arrives → skeleton removed, the same Control reused for real content) leaves the
            // shimmer ShaderMaterial stuck on the parent. Mirrors VignetteComponent's restore.
            Stop();
        }
    }
}

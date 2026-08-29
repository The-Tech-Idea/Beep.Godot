using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Camera zoom component. Attach to any Camera2D. Blind — smooth zoom in/out.
    /// Works for any camera — game world, minimap, UI preview.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class CameraZoomComponent : ControllerComponent
    {
        [Export] public Vector2 MinZoom { get; set; } = new(0.5f, 0.5f);
        [Export] public Vector2 MaxZoom { get; set; } = new(2f, 2f);
        [Export] public Vector2 ZoomStep { get; set; } = new(0.2f, 0.2f);
        [Export] public float SmoothSpeed { get; set; } = 5f;
        [Export] public float DefaultZoom { get; set; } = 1f;

        [Signal] public delegate void ZoomChangedEventHandler(Vector2 newZoom);

        private Camera2D? _cam;
        private Vector2 _targetZoom;
        private Vector2 _lastEmittedZoom;

        public float EffectiveSmoothSpeed => NonNegative(SmoothSpeed);
        public float EffectiveDefaultZoom => Mathf.Max(0.001f, FiniteOr(DefaultZoom, 1f));

        public override void _Ready()
        {
            base._Ready();
            ResolveCamera();
            if (_cam != null)
            {
                _cam.Zoom = ClampZoom(_cam.Zoom);
                _targetZoom = _cam.Zoom;
                _lastEmittedZoom = _cam.Zoom;
            }
            else if (!Engine.IsEditorHint())
                GD.PushWarning($"[{Name}] CameraZoomComponent needs a Camera2D parent to zoom; got '{GetParent()?.GetType().Name ?? "null"}'. Every zoom call will no-op. Parent it to the Camera2D.");
        }

        public override void _Input(InputEvent @event)
        {
            ResolveCamera();
            if (Engine.IsEditorHint() || _cam == null || !IsActive || !(@event is InputEventMouseButton mb)) return;
            if (mb.Pressed && mb.ButtonIndex == MouseButton.WheelUp)
            {
                ZoomIn();
                GetViewport()?.SetInputAsHandled();
            }
            else if (mb.Pressed && mb.ButtonIndex == MouseButton.WheelDown)
            {
                ZoomOut();
                GetViewport()?.SetInputAsHandled();
            }
        }

        public void ZoomIn()
        {
            ResolveCamera();
            if (_cam == null || !IsActive) return;
            _targetZoom = ClampZoom(_cam.Zoom - EffectiveZoomStep());
        }

        public void ZoomOut()
        {
            ResolveCamera();
            if (_cam == null || !IsActive) return;
            _targetZoom = ClampZoom(_cam.Zoom + EffectiveZoomStep());
        }

        public void SetZoom(float level)
        {
            if (!IsActive) return;
            _targetZoom = ClampZoom(new Vector2(level, level));
        }

        public void ResetZoom()
        {
            if (!IsActive) return;
            _targetZoom = ClampZoom(new Vector2(DefaultZoom, DefaultZoom));
        }

        public override void _Process(double delta)
        {
            ResolveCamera();
            if (Engine.IsEditorHint() || _cam == null || !IsActive) return;
            _cam.Zoom = SanitizeZoom(_cam.Zoom);
            _targetZoom = ClampZoom(_targetZoom);
            float t = Mathf.Clamp(EffectiveSmoothSpeed * DeltaSeconds(delta), 0f, 1f);
            _cam.Zoom = t >= 1f ? _targetZoom : _cam.Zoom.Lerp(_targetZoom, t);

            // Only emit if zoom changed significantly
            if (_cam.Zoom.DistanceTo(_lastEmittedZoom) > 0.01f)
            {
                EmitSignal(SignalName.ZoomChanged, _cam.Zoom);
                _lastEmittedZoom = _cam.Zoom;
            }
        }

        private void ResolveCamera()
        {
            if (_cam == null || !GodotObject.IsInstanceValid(_cam))
                _cam = GetParent() as Camera2D;
        }

        private Vector2 EffectiveZoomStep()
            => new(NonNegativeAbs(ZoomStep.X), NonNegativeAbs(ZoomStep.Y));

        private Vector2 ClampZoom(Vector2 value)
        {
            Vector2 minZoom = SanitizeZoom(MinZoom);
            Vector2 maxZoom = SanitizeZoom(MaxZoom);
            Vector2 min = new(Mathf.Min(minZoom.X, maxZoom.X), Mathf.Min(minZoom.Y, maxZoom.Y));
            Vector2 max = new(Mathf.Max(minZoom.X, maxZoom.X), Mathf.Max(minZoom.Y, maxZoom.Y));
            return SanitizeZoom(value, EffectiveDefaultZoom).Clamp(min, max);
        }

        private static float DeltaSeconds(double delta) =>
            double.IsFinite(delta) ? Mathf.Max(0f, (float)delta) : 0f;

        private static float NonNegative(float value) =>
            float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;

        private static float NonNegativeAbs(float value) =>
            float.IsFinite(value) ? Mathf.Max(0f, Mathf.Abs(value)) : 0f;

        private static float FiniteOr(float value, float fallback) =>
            float.IsFinite(value) ? value : fallback;

        private static Vector2 SanitizeZoom(Vector2 zoom, float fallback = 1f) => new(
            Mathf.Max(0.001f, FiniteOr(zoom.X, fallback)),
            Mathf.Max(0.001f, FiniteOr(zoom.Y, fallback)));
    }
}

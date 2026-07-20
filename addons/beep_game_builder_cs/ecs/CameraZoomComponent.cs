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

        public override void _Ready()
        {
            base._Ready();
            _cam = GetParent() as Camera2D;
            if (_cam != null)
            {
                _targetZoom = _cam.Zoom;
                _lastEmittedZoom = _cam.Zoom;
            }
            else if (!Engine.IsEditorHint())
                GD.PushWarning($"[{Name}] CameraZoomComponent needs a Camera2D parent to zoom; got '{GetParent()?.GetType().Name ?? "null"}'. Every zoom call will no-op. Parent it to the Camera2D.");
        }

        public override void _Input(InputEvent @event)
        {
            if (_cam == null || !IsActive || !(@event is InputEventMouseButton mb)) return;
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
            if (_cam == null || !IsActive) return;
            _targetZoom = (_cam.Zoom - ZoomStep).Clamp(MinZoom, MaxZoom);
        }

        public void ZoomOut()
        {
            if (_cam == null || !IsActive) return;
            _targetZoom = (_cam.Zoom + ZoomStep).Clamp(MinZoom, MaxZoom);
        }

        public void SetZoom(float level)
        {
            _targetZoom = new Vector2(level, level).Clamp(MinZoom, MaxZoom);
        }

        public void ResetZoom() => _targetZoom = new Vector2(DefaultZoom, DefaultZoom);

        public override void _Process(double delta)
        {
            if (_cam == null || !IsActive) return;
            _cam.Zoom = _cam.Zoom.Lerp(_targetZoom, SmoothSpeed * (float)delta);

            // Only emit if zoom changed significantly
            if (_cam.Zoom.DistanceTo(_lastEmittedZoom) > 0.01f)
            {
                EmitSignal(SignalName.ZoomChanged, _cam.Zoom);
                _lastEmittedZoom = _cam.Zoom;
            }
        }
    }
}

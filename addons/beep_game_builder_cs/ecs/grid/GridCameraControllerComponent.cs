using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Pan/zoom controller for authored top-down and isometric 2D maps.
    ///
    /// Attach this as a child of a Camera2D. It supports mouse drag, wheel zoom,
    /// keyboard/edge pan, and optional world bounds without depending on TileMap.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridCameraControllerComponent : ControllerComponent
    {
        [Signal] public delegate void CameraMovedEventHandler(Vector2 position);
        [Signal] public delegate void ZoomChangedEventHandler(Vector2 zoom);

        private Camera2D? _camera;
        private Vector2 _targetPosition;
        private Vector2 _targetZoom = Vector2.One;
        private bool _dragging;
        private Vector2 _lastEmittedPosition = new(float.NaN, float.NaN);
        private Vector2 _lastEmittedZoom = new(float.NaN, float.NaN);

        [Export] public bool UseMouseDrag { get; set; } = true;
        [Export] public MouseButton DragButton { get; set; } = MouseButton.Middle;
        [Export] public bool UseWheelZoom { get; set; } = true;
        [Export] public bool ZoomTowardMouse { get; set; } = true;
        [Export] public bool UseKeyboardPan { get; set; } = true;
        [Export] public bool UseEdgePan { get; set; } = false;

        [ExportGroup("Motion")]
        [Export(PropertyHint.Range, "50,4000,10")] public float PanSpeed { get; set; } = 900f;
        [Export(PropertyHint.Range, "0.02,1,0.01")] public float ZoomStep { get; set; } = 0.15f;
        [Export] public Vector2 MinZoom { get; set; } = new(0.45f, 0.45f);
        [Export] public Vector2 MaxZoom { get; set; } = new(3f, 3f);
        [Export(PropertyHint.Range, "0,40,0.5")] public float PositionSmoothing { get; set; } = 14f;
        [Export(PropertyHint.Range, "0,40,0.5")] public float ZoomSmoothing { get; set; } = 16f;

        [ExportGroup("Keyboard")]
        [Export] public Key PanUpKey { get; set; } = Key.W;
        [Export] public Key PanDownKey { get; set; } = Key.S;
        [Export] public Key PanLeftKey { get; set; } = Key.A;
        [Export] public Key PanRightKey { get; set; } = Key.D;
        [Export] public bool ArrowKeysAlsoPan { get; set; } = true;

        [ExportGroup("Edge Pan")]
        [Export(PropertyHint.Range, "1,96,1")] public int EdgePanPixels { get; set; } = 18;
        [Export(PropertyHint.Range, "0.1,4,0.1")] public float EdgePanMultiplier { get; set; } = 1f;

        [ExportGroup("Bounds")]
        [Export] public bool UseBounds { get; set; } = false;
        [Export] public Vector2 BoundsPosition { get; set; } = Vector2.Zero;
        [Export] public Vector2 BoundsSize { get; set; } = new(4096, 4096);
        [Export] public bool KeepViewportInsideBounds { get; set; } = true;

        public float EffectivePanSpeed => NonNegativeFinite(PanSpeed);
        public float EffectiveZoomStep => NonNegativeFinite(ZoomStep);
        public float EffectivePositionSmoothing => NonNegativeFinite(PositionSmoothing);
        public float EffectiveZoomSmoothing => NonNegativeFinite(ZoomSmoothing);
        public Vector2 EffectiveBoundsSize => PositiveVector(BoundsSize, new Vector2(4096, 4096));

        public override void _Ready()
        {
            base._Ready();
            _camera = GetParent() as Camera2D;
            if (_camera == null)
            {
                if (!Engine.IsEditorHint())
                    GD.PushWarning($"[{Name}] GridCameraControllerComponent needs a Camera2D parent; got '{GetParent()?.GetType().Name ?? "null"}'.");
                UpdateConfigurationWarnings();
                return;
            }

            (Vector2 minZoom, Vector2 maxZoom) = EffectiveZoomRange();
            _targetPosition = FiniteVector(_camera.GlobalPosition, Vector2.Zero);
            _targetZoom = FiniteVector(_camera.Zoom, Vector2.One).Clamp(minZoom, maxZoom);
            ApplyTargets(1f);
            UpdateConfigurationWarnings();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (GetParent() is not Camera2D)
                return new[] { "GridCameraControllerComponent must be a child of a Camera2D." };
            if (!FinitePositive(MinZoom.X) || !FinitePositive(MinZoom.Y) || !FinitePositive(MaxZoom.X) || !FinitePositive(MaxZoom.Y))
                return new[] { "MinZoom and MaxZoom must be finite positive values." };
            if (MinZoom.X > MaxZoom.X || MinZoom.Y > MaxZoom.Y)
                return new[] { "MinZoom must be less than or equal to MaxZoom on both axes." };
            if (UseBounds && (!FinitePositive(BoundsSize.X) || !FinitePositive(BoundsSize.Y)))
                return new[] { "BoundsSize must be finite and greater than zero when UseBounds is enabled." };
            return System.Array.Empty<string>();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (_camera == null || !IsActive) return;

            if (@event is InputEventMouseButton mouse)
            {
                HandleMouseButton(mouse);
                return;
            }

            if (UseMouseDrag && _dragging && @event is InputEventMouseMotion motion)
            {
                PanByScreenDelta(-motion.Relative);
                GetViewport()?.SetInputAsHandled();
            }
        }

        public override void _Process(double delta)
        {
            if (_camera == null || !IsActive) return;
            float step = DeltaSeconds(delta);

            Vector2 direction = Vector2.Zero;
            if (UseKeyboardPan)
                direction += KeyboardDirection();
            if (UseEdgePan && !Engine.IsEditorHint())
                direction += EdgeDirection();

            if (direction.LengthSquared() > 0f)
            {
                direction = direction.Normalized();
                PanByWorldDelta(direction * EffectivePanSpeed * step / AverageZoom(_targetZoom));
            }

            ApplyTargets(step);
            EmitChanges();
        }

        public void FocusWorld(Vector2 worldPosition, bool immediate = false)
        {
            _targetPosition = ClampPosition(FiniteVector(worldPosition, _targetPosition), _targetZoom);
            if (immediate) ApplyTargets(1f);
        }

        public void PanByWorldDelta(Vector2 worldDelta)
        {
            if (!IsFinite(worldDelta))
                return;

            _targetPosition = ClampPosition(_targetPosition + worldDelta, _targetZoom);
        }

        public void PanByScreenDelta(Vector2 screenDelta)
        {
            PanByWorldDelta(screenDelta / AverageZoom(_targetZoom));
        }

        public void SetZoomLevel(float uniformZoom, bool immediate = false)
            => SetZoom(new Vector2(uniformZoom, uniformZoom), immediate);

        public void SetZoom(Vector2 zoom, bool immediate = false)
        {
            (Vector2 minZoom, Vector2 maxZoom) = EffectiveZoomRange();
            _targetZoom = FiniteVector(zoom, _targetZoom).Clamp(minZoom, maxZoom);
            _targetPosition = ClampPosition(_targetPosition, _targetZoom);
            if (immediate) ApplyTargets(1f);
        }

        public void ZoomAtWorldPoint(Vector2 worldPoint, float zoomDelta, bool immediate = false)
        {
            float delta = float.IsFinite(zoomDelta) ? zoomDelta : 0f;
            (Vector2 minZoom, Vector2 maxZoom) = EffectiveZoomRange();
            Vector2 oldZoom = FiniteVector(_targetZoom, Vector2.One).Clamp(minZoom, maxZoom);
            Vector2 newZoom = (oldZoom + new Vector2(delta, delta)).Clamp(minZoom, maxZoom);
            if (newZoom.IsEqualApprox(oldZoom)) return;

            Vector2 ratio = new(oldZoom.X / newZoom.X, oldZoom.Y / newZoom.Y);
            Vector2 focus = FiniteVector(worldPoint, _targetPosition);
            _targetPosition = focus - (focus - _targetPosition) * ratio;
            _targetZoom = newZoom;
            _targetPosition = ClampPosition(_targetPosition, _targetZoom);
            if (immediate) ApplyTargets(1f);
        }

        public Vector2 ClampPosition(Vector2 worldPosition, Vector2 zoom)
        {
            Vector2 position = FiniteVector(worldPosition, _targetPosition);
            if (!UseBounds)
                return position;

            Vector2 boundsSize = EffectiveBoundsSize;
            if (boundsSize.X <= 0f || boundsSize.Y <= 0f)
                return position;

            Rect2 bounds = new(FiniteVector(BoundsPosition, Vector2.Zero), boundsSize);
            Vector2 min = bounds.Position;
            Vector2 max = bounds.End;

            if (KeepViewportInsideBounds && GetViewport() is { } viewport)
            {
                Vector2 safeZoom = FiniteVector(zoom, Vector2.One);
                safeZoom.X = FinitePositive(safeZoom.X) ? safeZoom.X : 1f;
                safeZoom.Y = FinitePositive(safeZoom.Y) ? safeZoom.Y : 1f;
                Vector2 halfView = viewport.GetVisibleRect().Size * 0.5f / safeZoom;
                min += halfView;
                max -= halfView;
            }

            float x = min.X <= max.X ? Mathf.Clamp(position.X, min.X, max.X) : bounds.GetCenter().X;
            float y = min.Y <= max.Y ? Mathf.Clamp(position.Y, min.Y, max.Y) : bounds.GetCenter().Y;
            return new Vector2(x, y);
        }

        private void HandleMouseButton(InputEventMouseButton mouse)
        {
            if (UseMouseDrag && mouse.ButtonIndex == DragButton)
            {
                _dragging = mouse.Pressed;
                GetViewport()?.SetInputAsHandled();
                return;
            }

            if (!UseWheelZoom || !mouse.Pressed)
                return;

            if (mouse.ButtonIndex == MouseButton.WheelUp)
            {
                ZoomFromMouse(EffectiveZoomStep);
                GetViewport()?.SetInputAsHandled();
            }
            else if (mouse.ButtonIndex == MouseButton.WheelDown)
            {
                ZoomFromMouse(-EffectiveZoomStep);
                GetViewport()?.SetInputAsHandled();
            }
        }

        private void ZoomFromMouse(float delta)
        {
            if (ZoomTowardMouse && GetViewport() is { } && _camera != null)
                ZoomAtWorldPoint(_camera.GetGlobalMousePosition(), delta);
            else
                SetZoom(_targetZoom + new Vector2(delta, delta));
        }

        private Vector2 KeyboardDirection()
        {
            Vector2 direction = Vector2.Zero;
            if (Input.IsKeyPressed(PanLeftKey) || (ArrowKeysAlsoPan && Input.IsKeyPressed(Key.Left))) direction.X -= 1f;
            if (Input.IsKeyPressed(PanRightKey) || (ArrowKeysAlsoPan && Input.IsKeyPressed(Key.Right))) direction.X += 1f;
            if (Input.IsKeyPressed(PanUpKey) || (ArrowKeysAlsoPan && Input.IsKeyPressed(Key.Up))) direction.Y -= 1f;
            if (Input.IsKeyPressed(PanDownKey) || (ArrowKeysAlsoPan && Input.IsKeyPressed(Key.Down))) direction.Y += 1f;
            return direction;
        }

        private Vector2 EdgeDirection()
        {
            if (GetViewport() is not { } viewport)
                return Vector2.Zero;

            Vector2 mouse = viewport.GetMousePosition();
            Vector2 size = viewport.GetVisibleRect().Size;
            float edge = Mathf.Max(1, EdgePanPixels);
            float multiplier = NonNegativeFinite(EdgePanMultiplier);
            Vector2 direction = Vector2.Zero;

            if (mouse.X <= edge) direction.X -= multiplier;
            else if (mouse.X >= size.X - edge) direction.X += multiplier;
            if (mouse.Y <= edge) direction.Y -= multiplier;
            else if (mouse.Y >= size.Y - edge) direction.Y += multiplier;

            return direction;
        }

        private void ApplyTargets(float delta)
        {
            if (_camera == null) return;

            _targetPosition = ClampPosition(_targetPosition, _targetZoom);
            float positionWeight = SmoothingWeight(EffectivePositionSmoothing, delta);
            float zoomWeight = SmoothingWeight(EffectiveZoomSmoothing, delta);

            _camera.GlobalPosition = _camera.GlobalPosition.Lerp(_targetPosition, positionWeight);
            _camera.Zoom = _camera.Zoom.Lerp(_targetZoom, zoomWeight);
            if (positionWeight >= 1f) _camera.GlobalPosition = _targetPosition;
            if (zoomWeight >= 1f) _camera.Zoom = _targetZoom;
        }

        private static float SmoothingWeight(float smoothing, float delta)
        {
            if (smoothing <= 0f || delta >= 1f) return 1f;
            return 1f - Mathf.Exp(-smoothing * delta);
        }

        private static float AverageZoom(Vector2 zoom)
        {
            float x = FinitePositive(zoom.X) ? zoom.X : 1f;
            float y = FinitePositive(zoom.Y) ? zoom.Y : 1f;
            return Mathf.Max(0.001f, (x + y) * 0.5f);
        }

        private (Vector2 Min, Vector2 Max) EffectiveZoomRange()
        {
            Vector2 min = PositiveVector(MinZoom, new Vector2(0.45f, 0.45f));
            Vector2 max = PositiveVector(MaxZoom, new Vector2(3f, 3f));
            if (min.X > max.X) (min.X, max.X) = (max.X, min.X);
            if (min.Y > max.Y) (min.Y, max.Y) = (max.Y, min.Y);
            return (min, max);
        }

        private static Vector2 PositiveVector(Vector2 value, Vector2 fallback)
        {
            float x = FinitePositive(value.X) ? value.X : fallback.X;
            float y = FinitePositive(value.Y) ? value.Y : fallback.Y;
            return new Vector2(x, y);
        }

        private static Vector2 FiniteVector(Vector2 value, Vector2 fallback)
        {
            float x = float.IsFinite(value.X) ? value.X : fallback.X;
            float y = float.IsFinite(value.Y) ? value.Y : fallback.Y;
            return new Vector2(x, y);
        }

        private static float NonNegativeFinite(float value)
            => float.IsFinite(value) && value > 0f ? value : 0f;

        private static bool FinitePositive(float value)
            => float.IsFinite(value) && value > 0f;

        private static bool IsFinite(Vector2 value)
            => float.IsFinite(value.X) && float.IsFinite(value.Y);

        private static float DeltaSeconds(double delta)
            => double.IsFinite(delta) && delta > 0.0 ? (float)Mathf.Min(delta, 86400.0) : 0f;

        private void EmitChanges()
        {
            if (_camera == null) return;

            if (!_camera.GlobalPosition.IsEqualApprox(_lastEmittedPosition))
            {
                EmitSignal(SignalName.CameraMoved, _camera.GlobalPosition);
                _lastEmittedPosition = _camera.GlobalPosition;
            }

            if (!_camera.Zoom.IsEqualApprox(_lastEmittedZoom))
            {
                EmitSignal(SignalName.ZoomChanged, _camera.Zoom);
                _lastEmittedZoom = _camera.Zoom;
            }
        }
    }
}

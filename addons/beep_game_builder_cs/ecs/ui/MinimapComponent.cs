using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Circular minimap. Extends Control (one documented exception, matching the
    /// ProgressRingComponent precedent) because it needs _Draw to render blips.
    /// Place it as a child of a HUD CanvasLayer. Blips are nodes in a tracked
    /// group (default "minimap_blips"); each blip's global position is mapped into
    /// the minimap's world radius. The player (optional, named "Player" or the
    /// configured path) is centered.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class MinimapComponent : Godot.Control
    {
        [Export] public float WorldRadius { get; set; } = 800f;
        [Export] public float BlipSize { get; set; } = 3f;

        // No exported Colors. The four that used to live here — a 50%-black disc, a white rim,
        // a green player dot and a red blip — were literals, so the minimap was the same black
        // circle with the same red dots in all 50 skins while every control around it followed
        // the palette. A colour is either the theme's or the texture's.
        private Color _plate, _rim, _player, _blip;

        /// <summary>Resolve once per draw, through the same helper the panels and badges use,
        /// so the minimap and the HUD plate beside it agree on what the surface colour is.</summary>
        private void ResolveColors()
        {
            _plate = UiSurface.Of(this);
            _rim = UiSurface.Ink(_plate);
            // The player is where you are (positive), a blip is a thing to react to (alert) —
            // which is what the palette's semantic roles mean.
            _player = UiSurface.Semantic(this, UiSurface.Role.Success);
            _blip = UiSurface.Semantic(this, UiSurface.Role.Danger);
        }
        [Export] public string BlipGroup { get; set; } = "minimap_blips";
        [Export] public NodePath PlayerPath { get; set; } = new("../Player");

        /// <summary>When <see cref="PlayerPath"/> resolves to nothing, centre on the viewport's
        /// active <see cref="Camera2D"/> instead. City-builder and strategy scenes have no player
        /// avatar at all — the view IS the camera — so without this their minimap can only ever
        /// draw an empty circle.</summary>
        [Export] public bool FollowCameraWhenNoPlayer { get; set; } = true;

        private Node2D? _center;
        private bool _trackingCamera;

        public override void _Ready()
        {
            // Only a floor, never an override — assigning unconditionally silently discarded
            // the size set in the scene (a 180px minimap came out 120px).
            if (CustomMinimumSize == Vector2.Zero) CustomMinimumSize = new Vector2(120, 120);
            if (!Engine.IsEditorHint())
                CallDeferred(nameof(ResolveCenter));
        }

        private bool _centerWarned;

        /// <summary>Resolve what the map is centred on: the configured player, else the active
        /// camera. Blips are drawn relative to this, so with neither there is no frame of
        /// reference and the map can only draw its own frame.</summary>
        private void ResolveCenter()
        {
            _trackingCamera = false;
            _center = PlayerPath.IsEmpty ? null : GetNodeOrNull<Node2D>(PlayerPath);

            if (_center == null && FollowCameraWhenNoPlayer && GetViewport()?.GetCamera2D() is { } cam)
            {
                _center = cam;               // Camera2D is a Node2D, so GlobalPosition still applies
                _trackingCamera = true;
            }

            if (_center == null && !_centerWarned)
            {
                GD.PushWarning($"[{Name}] MinimapComponent has nothing to centre on — PlayerPath '{PlayerPath}' resolved to no Node2D and the viewport has no active Camera2D. It draws its frame and no blips.");
                _centerWarned = true;
            }
        }

        public override void _Process(double delta)
        {
            QueueRedraw();
        }

        /// <summary>Repaint when the skin changes. Without this the minimap kept the previous
        /// palette's colours until something else happened to invalidate it — every other drawn
        /// component here already does the same.</summary>
        public override void _Notification(int what)
        {
            if (what == NotificationThemeChanged) QueueRedraw();
        }

        public override void _Draw()
        {
            Vector2 center = Size * 0.5f;
            float r = Mathf.Min(center.X, center.Y);
            ResolveColors();

            DrawCircle(center, r, _plate);
            DrawArc(center, r, 0, Mathf.Tau, 48, _rim, 2f);

            if (Engine.IsEditorHint()) return;

            if (_center == null || !GodotObject.IsInstanceValid(_center)) ResolveCenter();
            if (_center == null) return;
            Vector2 origin = _center.GlobalPosition;

            foreach (var n in GetTree().GetNodesInGroup(BlipGroup))
            {
                if (n is not Node2D blip || !GodotObject.IsInstanceValid(blip)) continue;
                Vector2 rel = (blip.GlobalPosition - origin).LimitLength(WorldRadius) / WorldRadius * r;
                DrawCircle(center + rel, BlipSize, _blip);
            }

            // A camera centre is a viewpoint, not an actor — drawing the same solid dot would
            // tell the player their avatar is there when no avatar exists.
            if (_trackingCamera)
            {
                float k = BlipSize * 2f;
                DrawRect(new Rect2(center - new Vector2(k, k), new Vector2(k * 2, k * 2)), _player, false, 1.5f);
            }
            else DrawCircle(center, BlipSize * 1.3f, _player);
        }
    }
}

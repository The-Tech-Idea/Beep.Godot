using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS;

/// <summary>Owned-actor strategic markers. Reads registry records without waking or suspending actors.</summary>
[Tool, GlobalClass]
public partial class ActorOverviewComponent : Node2D
{
    [Export] public NodePath PlayerPath { get; set; } = new("");
    [Export] public float ReferenceWorldUnits { get; set; } = 64;
    [Export] public float ShowBelowPixels { get; set; } = 12;
    [Export] public float MarkerRadiusPixels { get; set; } = 3;
    [Export] public double RefreshInterval { get; set; } = 0.1;
    [Export] public Color MarkerColor { get; set; } = new(0.2f, 0.95f, 0.55f);
    [Export] public Color SelectedColor { get; set; } = Colors.White;
    public bool IsOverviewActive { get; private set; }
    public int MarkerCount => _markers.Count;
    private PlayerContextComponent? _player;
    private readonly List<(string Id, Vector2 Position, bool Selected)> _markers = new();
    private double _elapsed;
    private Transform2D _lastTransform;

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;
        RefreshBindings();
    }

    public void RefreshBindings()
    {
        _player = PlayerPath.IsEmpty ? null : GetNodeOrNull<PlayerContextComponent>(PlayerPath);
        _markers.Clear(); IsOverviewActive = false; _elapsed = 0;
        RefreshOverview();
    }

    public override void _ExitTree()
    {
        _player = null; _markers.Clear(); IsOverviewActive = false;
        RequestReady();
        base._ExitTree();
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint()) return;
        _elapsed += double.IsFinite(delta) ? Math.Max(0, delta) : 0;
        double interval = double.IsFinite(RefreshInterval) ? Math.Clamp(RefreshInterval, 0.02, 1) : 0.1;
        if (_elapsed >= interval || GetGlobalTransformWithCanvas() != _lastTransform) RefreshOverview();
    }

    public void RefreshOverview()
    {
        _elapsed = 0;
        _markers.Clear();
        _lastTransform = GetGlobalTransformWithCanvas();
        var registry = GodotObject.IsInstanceValid(_player) ? _player!.Registry : null;
        float units = float.IsFinite(ReferenceWorldUnits) ? Mathf.Max(1, ReferenceWorldUnits) : 64;
        float threshold = float.IsFinite(ShowBelowPixels) ? Mathf.Max(0, ShowBelowPixels) : 12;
        Transform2D worldToScreen = Math.Abs(GlobalTransform.Determinant()) > 0.000001f
            ? _lastTransform * GlobalTransform.AffineInverse() : Transform2D.Identity;
        float pixels = Mathf.Max(worldToScreen.X.Length(), worldToScreen.Y.Length()) * units;
        IsOverviewActive = IsVisibleInTree() && GodotObject.IsInstanceValid(registry)
            && _player!.PlayerId.Length > 0 && threshold > 0 && float.IsFinite(pixels)
            && Math.Abs(_lastTransform.Determinant()) > 0.000001f
            && pixels <= threshold * (IsOverviewActive ? 1.5f : 1);
        if (IsOverviewActive)
        {
            Rect2 viewport = GetViewportRect();
            Transform2D inverse = _lastTransform.AffineInverse();
            Vector2 first = ToGlobal(inverse * viewport.Position);
            Rect2 world = new(first, Vector2.Zero);
            foreach (Vector2 corner in new[] { new Vector2(viewport.End.X, viewport.Position.Y), viewport.End,
                new Vector2(viewport.Position.X, viewport.End.Y) }) world = world.Expand(ToGlobal(inverse * corner));
            var selected = new HashSet<string>(_player!.GetSelectedActors());
            foreach (string id in registry!.QueryActors(world, _player.PlayerId, true))
            {
                var actor = registry.FindActor(id);
                if (actor is { IsDead: true } or { IsActive: false }) continue;
                Vector2 position = registry.GetActorPosition(id);
                Vector2 screen = _lastTransform * ToLocal(position);
                if (viewport.HasPoint(screen)) _markers.Add((id, position, selected.Contains(id)));
            }
        }
        QueueRedraw();
    }

    public Godot.Collections.Array<string> GetMarkerActors()
    {
        var result = new Godot.Collections.Array<string>();
        foreach (var marker in _markers) result.Add(marker.Id);
        return result;
    }

    public string PickActor(Vector2 worldPosition)
    {
        RefreshOverview();
        if (!IsOverviewActive || !worldPosition.IsFinite()) return "";
        Vector2 point = _lastTransform * ToLocal(worldPosition);
        float radius = Radius() + 3;
        float closest = radius * radius;
        string result = "";
        foreach (var marker in _markers)
        {
            float distance = (_lastTransform * ToLocal(marker.Position)).DistanceSquaredTo(point);
            if (distance < closest) { closest = distance; result = marker.Id; }
        }
        return result;
    }

    private float Radius() => float.IsFinite(MarkerRadiusPixels) ? Mathf.Clamp(MarkerRadiusPixels, 1, 12) : 3;

    public override void _Draw()
    {
        if (!IsOverviewActive) return;
        Transform2D transform = GetGlobalTransformWithCanvas();
        if (Math.Abs(transform.Determinant()) < 0.000001f) return;
        DrawSetTransformMatrix(transform.AffineInverse());
        foreach (var marker in _markers)
        {
            Vector2 screen = transform * ToLocal(marker.Position);
            float radius = Radius() + (marker.Selected ? 1 : 0);
            DrawCircle(screen, radius + 1, new Color(0.05f, 0.07f, 0.06f, 1));
            DrawCircle(screen, radius, marker.Selected ? SelectedColor : MarkerColor);
        }
        DrawSetTransformMatrix(Transform2D.Identity);
    }
}

using Godot;
using System.Linq;

namespace Beep.ECS;

/// <summary>Opt-in ambient scene residency. Continuous actors never stop because a camera moved.</summary>
[GlobalClass]
public partial class ActorResidencyComponent : Node
{
    [Export] public NodePath RegistryPath { get; set; } = new("");
    [Export] public NodePath CameraPath { get; set; } = new("");
    [Export(PropertyHint.Range, "0,4096,32")] public float WakeMargin { get; set; } = 256;
    [Export(PropertyHint.Range, "0,8192,32")] public float SleepMargin { get; set; } = 512;
    [Export(PropertyHint.Range, "0.05,5,0.05")] public float PollSeconds { get; set; } = 0.25f;
    [Export(PropertyHint.Range, "1,64,1")] public int TransitionsPerPoll { get; set; } = 4;
    private ActorRegistryComponent? _registry;
    private Camera2D? _camera;
    private double _remaining;

    public override void _Ready()
    {
        _registry = GetNodeOrNull<ActorRegistryComponent>(RegistryPath);
        _camera = GetNodeOrNull<Camera2D>(CameraPath);
    }

    public override void _Process(double delta)
    {
        _remaining -= delta;
        if (_remaining > 0) return;
        _remaining = float.IsFinite(PollSeconds) ? Mathf.Max(0.05f, PollSeconds) : 0.25;
        RefreshResidency();
    }

    public int RefreshResidency()
    {
        if (!GodotObject.IsInstanceValid(_registry) || !GodotObject.IsInstanceValid(_camera)
            || GetViewport().GetCamera2D() != _camera || GetTree().Paused) return 0;
        Rect2 screen = GetViewport().GetVisibleRect();
        Transform2D inverse = GetViewport().CanvasTransform.AffineInverse();
        Vector2[] corners = { inverse * screen.Position, inverse * screen.End,
            inverse * new Vector2(screen.End.X, screen.Position.Y), inverse * new Vector2(screen.Position.X, screen.End.Y) };
        Vector2 min = corners.Aggregate((a, b) => a.Min(b));
        Vector2 max = corners.Aggregate((a, b) => a.Max(b));
        Rect2 view = new(min, max - min);
        float wakeMargin = float.IsFinite(WakeMargin) ? Mathf.Max(0, WakeMargin) : 256;
        float sleepMargin = float.IsFinite(SleepMargin) ? Mathf.Max(wakeMargin + 32, SleepMargin) : wakeMargin + 256;
        Rect2 wakeArea = view.Grow(wakeMargin), keepArea = view.Grow(sleepMargin);
        int budget = Mathf.Clamp(TransitionsPerPoll, 1, 64), transitions = 0;
        // Wake visible records before retiring distant scenes. Selection/commands also wake directly.
        foreach (string id in _registry!.QueryActors(wakeArea, "", true))
        {
            if (_registry.IsDormant(id) && _registry.WakeActor(id) is not null) transitions++;
            if (transitions >= budget) return transitions;
        }
        foreach (string id in _registry.GetActorIds())
        {
            if (!_registry.IsDormant(id) && !keepArea.HasPoint(_registry.GetActorPosition(id)) && _registry.TrySleepActor(id)) transitions++;
            if (transitions >= budget) break;
        }
        return transitions;
    }
}

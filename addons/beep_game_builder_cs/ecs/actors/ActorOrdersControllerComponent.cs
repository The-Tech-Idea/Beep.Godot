using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Beep.ECS;

/// <summary>Unhandled pointer selection and grid orders for avatar-free RTS and colony players.</summary>
[Tool, GlobalClass]
public partial class ActorOrdersControllerComponent : Node2D
{
    [Export] public NodePath PlayerPath { get; set; } = new("");
    [Export] public NodePath GridPath { get; set; } = new("");
    [Export] public NodePath NavigationPath { get; set; } = new("");
    [Export] public NodePath OverviewPath { get; set; } = new("");
    [Export] public float PickRadius { get; set; } = 28;
    [Export(PropertyHint.Range, "1,32,1")] public int FormationSearchRadius { get; set; } = 8;
    private PlayerContextComponent? _player;
    private GridProjectionComponent? _grid;
    private GridNavigationComponent? _navigation;
    private Vector2 _start, _end;
    private bool _dragging;

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;
        _player = GetNodeOrNull<PlayerContextComponent>(PlayerPath);
        _player?.RegisterOrders(this);
        _grid = GetNodeOrNull<GridProjectionComponent>(GridPath);
        _navigation = GetNodeOrNull<GridNavigationComponent>(NavigationPath);
    }

    public override void _Process(double delta)
    {
        AdvanceFormation();
        // A GUI can consume release events after a drag began in the world.
        if (_dragging && (!Input.IsMouseButtonPressed(MouseButton.Left) || GetTree().Paused))
        {
            _dragging = false;
            QueueRedraw();
        }
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (_player?.Registry is null || !_player.ReadLocalInput || _grid is null
            || _player.ControlMode is not (PlayerControlMode.Orders or PlayerControlMode.Colony)) return;
        if (input is InputEventKey { Pressed: true, Echo: false } key && key.Keycode >= Key.Key0 && key.Keycode <= Key.Key9)
        {
            int group = (int)(key.Keycode - Key.Key0);
            if (key.CtrlPressed) _player.StoreGroup(group); else _player.RecallGroup(group);
            GetViewport().SetInputAsHandled();
        }
        if (input is InputEventMouseMotion && _dragging)
        {
            _end = GetGlobalMousePosition(); QueueRedraw();
            GetViewport().SetInputAsHandled();
        }
        if (input is not InputEventMouseButton mouse) return;
        Vector2 point = GetGlobalMousePosition();
        if (mouse.ButtonIndex == MouseButton.Left)
        {
            if (mouse.Pressed) { _start = _end = point; _dragging = true; }
            else if (_dragging)
            {
                _dragging = false;
                var canvas = GetGlobalTransformWithCanvas();
                if ((canvas * ToLocal(_start)).DistanceTo(canvas * ToLocal(point)) > 8)
                    _player.SelectRectangle(new Rect2(_start, point - _start).Abs(), mouse.ShiftPressed);
                else
                {
                    string id = PickActor(point, _player.PlayerId);
                    if (id.Length > 0) _player.SelectActor(id, mouse.ShiftPressed);
                    else if (!mouse.ShiftPressed) _player.ClearSelection();
                }
            }
            QueueRedraw(); GetViewport().SetInputAsHandled();
        }
        else if (mouse.ButtonIndex == MouseButton.Right && mouse.Pressed)
        {
            string target = PickActor(point);
            var actor = _player.Registry.FindActor(target);
            if (actor is not null && _player.Registry.AreHostile(_player.PlayerId, actor.OwnerId))
            {
                CancelFormation();
                _player.IssueOrder(ActorAction.Attack, _grid.WorldToCell(point), target, mouse.ShiftPressed);
            }
            else MoveSelection(_grid.WorldToCell(point), mouse.ShiftPressed);
            GetViewport().SetInputAsHandled();
        }
    }

    private string PickActor(Vector2 point, string owner = "")
    {
        if (owner == _player!.PlayerId && !OverviewPath.IsEmpty
            && GetNodeOrNull<ActorOverviewComponent>(OverviewPath) is { } overview)
        {
            string marker = overview.PickActor(point);
            if (overview.IsOverviewActive) return marker;
        }
        float radius = float.IsFinite(PickRadius) ? Mathf.Max(1, PickRadius) : 28;
        return _player!.Registry!.QueryActors(new Rect2(point - Vector2.One * radius, Vector2.One * radius * 2), owner)
            .Where(id => _player.Registry.FindActor(id) is { IsDead: false })
            .OrderBy(id => _player.Registry.FindActor(id)!.Body!.GlobalPosition.DistanceSquaredTo(point))
            .FirstOrDefault() ?? "";
    }

    /// <summary>Queues reachable destination assignment; returns actors accepted for planning, not dispatched orders.</summary>
    public int MoveSelection(Vector2I goal, bool append = false)
    {
        if (!append) CancelFormation();
        if (_player?.Registry is null || _navigation is null || _grid is null || !IsInsideTree()) return 0;
        var ids = _player.GetSelectedActors().Order(System.StringComparer.Ordinal).ToArray();
        var destinations = new List<Vector2I>();
        int limit = Mathf.Clamp(FormationSearchRadius, 1, 32);
        for (int radius = 0; radius <= limit; radius++)
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != radius) continue;
                    Vector2I cell = goal + new Vector2I(x, y);
                    if (_navigation.IsInBounds(cell) && !_navigation.IsBlocked(cell)) destinations.Add(cell);
                }
        return BeginFormation(ids, destinations, append);
    }

    public override void _ExitTree()
    {
        CancelFormation();
        if (GodotObject.IsInstanceValid(_player)) _player!.UnregisterOrders(this);
        _player = null; _grid = null; _navigation = null;
        _dragging = false;
        RequestReady();
    }

    public override void _Draw()
    {
        if (!_dragging) return;
        Rect2 rect = new Rect2(ToLocal(_start), ToLocal(_end) - ToLocal(_start)).Abs();
        DrawRect(rect, new Color(0.2f, 0.95f, 0.55f, 0.12f));
        DrawRect(rect, new Color(0.2f, 0.95f, 0.55f), false, 1.5f);
    }
}

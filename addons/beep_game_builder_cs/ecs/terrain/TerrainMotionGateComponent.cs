using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS;

/// <summary>Terrain readiness and demand before native floating-body integration. Does not move the body.</summary>
[GlobalClass]
public partial class TerrainMotionGateComponent : Node
{
    [Export] public NodePath GridPath { get; set; } = new("");
    [Export] public NodePath CellDataPath { get; set; } = new("");
    [Export] public NodePath CollisionPath { get; set; } = new("");
    private bool _enabled = true;
    [Export] public bool Enabled
    {
        get => _enabled;
        set { _enabled = value; if (!value) Release(); }
    }
    [Export(PropertyHint.Range, "1,256,1")] public int MaximumDemandChunks { get; set; } = 16;
    public string WaitReason { get; private set; } = "";
    public int DemandedChunkCount => _pinned.Count;
    private GridCellDataComponent? _cells;
    private ulong _lastPreparedFrame;
    private readonly HashSet<Vector2I> _wanted = new(), _pinned = new();

    public override void _ExitTree() => Release();

    public override void _PhysicsProcess(double delta)
    {
        if (_cells is not null && Engine.GetPhysicsFrames() > _lastPreparedFrame + 1) Release();
    }

    private void Release()
    {
        if (GodotObject.IsInstanceValid(_cells)) _cells!.ReleaseChunkPins(this);
        _cells = null;
        _pinned.Clear();
        WaitReason = "";
    }

    private bool Reject(string reason, bool release = false)
    {
        if (release) Release();
        WaitReason = reason;
        return false;
    }

    public bool PrepareMotion(Vector2 motion)
    {
        _lastPreparedFrame = Engine.GetPhysicsFrames();
        if (!Enabled) { Release(); return true; }
        var body = GetParentOrNull<CharacterBody2D>();
        var grid = GridPath.IsEmpty ? null : GetNodeOrNull<GridProjectionComponent>(GridPath);
        var cells = CellDataPath.IsEmpty ? null : GetNodeOrNull<GridCellDataComponent>(CellDataPath);
        var collision = CollisionPath.IsEmpty ? null : GetNodeOrNull<TerrainCollisionComponent>(CollisionPath);
        if (body is null || grid is null || cells is null || collision is null || !collision.UsesSources(grid, cells))
            return Reject("missing_sources", true);
        var tiles = grid.TileMapLayerPath.IsEmpty ? null : grid.GetNodeOrNull<TileMapLayer>(grid.TileMapLayerPath)?.TileSet;
        if (body.MotionMode != CharacterBody2D.MotionModeEnum.Floating
            || !grid.ElevatedTerrainPath.IsEmpty
            || (!grid.TileMapLayerPath.IsEmpty && (tiles is null
                || tiles.TileShape is not (TileSet.TileShapeEnum.Square or TileSet.TileShapeEnum.Isometric))))
            return Reject("unsupported_projection_or_body", true);
        if (!motion.IsFinite() || !body.GlobalPosition.IsFinite()) return Reject("invalid_motion", true);
        if (_cells != cells) { Release(); _cells = cells; }

        Rect2 swept = new(body.GlobalPosition, Vector2.Zero);
        foreach (var id in body.GetShapeOwners())
        {
            uint owner = (uint)id;
            if (body.IsShapeOwnerDisabled(owner)) continue;
            Transform2D transform = body.GlobalTransform * body.ShapeOwnerGetTransform(owner);
            for (int i = 0; i < body.ShapeOwnerGetShapeCount(owner); i++)
            {
                Rect2 rect = body.ShapeOwnerGetShape(owner, i).GetRect();
                swept = swept.Expand(transform * rect.Position).Expand(transform * rect.End)
                    .Expand(transform * new Vector2(rect.End.X, rect.Position.Y))
                    .Expand(transform * new Vector2(rect.Position.X, rect.End.Y));
            }
        }
        // Sliding may change direction. Bound any path of this length, not only its endpoint.
        swept = swept.Grow(motion.Length() + Mathf.Max(0, body.SafeMargin) + 1f);
        if (!swept.Position.IsFinite() || !swept.End.IsFinite()) return Reject("invalid_footprint", true);
        var bounds = new Rect2I(collision.BoundsOrigin, collision.BoundsSize);
        if (!bounds.HasPoint(grid.WorldToCell(body.GlobalPosition))
            || !bounds.HasPoint(grid.WorldToCell(body.GlobalPosition + motion))) return Reject("outside_world", true);
        Vector2I first = grid.WorldToCell(swept.Position), last = first;
        foreach (Vector2 point in new[] { swept.End, new Vector2(swept.End.X, swept.Position.Y), new Vector2(swept.Position.X, swept.End.Y) })
        {
            Vector2I cell = grid.WorldToCell(point);
            first = first.Min(cell);
            last = last.Max(cell);
        }
        // One cell covers isometric nearest-cell rounding and native staggered rows.
        first = (first - Vector2I.One).Max(bounds.Position);
        last = (last + Vector2I.One).Min(bounds.End - Vector2I.One);
        first = ChunkedCellStore<object>.ChunkFor(first);
        last = ChunkedCellStore<object>.ChunkFor(last);
        long count = ((long)last.X - first.X + 1) * ((long)last.Y - first.Y + 1);
        if (count <= 0 || count > Math.Clamp(MaximumDemandChunks, 1, 256)) return Reject("motion_demand_limit", true);
        _wanted.Clear();
        for (int y = first.Y; y <= last.Y; y++)
            for (int x = first.X; x <= last.X; x++) _wanted.Add(new(x, y));
        if (!_pinned.SetEquals(_wanted) || !cells.HasChunkPins(this))
        {
            if (!cells.ReplaceChunkPins(this, _wanted)) return Reject("pin_rejected", true);
            _pinned.Clear();
            _pinned.UnionWith(_wanted);
        }
        foreach (var chunk in _wanted)
            if (!cells.IsChunkAvailable(chunk)) return Reject("terrain_loading");
        foreach (var chunk in _wanted)
            if (!collision.IsChunkReady(chunk)) return Reject("collision_pending");
        WaitReason = "";
        return true;
    }
}

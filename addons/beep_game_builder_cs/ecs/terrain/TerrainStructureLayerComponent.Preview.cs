using Godot;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class TerrainStructureLayerComponent
{
    public override void _Ready() => SetProcess(Engine.IsEditorHint());

    public override void _Process(double delta)
    {
        // Editor layer transforms can change without going through the staging API.
        if (Engine.IsEditorHint() && PendingStructureMove.Count > 0) QueueRedraw();
    }

    public Godot.Collections.Dictionary GetStructureMovePreview()
    {
        var result = new Godot.Collections.Dictionary();
        if (!IsInsideTree() || !PendingStructureMove.ContainsKey("name")
            || !PendingStructureMove.ContainsKey("new_anchor") || !PendingStructureMove.ContainsKey("old_anchor")) return result;
        var mapping = GetNodeOrNull<TileMapLayer>(MappingLayerPath);
        if (mapping?.TileSet is not { } tiles || LibraryPack?.Tiles != tiles
            || Mathf.IsZeroApprox(GlobalTransform.Determinant())) return result;
        var live = GetNodeOrNull<Node2D>(new NodePath(PendingStructureMove["name"].AsString()));
        if (live is null || !live.HasMeta("terrain_structure_footprint")) return result;
        Vector2I footprint = live.GetMeta("terrain_structure_footprint").AsVector2I();
        // A viewport hint must not allocate unbounded geometry for malformed metadata.
        if (footprint.X <= 0 || footprint.Y <= 0 || (long)footprint.X * footprint.Y > 1024) return result;
        Vector2 half = (Vector2)tiles.TileSize / 2;
        Vector2[] corners;
        if (tiles.TileShape == TileSet.TileShapeEnum.Square)
            corners = new[] { new Vector2(-half.X, -half.Y), new Vector2(half.X, -half.Y), half, new Vector2(-half.X, half.Y) };
        else if (tiles.TileShape == TileSet.TileShapeEnum.Isometric)
            corners = new[] { new Vector2(0, -half.Y), new Vector2(half.X, 0), new Vector2(0, half.Y), new Vector2(-half.X, 0) };
        else return result;
        Vector2I target = PendingStructureMove["new_anchor"].AsVector2I();
        Transform2D basis = GlobalTransform.AffineInverse() * mapping.GlobalTransform;
        var segments = new List<Vector2>();
        for (int y = 0; y < footprint.Y; y++)
            for (int x = 0; x < footprint.X; x++)
            {
                Vector2 center = mapping.MapToLocal(target + new Vector2I(x, y));
                for (int edge = 0; edge < 4; edge++)
                {
                    segments.Add(basis * (center + corners[edge]));
                    segments.Add(basis * (center + corners[(edge + 1) % 4]));
                }
            }
        result["segments"] = segments.ToArray();
        result["from"] = basis * mapping.MapToLocal(PendingStructureMove["old_anchor"].AsVector2I());
        result["to"] = basis * mapping.MapToLocal(target);
        return result;
    }

    public override void _Draw()
    {
        if (!Engine.IsEditorHint()) return;
        var preview = GetStructureMovePreview();
        if (preview.Count == 0) return;
        var cyan = new Color(0.1f, 0.9f, 1f, 0.95f);
        DrawMultiline(preview["segments"].AsVector2Array(), cyan, 2, true);
        Vector2 from = preview["from"].AsVector2(), to = preview["to"].AsVector2();
        DrawCircle(from, 4, new Color(1f, 0.65f, 0.15f));
        DrawLine(from, to, cyan, 2, true);
        if (from.DistanceSquaredTo(to) > 1)
        {
            Vector2 direction = (to - from).Normalized();
            Vector2 side = direction.Orthogonal() * 4;
            DrawLine(to, to - direction * 10 + side, cyan, 2, true);
            DrawLine(to, to - direction * 10 - side, cyan, 2, true);
        }
    }
}

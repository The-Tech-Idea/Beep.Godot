using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS;

/// <summary>Shared visible prop dimensions in logical cell units, independent of art and projection.</summary>
[GlobalClass]
public partial class TerrainPropSizing : Resource
{
    private static TerrainPropSizing? _standard;
    internal static TerrainPropSizing Standard => _standard ??= GD.Load<TerrainPropSizing>(
        "res://addons/beep_game_builder_cs/textures/terrain/terrain_prop_sizing.tres");

    [Export] public Vector2 Trees { get; set; } = new(1.75f, 2.25f);
    [Export] public Vector2 Oasis { get; set; } = new(1.75f, 2.25f);
    [Export] public Vector2 Bushes { get; set; } = new(0.35f, 0.65f);
    [Export] public Vector2 Reeds { get; set; } = new(0.2f, 0.35f);
    [Export] public Vector2 SmallRocks { get; set; } = new(0.25f, 0.4f);
    [Export] public Vector2 LargeRocks { get; set; } = new(0.45f, 0.65f);
    private readonly Dictionary<(Texture2D Texture, int Columns, int Rows), Rect2[]> _visibleFrames = new();

    /// <summary>Randomness varies around the category midpoint; final limits always apply.</summary>
    public float SizeInCells(string kind, float jitter = 1f)
    {
        Vector2 range = kind switch
        {
            "oasis" => Oasis,
            "marsh" => Reeds,
            "bush" => Bushes,
            "small_rock" => SmallRocks,
            "large_rock" => LargeRocks,
            _ => Trees,
        };
        float a = float.IsFinite(range.X) ? Mathf.Clamp(range.X, 0.05f, 8f) : 0.05f;
        float b = float.IsFinite(range.Y) ? Mathf.Clamp(range.Y, 0.05f, 8f) : a;
        return Mathf.Clamp((a + b) * 0.5f * (float.IsFinite(jitter) ? jitter : 1f), Mathf.Min(a, b), Mathf.Max(a, b));
    }

    /// <summary>Excludes transparent padding; one image read per sheet layout, not per map cell.</summary>
    public Rect2 VisibleRegion(Texture2D texture, int columns, int rows, int index)
    {
        columns = Mathf.Clamp(columns, 1, 16);
        rows = Mathf.Clamp(rows, 1, 16);
        var key = (texture, columns, rows);
        if (!_visibleFrames.TryGetValue(key, out var frames))
        {
            using var source = texture.GetImage();
            var frame = source.GetSize() / new Vector2I(columns, rows);
            frames = new Rect2[columns * rows];
            if (frame.X > 0 && frame.Y > 0)
                for (int i = 0; i < frames.Length; i++)
                {
                    var origin = new Vector2I(i % columns, i / columns) * frame;
                    using var part = source.GetRegion(new Rect2I(origin, frame));
                    Rect2I used = part.GetUsedRect();
                    frames[i] = new Rect2(origin + used.Position, used.Size);
                }
            _visibleFrames.Add(key, frames);
        }
        return frames[Mathf.Clamp(index, 0, frames.Length - 1)];
    }

    /// <summary>Call after editing pixels or atlas regions in an existing runtime texture.</summary>
    public void ClearArtCache() => _visibleFrames.Clear();
}

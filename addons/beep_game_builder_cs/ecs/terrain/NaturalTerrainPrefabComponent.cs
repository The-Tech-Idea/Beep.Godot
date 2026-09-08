using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS;

/// <summary>A single authored plateau, positioned by its visible ground contact.</summary>
[Tool, GlobalClass]
public partial class NaturalTerrainPrefabComponent : Node2D
{
    public enum RockKind { RedSandstone, VolcanicBasalt, PaleLimestone, GreyGranite }
    public enum HeightKind { Full, Half, Quarter }
    private const string Root = "res://addons/beep_game_builder_cs/generated/natural_rock_walls/v1/";
    private static readonly string[] Materials = { "red_sandstone", "volcanic_basalt", "pale_limestone", "grey_granite" };
    private static readonly Dictionary<string, (Texture2D Texture, Rect2 Bounds)> Cache = new();
    private RockKind _rock;
    private HeightKind _height;
    private float _size = 0.5f;
    private float _rise;
    private bool _hideGreen = true;
    private bool _showAnchors = true;
    private bool _pending;
    private Sprite2D _sprite = null!;
    private Rect2 _bounds;
    private string _error = "";
    private string _geometryError = "";
    private bool _blocking;
    private uint _collisionLayer = 1;
    private Vector2[] _footprint = Array.Empty<Vector2>();
    private Vector2[] _surface = Array.Empty<Vector2>();
    private StaticBody2D _body = null!;
    private CollisionPolygon2D _collision = null!;

    [ExportGroup("Geometry")]
    [Export] public bool BlockingEnabled { get => _blocking; set { _blocking = value; Schedule(); } }
    [Export(PropertyHint.Layers2DPhysics)]
    public uint BlockingLayer { get => _collisionLayer; set { _collisionLayer = value; Schedule(); } }
    // Coordinates are normalized within the visible artwork bounds, not the green canvas.
    [Export] public Vector2[] FootprintOutline { get => (Vector2[])_footprint.Clone(); set { _footprint = value == null ? Array.Empty<Vector2>() : (Vector2[])value.Clone(); Schedule(); } }
    [Export] public Vector2[] SurfaceOutline { get => (Vector2[])_surface.Clone(); set { _surface = value == null ? Array.Empty<Vector2>() : (Vector2[])value.Clone(); Schedule(); } }

    [Export] public RockKind MaterialTheme { get => _rock; set { _rock = value; Schedule(); } }
    [Export] public HeightKind Elevation { get => _height; set { _height = value; Schedule(); } }
    [Export(PropertyHint.Range, "0.01,4,0.01")]
    public float ArtScale { get => _size; set { _size = float.IsFinite(value) ? Math.Max(0.01f, value) : 0.5f; Schedule(); } }
    [Export] public bool HideGreenBackground { get => _hideGreen; set { _hideGreen = value; Schedule(); } }
    [Export] public bool ShowPlacementAnchors { get => _showAnchors; set { _showAnchors = value; QueueRedraw(); } }
    // Art is hand-authored; designers can calibrate the front lip without changing the PNG.
    [Export(PropertyHint.Range, "0,500,1")]
    public float SurfaceRisePixels { get => _rise; set { _rise = float.IsFinite(value) ? Math.Max(0, value) : 0; Schedule(); } }

    public string GetAssetPath()
    {
        int material = Math.Clamp((int)_rock, 0, Materials.Length - 1);
        return Root + (_height == HeightKind.Full
            ? Materials[material] + "_plateau_01_green.png"
            : "small_plateaus/" + Materials[material] + (_height == HeightKind.Half ? "_half" : "_quarter") + "_height_green.png");
    }

    public Vector2 GetGroundAnchor() => Vector2.Zero;
    public Vector2 GetTopFrontAnchor() => new(0, -(_rise > 0 ? _rise : _height == HeightKind.Full ? 250 : _height == HeightKind.Half ? 150 : 70) * _size);
    public Vector2 GetLeftFootAnchor() => new(-_bounds.Size.X * _size / 2, 0);
    public Vector2 GetRightFootAnchor() => new(_bounds.Size.X * _size / 2, 0);
    public Rect2 GetVisibleBounds() => new(new Vector2(-_bounds.Size.X / 2, -_bounds.Size.Y) * _size, _bounds.Size * _size);

    public override void _Ready() => Refresh();
    private void Schedule()
    {
        if (!IsInsideTree() || _pending) return;
        _pending = true;
        Callable.From(Refresh).CallDeferred();
    }

    public void Refresh()
    {
        _pending = false;
        if (!IsInsideTree()) return;
        if (!GodotObject.IsInstanceValid(_sprite))
        {
            _sprite = new Sprite2D { Name = "PlateauArtwork", Centered = false, TextureFilter = TextureFilterEnum.Linear };
            AddChild(_sprite, false, InternalMode.Back);
        }
        string path = GetAssetPath();
        if (!Cache.TryGetValue(path, out var asset))
        {
            Texture2D texture = GD.Load<Texture2D>(path);
            if (texture == null)
            {
                _sprite.Texture = null;
                _bounds = default;
                _error = "Missing terrain texture: " + path;
                if (GodotObject.IsInstanceValid(_body)) _body.CollisionLayer = 0;
                UpdateConfigurationWarnings();
                return;
            }
            using Image image = texture.GetImage();
            if (image.IsCompressed()) image.Decompress();
            int minX = image.GetWidth(), minY = image.GetHeight(), maxX = -1, maxY = -1;
            for (int y = 0; y < image.GetHeight(); y++)
            for (int x = 0; x < image.GetWidth(); x++)
            {
                Color c = image.GetPixel(x, y);
                if (c.A < 0.1f || c.G - Math.Max(c.R, c.B) > 0.25f) continue;
                minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
            }
            asset = (texture, maxX < 0 ? new Rect2(0, 0, image.GetWidth(), image.GetHeight()) : new Rect2(minX, minY, maxX - minX + 1, maxY - minY + 1));
            Cache[path] = asset;
        }
        _bounds = asset.Bounds;
        _sprite.Texture = asset.Texture;
        _sprite.Scale = Vector2.One * _size;
        _sprite.Position = -new Vector2(_bounds.GetCenter().X, _bounds.End.Y) * _size;
        _sprite.Material = _hideGreen ? new ShaderMaterial { Shader = GD.Load<Shader>("res://addons/beep_game_builder_cs/shaders/natural_terrain_green.gdshader") } : null;
        _error = "";
        RefreshGeometry();
        UpdateConfigurationWarnings();
        QueueRedraw();
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (!string.IsNullOrEmpty(_error)) warnings.Add(_error);
        if (!string.IsNullOrEmpty(_geometryError)) warnings.Add(_geometryError);
        if (_blocking && _collisionLayer == 0) warnings.Add("Blocking is enabled but BlockingLayer is empty.");
        return warnings.ToArray();
    }

    public Vector2[] GetSurfacePolygon() => ResolveOutline(_surface, true);
    public Vector2[] GetFootprintPolygon() => ResolveOutline(_footprint, false);
    public bool ContainsSurfacePoint(Vector2 globalPoint)
    {
        Vector2[] polygon = GetSurfacePolygon();
        return polygon.Length >= 3 && Geometry2D.IsPointInPolygon(ToLocal(globalPoint), polygon);
    }

    private Vector2[] ResolveOutline(Vector2[] custom, bool surface)
    {
        if (_bounds.Size.X <= 0 || _bounds.Size.Y <= 0) return Array.Empty<Vector2>();
        Rect2 bounds = GetVisibleBounds();
        Vector2[] result;
        if (custom.Length > 0)
        {
            result = new Vector2[custom.Length];
            for (int i = 0; i < result.Length; i++) result[i] = bounds.Position + custom[i] * bounds.Size;
        }
        else
        {
            // The ground footprint is the estimated top footprint projected down by the cliff rise.
            float rise = Math.Min(-GetTopFrontAnchor().Y, bounds.Size.Y * 0.9f);
            float top = bounds.Position.Y + (surface ? 0 : rise);
            float bottom = surface ? -rise : 0;
            float width = bounds.Size.X * (surface ? 0.44f : 0.48f);
            float depth = bottom - top;
            result = new[] {
                new Vector2(-width * 0.65f, top), new Vector2(width * 0.65f, top),
                new Vector2(width, top + depth * 0.3f), new Vector2(width, top + depth * 0.7f),
                new Vector2(width * 0.65f, bottom), new Vector2(-width * 0.65f, bottom),
                new Vector2(-width, top + depth * 0.7f), new Vector2(-width, top + depth * 0.3f)
            };
        }
        if (result.Length < 3) return Array.Empty<Vector2>();
        foreach (Vector2 p in result) if (!p.IsFinite()) return Array.Empty<Vector2>();
        for (int i = 0; i < result.Length; i++)
        for (int j = i + 1; j < result.Length; j++)
        {
            if (result[i].IsEqualApprox(result[j])) return Array.Empty<Vector2>();
            if (j == i + 1 || (i == 0 && j == result.Length - 1)) continue;
            if (Geometry2D.SegmentIntersectsSegment(result[i], result[(i + 1) % result.Length],
                result[j], result[(j + 1) % result.Length]).VariantType != Variant.Type.Nil)
                return Array.Empty<Vector2>();
        }
        return Geometry2D.TriangulatePolygon(result).Length == 0 ? Array.Empty<Vector2>() : result;
    }

    private void RefreshGeometry()
    {
        Vector2[] ground = GetFootprintPolygon();
        Vector2[] surface = GetSurfacePolygon();
        _geometryError = ground.Length == 0 || surface.Length == 0
            ? "Outlines must be simple, non-degenerate polygons with at least three finite points. Invalid outlines are disabled." : "";
        if (!_blocking && !GodotObject.IsInstanceValid(_body)) return;
        if (!GodotObject.IsInstanceValid(_body))
        {
            _body = new StaticBody2D { Name = "TerrainBlocker", CollisionMask = 0 };
            _collision = new CollisionPolygon2D { Name = "Footprint" };
            _body.AddChild(_collision);
            AddChild(_body, false, InternalMode.Back);
        }
        _body.CollisionLayer = _blocking && ground.Length >= 3 ? _collisionLayer : 0;
        _collision.Polygon = ground;
    }
    public override void _Draw()
    {
        if (!Engine.IsEditorHint() || !_showAnchors) return;
        foreach (Vector2[] outline in new[] { GetSurfacePolygon(), GetFootprintPolygon() })
        {
            if (outline.Length < 3) continue;
            for (int i = 0; i < outline.Length; i++)
                DrawLine(outline[i], outline[(i + 1) % outline.Length], Colors.Coral, 1);
        }
        foreach (Vector2 p in new[] { GetGroundAnchor(), GetTopFrontAnchor(), GetLeftFootAnchor(), GetRightFootAnchor() })
        {
            DrawLine(p - new Vector2(6, 0), p + new Vector2(6, 0), Colors.Cyan, 1);
            DrawLine(p - new Vector2(0, 6), p + new Vector2(0, 6), Colors.Cyan, 1);
        }
    }
}

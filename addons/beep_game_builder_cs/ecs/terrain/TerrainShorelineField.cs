using Godot;
using System;
using System.Runtime.InteropServices;

namespace Beep.ECS;

/// <summary>
/// Experimental presentation snapshot, not a world or cell store. One signed
/// distance supplies the sea and the inward beach contour. No material noise.
/// </summary>
public partial class TerrainShorelineField : RefCounted
{
    private float[] _distances = Array.Empty<float>();
    private Vector2I _size;
    private int _detail;
    private ImageTexture? _texture;

    public ImageTexture BuildGenerated(TerrainGeneratorComponent generator, int detail = 8)
    {
        ArgumentNullException.ThrowIfNull(generator);
        GeneratedTerrainField field = generator.ResolveField();
        return Build(generator.BoundsSize, detail, field.IsWaterAtPosition);
    }

    public ImageTexture BuildLive(GridCellDataComponent cells, Vector2I origin, Vector2I size, int detail = 8)
        => Build(size, detail, TerrainCoastField.CreateLiveWaterSampler(cells, origin, size));

    internal ImageTexture Build(Vector2I cells, int detail, Func<Vector2, bool> isWater)
    {
        if (cells.X < 1 || cells.Y < 1) throw new ArgumentOutOfRangeException(nameof(cells));
        detail = Math.Clamp(detail, 1, 16);
        while ((long)cells.X * cells.Y * detail * detail > 1_250_000 && detail > 1) detail--;
        if ((long)cells.X * cells.Y > 1_250_000)
            throw new ArgumentOutOfRangeException(nameof(cells), "Shoreline snapshot exceeds 1,250,000 samples.");
        _detail = detail;
        _size = cells * detail;
        var water = new bool[_size.X * _size.Y];
        for (int y = 0; y < _size.Y; y++)
        for (int x = 0; x < _size.X; x++)
            water[y * _size.X + x] = isWater(new Vector2(x + 0.5f, y + 0.5f) / detail);
        _distances = TerrainEuclideanDistance.Signed(water, _size, detail);
        // Float data avoids the range clipping and 8-bit terracing of a colour map.
        byte[] bytes = MemoryMarshal.AsBytes(_distances.AsSpan()).ToArray();
        using var image = Image.CreateFromData(_size.X, _size.Y, false, Image.Format.Rf, bytes);
        _texture = ImageTexture.CreateFromImage(image);
        return _texture;
    }

    public int GetSampleDetail() => _detail;
    public ImageTexture? GetDistanceTexture() => _texture;

    public float SampleDistance(Vector2 cellPosition)
    {
        if (_distances.Length == 0) throw new InvalidOperationException("Build the shoreline snapshot before sampling.");
        Vector2 at = cellPosition * _detail - Vector2.One * 0.5f;
        at = at.Clamp(Vector2.Zero, new Vector2(_size.X - 1, _size.Y - 1));
        int x = (int)at.X, y = (int)at.Y;
        int nextX = Math.Min(x + 1, _size.X - 1), nextY = Math.Min(y + 1, _size.Y - 1);
        return Mathf.Lerp(
            Mathf.Lerp(_distances[y * _size.X + x], _distances[y * _size.X + nextX], at.X - x),
            Mathf.Lerp(_distances[nextY * _size.X + x], _distances[nextY * _size.X + nextX], at.X - x), at.Y - y);
    }
}

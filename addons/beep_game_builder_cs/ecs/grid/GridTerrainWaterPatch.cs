using Godot;
using System;

namespace Beep.ECS;

/// <summary>Immutable, bit-packed sub-cell shoreline carried by a live grid cell.</summary>
// IEquatable of the nullable type: the coast cache compares GridTerrainWaterPatch?[] spans, whose
// SequenceEqual constraint is IEquatable<T> for T = GridTerrainWaterPatch?.
internal sealed class GridTerrainWaterPatch : IEquatable<GridTerrainWaterPatch?>
{
    private readonly byte[] _bits;
    public int Resolution { get; }
    /// <summary>
    /// All dry or all wet: Create collapses such a patch to one bit, so it carries no sub-cell
    /// boundary. A whole-cell edit may keep it - it draws nothing a fine shoreline would.
    /// </summary>
    public bool IsUniform => Resolution == 1;

    private GridTerrainWaterPatch(int resolution, byte[] bits)
    {
        Resolution = resolution;
        _bits = bits;
    }

    // Value equality: two patches with the same resolution and bits ARE the same shoreline. The
    // painted renderer compares its retained samples, and the coast cache its patch arrays, to
    // decide whether anything changed. With reference equality every archive reload - which
    // decodes fresh instances of identical content - read as a change and rebuilt the whole
    // map's id, shade and coast textures.
    public bool Equals(GridTerrainWaterPatch? other)
        => other is not null && (ReferenceEquals(this, other)
            || (Resolution == other.Resolution && _bits.AsSpan().SequenceEqual(other._bits)));

    public override bool Equals(object? obj) => Equals(obj as GridTerrainWaterPatch);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Resolution);
        hash.AddBytes(_bits);
        return hash.ToHashCode();
    }

    public static GridTerrainWaterPatch Create(int resolution, Func<int, int, bool> sample)
    {
        if (resolution < 1 || resolution > 24) throw new ArgumentOutOfRangeException(nameof(resolution));
        var bits = new byte[(resolution * resolution + 7) / 8];
        int wet = 0;
        for (int y = 0; y < resolution; y++)
        for (int x = 0; x < resolution; x++)
        {
            if (!sample(x, y)) continue;
            int index = y * resolution + x;
            bits[index / 8] |= (byte)(1 << (index % 8));
            wet++;
        }
        return wet == 0 || wet == resolution * resolution
            ? new GridTerrainWaterPatch(1, new[] { (byte)(wet == 0 ? 0 : 1) })
            : new GridTerrainWaterPatch(resolution, bits);
    }

    public bool IsWater(Vector2 local, bool cellWater)
    {
        Vector2 at = (local * Resolution - Vector2.One * 0.5f)
            .Clamp(Vector2.Zero, Vector2.One * (Resolution - 1));
        int x = (int)at.X, y = (int)at.Y;
        int nextX = Math.Min(x + 1, Resolution - 1), nextY = Math.Min(y + 1, Resolution - 1);
        float coverage = Mathf.Lerp(
            Mathf.Lerp(Bit(x, y), Bit(nextX, y), at.X - x),
            Mathf.Lerp(Bit(x, nextY), Bit(nextX, nextY), at.X - x), at.Y - y);
        // Movement targets the centre, not an artificial half-cell square.
        // A compact smooth correction preserves that point without rectangular
        // notches where fine coastline and gameplay-majority classification differ.
        float distance = local.DistanceTo(Vector2.One * 0.5f);
        float centre = 1f - Mathf.SmoothStep(0.04f, 0.22f, distance);
        return Mathf.Lerp(coverage, cellWater ? 1f : 0f, centre) >= 0.5f;
    }

    internal bool Contains(Vector2 local)
        => Bit(Mathf.Clamp((int)(local.X * Resolution), 0, Resolution - 1),
            Mathf.Clamp((int)(local.Y * Resolution), 0, Resolution - 1)) > 0f;

    private float Bit(int x, int y)
    {
        int index = y * Resolution + x;
        return (_bits[index / 8] & (1 << (index % 8))) != 0 ? 1f : 0f;
    }

    public string Encode()
    {
        var bytes = new byte[_bits.Length + 1];
        bytes[0] = (byte)Resolution;
        _bits.CopyTo(bytes, 1);
        return Convert.ToBase64String(bytes);
    }

    public static GridTerrainWaterPatch Decode(string encoded)
    {
        byte[] bytes = Convert.FromBase64String(encoded);
        if (bytes.Length < 2 || bytes[0] < 1 || bytes[0] > 24
            || bytes.Length != 1 + (bytes[0] * bytes[0] + 7) / 8)
            throw new FormatException("Invalid grid water-surface patch.");
        return new GridTerrainWaterPatch(bytes[0], bytes.AsSpan(1).ToArray());
    }
}

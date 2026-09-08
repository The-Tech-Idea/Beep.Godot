using System;
using System.Collections.Generic;
using System.Threading;

namespace Beep.ECS;

/// <summary>Immutable, lossless terrain labels in independently addressable 32-sample chunks.</summary>
internal sealed class TerrainSampleKinds
{
    private const int ChunkSize = 32;
    private readonly record struct Chunk(string[] Palette, ushort[]? Indices, int Width);
    private readonly Chunk[] _chunks;
    private readonly int _width, _length, _chunksWide;
    public long PayloadBytes { get; private set; }
    public int UniformChunkCount { get; private set; }

    public TerrainSampleKinds(string[] source, int width, int height, CancellationToken cancellation = default)
    {
        if (width <= 0 || height <= 0 || (long)width * height != source.Length)
            throw new ArgumentException("Terrain sample dimensions do not match the source.");
        cancellation.ThrowIfCancellationRequested();
        _width = width;
        _length = source.Length;
        _chunksWide = (width - 1) / ChunkSize + 1;
        _chunks = new Chunk[checked(_chunksWide * ((height - 1) / ChunkSize + 1))];
        var palette = new List<string>();
        var lookup = new Dictionary<string, ushort>(StringComparer.Ordinal);
        for (int cy = 0; cy < height; cy += ChunkSize)
        for (int cx = 0; cx < width; cx += ChunkSize)
        {
            cancellation.ThrowIfCancellationRequested();
            int wide = Math.Min(ChunkSize, width - cx), high = Math.Min(ChunkSize, height - cy);
            string first = source[cy * width + cx];
            bool uniform = true;
            for (int y = 0; y < high && uniform; y++)
            for (int x = 0; x < wide; x++)
                if (!string.Equals(first, source[(cy + y) * width + cx + x], StringComparison.Ordinal))
                { uniform = false; break; }
            Chunk chunk;
            if (uniform)
            {
                chunk = new Chunk(new[] { first }, null, wide);
                UniformChunkCount++;
            }
            else
            {
                palette.Clear();
                lookup.Clear();
                var indices = new ushort[wide * high];
                for (int y = 0; y < high; y++)
                for (int x = 0; x < wide; x++)
                {
                    string kind = source[(cy + y) * width + cx + x];
                    if (!lookup.TryGetValue(kind, out ushort index))
                    {
                        index = (ushort)palette.Count; // At most 32*32 unique labels in a chunk.
                        lookup.Add(kind, index);
                        palette.Add(kind);
                    }
                    indices[y * wide + x] = index;
                }
                chunk = new Chunk(palette.ToArray(), indices, wide);
            }
            _chunks[(cy / ChunkSize) * _chunksWide + cx / ChunkSize] = chunk;
            PayloadBytes += chunk.Palette.Length * IntPtr.Size + (chunk.Indices?.Length ?? 0) * sizeof(ushort);
        }
    }

    public string this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_length) throw new IndexOutOfRangeException();
            int x = index % _width, y = index / _width;
            ref readonly Chunk chunk = ref _chunks[(y / ChunkSize) * _chunksWide + x / ChunkSize];
            return chunk.Palette[chunk.Indices is null ? 0 : chunk.Indices[(y % ChunkSize) * chunk.Width + x % ChunkSize]];
        }
    }
}

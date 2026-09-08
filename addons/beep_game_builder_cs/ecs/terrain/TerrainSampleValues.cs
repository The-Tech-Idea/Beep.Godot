using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Beep.ECS;

/// <summary>Immutable, lossless numeric samples in independently addressable 32-sample chunks.</summary>
internal sealed class TerrainSampleValues<T> where T : unmanaged
{
    private const int ChunkSize = 32;
    private readonly record struct Chunk(T Uniform, T[]? Values, int Width);
    private readonly Chunk[] _chunks;
    private readonly int _width, _length, _chunksWide;
    public long PayloadBytes { get; }
    public int UniformChunkCount { get; }

    public TerrainSampleValues(T[] source, int width, int height, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (width <= 0 || height <= 0 || (long)width * height != source.Length)
            throw new ArgumentException("Terrain sample dimensions do not match the source.");
        cancellation.ThrowIfCancellationRequested();
        _width = width;
        _length = source.Length;
        _chunksWide = (width - 1) / ChunkSize + 1;
        _chunks = new Chunk[checked(_chunksWide * ((height - 1) / ChunkSize + 1))];
        for (int cy = 0; cy < height; cy += ChunkSize)
        for (int cx = 0; cx < width; cx += ChunkSize)
        {
            cancellation.ThrowIfCancellationRequested();
            int wide = Math.Min(ChunkSize, width - cx), high = Math.Min(ChunkSize, height - cy);
            int first = cy * width + cx;
            bool uniform = true;
            // Compare bits, preserving signed zero and distinct NaN payloads in float fields.
            var firstBytes = MemoryMarshal.AsBytes(source.AsSpan(first, 1));
            for (int y = 0; y < high && uniform; y++)
            for (int x = 0; x < wide; x++)
                if (!firstBytes.SequenceEqual(MemoryMarshal.AsBytes(source.AsSpan((cy + y) * width + cx + x, 1))))
                { uniform = false; break; }
            T[]? values = null;
            if (uniform) UniformChunkCount++;
            else
            {
                values = new T[wide * high];
                for (int y = 0; y < high; y++)
                    source.AsSpan((cy + y) * width + cx, wide).CopyTo(values.AsSpan(y * wide, wide));
            }
            _chunks[(cy / ChunkSize) * _chunksWide + cx / ChunkSize] = new(source[first], values, wide);
            PayloadBytes += (long)(values?.Length ?? 1) * Unsafe.SizeOf<T>();
        }
    }

    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_length) throw new IndexOutOfRangeException();
            int x = index % _width, y = index / _width;
            ref readonly Chunk chunk = ref _chunks[(y / ChunkSize) * _chunksWide + x / ChunkSize];
            return chunk.Values is null ? chunk.Uniform : chunk.Values[(y % ChunkSize) * chunk.Width + x % ChunkSize];
        }
    }
}

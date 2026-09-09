using Godot;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Beep.ECS;

/// <summary>Sparse logical cells partitioned independently of rendering or simulation residency.</summary>
internal sealed class ChunkedCellStore<T> : IEnumerable<KeyValuePair<Vector2I, T>> where T : class
{
    public const int ChunkSize = 32;
    private readonly Dictionary<Vector2I, Dictionary<Vector2I, T>> _chunks = new();
    public int Count { get; private set; }
    public int ChunkCount => _chunks.Count;
    public IEnumerable<Vector2I> ChunkCoordinates => _chunks.Keys;
    public int CountInChunk(Vector2I coordinate) => _chunks.TryGetValue(coordinate, out var records) ? records.Count : 0;

    // Arithmetic shift is floor division, including negative coordinates. This is the
    // one place the chunk rule lives: every cell-to-chunk conversion in the addon goes
    // through ChunkAxis / ChunkFor / GridCellDataComponent.ChunkOf, never an inline shift.
    public static int ChunkAxis(long coordinate) => (int)(coordinate >> 5);
    public static Vector2I ChunkFor(Vector2I cell) => new(ChunkAxis(cell.X), ChunkAxis(cell.Y));

    public T this[Vector2I cell]
    {
        set
        {
            var key = ChunkFor(cell);
            if (!_chunks.TryGetValue(key, out var chunk)) _chunks.Add(key, chunk = new());
            if (chunk.TryAdd(cell, value)) Count++;
            else chunk[cell] = value;
        }
    }

    public bool TryGetValue(Vector2I cell, [NotNullWhen(true)] out T? value)
    {
        if (_chunks.TryGetValue(ChunkFor(cell), out var chunk) && chunk.TryGetValue(cell, out value)) return true;
        value = null;
        return false;
    }

    public bool ContainsKey(Vector2I cell) => TryGetValue(cell, out _);

    public IEnumerable<KeyValuePair<Vector2I, T>> InChunk(Vector2I chunk)
    {
        if (_chunks.TryGetValue(chunk, out var records))
            foreach (var pair in records) yield return pair;
    }

    public IEnumerable<T> Values
    {
        get { foreach (var chunk in _chunks.Values) foreach (var value in chunk.Values) yield return value; }
    }

    public void Clear() { _chunks.Clear(); Count = 0; }

    public void RemoveChunk(Vector2I coordinate)
    {
        if (_chunks.Remove(coordinate, out var records)) Count -= records.Count;
    }

    public void ReplaceChunk(Vector2I coordinate, ChunkedCellStore<T> replacement)
    {
        if (_chunks.Remove(coordinate, out var previous)) Count -= previous.Count;
        if (replacement._chunks.TryGetValue(coordinate, out var records))
        {
            _chunks.Add(coordinate, records);
            Count += records.Count;
        }
    }

    public IEnumerator<KeyValuePair<Vector2I, T>> GetEnumerator()
    {
        foreach (var chunk in _chunks.Values) foreach (var pair in chunk) yield return pair;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

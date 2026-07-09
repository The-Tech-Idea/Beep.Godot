using Godot;
using System;
using System.Collections.Generic;

/// <summary>Weighted random table. Add items with weights, then roll for random selection. Great for loot tables.</summary>
public class BeepWeightedTable<T>
{
    private List<(T Item, float Weight)> _entries = new();
    private float _totalWeight;

    public void Add(T item, float weight)
    {
        _entries.Add((item, weight));
        _totalWeight += weight;
    }

    public void Remove(T item)
    {
        var found = _entries.FindIndex(e => EqualityComparer<T>.Default.Equals(e.Item, item));
        if (found >= 0) { _totalWeight -= _entries[found].Weight; _entries.RemoveAt(found); }
    }

    public void Clear() { _entries.Clear(); _totalWeight = 0; }

    /// <summary>Roll and return a random item based on weights.</summary>
    public T Roll(Random? rng = null)
    {
        rng ??= new Random();
        float roll = (float)rng.NextDouble() * _totalWeight;
        float cumulative = 0;
        foreach (var (item, weight) in _entries)
        {
            cumulative += weight;
            if (roll <= cumulative) return item;
        }
        return _entries.Count > 0 ? _entries[_entries.Count - 1].Item : default;
    }

    /// <summary>Roll N times without replacement (each item can only be selected once).</summary>
    public List<T> RollMultiple(int count, Random? rng = null)
    {
        rng ??= new Random();
        var result = new List<T>();
        var pool = new List<(T Item, float Weight)>(_entries);
        float poolWeight = _totalWeight;

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            float roll = (float)rng.NextDouble() * poolWeight;
            float cumulative = 0;
            int selectedIdx = -1;
            for (int j = 0; j < pool.Count; j++)
            {
                cumulative += pool[j].Weight;
                if (roll <= cumulative) { selectedIdx = j; break; }
            }
            if (selectedIdx < 0) selectedIdx = pool.Count - 1;
            result.Add(pool[selectedIdx].Item);
            poolWeight -= pool[selectedIdx].Weight;
            pool.RemoveAt(selectedIdx);
        }
        return result;
    }

    public int Count => _entries.Count;
    public float TotalWeight => _totalWeight;
}

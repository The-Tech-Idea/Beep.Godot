using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS;

/// <summary>Author-selected climate variants; repeated frame indices act as weights.</summary>
internal sealed class TerrainFeatureFrameBindings
{
    private readonly Dictionary<string, int[]> _frames = new();
    private readonly HashSet<string> _unbound = new();
    private string _owner = "";

    public void Load(string[] entries, int total, string owner)
    {
        _frames.Clear();
        _unbound.Clear();
        _owner = owner;
        foreach (string entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry)) continue;
            string[] halves = entry.Split('=', StringSplitOptions.TrimEntries);
            if (halves.Length != 2)
            {
                GD.PushWarning($"[{owner}] woods binding '{entry}' is not \"kind[,kind...]=frame[,frame...]\".");
                continue;
            }
            var frames = new List<int>();
            foreach (string piece in halves[1].Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!int.TryParse(piece.Trim(), out int frame) || frame < 0 || frame >= total)
                    GD.PushWarning($"[{owner}] woods binding '{entry}' names frame '{piece}', outside 0..{total - 1}.");
                else frames.Add(frame);
            }
            if (frames.Count == 0) continue;
            foreach (string kind in halves[0].Split(',', StringSplitOptions.RemoveEmptyEntries))
                _frames[kind.Trim()] = frames.ToArray();
        }
    }

    public int[]? For(string terrain)
    {
        if (_frames.Count == 0) return null;
        if (_frames.TryGetValue(terrain, out int[]? frames)) return frames;
        if (_unbound.Add(terrain))
            GD.PushWarning($"[{_owner}] no WoodsFrameBindings entry for terrain '{terrain}'; using the whole sheet.");
        return null;
    }
}

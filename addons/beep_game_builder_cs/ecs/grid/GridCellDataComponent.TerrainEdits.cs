using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class GridCellDataComponent
{
    /// <summary>Terrain-owned fields only; gameplay changes do not conflict with an art edit.</summary>
    public Godot.Collections.Dictionary CaptureTerrainEditCell(Vector2I cell)
    {
        // The existing loader folds partial terrain metadata into its canonical generated record.
        // Compare that representation on both sides of a scene/save roundtrip.
        var full = CellRecord.FromDictionary(GetCell(cell), DefaultTerrainKind).ToDictionary(cell);
        return TerrainEditFields(full);
    }

    private static Godot.Collections.Dictionary TerrainEditFields(Godot.Collections.Dictionary full)
    {
        var metadata = new Godot.Collections.Dictionary();
        if (full.TryGetValue("metadata", out var value))
            foreach (var pair in value.AsGodotDictionary())
                if (pair.Key.AsString().StartsWith("terrain_", StringComparison.Ordinal)) metadata[pair.Key] = pair.Value;
        var result = new Godot.Collections.Dictionary { ["cell"] = full["cell"], ["terrain"] = full["terrain"], ["metadata"] = metadata };
        foreach (string key in new[] { "water_surface", "lake_surface" })
            if (full.TryGetValue(key, out var surface)) result[key] = surface;
        return result.Duplicate(true);
    }

    public Godot.Collections.Dictionary PreviewTerrainEdit(Vector2I cell, string kind)
    {
        var record = CellRecord.FromDictionary(CaptureTerrainEditCell(cell), DefaultTerrainKind);
        bool wetnessFlips = TerrainTileSets.IsWaterKind(record.TerrainKind) != TerrainTileSets.IsWaterKind(kind);
        record.TerrainKind = kind;
        if (wetnessFlips) record.ClearWaterPatches(); else record.ClearFineShoreline();
        record.ClearGeneratedShore();
        return TerrainEditFields(record.ToDictionary(cell));
    }

    public Godot.Collections.Dictionary PreviewTerrainElevation(Vector2I cell, string kind, string profile, float elevation)
    {
        if (string.IsNullOrWhiteSpace(profile) || !float.IsFinite(elevation) || elevation < 0 || elevation > 1)
            throw new ArgumentException("An elevation profile and a normalized 0-1 value are required.");
        var state = kind == GetTerrainKind(cell) ? CaptureTerrainEditCell(cell) : PreviewTerrainEdit(cell, kind);
        var metadata = state["metadata"].AsGodotDictionary();
        metadata[TerrainLibraryPack.ElevationProfileMetadata] = profile;
        metadata["terrain_elevation"] = elevation;
        return TerrainEditFields(CellRecord.FromDictionary(state, DefaultTerrainKind).ToDictionary(cell));
    }

    public string ValidateTerrainEditPatch(Godot.Collections.Array<Godot.Collections.Dictionary> expected,
        Godot.Collections.Array<Godot.Collections.Dictionary> replacement)
    {
        if (expected.Count != replacement.Count) return "Terrain patch lengths differ.";
        var seen = new HashSet<Vector2I>();
        for (int i = 0; i < expected.Count; i++)
        {
            if (!expected[i].ContainsKey("cell") || !replacement[i].ContainsKey("cell")) return "Terrain patch lacks a cell.";
            Vector2I cell = expected[i]["cell"].AsVector2I();
            if (replacement[i]["cell"].AsVector2I() != cell || !seen.Add(cell)) return "Terrain patch has duplicate or mismatched cells.";
            if (!replacement[i].ContainsKey("terrain") || string.IsNullOrWhiteSpace(replacement[i]["terrain"].AsString()))
                return "Terrain patch lacks a terrain kind.";
            try { RequireResidentCell(cell); }
            catch (Exception error) { return error.Message; }
            // Text resources round float-backed metadata. Rehydrate both snapshots
            // through the cell contract rather than comparing float vs saved double.
            var baseline = TerrainEditFields(CellRecord.FromDictionary(expected[i], DefaultTerrainKind).ToDictionary(cell));
            if (!CaptureTerrainEditCell(cell).RecursiveEqual(baseline)) return $"Terrain changed at {cell}; keep the working copy and resolve the conflict.";
        }
        return "";
    }

    /// <summary>Compare-and-apply one batch, preserving the latest non-terrain fields even during undo.</summary>
    public string ApplyTerrainEditPatch(Godot.Collections.Array<Godot.Collections.Dictionary> expected,
        Godot.Collections.Array<Godot.Collections.Dictionary> replacement)
    {
        string problem = ValidateTerrainEditPatch(expected, replacement);
        if (problem.Length > 0) return problem;
        var merged = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        for (int i = 0; i < replacement.Count; i++)
        {
            var patch = replacement[i];
            var full = GetCell(patch["cell"].AsVector2I()).Duplicate(true);
            full["terrain"] = patch["terrain"];
            var metadata = full.ContainsKey("metadata") ? full["metadata"].AsGodotDictionary().Duplicate(true) : new Godot.Collections.Dictionary();
            foreach (Variant key in metadata.Keys)
                if (key.AsString().StartsWith("terrain_", StringComparison.Ordinal)) metadata.Remove(key);
            if (patch.ContainsKey("metadata"))
                foreach (var pair in patch["metadata"].AsGodotDictionary())
                    if (pair.Key.AsString().StartsWith("terrain_", StringComparison.Ordinal)) metadata[pair.Key] = pair.Value;
            full["metadata"] = metadata;
            foreach (string key in new[] { "water_surface", "lake_surface" })
            {
                full.Remove(key);
                if (patch.ContainsKey(key)) full[key] = patch[key];
            }
            merged.Add(full);
        }
        try { if (merged.Count > 0) LoadCells(merged, clearExisting: false); }
        catch (Exception error) { return error.Message; }
        return "";
    }
}

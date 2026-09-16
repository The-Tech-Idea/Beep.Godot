using Godot;
using System;
using System.Collections.Generic;
using GeneratedCell = (Godot.Vector2I Cell, string Terrain, string Feature, int Relief, float Shade, float Elevation, string WaterSource, Beep.ECS.GridTerrainWaterPatch WaterPatch, string InlandTerrain, float BeachWidth, Beep.ECS.GridTerrainWaterPatch? LakePatch, float LakeWidth, int StartArea);

namespace Beep.ECS;

public partial class GridCellDataComponent
{
    private static CellRecord CreateGeneratedRecord(GeneratedCell cell, string defaultKind)
    {
        var record = new CellRecord(string.IsNullOrWhiteSpace(cell.Terrain) ? defaultKind : cell.Terrain)
        {
            WaterPatch = cell.WaterPatch, LakePatch = cell.LakePatch,
            Generated = new GeneratedMetadata(cell.Feature, cell.Relief, cell.Shade, cell.Elevation,
                cell.WaterSource, cell.InlandTerrain, cell.BeachWidth, cell.LakeWidth, cell.StartArea),
            HasGeneratedShore = cell.BeachWidth > 0 || cell.LakeWidth > 0
        };
        return record;
    }

    internal sealed class GeneratedPublication : IDisposable
    {
        private readonly GridCellDataComponent _target;
        private readonly IEnumerator<GeneratedCell> _source;
        private ChunkedCellStore<CellRecord>? _records = new();
        private readonly string _defaultKind;
        private bool _hasStartAreas;
        public int Loaded { get; private set; }
        public bool Complete { get; private set; }

        internal GeneratedPublication(GridCellDataComponent target, IEnumerable<GeneratedCell> cells, string defaultKind)
        {
            _target = target;
            _source = cells.GetEnumerator();
            _defaultKind = defaultKind;
        }

        public bool IsFor(GridCellDataComponent? target) => target == _target;

        public bool Step(int budget)
        {
            if (_records is null) throw new ObjectDisposedException(nameof(GeneratedPublication));
            for (int i = 0; i < Math.Clamp(budget, 1, 4096) && !Complete; i++)
            {
                if (!_source.MoveNext()) { Complete = true; break; }
                var cell = _source.Current;
                _records[cell.Cell] = CreateGeneratedRecord(cell, _defaultKind);
                if (cell.StartArea > 0) _hasStartAreas = true;
                Loaded++;
            }
            return Complete;
        }

        public void Commit()
        {
            if (!Complete || _records is null || !GodotObject.IsInstanceValid(_target) || !_target.IsInsideTree())
                throw new InvalidOperationException("Cell publication is incomplete or its target is unavailable.");
            _target._cells = _records;
            // The whole store is replaced here, so this replaces the flag rather than adding to it.
            _target.HasStartAreas = _hasStartAreas;
            _target.RebuildDailyIndex();
            _target._unavailableChunks.Clear();
            _target._evictedChunks.Clear();
            _records = null;
            _target.DefaultTerrainKind = _defaultKind;
            _target.ResetChunkRevisions();
            _target.TerrainRevision++;
            _target.MarkNavigationChanged();
            _target.EmitCellsChanged(TerrainChangeKind.Terrain | TerrainChangeKind.Navigation, new Godot.Collections.Array<Vector2I>());
        }

        public void Dispose()
        {
            _source.Dispose();
            _records = null;
        }
    }
}

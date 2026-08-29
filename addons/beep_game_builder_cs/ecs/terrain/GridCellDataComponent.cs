using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Per-cell land data for top-down/isometric farming, builder, tactics, and
    /// settlement games. It stores terrain kind, flags, crop id, growth age, and
    /// arbitrary small metadata without requiring a TileMap.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridCellDataComponent : Node
    {
        [Flags]
        public enum CellFlags
        {
            None = 0,
            Blocked = 1,
            Cleared = 2,
            Tilled = 4,
            Watered = 8,
            Planted = 16,
            HarvestReady = 32
        }

        [Signal] public delegate void CellChangedEventHandler(int x, int y);
        [Signal] public delegate void CellsChangedEventHandler();
        [Signal] public delegate void CropMaturedEventHandler(int x, int y, string cropId);
        [Signal] public delegate void DayAdvancedEventHandler(int days);

        [Export] public string DefaultTerrainKind { get; set; } = "grass";
        [Export] public bool ClearWaterOnNewDay { get; set; } = true;

        private readonly Dictionary<Vector2I, CellRecord> _cells = new();

        public void ClearCells()
        {
            if (_cells.Count == 0)
                return;

            _cells.Clear();
            EmitSignal(SignalName.CellsChanged);
        }

        public bool HasCell(Vector2I cell) => _cells.ContainsKey(cell);

        public int CellCount => _cells.Count;

        public string GetTerrainKind(Vector2I cell)
            => _cells.TryGetValue(cell, out CellRecord? record) ? record.TerrainKind : DefaultTerrainKind;

        public void SetTerrainKind(Vector2I cell, string terrainKind)
        {
            CellRecord record = GetOrCreate(cell);
            record.TerrainKind = string.IsNullOrWhiteSpace(terrainKind) ? DefaultTerrainKind : terrainKind.Trim();
            EmitSignal(SignalName.CellChanged, cell.X, cell.Y);
        }

        public int GetFlags(Vector2I cell)
            => _cells.TryGetValue(cell, out CellRecord? record) ? (int)record.Flags : 0;

        public void SetFlags(Vector2I cell, int flags)
        {
            CellRecord record = GetOrCreate(cell);
            record.Flags = (CellFlags)flags;
            EmitSignal(SignalName.CellChanged, cell.X, cell.Y);
        }

        public void AddFlag(Vector2I cell, CellFlags flag)
        {
            CellRecord record = GetOrCreate(cell);
            record.Flags |= flag;
            EmitSignal(SignalName.CellChanged, cell.X, cell.Y);
        }

        public void RemoveFlag(Vector2I cell, CellFlags flag)
        {
            if (!_cells.TryGetValue(cell, out CellRecord? record))
                return;

            record.Flags &= ~flag;
            EmitSignal(SignalName.CellChanged, cell.X, cell.Y);
        }

        public bool HasFlag(Vector2I cell, CellFlags flag)
            => _cells.TryGetValue(cell, out CellRecord? record) && (record.Flags & flag) == flag;

        public void ClearLand(Vector2I cell)
        {
            CellRecord record = GetOrCreate(cell);
            record.Flags |= CellFlags.Cleared;
            record.Flags &= ~CellFlags.Blocked;
            EmitSignal(SignalName.CellChanged, cell.X, cell.Y);
        }

        public void Till(Vector2I cell)
        {
            CellRecord record = GetOrCreate(cell);
            record.Flags |= CellFlags.Cleared | CellFlags.Tilled;
            EmitSignal(SignalName.CellChanged, cell.X, cell.Y);
        }

        public void Water(Vector2I cell)
        {
            CellRecord record = GetOrCreate(cell);
            record.Flags |= CellFlags.Watered;
            EmitSignal(SignalName.CellChanged, cell.X, cell.Y);
        }

        public bool PlantCrop(Vector2I cell, string cropId, int daysToMature)
        {
            if (string.IsNullOrWhiteSpace(cropId))
                return false;

            CellRecord record = GetOrCreate(cell);
            if ((record.Flags & CellFlags.Tilled) == 0)
                return false;

            record.CropId = cropId.Trim();
            record.CropAgeDays = 0;
            record.CropDaysToMature = Mathf.Max(0, daysToMature);
            record.Flags |= CellFlags.Planted;
            record.Flags &= ~CellFlags.HarvestReady;
            EmitSignal(SignalName.CellChanged, cell.X, cell.Y);
            return true;
        }

        public bool HarvestCrop(Vector2I cell, bool clearTilled = false)
        {
            if (!_cells.TryGetValue(cell, out CellRecord? record) || string.IsNullOrEmpty(record.CropId))
                return false;

            record.CropId = "";
            record.CropAgeDays = 0;
            record.CropDaysToMature = 0;
            record.Flags &= ~(CellFlags.Planted | CellFlags.HarvestReady | CellFlags.Watered);
            if (clearTilled)
                record.Flags &= ~CellFlags.Tilled;
            EmitSignal(SignalName.CellChanged, cell.X, cell.Y);
            return true;
        }

        public string GetCropId(Vector2I cell)
            => _cells.TryGetValue(cell, out CellRecord? record) ? record.CropId : "";

        public int GetCropAgeDays(Vector2I cell)
            => _cells.TryGetValue(cell, out CellRecord? record) ? record.CropAgeDays : 0;

        public void SetMetadata(Vector2I cell, string key, Variant value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            CellRecord record = GetOrCreate(cell);
            record.Metadata[key.Trim()] = value;
            EmitSignal(SignalName.CellChanged, cell.X, cell.Y);
        }

        public Variant GetMetadata(Vector2I cell, string key)
            => _cells.TryGetValue(cell, out CellRecord? record) && record.Metadata.ContainsKey(key)
                ? record.Metadata[key]
                : default;

        public void AdvanceDay(int days = 1)
        {
            int advance = Mathf.Max(1, days);
            foreach ((Vector2I cell, CellRecord record) in _cells)
            {
                if (ClearWaterOnNewDay)
                    record.Flags &= ~CellFlags.Watered;

                if (string.IsNullOrEmpty(record.CropId))
                    continue;

                record.CropAgeDays += advance;
                if (record.CropDaysToMature >= 0 && record.CropAgeDays >= record.CropDaysToMature && (record.Flags & CellFlags.HarvestReady) == 0)
                {
                    record.Flags |= CellFlags.HarvestReady;
                    EmitSignal(SignalName.CropMatured, cell.X, cell.Y, record.CropId);
                }

                EmitSignal(SignalName.CellChanged, cell.X, cell.Y);
            }

            EmitSignal(SignalName.DayAdvanced, advance);
        }

        public Godot.Collections.Dictionary GetCell(Vector2I cell)
            => _cells.TryGetValue(cell, out CellRecord? record) ? record.ToDictionary(cell) : EmptyCell(cell);

        public Godot.Collections.Array<Godot.Collections.Dictionary> GetCells()
        {
            var result = new Godot.Collections.Array<Godot.Collections.Dictionary>();
            foreach ((Vector2I cell, CellRecord record) in _cells)
                result.Add(record.ToDictionary(cell));
            return result;
        }

        public void LoadCells(Godot.Collections.Array cells, bool clearExisting = true)
        {
            bool changed = false;
            if (clearExisting)
            {
                changed = _cells.Count > 0;
                _cells.Clear();
            }

            foreach (Variant value in cells)
            {
                if (!GridVariantReader.TryDictionary(value, out Godot.Collections.Dictionary dict))
                    continue;

                Vector2I cell = DictVector2I(dict, "cell", new Vector2I(int.MinValue, int.MinValue));
                if (cell.X == int.MinValue || cell.Y == int.MinValue)
                    continue;

                _cells[cell] = CellRecord.FromDictionary(dict, DefaultTerrainKind);
                changed = true;
                EmitSignal(SignalName.CellChanged, cell.X, cell.Y);
            }

            if (changed)
                EmitSignal(SignalName.CellsChanged);
        }

        public void LoadCells(Godot.Collections.Array<Godot.Collections.Dictionary> cells, bool clearExisting = true)
        {
            var untyped = new Godot.Collections.Array();
            foreach (Godot.Collections.Dictionary cell in cells)
                untyped.Add(cell);
            LoadCells(untyped, clearExisting);
        }

        private CellRecord GetOrCreate(Vector2I cell)
        {
            if (_cells.TryGetValue(cell, out CellRecord? record))
                return record;

            record = new CellRecord(DefaultTerrainKind);
            _cells[cell] = record;
            return record;
        }

        private Godot.Collections.Dictionary EmptyCell(Vector2I cell)
            => new()
            {
                ["cell"] = cell,
                ["terrain"] = DefaultTerrainKind,
                ["flags"] = 0,
                ["crop_id"] = "",
                ["crop_age_days"] = 0,
                ["crop_days_to_mature"] = 0,
                ["metadata"] = new Godot.Collections.Dictionary()
            };

        private static string DictString(Godot.Collections.Dictionary dict, string key, string fallback)
            => dict.ContainsKey(key) ? dict[key].AsString() : fallback;

        private static int DictInt(Godot.Collections.Dictionary dict, string key, int fallback)
            => GridVariantReader.Int(dict, key, fallback);

        private static Vector2I DictVector2I(Godot.Collections.Dictionary dict, string key, Vector2I fallback)
            => GridVariantReader.Vector2I(dict, key, fallback);

        private sealed class CellRecord
        {
            public CellRecord(string terrainKind)
            {
                TerrainKind = terrainKind;
            }

            public string TerrainKind { get; set; }
            public CellFlags Flags { get; set; }
            public string CropId { get; set; } = "";
            public int CropAgeDays { get; set; }
            public int CropDaysToMature { get; set; }
            public Godot.Collections.Dictionary Metadata { get; private set; } = new();

            public Godot.Collections.Dictionary ToDictionary(Vector2I cell)
                => new()
                {
                    ["cell"] = cell,
                    ["terrain"] = TerrainKind,
                    ["flags"] = (int)Flags,
                    ["crop_id"] = CropId,
                    ["crop_age_days"] = CropAgeDays,
                    ["crop_days_to_mature"] = CropDaysToMature,
                    ["metadata"] = Metadata.Duplicate(deep: true)
                };

            public static CellRecord FromDictionary(Godot.Collections.Dictionary dict, string defaultTerrain)
            {
                var record = new CellRecord(DictString(dict, "terrain", defaultTerrain))
                {
                    Flags = (CellFlags)DictInt(dict, "flags", 0),
                    CropId = DictString(dict, "crop_id", ""),
                    CropAgeDays = DictInt(dict, "crop_age_days", 0),
                    CropDaysToMature = DictInt(dict, "crop_days_to_mature", 0)
                };

                if (dict.ContainsKey("metadata") && dict["metadata"].VariantType == Variant.Type.Dictionary)
                    record.Metadata = dict["metadata"].AsGodotDictionary().Duplicate(deep: true);

                return record;
            }
        }
    }
}

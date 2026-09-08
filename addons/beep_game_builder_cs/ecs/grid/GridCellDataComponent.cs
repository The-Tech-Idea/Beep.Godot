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

        private string _defaultTerrainKind = "grass";
        [Export] public string DefaultTerrainKind
        {
            get => _defaultTerrainKind;
            set
            {
                if (_defaultTerrainKind == value) return;
                if (_evictedChunks.Count > 0) throw new InvalidOperationException("Load or replace archived chunks before changing the default terrain.");
                _defaultTerrainKind = value;
                ResetChunkRevisions();
            }
        }
        [Export] public bool ClearWaterOnNewDay { get; set; } = true;

        private ChunkedCellStore<CellRecord> _cells = new();
        /// <summary>Terrain, elevation and fine-water data only; existing-cell workflow flags do not repaint ground.</summary>
        internal ulong TerrainRevision { get; private set; }
        /// <summary>Traversal inputs only; farming and visual changes do not invalidate searches.</summary>
        internal ulong NavigationRevision { get; private set; }
        /// <summary>Navigation changes except verified eviction of unpinned chunks.</summary>
        internal ulong PinnedNavigationRevision { get; private set; }

        private void MarkNavigationChanged()
        {
            NavigationRevision++;
            PinnedNavigationRevision++;
        }

        public void ClearCells()
        {
            if (_cells.Count == 0 && _unavailableChunks.Count == 0)
                return;

            _cells.Clear();
            _unavailableChunks.Clear();
            _evictedChunks.Clear();
            ResetChunkRevisions();
            TerrainRevision++;
            MarkNavigationChanged();
            EmitSignal(SignalName.CellsChanged);
        }

        public bool HasCell(Vector2I cell) => _cells.ContainsKey(cell);

        public int CellCount => _cells.Count;
        public int StoredChunkCount => _cells.ChunkCount;
        public int GetStoredChunkCellCount(Vector2I chunk) => _cells.CountInChunk(chunk);
        public Godot.Collections.Array<Vector2I> GetStoredChunks() => new(_cells.ChunkCoordinates);

        /// <summary>Snapshot at most 32x32 cells; argument is a chunk coordinate, not a cell.</summary>
        public Godot.Collections.Array<Godot.Collections.Dictionary> GetChunkCells(Vector2I chunk)
        {
            var result = new Godot.Collections.Array<Godot.Collections.Dictionary>();
            foreach (var (cell, record) in _cells.InChunk(chunk)) result.Add(record.ToDictionary(cell));
            return result;
        }

        public string GetTerrainKind(Vector2I cell)
            => _cells.TryGetValue(cell, out CellRecord? record) ? record.TerrainKind : DefaultTerrainKind;

        internal GridTerrainWaterPatch? WaterPatchAtCell(Vector2I cell)
            => _cells.TryGetValue(cell, out CellRecord? record) ? record.WaterPatch : null;

        internal GridTerrainWaterPatch? LakePatchAtCell(Vector2I cell)
            => _cells.TryGetValue(cell, out var record) ? record.LakePatch : null;

        internal (string Inland, float Width, float LakeWidth) ShoreAtCell(Vector2I cell)
        {
            if (!_cells.TryGetValue(cell, out var record)) return (DefaultTerrainKind, 0f, 0f);
            return (record.HasMetadata("terrain_shore_inland") ? record.GetMetadata("terrain_shore_inland").AsString() : record.TerrainKind,
                Mathf.Clamp(GridVariantReader.Float(record.GetMetadata("terrain_beach_width")), 0f, 4f),
                Mathf.Clamp(GridVariantReader.Float(record.GetMetadata("terrain_lake_shore_width")), 0f, 3f));
        }

        public void SetTerrainKind(Vector2I cell, string terrainKind)
        {
            CellRecord record = GetOrCreate(cell);
            string kind = string.IsNullOrWhiteSpace(terrainKind) ? DefaultTerrainKind : terrainKind.Trim();
            if (GridTerrainRules.Normalize(record.TerrainKind) != GridTerrainRules.Normalize(kind)) MarkNavigationChanged();
            bool wetnessFlips = TerrainTileSets.IsWaterKind(record.TerrainKind) != TerrainTileSets.IsWaterKind(kind);
            record.TerrainKind = kind;
            // An edit is explicit whole-cell geometry: a generated fine shoreline through the cell
            // goes, and so does everything when the cell's wetness flips. A UNIFORM patch draws no
            // boundary, so a land-to-land or water-to-water edit keeps it - which is what lets such
            // an edit leave the coast field untouched instead of buying a distance transform for
            // one cell whose water did not move.
            if (wetnessFlips) record.ClearWaterPatches();
            else record.ClearFineShoreline();
            record.ClearGeneratedShore();
            TerrainRevision++;
            NotifyCellChanged(cell);
        }

        public int GetFlags(Vector2I cell)
            => _cells.TryGetValue(cell, out CellRecord? record) ? (int)record.Flags : 0;

        /// <summary>Paints a caller-clipped cell rectangle without replacing flags, crops or metadata.</summary>
        public void FillTerrain(Rect2I area, string terrainKind)
        {
            RequireResidentArea(area);
            string kind = string.IsNullOrWhiteSpace(terrainKind) ? DefaultTerrainKind : terrainKind.Trim();
            bool changed = false;
            bool navigationChanged = false;
            for (int y = area.Position.Y; y < area.End.Y; y++)
            for (int x = area.Position.X; x < area.End.X; x++)
            {
                var cell = new Vector2I(x, y);
                bool existed = HasCell(cell);
                CellRecord record = GetOrCreate(cell);
                if (existed && record.TerrainKind == kind && !record.HasFineShoreline
                    && !record.HasMetadata("terrain_shore_inland")) continue;
                navigationChanged |= GridTerrainRules.Normalize(record.TerrainKind) != GridTerrainRules.Normalize(kind);
                bool wetnessFlips = TerrainTileSets.IsWaterKind(record.TerrainKind) != TerrainTileSets.IsWaterKind(kind);
                record.TerrainKind = kind;
                if (wetnessFlips) record.ClearWaterPatches();
                else record.ClearFineShoreline();
                record.ClearGeneratedShore();
                MarkCellChanged(cell);
                changed = true;
            }
            if (changed)
            {
                TerrainRevision++;
                if (navigationChanged) MarkNavigationChanged();
                EmitSignal(SignalName.CellsChanged);
            }
        }

        public void SetFlags(Vector2I cell, int flags)
        {
            CellRecord record = GetOrCreate(cell);
            if (((record.Flags ^ (CellFlags)flags) & CellFlags.Blocked) != 0) MarkNavigationChanged();
            record.Flags = (CellFlags)flags;
            NotifyCellChanged(cell);
        }

        public void AddFlag(Vector2I cell, CellFlags flag)
        {
            CellRecord record = GetOrCreate(cell);
            if ((flag & CellFlags.Blocked) != 0 && (record.Flags & CellFlags.Blocked) == 0) MarkNavigationChanged();
            record.Flags |= flag;
            NotifyCellChanged(cell);
        }

        public void RemoveFlag(Vector2I cell, CellFlags flag)
        {
            if (!_cells.TryGetValue(cell, out CellRecord? record))
                return;

            if ((flag & record.Flags & CellFlags.Blocked) != 0) MarkNavigationChanged();
            record.Flags &= ~flag;
            NotifyCellChanged(cell);
        }

        public bool HasFlag(Vector2I cell, CellFlags flag)
            => _cells.TryGetValue(cell, out CellRecord? record) && (record.Flags & flag) == flag;

        public void ClearLand(Vector2I cell)
        {
            CellRecord record = GetOrCreate(cell);
            if ((record.Flags & CellFlags.Blocked) != 0) MarkNavigationChanged();
            record.Flags |= CellFlags.Cleared;
            record.Flags &= ~CellFlags.Blocked;
            NotifyCellChanged(cell);
        }

        public void Till(Vector2I cell)
        {
            CellRecord record = GetOrCreate(cell);
            record.Flags |= CellFlags.Cleared | CellFlags.Tilled;
            NotifyCellChanged(cell);
        }

        public void Water(Vector2I cell)
        {
            CellRecord record = GetOrCreate(cell);
            record.Flags |= CellFlags.Watered;
            NotifyCellChanged(cell);
        }

        public bool PlantCrop(Vector2I cell, string cropId, int daysToMature, int regrowDays = -1)
        {
            if (string.IsNullOrWhiteSpace(cropId))
                return false;

            CellRecord record = GetOrCreate(cell);
            if ((record.Flags & CellFlags.Tilled) == 0)
                return false;

            record.CropId = cropId.Trim();
            record.CropAgeDays = 0;
            record.CropDaysToMature = Mathf.Max(0, daysToMature);
            record.CropRegrowDays = Mathf.Max(-1, regrowDays);
            record.Flags |= CellFlags.Planted;
            record.Flags &= ~CellFlags.HarvestReady;
            NotifyCellChanged(cell);
            return true;
        }

        public bool HarvestCrop(Vector2I cell, bool clearTilled = false)
        {
            if (!_cells.TryGetValue(cell, out CellRecord? record) || string.IsNullOrEmpty(record.CropId))
                return false;

            if (record.CropRegrowDays >= 0)
            {
                // A regrowing crop stays in the ground: the harvest resets its
                // growth clock instead of clearing it, and clearTilled is
                // ignored because the plant still occupies the tilled cell.
                // The same regrow interval of 0 means "ready again after the
                // next AdvanceDay", matching daysToMature = 0 on first growth.
                record.CropAgeDays = 0;
                record.CropDaysToMature = record.CropRegrowDays;
                record.Flags &= ~(CellFlags.HarvestReady | CellFlags.Watered);
                NotifyCellChanged(cell);
                return true;
            }

            record.CropId = "";
            record.CropAgeDays = 0;
            record.CropDaysToMature = 0;
            record.CropRegrowDays = -1;
            record.Flags &= ~(CellFlags.Planted | CellFlags.HarvestReady | CellFlags.Watered);
            if (clearTilled)
                record.Flags &= ~CellFlags.Tilled;
            NotifyCellChanged(cell);
            return true;
        }

        /// <summary>
        /// Removes a crop outright even if it would regrow - the scythe/undo
        /// path, as opposed to HarvestCrop which honors the regrow cycle.
        /// </summary>
        public bool RemoveCrop(Vector2I cell, bool clearTilled = false)
        {
            if (!_cells.TryGetValue(cell, out CellRecord? record) || string.IsNullOrEmpty(record.CropId))
                return false;

            record.CropRegrowDays = -1;
            return HarvestCrop(cell, clearTilled);
        }

        public string GetCropId(Vector2I cell)
            => _cells.TryGetValue(cell, out CellRecord? record) ? record.CropId : "";

        public int GetCropAgeDays(Vector2I cell)
            => _cells.TryGetValue(cell, out CellRecord? record) ? record.CropAgeDays : 0;

        public int GetCropRegrowDays(Vector2I cell)
            => _cells.TryGetValue(cell, out CellRecord? record) ? record.CropRegrowDays : -1;

        public void SetMetadata(Vector2I cell, string key, Variant value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            CellRecord record = GetOrCreate(cell);
            string normalizedKey = key.Trim();
            if (normalizedKey is "terrain_relief" or "terrain_ramp_direction"
                && !record.GetMetadata(normalizedKey).Equals(value)) MarkNavigationChanged();
            record.SetMetadata(normalizedKey, CopyMetadataValue(value));
            if (normalizedKey is "terrain_elevation" or "terrain_shore_inland" or "terrain_beach_width" or "terrain_lake_shore_width") TerrainRevision++;
            NotifyCellChanged(cell);
        }

        public Variant GetMetadata(Vector2I cell, string key)
            => _cells.TryGetValue(cell, out CellRecord? record)
                ? CopyMetadataValue(record.GetMetadata(key))
                : default;

        public void AdvanceDay(int days = 1)
        {
            int advance = Mathf.Max(1, days);
            _simulationDepth++;
            try
            {
                foreach ((Vector2I cell, CellRecord record) in _cells)
                {
                    bool waterChanged = ClearWaterOnNewDay && (record.Flags & CellFlags.Watered) != 0;
                    if (ClearWaterOnNewDay)
                        record.Flags &= ~CellFlags.Watered;

                    if (string.IsNullOrEmpty(record.CropId))
                    {
                        if (waterChanged) NotifyCellChanged(cell);
                        continue;
                    }

                    record.CropAgeDays += advance;
                    MarkCellChanged(cell);
                    if (record.CropDaysToMature >= 0 && record.CropAgeDays >= record.CropDaysToMature && (record.Flags & CellFlags.HarvestReady) == 0)
                    {
                        record.Flags |= CellFlags.HarvestReady;
                        EmitSignal(SignalName.CropMatured, cell.X, cell.Y, record.CropId);
                    }

                    NotifyCellChanged(cell);
                }
            }
            finally { _simulationDepth--; }

            EmitSignal(SignalName.DayAdvanced, advance);
        }

        /// <summary>
        /// Lean typed view of every stored cell's flags, for same-assembly
        /// drawers. GetCells marshals a full Godot Dictionary per cell, which
        /// is the right shape for GDScript and saves and the wrong one for a
        /// per-frame overlay draw.
        /// </summary>
        internal IEnumerable<(Vector2I Cell, CellFlags Flags)> EnumerateFlags()
        {
            foreach ((Vector2I cell, CellRecord record) in _cells)
                yield return (cell, record.Flags);
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

        /// <summary>
        /// Bulk load emits ONE CellsChanged at the end, never per-cell
        /// CellChanged. A generated map is ten thousand cells, and every
        /// per-cell emission used to make GridTileMapLayerBridgeComponent run a
        /// full TileMapLayer internals update - ten thousand of them - before
        /// the CellsChanged rebuild repainted the same map once more. A
        /// listener that needs per-cell granularity gets it from the editing
        /// API (Till, Water, SetFlags, ...), which is where single edits happen.
        /// </summary>
        public void LoadCells(Godot.Collections.Array cells, bool clearExisting = true)
        {
            // Validate snapshots before changing the live store, including fine surfaces.
            var parsed = new ChunkedCellStore<CellRecord>();
            foreach (Variant value in cells)
            {
                if (!GridVariantReader.TryDictionary(value, out var dict)) continue;
                Vector2I cell = DictVector2I(dict, "cell", new Vector2I(int.MinValue, int.MinValue));
                if (cell.X == int.MinValue || cell.Y == int.MinValue) continue;
                parsed[cell] = CellRecord.FromDictionary(dict, DefaultTerrainKind);
            }
            if (!clearExisting)
                foreach (var (cell, _) in parsed) RequireResidentCell(cell);
            bool changed = false;
            if (clearExisting)
            {
                changed = _cells.Count > 0 || _unavailableChunks.Count > 0;
                _cells.Clear();
                _unavailableChunks.Clear();
                _evictedChunks.Clear();
                ResetChunkRevisions();
            }

            foreach (var (cell, record) in parsed)
            {
                _cells[cell] = record;
                if (!clearExisting) MarkCellChanged(cell);
                changed = true;
            }

            if (changed)
            {
                TerrainRevision++;
                MarkNavigationChanged();
                EmitSignal(SignalName.CellsChanged);
            }
        }

        public void LoadCells(Godot.Collections.Array<Godot.Collections.Dictionary> cells, bool clearExisting = true)
        {
            var untyped = new Godot.Collections.Array();
            foreach (Godot.Collections.Dictionary cell in cells)
                untyped.Add(cell);
            LoadCells(untyped, clearExisting);
        }

        /// <summary>
        /// The typed bulk-load fast path for same-assembly writers - the
        /// terrain generator hands a whole map over here. The Variant overloads
        /// above stay for GDScript and saved data; this one skips marshalling
        /// one Godot Dictionary per cell, which for a generated map was the
        /// single biggest allocation in a world build. Same signal contract as
        /// the Variant path: one CellsChanged for the whole batch.
        /// </summary>
        internal int LoadGeneratedCells(IEnumerable<(Vector2I Cell, string Terrain, string Feature, int Relief, float Shade, float Elevation, string WaterSource, GridTerrainWaterPatch WaterPatch, string InlandTerrain, float BeachWidth, GridTerrainWaterPatch? LakePatch, float LakeWidth)> cells, bool clearExisting = true, string? defaultTerrainKind = null)
        {
            if (!clearExisting && _evictedChunks.Count > 0)
            {
                var buffered = System.Linq.Enumerable.ToArray(cells);
                foreach (var cell in buffered) RequireResidentCell(cell.Cell);
                cells = buffered;
            }
            if (!clearExisting && defaultTerrainKind is not null) DefaultTerrainKind = defaultTerrainKind;
            bool changed = false;
            int loaded = 0;
            if (clearExisting)
            {
                changed = _cells.Count > 0 || _unavailableChunks.Count > 0;
                _cells.Clear();
                _unavailableChunks.Clear();
                _evictedChunks.Clear();
                ResetChunkRevisions();
            }

            if (clearExisting && defaultTerrainKind is not null) DefaultTerrainKind = defaultTerrainKind;
            foreach (var cell in cells)
            {
                _cells[cell.Cell] = CreateGeneratedRecord(cell, DefaultTerrainKind);
                if (!clearExisting) MarkCellChanged(cell.Cell);
                loaded++;
                changed = true;
            }

            if (changed)
            {
                TerrainRevision++;
                MarkNavigationChanged();
                EmitSignal(SignalName.CellsChanged);
            }
            return loaded;
        }

        private CellRecord GetOrCreate(Vector2I cell)
        {
            RequireResidentCell(cell);
            if (_cells.TryGetValue(cell, out CellRecord? record))
                return record;

            record = new CellRecord(DefaultTerrainKind);
            _cells[cell] = record;
            MarkCellChanged(cell);
            TerrainRevision++;
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
                ["crop_regrow_days"] = -1,
                ["metadata"] = new Godot.Collections.Dictionary()
            };

        private static string DictString(Godot.Collections.Dictionary dict, string key, string fallback)
            => dict.ContainsKey(key) ? dict[key].AsString() : fallback;

        private static int DictInt(Godot.Collections.Dictionary dict, string key, int fallback)
            => GridVariantReader.Int(dict, key, fallback);

        private static Vector2I DictVector2I(Godot.Collections.Dictionary dict, string key, Vector2I fallback)
            => GridVariantReader.Vector2I(dict, key, fallback);

        private sealed record GeneratedMetadata(string Feature, int Relief, float Shade, float Elevation,
            string WaterSource, string Inland, float BeachWidth, float LakeWidth);

        internal int MetadataDictionaryCount
        {
            get
            {
                int count = 0;
                foreach (var record in _cells.Values) if (record.HasMetadataDictionary) count++;
                return count;
            }
        }

        private sealed class CellRecord
        {
            public CellRecord(string terrainKind)
            {
                TerrainKind = terrainKind;
            }

            public string TerrainKind { get; set; }
            public GridTerrainWaterPatch? WaterPatch { get; set; }
            public GridTerrainWaterPatch? LakePatch { get; set; }
            public CellFlags Flags { get; set; }
            public string CropId { get; set; } = "";
            public int CropAgeDays { get; set; }
            public int CropDaysToMature { get; set; }
            public int CropRegrowDays { get; set; } = -1;
            private Godot.Collections.Dictionary? _metadata;
            public GeneratedMetadata? Generated { get; set; }
            public bool HasGeneratedShore { get; set; }
            public bool HasMetadataDictionary => _metadata is not null;

            public CellRecord CopyForSnapshot()
            {
                var copy = (CellRecord)MemberwiseClone();
                copy._metadata = _metadata?.Duplicate(deep: true);
                return copy;
            }

            public void DisposeSnapshotMetadata() => _metadata?.Dispose();

            public void SetMetadata(string key, Variant value) => (_metadata ??= new())[key] = value;

            public bool HasMetadata(string key) => _metadata?.ContainsKey(key) == true || (Generated is not null && (key switch
            {
                "terrain_feature" or "terrain_relief" or "terrain_shade" or "terrain_elevation" or "terrain_water_source" => true,
                "terrain_shore_inland" or "terrain_beach_width" or "terrain_lake_shore_width" => HasGeneratedShore,
                _ => false
            }));

            public Variant GetMetadata(string key)
            {
                if (_metadata is not null && _metadata.TryGetValue(key, out var value)) return value;
                if (Generated is not { } data) return default;
                return key switch
                {
                    "terrain_feature" => data.Feature,
                    "terrain_relief" => data.Relief,
                    "terrain_shade" => data.Shade,
                    "terrain_elevation" => data.Elevation,
                    "terrain_water_source" => data.WaterSource,
                    "terrain_shore_inland" when HasGeneratedShore => data.Inland,
                    "terrain_beach_width" when HasGeneratedShore => data.BeachWidth,
                    "terrain_lake_shore_width" when HasGeneratedShore => data.LakeWidth,
                    _ => default
                };
            }

            private Godot.Collections.Dictionary CaptureMetadata()
            {
                var result = _metadata?.Duplicate(deep: true) ?? new();
                if (Generated is not null)
                    foreach (string key in GeneratedKeys)
                    {
                        using var name = (Variant)key;
                        if (result.ContainsKey(name) || !HasMetadata(key)) continue;
                        using var value = GetMetadata(key);
                        result[name] = value;
                    }
                return result;
            }

            // A string or container handed to a Godot dictionary becomes a Variant that owns native
            // memory through a finalizable Disposer unless it is disposed here. A chunk snapshot
            // writes a thousand records with a dozen such values each; left to the finalizer they
            // surfaced as GC pauses on the main thread - 11% of a sampled trace, and up to 126 ms
            // stalls in a method that does no work - so every one is disposed once it is copied in.
            private static void Set(Godot.Collections.Dictionary target, string key, Variant value)
            {
                using var name = (Variant)key;
                using (value) target[name] = value;
            }

            private static readonly string[] GeneratedKeys = { "terrain_feature", "terrain_relief", "terrain_shade",
                "terrain_elevation", "terrain_water_source", "terrain_shore_inland", "terrain_beach_width", "terrain_lake_shore_width" };

            /// <summary>Whether a patch here carries a sub-cell boundary; a uniform patch does not.</summary>
            public bool HasFineShoreline => WaterPatch is { IsUniform: false } || LakePatch is { IsUniform: false };

            public void ClearWaterPatches()
            {
                WaterPatch = null;
                LakePatch = null;
            }

            /// <summary>Drops the patches that draw a boundary; uniform ones may stay (see SetTerrainKind).</summary>
            public void ClearFineShoreline()
            {
                if (WaterPatch is { IsUniform: false }) WaterPatch = null;
                if (LakePatch is { IsUniform: false }) LakePatch = null;
            }

            public void ClearGeneratedShore()
            {
                HasGeneratedShore = false;
                _metadata?.Remove("terrain_shore_inland");
                _metadata?.Remove("terrain_beach_width");
                _metadata?.Remove("terrain_lake_shore_width");
            }

            public Godot.Collections.Dictionary ToDictionary(Vector2I cell, bool validatePortableMetadata = false)
            {
                using var metadata = CaptureMetadata();
                // Other fields are authored here as portable primitives. Metadata sits
                // two levels below the snapshot cell array, preserving the depth limit.
                if (validatePortableMetadata) ValidatePortable(metadata, 2);
                var result = new Godot.Collections.Dictionary();
                Set(result, "cell", cell);
                Set(result, "terrain", TerrainKind);
                Set(result, "flags", (int)Flags);
                Set(result, "crop_id", CropId);
                Set(result, "crop_age_days", CropAgeDays);
                Set(result, "crop_days_to_mature", CropDaysToMature);
                Set(result, "crop_regrow_days", CropRegrowDays);
                Set(result, "metadata", metadata);
                if (WaterPatch is not null) Set(result, "water_surface", WaterPatch.Encode());
                if (LakePatch is not null) Set(result, "lake_surface", LakePatch.Encode());
                return result;
            }

            public static CellRecord FromDictionary(Godot.Collections.Dictionary dict, string defaultTerrain)
            {
                var record = new CellRecord(defaultTerrain);
                // One pass over the pairs, each disposed as it goes, instead of a keyed lookup per
                // field: every lookup by string key marshals a Variant that owns native memory.
                foreach (var pair in dict)
                {
                    using var key = pair.Key;
                    using var value = pair.Value;
                    if (key.VariantType != Variant.Type.String) continue;
                    switch (key.AsString())
                    {
                        case "terrain": record.TerrainKind = value.AsString(); break;
                        case "flags": record.Flags = (CellFlags)GridVariantReader.Int(value, 0); break;
                        case "crop_id": record.CropId = value.AsString(); break;
                        case "crop_age_days": record.CropAgeDays = GridVariantReader.Int(value, 0); break;
                        case "crop_days_to_mature": record.CropDaysToMature = GridVariantReader.Int(value, 0); break;
                        // Saves from before regrow existed load as non-regrowing.
                        case "crop_regrow_days": record.CropRegrowDays = GridVariantReader.Int(value, -1); break;
                        case "metadata":
                            if (value.VariantType == Variant.Type.Dictionary)
                            {
                                using var metadata = value.AsGodotDictionary();
                                record.AdoptMetadata(metadata);
                            }
                            break;
                        case "water_surface":
                            if (value.VariantType != Variant.Type.String)
                                throw new FormatException("Grid water_surface must be an encoded string.");
                            record.WaterPatch = GridTerrainWaterPatch.Decode(value.AsString());
                            break;
                        case "lake_surface":
                            if (value.VariantType != Variant.Type.String)
                                throw new FormatException("Grid lake_surface must be an encoded string.");
                            record.LakePatch = GridTerrainWaterPatch.Decode(value.AsString());
                            break;
                    }
                }
                return record;
            }

            /// <summary>
            /// The generated terrain facts travel inside a snapshot's metadata dictionary, and used
            /// to be kept there on load: one native Dictionary per reloaded cell, read back through
            /// string-keyed Variant lookups every time the renderer sampled it. Folding them into the
            /// typed record makes a reloaded cell as light and as quick to read as a generated one;
            /// only genuinely custom metadata keeps a dictionary.
            /// </summary>
            private void AdoptMetadata(Godot.Collections.Dictionary metadata)
            {
                string feature = "", waterSource = "", inland = "";
                int relief = 0;
                float shade = 1f, elevation = 0f, beachWidth = 0f, lakeWidth = 0f;
                bool generated = false, shore = false;
                Godot.Collections.Dictionary? custom = null;
                foreach (var pair in metadata)
                {
                    using var key = pair.Key;
                    using var value = pair.Value;
                    switch (key.VariantType == Variant.Type.String ? key.AsString() : "")
                    {
                        case "terrain_feature": feature = value.AsString(); generated = true; break;
                        case "terrain_relief": relief = GridVariantReader.Int(value, 0); generated = true; break;
                        case "terrain_shade": shade = GridVariantReader.Float(value, 1f); generated = true; break;
                        case "terrain_elevation": elevation = GridVariantReader.Float(value, 0f); generated = true; break;
                        case "terrain_water_source": waterSource = value.AsString(); generated = true; break;
                        case "terrain_shore_inland": inland = value.AsString(); shore = true; break;
                        case "terrain_beach_width": beachWidth = GridVariantReader.Float(value, 0f); shore = true; break;
                        case "terrain_lake_shore_width": lakeWidth = GridVariantReader.Float(value, 0f); shore = true; break;
                        default:
                            // The only user-controlled values in a record; live objects, callables,
                            // signals and RIDs are rejected here, at the depth the whole-array walk used.
                            ValidatePortable(value, 3);
                            (custom ??= new())[key] = value;
                            break;
                    }
                }
                if (generated || shore)
                {
                    Generated = new GeneratedMetadata(feature, relief, shade, elevation, waterSource, inland, beachWidth, lakeWidth);
                    HasGeneratedShore = shore;
                }
                _metadata = custom;
            }
        }
    }
}

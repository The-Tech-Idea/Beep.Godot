using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Beep.ECS
{
    /// <summary>
    /// Loads one generated mountain/hill asset-pack manifest, builds a runtime
    /// TileSetAtlasSource from its atlas, creates or reuses a TileMapLayer, and
    /// paints a deterministic top-down mountain footprint.
    ///
    /// NOT A MAP GENERATOR, and not the source of a map's mountains. This paints
    /// VISUALS ONLY from an authored asset pack: it writes nothing to
    /// GridCellDataComponent, so gameplay - pathfinding, placement, terrain
    /// queries - cannot see anything it paints.
    ///
    /// Where mountains are on a generated map is already decided by
    /// GridTerrainGeneratorComponent, which owns TerrainRelief (Flat, Hills,
    /// Mountains) and writes it to the cell data. Use this for an authored
    /// set-piece placed on top of that, never as a second way to make terrain.
    ///
    /// Not yet wired to the cell data: what an authored mountain should mean to
    /// gameplay - blocked, which terrain kind, which relief - is a decision for
    /// the mountain asset-pack workstream that owns this file, not something to
    /// guess at from here.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class MountainTileMapLayerGeneratorComponent : Node
    {
        [Signal] public delegate void MountainGeneratedEventHandler(int paintedCells);

        [Export(PropertyHint.File, "*.json")]
        public string ManifestPath { get; set; } = "res://tmp/mountain_floor17_green_dev/floor17_manifest.json";

        [Export] public NodePath TileMapLayerPath { get; set; } = new("");
        [Export] public bool CreateLayerIfMissing { get; set; } = true;
        [Export] public string CreatedLayerName { get; set; } = "GeneratedMountainTileMapLayer";
        [Export] public bool GenerateOnReady { get; set; } = false;
        [Export] public bool GenerateInEditor { get; set; } = false;

        [ExportGroup("TileMap")]
        [Export] public int SourceId { get; set; } = 0;
        [Export] public int AlternativeTile { get; set; } = 0;
        [Export] public Vector2I TileSize { get; set; } = new(128, 96);
        [Export] public Vector2I RuntimeAtlasSlotSize { get; set; } = new(192, 192);
        [Export] public bool AutoExpandRuntimeAtlasSlot { get; set; } = true;
        [Export] public Vector2I MaxSourceSpriteSize { get; set; } = new(320, 320);
        [Export] public bool RebuildTileSetFromManifest { get; set; } = true;
        [Export] public bool ClearLayerBeforeGenerate { get; set; } = true;

        [ExportGroup("Mountain")]
        [Export] public Vector2I OriginCell { get; set; } = new(0, 0);
        [Export] public Vector2I MountainSize { get; set; } = new(18, 14);
        [Export(PropertyHint.Range, "0,999999,1")] public int Seed { get; set; } = 43117;
        [Export(PropertyHint.Range, "0,1,0.01")] public float EdgeThickness { get; set; } = 0.22f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float InnerPlateauRadius { get; set; } = 0.52f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float PropDensity { get; set; } = 0.10f;
        [Export] public bool AddRoadCut { get; set; } = true;
        [Export(PropertyHint.Range, "-16,16,1")] public int RoadOffset { get; set; } = 0;

        private readonly Dictionary<string, List<MountainAsset>> _assetsByCategory = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<MountainAsset>> _assetsByRole = new(StringComparer.Ordinal);
        private readonly List<MountainAsset> _assets = new();
        private TileMapLayer? _tileMapLayer;
        private string _loadedManifestPath = "";

        public override void _Ready()
        {
            ResolveLayer();
            UpdateConfigurationWarnings();

            if (GenerateOnReady && (!Engine.IsEditorHint() || GenerateInEditor))
                CallDeferred(nameof(GenerateMountain));
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (string.IsNullOrWhiteSpace(ManifestPath))
                return new[] { "ManifestPath must point to a generated mountain asset-pack manifest.json." };
            if (!CreateLayerIfMissing && TileMapLayerPath.IsEmpty)
                return new[] { "TileMapLayerPath is required when CreateLayerIfMissing is disabled." };
            if (TileSize.X <= 0 || TileSize.Y <= 0)
                return new[] { "TileSize must be greater than zero on both axes." };
            if (RuntimeAtlasSlotSize.X <= 0 || RuntimeAtlasSlotSize.Y <= 0)
                return new[] { "RuntimeAtlasSlotSize must be greater than zero on both axes." };
            if (MountainSize.X <= 0 || MountainSize.Y <= 0)
                return new[] { "MountainSize must be greater than zero on both axes." };
            return Array.Empty<string>();
        }

        public int GenerateMountain()
        {
            ResolveLayer();
            if (_tileMapLayer == null)
            {
                GD.PushWarning($"[{Name}] Mountain generation requires a TileMapLayer.");
                return 0;
            }

            if (!LoadManifestIfNeeded() || _assets.Count == 0)
                return 0;

            if (RebuildTileSetFromManifest || _tileMapLayer.TileSet == null)
                BuildTileSet(_tileMapLayer);

            if (_tileMapLayer.TileSet == null)
                return 0;

            if (ClearLayerBeforeGenerate)
                _tileMapLayer.Clear();

            int painted = PaintMountain(_tileMapLayer);
            _tileMapLayer.UpdateInternals();
            EmitSignal(SignalName.MountainGenerated, painted);
            return painted;
        }

        public TileMapLayer? GetTileMapLayer()
        {
            ResolveLayer();
            return _tileMapLayer;
        }

        public Godot.Collections.Dictionary GetLastGenerationSummary()
        {
            var categories = new Godot.Collections.Dictionary();
            foreach ((string category, List<MountainAsset> items) in _assetsByCategory)
                categories[category] = items.Count;
            var roles = new Godot.Collections.Dictionary();
            foreach ((string role, List<MountainAsset> items) in _assetsByRole)
                roles[role] = items.Count;

            return new Godot.Collections.Dictionary
            {
                ["manifest"] = _loadedManifestPath,
                ["asset_count"] = _assets.Count,
                ["categories"] = categories,
                ["roles"] = roles,
                ["used_cells"] = _tileMapLayer?.GetUsedCells().Count ?? 0
            };
        }

        private int PaintMountain(TileMapLayer layer)
        {
            if (HasRole("floor_center"))
                return PaintFloor17Rectangle(layer);

            int width = Mathf.Max(1, MountainSize.X);
            int height = Mathf.Max(1, MountainSize.Y);
            float rx = Mathf.Max(1.0f, (width - 1) * 0.5f);
            float ry = Mathf.Max(1.0f, (height - 1) * 0.5f);
            float cx = (width - 1) * 0.5f;
            float cy = (height - 1) * 0.5f;
            int painted = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = (x - cx) / rx;
                    float ny = (y - cy) / ry;
                    float distance = Mathf.Sqrt((nx * nx) + (ny * ny));
                    if (distance > 1.0f)
                        continue;

                    Vector2I local = new(x, y);
                    Vector2I cell = OriginCell + local;
                    bool road = AddRoadCut && IsRoadCell(local, width, height);
                    bool edge = distance >= 1.0f - Mathf.Clamp(EdgeThickness, 0.01f, 0.95f) || HasOutsideCardinalNeighbour(local, width, height, cx, cy, rx, ry);
                    bool inner = distance <= Mathf.Clamp(InnerPlateauRadius, 0.05f, 0.95f);
                    string role = RoleForCell(local, width, height, road, edge, inner);
                    MountainAsset? asset = PickRoleAsset(role, local);
                    if (asset == null)
                        continue;

                    PaintCell(layer, cell, asset);
                    painted++;
                }
            }

            if (PropDensity > 0.0f)
                painted += PaintProps(layer, width, height, cx, cy, rx, ry);

            return painted;
        }

        private int PaintFloor17Rectangle(TileMapLayer layer)
        {
            int width = Mathf.Max(2, MountainSize.X);
            int height = Mathf.Max(2, MountainSize.Y);
            int painted = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2I local = new(x, y);
                    MountainAsset? asset = PickRoleAsset(Floor17RoleForCell(local, width, height), local);
                    if (asset == null)
                        continue;

                    PaintCell(layer, OriginCell + local, asset);
                    painted++;
                }
            }

            return painted;
        }

        private static string Floor17RoleForCell(Vector2I local, int width, int height)
        {
            int lastX = width - 1;
            int lastY = height - 1;
            if (local.X == 0 && local.Y == 0) return "floor_corner_nw";
            if (local.X == lastX && local.Y == 0) return "floor_corner_ne";
            if (local.X == 0 && local.Y == lastY) return "floor_corner_sw";
            if (local.X == lastX && local.Y == lastY) return "floor_corner_se";
            if (local.Y == 0) return "floor_edge_n";
            if (local.Y == lastY) return "floor_edge_s";
            if (local.X == 0) return "floor_edge_w";
            if (local.X == lastX) return "floor_edge_e";
            return "floor_center";
        }

        private int PaintProps(TileMapLayer layer, int width, int height, float cx, float cy, float rx, float ry)
        {
            int painted = 0;
            float density = Mathf.Clamp(PropDensity, 0.0f, 1.0f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var local = new Vector2I(x, y);
                    if (AddRoadCut && IsRoadCell(local, width, height))
                        continue;

                    float nx = (x - cx) / rx;
                    float ny = (y - cy) / ry;
                    float distance = Mathf.Sqrt((nx * nx) + (ny * ny));
                    if (distance > 0.84f || distance < 0.24f)
                        continue;

                    if (Hash01(x, y, Seed + 7001) > density)
                        continue;

                    string role = Hash01(x, y, Seed + 7013) < 0.55f ? "prop_boulder" : "prop_vegetation";
                    MountainAsset? asset = PickRoleAsset(role, local);
                    if (asset == null)
                        continue;

                    PaintCell(layer, OriginCell + local, asset);
                    painted++;
                }
            }

            return painted;
        }

        private string RoleForCell(Vector2I local, int width, int height, bool road, bool edge, bool inner)
        {
            if (road)
                return HasRole("road_vertical") ? "road_vertical" : "ramp_south";

            float y01 = height <= 1 ? 0.0f : local.Y / (float)(height - 1);
            if (edge)
            {
                bool left = local.X < width / 2;
                if (local.Y >= height - 2)
                    return left ? "cliff_front_left" : "cliff_front_right";
                if (y01 > 0.58f)
                    return "cliff_front";
                if (y01 < 0.20f)
                    return left ? "top_corner_nw" : "top_corner_ne";
                if (local.X <= 1)
                    return "cliff_side_left";
                if (local.X >= width - 2)
                    return "cliff_side_right";
                return local.Y < height / 2 ? "top_north_edge" : "top_south_edge";
            }

            if (inner)
                return "top_center";

            if (y01 > 0.62f)
                return "ramp_south";

            return "transition_rock_ground";
        }

        private bool IsRoadCell(Vector2I local, int width, int height)
        {
            if (!HasRole("road_vertical") && !HasRole("ramp_south"))
                return false;

            float y01 = height <= 1 ? 0.0f : local.Y / (float)(height - 1);
            float wave = Mathf.Sin((y01 * Mathf.Pi * 1.35f) + Seed * 0.017f) * width * 0.10f;
            float roadX = ((width - 1) * 0.5f) + RoadOffset + wave;
            return Mathf.Abs(local.X - roadX) <= (width >= 14 ? 1.0f : 0.65f) && y01 >= 0.22f && y01 <= 0.94f;
        }

        private bool HasOutsideCardinalNeighbour(Vector2I local, int width, int height, float cx, float cy, float rx, float ry)
        {
            return IsOutside(local + Vector2I.Left, width, height, cx, cy, rx, ry)
                || IsOutside(local + Vector2I.Right, width, height, cx, cy, rx, ry)
                || IsOutside(local + Vector2I.Up, width, height, cx, cy, rx, ry)
                || IsOutside(local + Vector2I.Down, width, height, cx, cy, rx, ry);
        }

        private static bool IsOutside(Vector2I local, int width, int height, float cx, float cy, float rx, float ry)
        {
            if (local.X < 0 || local.Y < 0 || local.X >= width || local.Y >= height)
                return true;

            float nx = (local.X - cx) / rx;
            float ny = (local.Y - cy) / ry;
            return Mathf.Sqrt((nx * nx) + (ny * ny)) > 1.0f;
        }

        private void PaintCell(TileMapLayer layer, Vector2I cell, MountainAsset asset)
        {
            layer.SetCell(cell, SourceId, asset.TileCoords, AlternativeTile);
        }

        private MountainAsset? PickRoleAsset(string role, Vector2I local)
        {
            if (_assetsByRole.TryGetValue(role, out List<MountainAsset>? roleAssets) && roleAssets.Count > 0)
                return roleAssets[Mathf.Abs(HashInt(local.X, local.Y, Seed + role.GetHashCode(StringComparison.Ordinal))) % roleAssets.Count];

            string fallbackCategory = CategoryFallbackForRole(role);
            if (_assetsByCategory.TryGetValue(fallbackCategory, out List<MountainAsset>? categoryAssets) && categoryAssets.Count > 0)
                return categoryAssets[Mathf.Abs(HashInt(local.X, local.Y, Seed + fallbackCategory.GetHashCode(StringComparison.Ordinal))) % categoryAssets.Count];

            return _assets.Count == 0
                ? null
                : _assets[Mathf.Abs(HashInt(local.X, local.Y, Seed + role.GetHashCode(StringComparison.Ordinal))) % _assets.Count];
        }

        private static string CategoryFallbackForRole(string role)
        {
            if (role.StartsWith("top_", StringComparison.Ordinal)) return "top_surface";
            if (role.StartsWith("cliff_", StringComparison.Ordinal)) return "cliff_face";
            if (role.StartsWith("ramp_", StringComparison.Ordinal)) return "slope_ramp";
            if (role.StartsWith("road_", StringComparison.Ordinal)) return "path_cut";
            if (role.StartsWith("transition_", StringComparison.Ordinal)) return "rock_ground_transition";
            if (role.StartsWith("overlay_", StringComparison.Ordinal)) return "strata_overlay";
            if (role.StartsWith("prop_vegetation", StringComparison.Ordinal)) return "vegetation";
            if (role.StartsWith("prop_", StringComparison.Ordinal)) return "debris";
            if (role.StartsWith("special_", StringComparison.Ordinal)) return "special_feature";
            if (role.StartsWith("shadow_", StringComparison.Ordinal)) return "shadow";
            return "misc";
        }

        private bool LoadManifestIfNeeded()
        {
            string diskPath = DiskPath(ManifestPath);
            if (_loadedManifestPath == diskPath && _assets.Count > 0)
                return true;

            _assets.Clear();
            _assetsByCategory.Clear();
            _assetsByRole.Clear();
            _loadedManifestPath = "";

            if (!File.Exists(diskPath))
            {
                GD.PushWarning($"[{Name}] Mountain manifest does not exist: {ManifestPath}");
                return false;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(diskPath));
                JsonElement root = document.RootElement;
                string atlasPath = ResolveAtlasPath(diskPath, ReadString(root, "source_atlas", "atlas.png"));
                if (!root.TryGetProperty("assets", out JsonElement assetsElement) || assetsElement.ValueKind != JsonValueKind.Array)
                {
                    GD.PushWarning($"[{Name}] Mountain manifest has no assets array: {ManifestPath}");
                    return false;
                }

                foreach (JsonElement assetElement in assetsElement.EnumerateArray())
                {
                    MountainAsset? asset = ReadAsset(assetElement, atlasPath, MaxSourceSpriteSize);
                    if (asset == null)
                        continue;

                    _assets.Add(asset);
                    if (!_assetsByRole.TryGetValue(asset.Role, out List<MountainAsset>? roleAssets))
                    {
                        roleAssets = new List<MountainAsset>();
                        _assetsByRole[asset.Role] = roleAssets;
                    }
                    roleAssets.Add(asset);

                    if (!_assetsByCategory.TryGetValue(asset.Category, out List<MountainAsset>? categoryAssets))
                    {
                        categoryAssets = new List<MountainAsset>();
                        _assetsByCategory[asset.Category] = categoryAssets;
                    }
                    categoryAssets.Add(asset);
                }

                _loadedManifestPath = diskPath;
                return _assets.Count > 0;
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[{Name}] Could not read mountain manifest '{ManifestPath}': {ex.Message}");
                return false;
            }
        }

        private void BuildTileSet(TileMapLayer layer)
        {
            if (_assets.Count == 0)
                return;

            string atlasPath = _assets[0].AtlasPath;
            Image image = Image.LoadFromFile(atlasPath);
            if (image.IsEmpty())
            {
                GD.PushWarning($"[{Name}] Could not load mountain atlas: {atlasPath}");
                return;
            }

            Vector2I slotSize = RuntimeSlotSize();
            int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(_assets.Count)));
            int rows = Mathf.CeilToInt(_assets.Count / (float)columns);
            Image runtimeAtlas = Image.CreateEmpty(columns * slotSize.X, rows * slotSize.Y, false, Image.Format.Rgba8);
            runtimeAtlas.Fill(Colors.Transparent);

            var source = new TileSetAtlasSource
            {
                TextureRegionSize = slotSize
            };

            for (int i = 0; i < _assets.Count; i++)
            {
                MountainAsset asset = _assets[i];
                Vector2I tileCoords = new(i % columns, i / columns);
                asset.TileCoords = tileCoords;
                Vector2I destination = new(
                    (tileCoords.X * slotSize.X) + Mathf.Max(0, (slotSize.X - asset.SourceRect.Size.X) / 2),
                    (tileCoords.Y * slotSize.Y) + Mathf.Max(0, slotSize.Y - asset.SourceRect.Size.Y));

                runtimeAtlas.BlendRect(image, asset.SourceRect, destination);
                source.CreateTile(tileCoords);
            }

            // The runtime atlas is composited here rather than imported, so
            // nothing else can give it a mip chain. Without one, mountain tiles
            // minified at map zoom alias into noise and the filter hint on the
            // layer silently falls back to plain linear.
            runtimeAtlas.GenerateMipmaps();
            source.Texture = ImageTexture.CreateFromImage(runtimeAtlas);
            var tileSet = new TileSet { TileSize = new Vector2I(Mathf.Max(1, TileSize.X), Mathf.Max(1, TileSize.Y)) };
            tileSet.AddSource(source, SourceId);
            layer.TileSet = tileSet;
        }

        private Vector2I RuntimeSlotSize()
        {
            int width = Mathf.Max(1, RuntimeAtlasSlotSize.X);
            int height = Mathf.Max(1, RuntimeAtlasSlotSize.Y);
            if (!AutoExpandRuntimeAtlasSlot)
                return new Vector2I(width, height);

            foreach (MountainAsset asset in _assets)
            {
                width = Mathf.Max(width, asset.SourceRect.Size.X);
                height = Mathf.Max(height, asset.SourceRect.Size.Y);
            }

            return new Vector2I(width, height);
        }

        private void ResolveLayer()
        {
            if (_tileMapLayer != null && GodotObject.IsInstanceValid(_tileMapLayer))
                return;

            _tileMapLayer = !TileMapLayerPath.IsEmpty ? GetNodeOrNull<TileMapLayer>(TileMapLayerPath) : null;
            if (_tileMapLayer != null || !CreateLayerIfMissing)
                return;

            _tileMapLayer = new TileMapLayer
            {
                Name = string.IsNullOrWhiteSpace(CreatedLayerName) ? "GeneratedMountainTileMapLayer" : CreatedLayerName,
                // Mountains are relief standing on the ground, so they take the
                // stack's mountain level rather than Node2D's default of 0 -
                // which is the sea's slot.
                ZIndex = TerrainLayers.ZFor(TerrainLayers.Mountains),
                ZAsRelative = false,
                TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
            };
            AddChild(_tileMapLayer);
            if (Engine.IsEditorHint())
                _tileMapLayer.Owner = Owner;
        }

        private bool HasRole(string role)
            => _assetsByRole.TryGetValue(role, out List<MountainAsset>? assets) && assets.Count > 0;

        private static MountainAsset? ReadAsset(JsonElement element, string atlasPath, Vector2I maxSourceSpriteSize)
        {
            string id = ReadString(element, "id", "");
            string role = Normalize(ReadString(element, "role", ReadString(element, "category", "misc")));
            string category = Normalize(ReadString(element, "category", "misc"));
            Rect2I sourceRect = ReadSourceRect(element);
            int width = sourceRect.Size.X;
            int height = sourceRect.Size.Y;
            if (maxSourceSpriteSize.X > 0 && width > maxSourceSpriteSize.X)
                return null;
            if (maxSourceSpriteSize.Y > 0 && height > maxSourceSpriteSize.Y)
                return null;

            bool walkable = ReadBool(element, "walkable", false);
            bool climbable = ReadBool(element, "climbable", false);

            return new MountainAsset(
                string.IsNullOrWhiteSpace(id) ? $"{role}_{sourceRect.Position.X}_{sourceRect.Position.Y}" : id,
                role,
                category,
                atlasPath,
                sourceRect,
                walkable,
                climbable);
        }

        private static Rect2I ReadSourceRect(JsonElement element)
        {
            if (element.TryGetProperty("atlas", out JsonElement atlas) && atlas.ValueKind == JsonValueKind.Object)
            {
                int tileWidth = ReadInt(element, "tile_width", 192);
                int tileHeight = ReadInt(element, "tile_height", 192);
                int ax = ReadInt(atlas, "x", 0);
                int ay = ReadInt(atlas, "y", 0);
                return new Rect2I(ax * tileWidth, ay * tileHeight, tileWidth, tileHeight);
            }

            if (!element.TryGetProperty("source_rect", out JsonElement rect) || rect.ValueKind != JsonValueKind.Object)
                return new Rect2I(0, 0, 1, 1);

            return new Rect2I(
                ReadInt(rect, "x", 0),
                ReadInt(rect, "y", 0),
                Mathf.Max(1, ReadInt(rect, "width", 1)),
                Mathf.Max(1, ReadInt(rect, "height", 1)));
        }

        private static string ResolveAtlasPath(string manifestDiskPath, string sourceAtlas)
        {
            if (sourceAtlas.StartsWith("res://", StringComparison.Ordinal)
                || sourceAtlas.StartsWith("user://", StringComparison.Ordinal))
            {
                return DiskPath(sourceAtlas);
            }

            if (Path.IsPathRooted(sourceAtlas))
                return sourceAtlas;

            string? dir = Path.GetDirectoryName(manifestDiskPath);
            return Path.GetFullPath(Path.Combine(dir ?? "", sourceAtlas));
        }

        private static string DiskPath(string path)
            => path.StartsWith("res://", StringComparison.Ordinal) || path.StartsWith("user://", StringComparison.Ordinal)
                ? ProjectSettings.GlobalizePath(path)
                : path;

        private static string ReadString(JsonElement element, string name, string fallback)
            => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;

        private static int ReadInt(JsonElement element, string name, int fallback)
            => element.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int result) ? result : fallback;

        private static bool ReadBool(JsonElement element, string name, bool fallback)
            => element.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : fallback;

        private static string Normalize(string value)
            => string.IsNullOrWhiteSpace(value) ? "misc" : value.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');

        private static float Hash01(int x, int y, int seed)
            => (HashInt(x, y, seed) & 0x00ffffff) / 16777215.0f;

        private static int HashInt(int x, int y, int seed)
        {
            unchecked
            {
                uint value = (uint)(x * 374761393) + (uint)(y * 668265263) + (uint)seed;
                value = (value ^ (value >> 13)) * 1274126177u;
                value ^= value >> 16;
                return (int)value;
            }
        }

        private sealed class MountainAsset
        {
            public MountainAsset(string id, string role, string category, string atlasPath, Rect2I sourceRect, bool walkable, bool climbable)
            {
                Id = id;
                Role = role;
                Category = category;
                AtlasPath = atlasPath;
                SourceRect = sourceRect;
                Walkable = walkable;
                Climbable = climbable;
            }

            public string Id { get; }
            public string Role { get; }
            public string Category { get; }
            public string AtlasPath { get; }
            public Rect2I SourceRect { get; }
            public bool Walkable { get; }
            public bool Climbable { get; }
            public Vector2I TileCoords { get; set; } = Vector2I.Zero;
        }
    }
}

using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Beep.ECS
{
    public enum MountainPrefabSourceMode
    {
        Auto,
        BakedPrefabImage,
        ManifestPlacements,
        PrefabChunks
    }

    public enum MountainPrefabLayoutPreset
    {
        Reference,
        Compact,
        Wide,
        HighCastle
    }

    /// <summary>
    /// Instantiates a complete mountain/island object from a generated prefab
    /// manifest. Use this for the reference-style atlas/prefab mountain output:
    /// layered Sprite2D pieces for the art, Area2D regions for walkable levels,
    /// and anchor markers for castle/player placement.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class MountainPrefabGeneratorComponent : Node2D
    {
        [Signal] public delegate void PrefabGeneratedEventHandler(int partCount);

        [Export(PropertyHint.File, "*.json")]
        public string PrefabManifestPath { get; set; } = "res://addons/beep_game_builder_cs/generated/mountains/shape_based_mountain_prefab/prefab_manifest.json";

        [Export] public bool GenerateOnReady { get; set; } = false;
        [Export] public bool GenerateInEditor { get; set; } = false;
        [Export] public bool ClearExistingGeneratedParts { get; set; } = true;
        [Export] public bool UseSingleBakedPrefabImage { get; set; } = false;
        [Export] public MountainPrefabSourceMode SourceMode { get; set; } = MountainPrefabSourceMode.Auto;
        [Export(PropertyHint.File, "*.json")]
        public string PrefabChunkManifestPath { get; set; } = "";
        [Export] public MountainPrefabLayoutPreset LayoutPreset { get; set; } = MountainPrefabLayoutPreset.Reference;
        [Export] public bool IncludeCompletePrefabChunk { get; set; } = false;
        [Export(PropertyHint.File, "*.tscn")]
        public string SaveGeneratedScenePath { get; set; } = "res://generated/reference_mountain_prefab.tscn";
        [Export] public string GeneratedPartGroup { get; set; } = "generated_mountain_prefab_part";
        [Export] public bool CreateWalkableAreas { get; set; } = true;
        [Export] public string WalkableAreaGroup { get; set; } = "generated_mountain_walkable_region";
        [Export(PropertyHint.Layers2DPhysics)] public uint WalkableCollisionLayer { get; set; } = 1;
        [Export(PropertyHint.Layers2DPhysics)] public uint WalkableCollisionMask { get; set; } = 0;
        [Export] public bool CreateAnchorNodes { get; set; } = true;
        [Export] public string AnchorGroup { get; set; } = "generated_mountain_anchor";
        [Export] public bool CreateRouteConnectorAreas { get; set; } = true;
        [Export] public string RouteConnectorGroup { get; set; } = "generated_mountain_route_connector";
        [Export(PropertyHint.Range, "4,256,1")] public float RouteConnectorWidth { get; set; } = 48.0f;

        [ExportGroup("Placement")]
        [Export] public Vector2 PrefabOffset { get; set; } = Vector2.Zero;
        [Export(PropertyHint.Range, "0.05,8,0.01")] public float PrefabScale { get; set; } = 1.0f;
        [Export] public int BaseZIndex { get; set; } = 0;
        [Export] public bool UseHeightForZIndex { get; set; } = true;
        [Export(PropertyHint.Range, "0,100,1")] public int HeightZIndexStep { get; set; } = 10;
        [Export] public CanvasItem.TextureFilterEnum TextureFilter { get; set; } = CanvasItem.TextureFilterEnum.Linear;

        private string _lastManifestPath = "";
        private int _lastPartCount;
        private int _lastWalkableAreaCount;
        private int _lastAnchorCount;
        private int _lastRouteConnectorCount;
        private bool _lastRouteConnected;
        private int _lastMissingRouteEdgeCount;
        private string _lastVisualSourceMode = "";
        private string _lastPrefabChunkManifestPath = "";
        private readonly Godot.Collections.Array<Godot.Collections.Dictionary> _lastLevels = new();
        private readonly Godot.Collections.Array<Godot.Collections.Dictionary> _lastWalkableRegions = new();
        private readonly Godot.Collections.Array<Godot.Collections.Dictionary> _lastRouteEdges = new();
        private readonly Godot.Collections.Array<Godot.Collections.Dictionary> _lastRouteRegions = new();
        private readonly Godot.Collections.Array<Godot.Collections.Dictionary> _lastPrefabChunks = new();
        private readonly Godot.Collections.Dictionary _lastAnchors = new();

        public override void _Ready()
        {
            UpdateConfigurationWarnings();
            if (GenerateOnReady && (!Engine.IsEditorHint() || GenerateInEditor))
                CallDeferred(nameof(GeneratePrefab));
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (string.IsNullOrWhiteSpace(PrefabManifestPath))
                return new[] { "PrefabManifestPath must point to a mountain prefab_manifest.json." };
            if (PrefabScale <= 0.0f || !float.IsFinite(PrefabScale))
                return new[] { "PrefabScale must be a positive finite number." };
            return Array.Empty<string>();
        }

        public int GeneratePrefab()
        {
            string manifestDiskPath = DiskPath(PrefabManifestPath);
            if (!File.Exists(manifestDiskPath))
            {
                GD.PushWarning($"[{Name}] Mountain prefab manifest does not exist: {PrefabManifestPath}");
                return 0;
            }

            if (ClearExistingGeneratedParts)
                ClearGeneratedParts();

            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestDiskPath));
                JsonElement root = document.RootElement;
                int count = AddVisualSprites(root, manifestDiskPath);
                ReadPrefabGameplayData(root);
                _lastWalkableAreaCount = CreateWalkableAreas ? AddWalkableAreas(root) : 0;
                _lastRouteConnectorCount = CreateRouteConnectorAreas ? AddRouteConnectorAreas() : 0;
                _lastRouteConnected = ComputeRouteConnected(out int missingRouteEdgeCount);
                _lastMissingRouteEdgeCount = missingRouteEdgeCount;
                _lastAnchorCount = CreateAnchorNodes ? AddAnchorNodes(root) : 0;

                _lastManifestPath = manifestDiskPath;
                _lastPartCount = count;
                EmitSignal(SignalName.PrefabGenerated, count);
                return count;
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[{Name}] Could not generate mountain prefab '{PrefabManifestPath}': {ex.Message}");
                return 0;
            }
        }

        public Godot.Collections.Dictionary GetLastGenerationSummary()
            => new()
            {
                ["manifest"] = _lastManifestPath,
                ["part_count"] = _lastPartCount,
                ["walkable_area_count"] = _lastWalkableAreaCount,
                ["anchor_count"] = _lastAnchorCount,
                ["route_connector_count"] = _lastRouteConnectorCount,
                ["route_connected"] = _lastRouteConnected,
                ["missing_route_edge_count"] = _lastMissingRouteEdgeCount,
                ["create_walkable_areas"] = CreateWalkableAreas,
                ["create_anchor_nodes"] = CreateAnchorNodes,
                ["create_route_connector_areas"] = CreateRouteConnectorAreas,
                ["use_single_baked_prefab_image"] = UseSingleBakedPrefabImage,
                ["levels"] = _lastLevels.Count,
                ["walkable_regions"] = _lastWalkableRegions.Count,
                ["route_edges"] = _lastRouteEdges.Count,
                ["route_regions"] = _lastRouteRegions.Count,
                ["anchors"] = _lastAnchors.Count,
                ["visual_source_mode"] = _lastVisualSourceMode,
                ["prefab_chunk_manifest"] = _lastPrefabChunkManifestPath,
                ["prefab_chunks"] = _lastPrefabChunks.Count,
                ["layout_preset"] = LayoutPreset.ToString()
            };

        public Godot.Collections.Array<Godot.Collections.Dictionary> GetMountainLevels()
            => DuplicateArray(_lastLevels);

        public Godot.Collections.Array<Godot.Collections.Dictionary> GetWalkableRegions()
            => DuplicateArray(_lastWalkableRegions);

        public Godot.Collections.Array<Godot.Collections.Dictionary> GetRouteEdges()
            => DuplicateArray(_lastRouteEdges);

        public Godot.Collections.Array<Godot.Collections.Dictionary> GetRouteRegions()
            => DuplicateArray(_lastRouteRegions);

        public Godot.Collections.Dictionary GetAnchors()
            => _lastAnchors.Duplicate(true);

        public Godot.Collections.Array<Godot.Collections.Dictionary> GetPrefabChunkAssets()
            => DuplicateArray(_lastPrefabChunks);

        public bool IsRouteConnected()
            => _lastRouteConnected;

        public Error SaveGeneratedScene()
            => SaveGeneratedSceneToPath(SaveGeneratedScenePath);

        public Error SaveGeneratedSceneToPath(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                GD.PushWarning($"[{Name}] SaveGeneratedScenePath is empty.");
                return Error.InvalidParameter;
            }

            if (_lastPartCount <= 0)
                GeneratePrefab();

            string diskPath = DiskPath(scenePath);
            string? directory = Path.GetDirectoryName(diskPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var packedScene = new PackedScene();
            PrepareOwnersForPacking(this, this);
            Error packError = packedScene.Pack(this);
            if (packError != Error.Ok)
                return packError;

            return ResourceSaver.Save(packedScene, scenePath);
        }

        private static void PrepareOwnersForPacking(Node sceneRoot, Node current)
        {
            foreach (Node child in current.GetChildren())
            {
                child.Owner = sceneRoot;
                PrepareOwnersForPacking(sceneRoot, child);
            }
        }

        public Godot.Collections.Dictionary GetRouteConnectivitySummary()
            => new()
            {
                ["connected"] = _lastRouteConnected,
                ["route_edges"] = _lastRouteEdges.Count,
                ["route_regions"] = _lastRouteRegions.Count,
                ["route_connector_count"] = _lastRouteConnectorCount,
                ["missing_route_edge_count"] = _lastMissingRouteEdgeCount
            };

        public Vector2 GetAnchorPosition(string anchorId)
        {
            if (!_lastAnchors.TryGetValue(anchorId, out Variant value)
                || value.VariantType != Variant.Type.Dictionary)
            {
                return Vector2.Zero;
            }

            var anchor = value.AsGodotDictionary();
            float x = VariantToFloat(anchor.GetValueOrDefault("x", 0.0f), 0.0f);
            float y = VariantToFloat(anchor.GetValueOrDefault("y", 0.0f), 0.0f);
            return PrefabOffset + new Vector2(x, y) * Mathf.Max(0.01f, PrefabScale);
        }

        public int GetHeightLevelAtLocalPosition(Vector2 localPosition)
        {
            Godot.Collections.Dictionary routeRegion = GetRouteRegionAtLocalPosition(localPosition);
            if (routeRegion.Count > 0)
                return VariantToInt(routeRegion.GetValueOrDefault("to_level", routeRegion.GetValueOrDefault("to_height_level", 0)), 0);

            Godot.Collections.Dictionary walkableRegion = GetWalkableRegionAtLocalPosition(localPosition);
            return walkableRegion.Count > 0
                ? VariantToInt(walkableRegion.GetValueOrDefault("height_level", walkableRegion.GetValueOrDefault("level", 0)), 0)
                : -1;
        }

        public Godot.Collections.Dictionary GetWalkableRegionAtLocalPosition(Vector2 localPosition)
            => FindRegionContainingPoint(_lastWalkableRegions, ToManifestPoint(localPosition));

        public Godot.Collections.Dictionary GetRouteRegionAtLocalPosition(Vector2 localPosition)
            => FindRegionContainingPoint(_lastRouteRegions, ToManifestPoint(localPosition));

        private int AddVisualSprites(JsonElement root, string manifestDiskPath)
        {
            _lastVisualSourceMode = "";
            _lastPrefabChunkManifestPath = "";
            _lastPrefabChunks.Clear();

            MountainPrefabSourceMode mode = UseSingleBakedPrefabImage ? MountainPrefabSourceMode.BakedPrefabImage : SourceMode;
            if (mode == MountainPrefabSourceMode.BakedPrefabImage)
            {
                _lastVisualSourceMode = "baked_prefab_image";
                return AddBakedPrefabSprite(root, manifestDiskPath);
            }

            if (mode == MountainPrefabSourceMode.PrefabChunks || mode == MountainPrefabSourceMode.Auto)
            {
                string chunkManifestPath = ResolvePrefabChunkManifestPath(root, manifestDiskPath);
                if (!string.IsNullOrWhiteSpace(chunkManifestPath) && File.Exists(chunkManifestPath))
                {
                    int chunkCount = AddPrefabChunkSprites(chunkManifestPath);
                    if (chunkCount > 0 || mode == MountainPrefabSourceMode.PrefabChunks)
                    {
                        _lastVisualSourceMode = "prefab_chunks";
                        _lastPrefabChunkManifestPath = chunkManifestPath;
                        return chunkCount;
                    }
                }

                if (mode == MountainPrefabSourceMode.PrefabChunks)
                {
                    GD.PushWarning($"[{Name}] PrefabChunks mode could not load a chunk manifest for: {PrefabManifestPath}");
                    return 0;
                }
            }

            _lastVisualSourceMode = "manifest_placements";
            return AddLayeredPrefabSprites(root, manifestDiskPath);
        }

        private int AddPrefabChunkSprites(string chunkManifestDiskPath)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(chunkManifestDiskPath));
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("assets", out JsonElement assets) || assets.ValueKind != JsonValueKind.Array)
            {
                GD.PushWarning($"[{Name}] Prefab chunk manifest has no assets array: {chunkManifestDiskPath}");
                return 0;
            }

            int count = 0;
            foreach (JsonElement asset in assets.EnumerateArray())
            {
                string category = ReadString(asset, "category", "");
                if (!IncludeCompletePrefabChunk && string.Equals(category, "complete_prefab", StringComparison.Ordinal))
                    continue;

                string file = ReadString(asset, "file", "");
                if (string.IsNullOrWhiteSpace(file))
                    continue;

                string texturePath = ResolvePath(file, chunkManifestDiskPath);
                Texture2D? texture = LoadTexture(texturePath);
                if (texture == null)
                    continue;

                string role = ReadString(asset, "role", $"prefab_chunk_{count:000}");
                int heightLevel = ReadInt(asset, "height_level", Mathf.Max(ReadInt(asset, "from_level", 0), ReadInt(asset, "to_level", 0)));
                int zIndex = BaseZIndex + CategoryZOffset(category);
                if (UseHeightForZIndex)
                    zIndex += heightLevel * Mathf.Max(0, HeightZIndexStep);

                Vector2 position = ApplyLayoutPreset(role, category, ReadVector2(asset, "default_position", Vector2.Zero));
                var sprite = NewPartSprite(role, texture, position, Mathf.Max(0.01f, PrefabScale), zIndex);
                sprite.SetMeta("mountain_role", role);
                sprite.SetMeta("mountain_asset_id", ReadString(asset, "id", ""));
                sprite.SetMeta("mountain_category", category);
                sprite.SetMeta("mountain_height_level", heightLevel);
                sprite.SetMeta("mountain_from_level", ReadNullableInt(asset, "from_level"));
                sprite.SetMeta("mountain_to_level", ReadNullableInt(asset, "to_level"));
                sprite.SetMeta("mountain_z_index_from_height", UseHeightForZIndex);
                sprite.SetMeta("mountain_walkable", ReadBool(asset, "walkable", false));
                sprite.SetMeta("mountain_climbable", ReadBool(asset, "climbable", false));
                sprite.SetMeta("mountain_visual_includes_wall", ReadBool(asset, "visual_includes_wall", false));
                sprite.SetMeta("mountain_prefab_chunk", true);
                AddChild(sprite);

                _lastPrefabChunks.Add(JsonObjectToDictionary(asset));
                count++;
            }

            return count;
        }

        private Vector2 ToManifestPoint(Vector2 localPosition)
            => (localPosition - PrefabOffset) / Mathf.Max(0.01f, PrefabScale);

        private static Godot.Collections.Dictionary FindRegionContainingPoint(Godot.Collections.Array<Godot.Collections.Dictionary> regions, Vector2 point)
        {
            foreach (Godot.Collections.Dictionary region in regions)
            {
                Vector2[] polygon = ReadPointArray(region, 1.0f);
                if (polygon.Length >= 3 && PointInPolygon(point, polygon))
                    return region.Duplicate(true);

                float x = VariantToFloat(region.GetValueOrDefault("x", 0.0f), 0.0f);
                float y = VariantToFloat(region.GetValueOrDefault("y", 0.0f), 0.0f);
                float width = VariantToFloat(region.GetValueOrDefault("width", 0.0f), 0.0f);
                float height = VariantToFloat(region.GetValueOrDefault("height", 0.0f), 0.0f);
                if (width > 0.0f && height > 0.0f && new Rect2(x, y, width, height).HasPoint(point))
                    return region.Duplicate(true);
            }

            return new Godot.Collections.Dictionary();
        }

        private static bool PointInPolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                bool crosses = (a.Y > point.Y) != (b.Y > point.Y);
                if (crosses)
                {
                    float xAtY = ((b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y)) + a.X;
                    if (point.X < xAtY)
                        inside = !inside;
                }
            }

            return inside;
        }

        private int AddLayeredPrefabSprites(JsonElement root, string manifestDiskPath)
        {
            string sourcePack = ReadString(root, "source_pack", "");
            if (string.IsNullOrWhiteSpace(sourcePack))
            {
                GD.PushWarning($"[{Name}] Prefab manifest has no source_pack.");
                return 0;
            }

            string sourcePackDiskPath = ResolvePath(sourcePack, manifestDiskPath);
            if (!root.TryGetProperty("placements", out JsonElement placements) || placements.ValueKind != JsonValueKind.Array)
            {
                GD.PushWarning($"[{Name}] Prefab manifest has no placements array.");
                return 0;
            }

            int count = 0;
            foreach (JsonElement placement in placements.EnumerateArray())
            {
                string file = ReadString(placement, "file", "");
                if (string.IsNullOrWhiteSpace(file))
                    continue;

                string texturePath = ResolvePath(Path.Combine(sourcePackDiskPath, file), manifestDiskPath);
                Texture2D? texture = LoadTexture(texturePath);
                if (texture == null)
                    continue;

                Vector2 position = ReadVector2(placement, "position", Vector2.Zero);
                float scale = Mathf.Max(0.01f, ReadFloat(placement, "scale", 1.0f)) * Mathf.Max(0.01f, PrefabScale);
                int heightLevel = ReadPlacementHeightLevel(placement);
                int zIndex = BaseZIndex + ReadInt(placement, "z_index", 0);
                if (UseHeightForZIndex)
                    zIndex += heightLevel * Mathf.Max(0, HeightZIndexStep);
                string role = ReadString(placement, "role", $"part_{count:000}");

                var sprite = NewPartSprite(role, texture, position, scale, zIndex);
                sprite.SetMeta("mountain_role", role);
                sprite.SetMeta("mountain_asset_id", ReadString(placement, "asset_id", ""));
                sprite.SetMeta("mountain_height_level", heightLevel);
                sprite.SetMeta("mountain_z_index_from_height", UseHeightForZIndex);
                sprite.SetMeta("mountain_walkable", ReadBool(placement, "walkable", false));
                sprite.SetMeta("mountain_climbable", ReadBool(placement, "climbable", false));
                AddChild(sprite);
                count++;
            }

            return count;
        }

        private int AddWalkableAreas(JsonElement root)
        {
            if (!root.TryGetProperty("walkable_regions", out JsonElement regions) || regions.ValueKind != JsonValueKind.Array)
                return 0;

            int count = 0;
            foreach (JsonElement region in regions.EnumerateArray())
            {
                string id = ReadString(region, "id", $"walkable_region_{count:000}");
                string kind = ReadString(region, "kind", "walkable");
                int level = ReadInt(region, "level", 0);
                int heightLevel = ReadInt(region, "height_level", level);
                float elevationPx = ReadFloat(region, "elevation_px", heightLevel * 32.0f);
                float scale = Mathf.Max(0.01f, PrefabScale);
                var area = new Area2D
                {
                    Name = SafeNodeName(id),
                    Position = PrefabOffset,
                    CollisionLayer = WalkableCollisionLayer,
                    CollisionMask = WalkableCollisionMask
                };

                Node collisionNode;
                Vector2[] polygon = ReadPointArray(region, scale);
                if (polygon.Length >= 3)
                {
                    collisionNode = new CollisionPolygon2D
                    {
                        Polygon = polygon
                    };
                }
                else
                {
                    float x = ReadFloat(region, "x", 0.0f);
                    float y = ReadFloat(region, "y", 0.0f);
                    float width = ReadFloat(region, "width", 0.0f);
                    float height = ReadFloat(region, "height", 0.0f);
                    if (width <= 0.0f || height <= 0.0f)
                        continue;

                    collisionNode = new CollisionShape2D
                    {
                        Position = new Vector2((x + width * 0.5f) * scale, (y + height * 0.5f) * scale),
                        Shape = new RectangleShape2D { Size = new Vector2(width * scale, height * scale) }
                    };
                }

                area.SetMeta("mountain_region_id", id);
                area.SetMeta("mountain_region_kind", kind);
                area.SetMeta("mountain_level", level);
                area.SetMeta("mountain_height_level", heightLevel);
                area.SetMeta("mountain_elevation_px", elevationPx);
                area.SetMeta("mountain_walkable", true);
                area.SetMeta("mountain_region_shape", polygon.Length >= 3 ? "polygon" : "rectangle");
                if (!string.IsNullOrWhiteSpace(WalkableAreaGroup))
                    area.AddToGroup(WalkableAreaGroup);
                area.AddChild(collisionNode);
                AddChild(area);
                if (Engine.IsEditorHint())
                {
                    area.Owner = Owner;
                    collisionNode.Owner = Owner;
                }
                count++;
            }

            return count;
        }

        private static int ReadPlacementHeightLevel(JsonElement placement)
        {
            int heightLevel = ReadInt(placement, "height_level", int.MinValue);
            if (heightLevel != int.MinValue)
                return heightLevel;

            heightLevel = ReadInt(placement, "level", int.MinValue);
            if (heightLevel != int.MinValue)
                return heightLevel;

            int toLevel = ReadInt(placement, "to_level", int.MinValue);
            int fromLevel = ReadInt(placement, "from_level", int.MinValue);
            if (toLevel != int.MinValue || fromLevel != int.MinValue)
                return Mathf.Max(toLevel == int.MinValue ? 0 : toLevel, fromLevel == int.MinValue ? 0 : fromLevel);

            return 0;
        }

        private int AddRouteConnectorAreas()
        {
            if (_lastRouteRegions.Count > 0)
                return AddExplicitRouteRegionAreas();

            if (_lastRouteEdges.Count == 0 || _lastLevels.Count == 0 || _lastWalkableRegions.Count == 0)
                return 0;

            Dictionary<string, string> levelToRegion = BuildLevelToRegionMap();
            Dictionary<string, Rect2> regions = BuildRegionMap();
            int count = 0;

            foreach (Godot.Collections.Dictionary edge in _lastRouteEdges)
            {
                string fromLevel = VariantToString(edge.GetValueOrDefault("from", ""), "");
                string toLevel = VariantToString(edge.GetValueOrDefault("to", ""), "");
                if (string.IsNullOrWhiteSpace(fromLevel) || string.IsNullOrWhiteSpace(toLevel))
                    continue;
                if (!levelToRegion.TryGetValue(fromLevel, out string? fromRegionId)
                    || !levelToRegion.TryGetValue(toLevel, out string? toRegionId))
                {
                    continue;
                }
                if (!regions.TryGetValue(fromRegionId, out Rect2 fromRegion)
                    || !regions.TryGetValue(toRegionId, out Rect2 toRegion))
                {
                    continue;
                }

                float scale = Mathf.Max(0.01f, PrefabScale);
                string role = VariantToString(edge.GetValueOrDefault("role", "route_connector"), "route_connector");
                Vector2[] routePoints = ReadPointArray(edge, 1.0f);
                if (routePoints.Length < 2)
                {
                    routePoints = new[] { RectCenter(fromRegion), RectCenter(toRegion) };
                }

                for (int i = 0; i < routePoints.Length - 1; i++)
                {
                    Vector2 from = routePoints[i];
                    Vector2 to = routePoints[i + 1];
                    Vector2 delta = to - from;
                    float length = delta.Length();
                    if (length <= 0.01f)
                        continue;

                    var area = new Area2D
                    {
                        Name = SafeNodeName($"route_{fromLevel}_to_{toLevel}_{i + 1:00}"),
                        Position = PrefabOffset + ((from + to) * 0.5f) * scale,
                        Rotation = delta.Angle(),
                        CollisionLayer = WalkableCollisionLayer,
                        CollisionMask = WalkableCollisionMask
                    };
                    var shape = new CollisionShape2D
                    {
                        Shape = new RectangleShape2D
                        {
                            Size = new Vector2((length * scale) + RouteConnectorWidth * scale, Mathf.Max(4.0f, RouteConnectorWidth * scale))
                        }
                    };
                    area.SetMeta("mountain_route_from", fromLevel);
                    area.SetMeta("mountain_route_to", toLevel);
                    area.SetMeta("mountain_route_role", role);
                    area.SetMeta("mountain_route_segment", i);
                    area.SetMeta("mountain_walkable", true);
                    area.SetMeta("mountain_climbable", true);
                    if (!string.IsNullOrWhiteSpace(RouteConnectorGroup))
                        area.AddToGroup(RouteConnectorGroup);
                    area.AddChild(shape);
                    AddChild(area);
                    if (Engine.IsEditorHint())
                    {
                        area.Owner = Owner;
                        shape.Owner = Owner;
                    }
                    count++;
                }
            }

            return count;
        }

        private int AddExplicitRouteRegionAreas()
        {
            int count = 0;
            float scale = Mathf.Max(0.01f, PrefabScale);
            foreach (Godot.Collections.Dictionary region in _lastRouteRegions)
            {
                string id = VariantToString(region.GetValueOrDefault("id", $"route_region_{count:000}"), $"route_region_{count:000}");
                string fromLevel = VariantToString(region.GetValueOrDefault("from", ""), "");
                string toLevel = VariantToString(region.GetValueOrDefault("to", ""), "");
                string role = VariantToString(region.GetValueOrDefault("role", "height_ramp_tile"), "height_ramp_tile");
                Vector2[] polygon = ReadPointArray(region, scale);
                if (polygon.Length < 3)
                    continue;

                var area = new Area2D
                {
                    Name = SafeNodeName(id),
                    Position = PrefabOffset,
                    CollisionLayer = WalkableCollisionLayer,
                    CollisionMask = WalkableCollisionMask
                };
                var shape = new CollisionPolygon2D
                {
                    Polygon = polygon
                };
                int fromHeightLevel = VariantToInt(region.GetValueOrDefault("from_level", 0), 0);
                int toHeightLevel = VariantToInt(region.GetValueOrDefault("to_level", fromHeightLevel), fromHeightLevel);
                float fromElevationPx = VariantToFloat(region.GetValueOrDefault("from_elevation_px", fromHeightLevel * 36.0f), fromHeightLevel * 36.0f);
                float toElevationPx = VariantToFloat(region.GetValueOrDefault("to_elevation_px", toHeightLevel * 36.0f), toHeightLevel * 36.0f);
                area.SetMeta("mountain_route_region_id", id);
                area.SetMeta("mountain_route_from", fromLevel);
                area.SetMeta("mountain_route_to", toLevel);
                area.SetMeta("mountain_route_role", role);
                area.SetMeta("mountain_from_level", fromHeightLevel);
                area.SetMeta("mountain_to_level", toHeightLevel);
                area.SetMeta("mountain_from_elevation_px", fromElevationPx);
                area.SetMeta("mountain_to_elevation_px", toElevationPx);
                area.SetMeta("mountain_height_delta_px", toElevationPx - fromElevationPx);
                area.SetMeta("mountain_walkable", VariantToBool(region.GetValueOrDefault("walkable", true), true));
                area.SetMeta("mountain_climbable", VariantToBool(region.GetValueOrDefault("climbable", true), true));
                area.SetMeta("mountain_visual_includes_wall", VariantToBool(region.GetValueOrDefault("visual_includes_wall", false), false));
                if (!string.IsNullOrWhiteSpace(RouteConnectorGroup))
                    area.AddToGroup(RouteConnectorGroup);
                area.AddChild(shape);
                AddChild(area);
                if (Engine.IsEditorHint())
                {
                    area.Owner = Owner;
                    shape.Owner = Owner;
                }
                count++;
            }

            return count;
        }

        private int AddAnchorNodes(JsonElement root)
        {
            if (!root.TryGetProperty("anchors", out JsonElement anchors) || anchors.ValueKind != JsonValueKind.Object)
                return 0;

            int count = 0;
            foreach (JsonProperty property in anchors.EnumerateObject())
            {
                JsonElement anchor = property.Value;
                if (anchor.ValueKind != JsonValueKind.Object)
                    continue;

                float scale = Mathf.Max(0.01f, PrefabScale);
                float x = ReadFloat(anchor, "x", 0.0f);
                float y = ReadFloat(anchor, "y", 0.0f);
                int heightLevel = ReadInt(anchor, "height_level", ReadInt(anchor, "level", 0));
                float elevationPx = ReadFloat(anchor, "elevation_px", heightLevel * 36.0f);
                int zIndex = BaseZIndex + ReadInt(anchor, "z_index", 100);
                if (UseHeightForZIndex)
                    zIndex += heightLevel * Mathf.Max(0, HeightZIndexStep);
                string id = property.Name;
                var marker = new Marker2D
                {
                    Name = SafeNodeName(id),
                    Position = PrefabOffset + new Vector2(x, y) * scale,
                    ZIndex = zIndex
                };
                marker.SetMeta("mountain_anchor_id", id);
                marker.SetMeta("mountain_level", ReadInt(anchor, "level", 0));
                marker.SetMeta("mountain_height_level", heightLevel);
                marker.SetMeta("mountain_elevation_px", elevationPx);
                marker.SetMeta("mountain_kind", ReadString(anchor, "kind", ""));
                marker.SetMeta("mountain_pivot", ReadString(anchor, "pivot", ""));
                if (!string.IsNullOrWhiteSpace(AnchorGroup))
                    marker.AddToGroup(AnchorGroup);
                AddChild(marker);
                if (Engine.IsEditorHint())
                    marker.Owner = Owner;
                count++;
            }

            return count;
        }

        private int AddBakedPrefabSprite(JsonElement root, string manifestDiskPath)
        {
            string prefabImage = ReadString(root, "prefab_image", "prefab.png");
            string texturePath = ResolvePath(prefabImage, manifestDiskPath);
            Texture2D? texture = LoadTexture(texturePath);
            if (texture == null)
                return 0;

            var sprite = NewPartSprite("baked_prefab", texture, Vector2.Zero, Mathf.Max(0.01f, PrefabScale), BaseZIndex);
            AddChild(sprite);
            return 1;
        }

        private string ResolvePrefabChunkManifestPath(JsonElement root, string manifestDiskPath)
        {
            if (!string.IsNullOrWhiteSpace(PrefabChunkManifestPath))
                return DiskPath(PrefabChunkManifestPath);

            string chunkManifest = ReadString(root, "prefab_chunk_manifest", "");
            return string.IsNullOrWhiteSpace(chunkManifest)
                ? ""
                : ResolvePath(chunkManifest, manifestDiskPath);
        }

        private static int CategoryZOffset(string category)
        {
            return category switch
            {
                "path_overlay" => 4,
                "route_chunk" => 5,
                "castle_chunk" => 8,
                "castle_floor_overlay" => 9,
                "prop_chunk" => 10,
                "complete_prefab" => 0,
                _ => 0
            };
        }

        private Vector2 ApplyLayoutPreset(string role, string category, Vector2 referencePosition)
        {
            if (LayoutPreset == MountainPrefabLayoutPreset.Reference)
                return referencePosition;

            Vector2 offset = Vector2.Zero;
            switch (LayoutPreset)
            {
                case MountainPrefabLayoutPreset.Compact:
                    if (role.Contains("level_1_right", StringComparison.Ordinal)) offset += new Vector2(-28.0f, 4.0f);
                    if (role.Contains("level_2_left", StringComparison.Ordinal)) offset += new Vector2(22.0f, 4.0f);
                    if (role.Contains("route_0_to_1", StringComparison.Ordinal)) offset += new Vector2(-12.0f, 2.0f);
                    if (role.Contains("route_1_to_2", StringComparison.Ordinal)) offset += new Vector2(8.0f, 0.0f);
                    if (role.Contains("route_2_to_3", StringComparison.Ordinal)) offset += new Vector2(10.0f, -4.0f);
                    if (category == "castle_chunk") offset += new Vector2(12.0f, -4.0f);
                    break;

                case MountainPrefabLayoutPreset.Wide:
                    if (role.Contains("level_0_base", StringComparison.Ordinal)) offset += new Vector2(-18.0f, 10.0f);
                    if (role.Contains("level_1_right", StringComparison.Ordinal)) offset += new Vector2(74.0f, 10.0f);
                    if (role.Contains("level_2_left", StringComparison.Ordinal)) offset += new Vector2(-58.0f, 0.0f);
                    if (role.Contains("route_0_to_1", StringComparison.Ordinal)) offset += new Vector2(28.0f, 8.0f);
                    if (role.Contains("route_1_to_2", StringComparison.Ordinal)) offset += new Vector2(-20.0f, 2.0f);
                    if (role.Contains("route_2_to_3", StringComparison.Ordinal)) offset += new Vector2(-6.0f, -4.0f);
                    if (category == "castle_chunk") offset += new Vector2(12.0f, -4.0f);
                    break;

                case MountainPrefabLayoutPreset.HighCastle:
                    if (category == "castle_chunk") offset += new Vector2(0.0f, -72.0f);
                    if (role.Contains("route_2_to_3", StringComparison.Ordinal)) offset += new Vector2(0.0f, -42.0f);
                    if (role.Contains("route_1_to_2", StringComparison.Ordinal)) offset += new Vector2(0.0f, -8.0f);
                    if (role.Contains("level_2_left", StringComparison.Ordinal)) offset += new Vector2(0.0f, -10.0f);
                    break;
            }

            return referencePosition + offset;
        }

        private Sprite2D NewPartSprite(string role, Texture2D texture, Vector2 position, float scale, int zIndex)
        {
            var sprite = new Sprite2D
            {
                Name = SafeNodeName(role),
                Texture = texture,
                Centered = false,
                Position = PrefabOffset + position * Mathf.Max(0.01f, PrefabScale),
                Scale = new Vector2(scale, scale),
                ZIndex = zIndex,
                TextureFilter = TextureFilter
            };
            if (!string.IsNullOrWhiteSpace(GeneratedPartGroup))
                sprite.AddToGroup(GeneratedPartGroup);
            if (Engine.IsEditorHint())
                sprite.Owner = Owner;
            return sprite;
        }

        private void ClearGeneratedParts()
        {
            var toRemove = new List<Node>();
            foreach (Node child in GetChildren())
            {
                bool generatedPart = string.IsNullOrWhiteSpace(GeneratedPartGroup) || child.IsInGroup(GeneratedPartGroup);
                bool walkableArea = !string.IsNullOrWhiteSpace(WalkableAreaGroup) && child.IsInGroup(WalkableAreaGroup);
                bool anchor = !string.IsNullOrWhiteSpace(AnchorGroup) && child.IsInGroup(AnchorGroup);
                bool routeConnector = !string.IsNullOrWhiteSpace(RouteConnectorGroup) && child.IsInGroup(RouteConnectorGroup);
                if (generatedPart || walkableArea || anchor || routeConnector)
                    toRemove.Add(child);
            }

            foreach (Node child in toRemove)
            {
                RemoveChild(child);
                child.QueueFree();
            }
        }

        private bool ComputeRouteConnected(out int missingEdgeCount)
        {
            missingEdgeCount = 0;
            if (_lastLevels.Count <= 1)
                return true;
            if (_lastRouteEdges.Count == 0)
            {
                missingEdgeCount = _lastLevels.Count - 1;
                return false;
            }

            var levelIds = new List<string>();
            foreach (Godot.Collections.Dictionary level in _lastLevels)
            {
                string id = VariantToString(level.GetValueOrDefault("id", ""), "");
                if (!string.IsNullOrWhiteSpace(id))
                    levelIds.Add(id);
            }
            if (levelIds.Count <= 1)
                return true;

            Dictionary<string, string> levelToRegion = BuildLevelToRegionMap();
            Dictionary<string, Rect2> regions = BuildRegionMap();
            var graph = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (string id in levelIds)
                graph[id] = new HashSet<string>(StringComparer.Ordinal);

            foreach (Godot.Collections.Dictionary edge in _lastRouteEdges)
            {
                string from = VariantToString(edge.GetValueOrDefault("from", ""), "");
                string to = VariantToString(edge.GetValueOrDefault("to", ""), "");
                if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to) || !graph.ContainsKey(from) || !graph.ContainsKey(to))
                {
                    missingEdgeCount++;
                    continue;
                }
                if (!levelToRegion.TryGetValue(from, out string? fromRegion)
                    || !levelToRegion.TryGetValue(to, out string? toRegion)
                    || !regions.ContainsKey(fromRegion)
                    || !regions.ContainsKey(toRegion))
                {
                    missingEdgeCount++;
                    continue;
                }

                graph[from].Add(to);
                graph[to].Add(from);
            }

            var visited = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();
            queue.Enqueue(levelIds[0]);
            visited.Add(levelIds[0]);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                foreach (string next in graph[current])
                {
                    if (visited.Add(next))
                        queue.Enqueue(next);
                }
            }

            return visited.Contains(levelIds[^1]);
        }

        private Dictionary<string, string> BuildLevelToRegionMap()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Godot.Collections.Dictionary level in _lastLevels)
            {
                string id = VariantToString(level.GetValueOrDefault("id", ""), "");
                string region = VariantToString(level.GetValueOrDefault("walkable_region", ""), "");
                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(region))
                    map[id] = region;
            }
            return map;
        }

        private Dictionary<string, Rect2> BuildRegionMap()
        {
            var map = new Dictionary<string, Rect2>(StringComparer.Ordinal);
            foreach (Godot.Collections.Dictionary region in _lastWalkableRegions)
            {
                string id = VariantToString(region.GetValueOrDefault("id", ""), "");
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                Vector2[] points = ReadPointArray(region, 1.0f);
                if (points.Length >= 3)
                {
                    map[id] = BoundsForPoints(points);
                    continue;
                }

                float x = VariantToFloat(region.GetValueOrDefault("x", 0.0f), 0.0f);
                float y = VariantToFloat(region.GetValueOrDefault("y", 0.0f), 0.0f);
                float width = VariantToFloat(region.GetValueOrDefault("width", 0.0f), 0.0f);
                float height = VariantToFloat(region.GetValueOrDefault("height", 0.0f), 0.0f);
                if (width > 0.0f && height > 0.0f)
                    map[id] = new Rect2(x, y, width, height);
            }
            return map;
        }

        private static Vector2 RectCenter(Rect2 rect)
            => rect.Position + rect.Size * 0.5f;

        private static Rect2 BoundsForPoints(IReadOnlyList<Vector2> points)
        {
            float minX = points[0].X;
            float maxX = points[0].X;
            float minY = points[0].Y;
            float maxY = points[0].Y;
            for (int i = 1; i < points.Count; i++)
            {
                minX = Mathf.Min(minX, points[i].X);
                maxX = Mathf.Max(maxX, points[i].X);
                minY = Mathf.Min(minY, points[i].Y);
                maxY = Mathf.Max(maxY, points[i].Y);
            }
            return new Rect2(minX, minY, maxX - minX, maxY - minY);
        }

        private static Vector2[] ReadPointArray(JsonElement element, float scale)
        {
            if (!element.TryGetProperty("points", out JsonElement pointsElement) || pointsElement.ValueKind != JsonValueKind.Array)
                return Array.Empty<Vector2>();

            var points = new List<Vector2>();
            foreach (JsonElement pointElement in pointsElement.EnumerateArray())
            {
                if (pointElement.ValueKind != JsonValueKind.Object)
                    continue;
                points.Add(new Vector2(ReadFloat(pointElement, "x", 0.0f) * scale, ReadFloat(pointElement, "y", 0.0f) * scale));
            }
            return points.ToArray();
        }

        private static Vector2[] ReadPointArray(Godot.Collections.Dictionary dictionary, float scale)
        {
            if (!dictionary.TryGetValue("points", out Variant pointsValue) || pointsValue.VariantType != Variant.Type.Array)
                return Array.Empty<Vector2>();

            var array = pointsValue.AsGodotArray();
            var points = new List<Vector2>();
            foreach (Variant item in array)
            {
                if (item.VariantType != Variant.Type.Dictionary)
                    continue;
                var point = item.AsGodotDictionary();
                points.Add(new Vector2(
                    VariantToFloat(point.GetValueOrDefault("x", 0.0f), 0.0f) * scale,
                    VariantToFloat(point.GetValueOrDefault("y", 0.0f), 0.0f) * scale));
            }
            return points.ToArray();
        }

        private void ReadPrefabGameplayData(JsonElement root)
        {
            _lastLevels.Clear();
            _lastWalkableRegions.Clear();
            _lastRouteEdges.Clear();
            _lastRouteRegions.Clear();
            _lastAnchors.Clear();

            ReadArrayOfObjects(root, "levels", _lastLevels);
            ReadArrayOfObjects(root, "walkable_regions", _lastWalkableRegions);
            ReadArrayOfObjects(root, "route_edges", _lastRouteEdges);
            ReadArrayOfObjects(root, "route_regions", _lastRouteRegions);

            if (!root.TryGetProperty("anchors", out JsonElement anchors) || anchors.ValueKind != JsonValueKind.Object)
                return;

            foreach (JsonProperty property in anchors.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object)
                    _lastAnchors[property.Name] = JsonObjectToDictionary(property.Value);
            }
        }

        private static void ReadArrayOfObjects(JsonElement root, string name, Godot.Collections.Array<Godot.Collections.Dictionary> destination)
        {
            if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.Array)
                return;

            foreach (JsonElement item in value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                    destination.Add(JsonObjectToDictionary(item));
            }
        }

        private static Godot.Collections.Array<Godot.Collections.Dictionary> DuplicateArray(Godot.Collections.Array<Godot.Collections.Dictionary> source)
        {
            var copy = new Godot.Collections.Array<Godot.Collections.Dictionary>();
            foreach (Godot.Collections.Dictionary item in source)
                copy.Add(item.Duplicate(true));
            return copy;
        }

        private static Godot.Collections.Dictionary JsonObjectToDictionary(JsonElement element)
        {
            var dictionary = new Godot.Collections.Dictionary();
            foreach (JsonProperty property in element.EnumerateObject())
                dictionary[property.Name] = JsonValueToVariant(property.Value);
            return dictionary;
        }

        private static Variant JsonValueToVariant(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? "",
                JsonValueKind.Number when value.TryGetInt32(out int integer) => integer,
                JsonValueKind.Number when value.TryGetDouble(out double dbl) => dbl,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Object => JsonObjectToDictionary(value),
                JsonValueKind.Array => JsonArrayToGodotArray(value),
                _ => new Variant()
            };
        }

        private static Godot.Collections.Array JsonArrayToGodotArray(JsonElement value)
        {
            var array = new Godot.Collections.Array();
            foreach (JsonElement item in value.EnumerateArray())
                array.Add(JsonValueToVariant(item));
            return array;
        }

        private static Texture2D? LoadTexture(string path)
        {
            Image image = Image.LoadFromFile(path);
            return image.IsEmpty() ? null : ImageTexture.CreateFromImage(image);
        }

        private static string ResolvePath(string path, string manifestDiskPath)
        {
            if (path.StartsWith("res://", StringComparison.Ordinal) || path.StartsWith("user://", StringComparison.Ordinal))
                return DiskPath(path);
            if (Path.IsPathRooted(path))
                return path;

            string? manifestDir = Path.GetDirectoryName(manifestDiskPath);
            string relativeToManifest = Path.GetFullPath(Path.Combine(manifestDir ?? "", path));
            if (File.Exists(relativeToManifest) || Directory.Exists(relativeToManifest))
                return relativeToManifest;

            return Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), path));
        }

        private static string DiskPath(string path)
            => path.StartsWith("res://", StringComparison.Ordinal) || path.StartsWith("user://", StringComparison.Ordinal)
                ? ProjectSettings.GlobalizePath(path)
                : path;

        private static Vector2 ReadVector2(JsonElement element, string name, Vector2 fallback)
        {
            if (!element.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.Object)
                return fallback;

            return new Vector2(ReadFloat(value, "x", fallback.X), ReadFloat(value, "y", fallback.Y));
        }

        private static string ReadString(JsonElement element, string name, string fallback)
            => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;

        private static int ReadInt(JsonElement element, string name, int fallback)
            => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int result) ? result : fallback;

        private static int ReadNullableInt(JsonElement element, string name)
            => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int result) ? result : -1;

        private static bool ReadBool(JsonElement element, string name, bool fallback)
            => element.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : fallback;

        private static float ReadFloat(JsonElement element, string name, float fallback)
        {
            if (!element.TryGetProperty(name, out JsonElement value))
                return fallback;
            if (value.ValueKind != JsonValueKind.Number)
                return fallback;
            if (value.TryGetSingle(out float single))
                return single;
            if (value.TryGetDouble(out double dbl))
                return (float)dbl;
            return fallback;
        }

        private static string SafeNodeName(string value)
            => string.IsNullOrWhiteSpace(value) ? "MountainPart" : value.Trim().Replace(' ', '_').Replace('-', '_');

        private static float VariantToFloat(Variant value, float fallback)
        {
            return value.VariantType switch
            {
                Variant.Type.Int => value.AsInt32(),
                Variant.Type.Float => (float)value.AsDouble(),
                _ => fallback
            };
        }

        private static string VariantToString(Variant value, string fallback)
            => value.VariantType == Variant.Type.String ? value.AsString() : fallback;

        private static int VariantToInt(Variant value, int fallback)
        {
            return value.VariantType switch
            {
                Variant.Type.Int => value.AsInt32(),
                Variant.Type.Float => Mathf.RoundToInt((float)value.AsDouble()),
                _ => fallback
            };
        }

        private static bool VariantToBool(Variant value, bool fallback)
            => value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }
}

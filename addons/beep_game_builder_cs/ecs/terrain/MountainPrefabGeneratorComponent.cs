using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Beep.ECS
{
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
        public string PrefabManifestPath { get; set; } = "res://tmp/mountain_semantic_green_large_levelled_castle/prefab_manifest.json";

        [Export] public bool GenerateOnReady { get; set; } = false;
        [Export] public bool GenerateInEditor { get; set; } = false;
        [Export] public bool ClearExistingGeneratedParts { get; set; } = true;
        [Export] public bool UseSingleBakedPrefabImage { get; set; } = false;
        [Export] public string GeneratedPartGroup { get; set; } = "generated_mountain_prefab_part";
        [Export] public bool CreateWalkableAreas { get; set; } = true;
        [Export] public string WalkableAreaGroup { get; set; } = "generated_mountain_walkable_region";
        [Export(PropertyHint.Layers2DPhysics)] public uint WalkableCollisionLayer { get; set; } = 1;
        [Export(PropertyHint.Layers2DPhysics)] public uint WalkableCollisionMask { get; set; } = 0;
        [Export] public bool CreateAnchorNodes { get; set; } = true;
        [Export] public string AnchorGroup { get; set; } = "generated_mountain_anchor";

        [ExportGroup("Placement")]
        [Export] public Vector2 PrefabOffset { get; set; } = Vector2.Zero;
        [Export(PropertyHint.Range, "0.05,8,0.01")] public float PrefabScale { get; set; } = 1.0f;
        [Export] public int BaseZIndex { get; set; } = 0;
        [Export] public CanvasItem.TextureFilterEnum TextureFilter { get; set; } = CanvasItem.TextureFilterEnum.Linear;

        private string _lastManifestPath = "";
        private int _lastPartCount;
        private int _lastWalkableAreaCount;
        private int _lastAnchorCount;
        private readonly Godot.Collections.Array<Godot.Collections.Dictionary> _lastLevels = new();
        private readonly Godot.Collections.Array<Godot.Collections.Dictionary> _lastWalkableRegions = new();
        private readonly Godot.Collections.Array<Godot.Collections.Dictionary> _lastRouteEdges = new();
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
                int count = UseSingleBakedPrefabImage
                    ? AddBakedPrefabSprite(root, manifestDiskPath)
                    : AddLayeredPrefabSprites(root, manifestDiskPath);
                ReadPrefabGameplayData(root);
                _lastWalkableAreaCount = CreateWalkableAreas ? AddWalkableAreas(root) : 0;
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
                ["create_walkable_areas"] = CreateWalkableAreas,
                ["create_anchor_nodes"] = CreateAnchorNodes,
                ["use_single_baked_prefab_image"] = UseSingleBakedPrefabImage,
                ["levels"] = _lastLevels.Count,
                ["walkable_regions"] = _lastWalkableRegions.Count,
                ["route_edges"] = _lastRouteEdges.Count,
                ["anchors"] = _lastAnchors.Count
            };

        public Godot.Collections.Array<Godot.Collections.Dictionary> GetMountainLevels()
            => DuplicateArray(_lastLevels);

        public Godot.Collections.Array<Godot.Collections.Dictionary> GetWalkableRegions()
            => DuplicateArray(_lastWalkableRegions);

        public Godot.Collections.Array<Godot.Collections.Dictionary> GetRouteEdges()
            => DuplicateArray(_lastRouteEdges);

        public Godot.Collections.Dictionary GetAnchors()
            => _lastAnchors.Duplicate(true);

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
                int zIndex = BaseZIndex + ReadInt(placement, "z_index", 0);
                string role = ReadString(placement, "role", $"part_{count:000}");

                var sprite = NewPartSprite(role, texture, position, scale, zIndex);
                sprite.SetMeta("mountain_role", role);
                sprite.SetMeta("mountain_asset_id", ReadString(placement, "asset_id", ""));
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
                float x = ReadFloat(region, "x", 0.0f);
                float y = ReadFloat(region, "y", 0.0f);
                float width = ReadFloat(region, "width", 0.0f);
                float height = ReadFloat(region, "height", 0.0f);
                if (width <= 0.0f || height <= 0.0f)
                    continue;

                string id = ReadString(region, "id", $"walkable_region_{count:000}");
                string kind = ReadString(region, "kind", "walkable");
                int level = ReadInt(region, "level", 0);
                float scale = Mathf.Max(0.01f, PrefabScale);
                var area = new Area2D
                {
                    Name = SafeNodeName(id),
                    Position = PrefabOffset + new Vector2(x, y) * scale,
                    CollisionLayer = WalkableCollisionLayer,
                    CollisionMask = WalkableCollisionMask
                };
                var shape = new CollisionShape2D
                {
                    Position = new Vector2(width * scale * 0.5f, height * scale * 0.5f),
                    Shape = new RectangleShape2D { Size = new Vector2(width * scale, height * scale) }
                };
                area.SetMeta("mountain_region_id", id);
                area.SetMeta("mountain_region_kind", kind);
                area.SetMeta("mountain_level", level);
                area.SetMeta("mountain_walkable", true);
                if (!string.IsNullOrWhiteSpace(WalkableAreaGroup))
                    area.AddToGroup(WalkableAreaGroup);
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
                string id = property.Name;
                var marker = new Marker2D
                {
                    Name = SafeNodeName(id),
                    Position = PrefabOffset + new Vector2(x, y) * scale,
                    ZIndex = BaseZIndex + ReadInt(anchor, "z_index", 100)
                };
                marker.SetMeta("mountain_anchor_id", id);
                marker.SetMeta("mountain_level", ReadInt(anchor, "level", 0));
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
                if (generatedPart || walkableArea || anchor)
                    toRemove.Add(child);
            }

            foreach (Node child in toRemove)
            {
                RemoveChild(child);
                child.QueueFree();
            }
        }

        private void ReadPrefabGameplayData(JsonElement root)
        {
            _lastLevels.Clear();
            _lastWalkableRegions.Clear();
            _lastRouteEdges.Clear();
            _lastAnchors.Clear();

            ReadArrayOfObjects(root, "levels", _lastLevels);
            ReadArrayOfObjects(root, "walkable_regions", _lastWalkableRegions);
            ReadArrayOfObjects(root, "route_edges", _lastRouteEdges);

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
            => element.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int result) ? result : fallback;

        private static bool ReadBool(JsonElement element, string name, bool fallback)
            => element.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : fallback;

        private static float ReadFloat(JsonElement element, string name, float fallback)
        {
            if (!element.TryGetProperty(name, out JsonElement value))
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
    }
}

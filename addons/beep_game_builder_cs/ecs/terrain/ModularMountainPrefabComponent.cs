using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Beep.ECS
{
    public enum ModularMountainMaterialTheme
    {
        Sandstone = 0,
        GrassGranite = 1,
        GreyRock = 2,
        VolcanicBasalt = 3,
        MeadowHill = 5,
        RedRockMesa = 6,
        AlpineSnow = 7,
        Custom = 4
    }

    public enum ModularMountainPrefabStyle
    {
        MountainPrefab1 = 0,
        MountainPrefab2NaturalPlateau = 1
    }

    /// <summary>
    /// Builds a ramp-free front-facing mountain structure from authored plates.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ModularMountainPrefabComponent : Node2D
    {
        [Signal] public delegate void MountainGeneratedEventHandler(int generatedPartCount);

        [ExportGroup("Source")]
        [Export] public ModularMountainPrefabStyle PrefabStyle { get; set; } = ModularMountainPrefabStyle.MountainPrefab1;
        [Export] public ModularMountainMaterialTheme MaterialTheme { get; set; } = ModularMountainMaterialTheme.Sandstone;

        [Export(PropertyHint.File, "*.json")]
        public string PackManifestPath { get; set; } = "res://addons/beep_game_builder_cs/generated/mountains/low_poly_sandstone/authored_prefabs/modular_front_2_5d/modular_mountain_pack_manifest.json";

        [Export] public string BasePrefabId { get; set; } = "three_level_wide_no_ramps";
        [Export] public bool GenerateOnReady { get; set; } = true;
        [Export] public bool GenerateInEditor { get; set; } = false;
        [Export] public bool ClearExistingGeneratedParts { get; set; } = true;

        [ExportGroup("Placement")]
        [Export] public Vector2 PrefabOffset { get; set; } = Vector2.Zero;
        [Export(PropertyHint.Range, "0.05,4,0.01")] public float PrefabScale { get; set; } = 1.0f;
        [Export] public int BaseZIndex { get; set; } = 0;
        [Export] public CanvasItem.TextureFilterEnum TextureFilter { get; set; } = CanvasItem.TextureFilterEnum.Linear;

        private const string GeneratedGroup = "generated_modular_mountain_part";
        private int _lastGeneratedPartCount;
        private int _lastLevelCount;
        private string _lastBaseId = "";

        public override void _Ready()
        {
            UpdateConfigurationWarnings();
            if (GenerateOnReady && (!Engine.IsEditorHint() || GenerateInEditor))
                CallDeferred(nameof(GenerateMountain));
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (MaterialTheme == ModularMountainMaterialTheme.Custom && string.IsNullOrWhiteSpace(PackManifestPath))
                return new[] { "PackManifestPath must point to a modular mountain pack manifest." };
            if (PrefabScale <= 0.0f || !float.IsFinite(PrefabScale))
                return new[] { "PrefabScale must be a positive finite number." };
            return Array.Empty<string>();
        }

        public int GenerateMountain()
        {
            string manifestPath = ResolveManifestPath();
            string manifestDiskPath = DiskPath(manifestPath);
            if (!File.Exists(manifestDiskPath))
            {
                GD.PushWarning($"[{Name}] Modular mountain manifest does not exist: {manifestPath}");
                return 0;
            }

            if (ClearExistingGeneratedParts)
                ClearGeneratedParts();

            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestDiskPath));
                JsonElement root = document.RootElement;
                JsonElement basePrefab = FindById(root, "base_prefabs", BasePrefabId);
                if (basePrefab.ValueKind != JsonValueKind.Object)
                    throw new InvalidDataException($"Base prefab '{BasePrefabId}' was not found.");

                Vector2 baseSize = ReadSize(basePrefab, "image_size");
                int levelCount = Math.Max(1, (int)ReadFloat(basePrefab, "level_count", 1.0f));
                int generatedPartCount = AddBaseSprites(root, basePrefab, baseSize, manifestDiskPath);
                _lastGeneratedPartCount = generatedPartCount;
                _lastLevelCount = levelCount;
                _lastBaseId = BasePrefabId;
                EmitSignal(SignalName.MountainGenerated, generatedPartCount);
                return generatedPartCount;
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[{Name}] Could not generate modular mountain: {ex.Message}");
                return 0;
            }
        }

        public Godot.Collections.Dictionary GetLastGenerationSummary()
            => new()
            {
                ["base_prefab_id"] = _lastBaseId,
                ["prefab_style"] = PrefabStyle.ToString(),
                ["material_theme"] = MaterialTheme.ToString(),
                ["manifest_path"] = ResolveManifestPath(),
                ["level_count"] = _lastLevelCount,
                ["generated_part_count"] = _lastGeneratedPartCount,
                ["ramp_count"] = 0
            };

        private int AddBaseSprites(JsonElement root, JsonElement basePrefab, Vector2 baseSize, string manifestDiskPath)
        {
            if (basePrefab.TryGetProperty("plate_assembly", out JsonElement assembly) &&
                assembly.ValueKind == JsonValueKind.Array)
            {
                Dictionary<string, JsonElement> plates = IndexById(root, "plate_modules");
                int generatedPartCount = 0;
                foreach (JsonElement part in assembly.EnumerateArray())
                {
                    string plateId = ReadString(part, "plate", "");
                    if (!plates.TryGetValue(plateId, out JsonElement plate))
                        throw new InvalidDataException($"Plate module '{plateId}' was not found.");
                    Texture2D plateTexture = LoadTexture(ResolvePath(ReadString(plate, "file", ""), manifestDiskPath));
                    Vector2 center = ReadVector2(part, "center") - baseSize * 0.5f;
                    int level = (int)ReadFloat(part, "level", 0.0f);
                    int relativeZIndex = (int)ReadFloat(part, "z_index", level * 10.0f);
                    var plateSprite = NewSprite(
                        $"MountainPlate{level}",
                        plateTexture,
                        PrefabOffset + center * PrefabScale,
                        PrefabScale,
                        BaseZIndex + relativeZIndex);
                    plateSprite.SetMeta("mountain_role", "level_plate");
                    plateSprite.SetMeta("mountain_plate_id", plateId);
                    plateSprite.SetMeta("mountain_level", level);
                    plateSprite.SetMeta("mountain_walkable", true);
                    AddChild(plateSprite);
                    generatedPartCount++;
                }
                return generatedPartCount;
            }

            string file = ReadString(basePrefab, "file", "");
            Texture2D texture = LoadTexture(ResolvePath(file, manifestDiskPath));
            var sprite = NewSprite("MountainBase", texture, PrefabOffset, PrefabScale, BaseZIndex);
            sprite.SetMeta("mountain_role", "ramp_free_base");
            sprite.SetMeta("mountain_base_id", BasePrefabId);
            AddChild(sprite);
            return 1;
        }

        private Sprite2D NewSprite(string name, Texture2D texture, Vector2 position, float scale, int zIndex)
        {
            var sprite = new Sprite2D
            {
                Name = name,
                Texture = texture,
                Position = position,
                Scale = Vector2.One * scale,
                ZIndex = zIndex,
                TextureFilter = TextureFilter
            };
            sprite.AddToGroup(GeneratedGroup, true);
            return sprite;
        }

        private void ClearGeneratedParts()
        {
            foreach (Node child in GetChildren())
            {
                if (child.IsInGroup(GeneratedGroup))
                    child.Free();
            }
            _lastGeneratedPartCount = 0;
            _lastLevelCount = 0;
            _lastBaseId = "";
        }

        private static Texture2D LoadTexture(string path)
        {
            Image image = Image.LoadFromFile(path);
            if (image.IsEmpty())
                throw new InvalidDataException($"Could not load texture: {path}");
            image.GenerateMipmaps();
            return ImageTexture.CreateFromImage(image);
        }

        private static JsonElement FindById(JsonElement root, string arrayName, string id)
        {
            if (!root.TryGetProperty(arrayName, out JsonElement array) || array.ValueKind != JsonValueKind.Array)
                return default;
            foreach (JsonElement item in array.EnumerateArray())
            {
                if (string.Equals(ReadString(item, "id", ""), id, StringComparison.Ordinal))
                    return item;
            }
            return default;
        }

        private static Dictionary<string, JsonElement> IndexById(JsonElement root, string arrayName)
        {
            var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            if (!root.TryGetProperty(arrayName, out JsonElement array) || array.ValueKind != JsonValueKind.Array)
                return result;
            foreach (JsonElement item in array.EnumerateArray())
            {
                string id = ReadString(item, "id", "");
                if (!string.IsNullOrWhiteSpace(id))
                    result[id] = item;
            }
            return result;
        }

        private static Vector2 ReadSize(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.Array)
                return Vector2.Zero;
            JsonElement.ArrayEnumerator items = value.EnumerateArray();
            if (!items.MoveNext())
                return Vector2.Zero;
            float width = items.Current.GetSingle();
            if (!items.MoveNext())
                return Vector2.Zero;
            return new Vector2(width, items.Current.GetSingle());
        }

        private static Vector2 ReadVector2(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.Object)
                return Vector2.Zero;
            return new Vector2(ReadFloat(value, "x", 0.0f), ReadFloat(value, "y", 0.0f));
        }

        private static string ReadString(JsonElement element, string name, string fallback)
            => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? fallback
                : fallback;

        private static float ReadFloat(JsonElement element, string name, float fallback)
            => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number
                ? value.GetSingle()
                : fallback;

        private static string ResolvePath(string path, string manifestDiskPath)
        {
            if (path.StartsWith("res://", StringComparison.Ordinal) || path.StartsWith("user://", StringComparison.Ordinal))
                return DiskPath(path);
            if (Path.IsPathRooted(path))
                return path;
            string? manifestDirectory = Path.GetDirectoryName(manifestDiskPath);
            string besideManifest = Path.GetFullPath(Path.Combine(manifestDirectory ?? "", path));
            if (File.Exists(besideManifest))
                return besideManifest;
            return Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), path));
        }

        private static string DiskPath(string path)
            => path.StartsWith("res://", StringComparison.Ordinal) || path.StartsWith("user://", StringComparison.Ordinal)
                ? ProjectSettings.GlobalizePath(path)
                : path;

        private string ResolveManifestPath()
        {
            if (PrefabStyle == ModularMountainPrefabStyle.MountainPrefab2NaturalPlateau)
            {
                return MaterialTheme switch
                {
                    ModularMountainMaterialTheme.GrassGranite => BuiltInPrefab2ThemeManifest("grass_granite"),
                    ModularMountainMaterialTheme.GreyRock => BuiltInPrefab2ThemeManifest("grey_rock"),
                    ModularMountainMaterialTheme.VolcanicBasalt => BuiltInPrefab2ThemeManifest("volcanic_basalt"),
                    ModularMountainMaterialTheme.MeadowHill => BuiltInPrefab2ThemeManifest("meadow_hill"),
                    ModularMountainMaterialTheme.RedRockMesa => BuiltInPrefab2ThemeManifest("red_rock_mesa"),
                    ModularMountainMaterialTheme.AlpineSnow => BuiltInPrefab2ThemeManifest("alpine_snow"),
                    ModularMountainMaterialTheme.Custom => PackManifestPath,
                    _ => BuiltInPrefab2ThemeManifest("sandstone")
                };
            }

            return MaterialTheme switch
            {
                ModularMountainMaterialTheme.GrassGranite => BuiltInThemeManifest("grass_granite"),
                ModularMountainMaterialTheme.GreyRock => BuiltInThemeManifest("grey_rock"),
                ModularMountainMaterialTheme.VolcanicBasalt => BuiltInThemeManifest("volcanic_basalt"),
                ModularMountainMaterialTheme.MeadowHill => BuiltInThemeManifest("meadow_hill"),
                ModularMountainMaterialTheme.RedRockMesa => BuiltInThemeManifest("red_rock_mesa"),
                ModularMountainMaterialTheme.AlpineSnow => BuiltInThemeManifest("alpine_snow"),
                ModularMountainMaterialTheme.Custom => PackManifestPath,
                _ => "res://addons/beep_game_builder_cs/generated/mountains/low_poly_sandstone/authored_prefabs/modular_front_2_5d/modular_mountain_pack_manifest.json"
            };
        }

        private static string BuiltInThemeManifest(string themeId)
            => $"res://addons/beep_game_builder_cs/generated/mountains/low_poly_sandstone/authored_prefabs/modular_themes/{themeId}/modular_mountain_pack_manifest.json";

        private static string BuiltInPrefab2ThemeManifest(string themeId)
            => $"res://addons/beep_game_builder_cs/generated/mountains/natural_plateau/authored_prefabs/mountain_prefab_2/themes/{themeId}/mountain_prefab_2_manifest.json";

    }
}

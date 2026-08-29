using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Beep.ECS
{
    /// <summary>
    /// Places a small deterministic set of transparent prop stamps above a
    /// generated terrain. Each logical terrain kind has its own optional
    /// palette, so desert cells do not receive grass and swamp cells do not
    /// receive cactus. This component deliberately accepts individual sprites,
    /// not whole sprite sheets.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class SeededTerrainPropScatterComponent : Node2D
    {
        [Export] public NodePath TerrainGeneratorPath { get; set; } = new("");
        [Export] public NodePath CellDataPath { get; set; } = new("");

        [ExportGroup("Area")]
        [Export] public Vector2I SizeInTiles { get; set; } = new(20, 12);
        [Export(PropertyHint.Range, "1,256,1")] public int TileSize { get; set; } = 64;
        [Export] public int Seed { get; set; } = 31415;

        [ExportGroup("Scatter")]
        [Export] public bool GenerateOnReady { get; set; } = false;
        [Export] public bool GenerateInEditor { get; set; } = false;
        [Export(PropertyHint.Range, "0,256,1")] public int MaxProps { get; set; } = 28;
        [Export(PropertyHint.Range, "0,1,0.01")] public float GrassCoverage { get; set; } = 0.045f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float DesertCoverage { get; set; } = 0.055f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float MudCoverage { get; set; } = 0.040f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float RockCoverage { get; set; } = 0.070f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float WaterEdgeCoverage { get; set; } = 0.025f;
        [Export(PropertyHint.Range, "0,3,0.05")] public float MinimumDistanceTiles { get; set; } = 0.85f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float ScatterJitter { get; set; } = 0.70f;
        [Export(PropertyHint.Range, "0.05,2,0.01")] public float MinScale { get; set; } = 0.32f;
        [Export(PropertyHint.Range, "0.05,2,0.01")] public float MaxScale { get; set; } = 0.52f;
        [Export] public int RenderZIndex { get; set; } = -84;

        [ExportGroup("Grassland Props")]
        [Export(PropertyHint.File, "*.png,*.webp")] public string GrassPrimaryPath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string GrassSecondaryPath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string GrassAccentPath { get; set; } = "";

        [ExportGroup("Desert And Sand Props")]
        [Export(PropertyHint.File, "*.png,*.webp")] public string DesertPrimaryPath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string DesertSecondaryPath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string DesertAccentPath { get; set; } = "";

        [ExportGroup("Mud And Swamp Props")]
        [Export(PropertyHint.File, "*.png,*.webp")] public string MudPrimaryPath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string MudSecondaryPath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string MudAccentPath { get; set; } = "";

        [ExportGroup("Rock And Snow Props")]
        [Export(PropertyHint.File, "*.png,*.webp")] public string RockPrimaryPath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string RockSecondaryPath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string SnowAccentPath { get; set; } = "";

        [ExportGroup("Water Edge Props")]
        [Export] public bool AllowShallowWaterProps { get; set; } = false;
        [Export(PropertyHint.File, "*.png,*.webp")] public string WaterEdgePrimaryPath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string WaterEdgeSecondaryPath { get; set; } = "";

        private GridTerrainGeneratorComponent? _generator;
        private GridCellDataComponent? _cells;

        public override void _Ready()
        {
            ResolveSources();
            if (!GenerateOnReady || (Engine.IsEditorHint() && !GenerateInEditor))
                return;

            CallDeferred(nameof(Rebuild));
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (TerrainGeneratorPath.IsEmpty && CellDataPath.IsEmpty)
                return new[] { "Assign TerrainGeneratorPath or CellDataPath." };
            if (SizeInTiles.X <= 0 || SizeInTiles.Y <= 0)
                return new[] { "SizeInTiles must be greater than zero." };
            if (!HasAnyConfiguredPath())
                return new[] { "Assign at least one individual transparent prop sprite." };
            return Array.Empty<string>();
        }

        public void Rebuild()
        {
            ResolveSources();
            RemoveGeneratedStamps();
            if (_generator is null && _cells is null)
                return;

            Dictionary<string, List<Texture2D>> palettes = LoadPalettes();
            if (palettes.Count == 0)
                return;

            int targetCount = Mathf.Clamp(MaxProps, 0, 256);
            int placed = 0;
            Vector2I area = new(Mathf.Max(1, SizeInTiles.X), Mathf.Max(1, SizeInTiles.Y));
            float tile = Mathf.Max(1, TileSize);
            float jitter = Mathf.Clamp(ScatterJitter, 0.0f, 1.0f);
            float minDistanceSquared = Mathf.Max(0.0f, MinimumDistanceTiles);
            minDistanceSquared *= minDistanceSquared;
            var placedTiles = new List<Vector2>();

            for (int y = 0; y < area.Y && placed < targetCount; y++)
            {
                for (int x = 0; x < area.X && placed < targetCount; x++)
                {
                    Vector2 cellCenter = new(x + 0.5f, y + 0.5f);
                    string paletteKey = PaletteAt(cellCenter);
                    if (string.IsNullOrEmpty(paletteKey) || !palettes.TryGetValue(paletteKey, out List<Texture2D>? textures) || textures.Count == 0)
                        continue;

                    float coverage = CoverageFor(paletteKey);
                    if (coverage <= 0.0f || Hash01(x, y, Seed + 7103) > coverage)
                        continue;

                    Vector2 offset = new(
                        (Hash01(x, y, Seed + 7207) - 0.5f) * jitter,
                        (Hash01(x, y, Seed + 7309) - 0.5f) * jitter);
                    Vector2 tilePosition = cellCenter + offset;
                    if (!FootprintMatchesPalette(tilePosition, paletteKey))
                        continue;

                    if (!IsFarEnough(tilePosition, placedTiles, minDistanceSquared))
                        continue;

                    var stamp = new Sprite2D
                    {
                        Name = $"GeneratedTerrainStamp_{placed:000}",
                        Texture = textures[Mathf.FloorToInt(Hash01(x, y, Seed + 7411) * textures.Count) % textures.Count],
                        Centered = true,
                        Position = tilePosition * tile,
                        Scale = Vector2.One * Mathf.Lerp(Mathf.Min(MinScale, MaxScale), Mathf.Max(MinScale, MaxScale), Hash01(x, y, Seed + 7523)),
                        Rotation = Mathf.Lerp(-0.10f, 0.10f, Hash01(x, y, Seed + 7639)),
                        ZIndex = RenderZIndex,
                        TextureFilter = TextureFilterEnum.Linear,
                    };
                    AddChild(stamp);
                    placedTiles.Add(tilePosition);
                    placed++;
                }
            }
        }

        private void ResolveSources()
        {
            if (_generator == null || !GodotObject.IsInstanceValid(_generator))
                _generator = !TerrainGeneratorPath.IsEmpty
                    ? GetNodeOrNull<GridTerrainGeneratorComponent>(TerrainGeneratorPath)
                    : null;

            if (_cells == null || !GodotObject.IsInstanceValid(_cells))
                _cells = !CellDataPath.IsEmpty
                    ? GetNodeOrNull<GridCellDataComponent>(CellDataPath)
                    : null;
        }

        private string CellTerrainKindAt(Vector2 tilePosition)
            => _cells?.GetTerrainKind(new Vector2I(Mathf.FloorToInt(tilePosition.X), Mathf.FloorToInt(tilePosition.Y)))
                ?? string.Empty;

        private string GeneratorTerrainKindAt(Vector2 tilePosition)
            => _generator?.TerrainKindAtPosition(tilePosition) ?? string.Empty;

        private Dictionary<string, List<Texture2D>> LoadPalettes()
        {
            var palettes = new Dictionary<string, List<Texture2D>>(StringComparer.Ordinal);
            AddPalette(palettes, "grass", GrassPrimaryPath, GrassSecondaryPath, GrassAccentPath);
            AddPalette(palettes, "desert", DesertPrimaryPath, DesertSecondaryPath, DesertAccentPath);
            AddPalette(palettes, "mud", MudPrimaryPath, MudSecondaryPath, MudAccentPath);
            AddPalette(palettes, "rock", RockPrimaryPath, RockSecondaryPath, SnowAccentPath);
            AddPalette(palettes, "water", WaterEdgePrimaryPath, WaterEdgeSecondaryPath);

            return palettes;
        }

        private static void AddPalette(Dictionary<string, List<Texture2D>> palettes, string key, params string[] paths)
        {
            var textures = new List<Texture2D>();
            foreach (string path in paths)
                AddTexture(path, textures);

            if (textures.Count > 0)
                palettes[key] = textures;
        }

        private static void AddTexture(string path, ICollection<Texture2D> textures)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            Image image = Image.LoadFromFile(path);
            if (image.IsEmpty())
            {
                GD.PushWarning($"Seeded terrain prop scatter could not load '{path}'.");
                return;
            }

            textures.Add(ImageTexture.CreateFromImage(image));
        }

        private void RemoveGeneratedStamps()
        {
            foreach (Node child in GetChildren())
            {
                if (child.Name.ToString().StartsWith("GeneratedTerrainStamp_", StringComparison.Ordinal))
                    child.QueueFree();
            }
        }

        private static string Normalize(string value)
            => string.IsNullOrWhiteSpace(value) ? "grass" : value.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');

        private float CoverageFor(string paletteKey) => paletteKey switch
        {
            "grass" => Mathf.Clamp(GrassCoverage, 0.0f, 1.0f),
            "desert" => Mathf.Clamp(DesertCoverage, 0.0f, 1.0f),
            "mud" => Mathf.Clamp(MudCoverage, 0.0f, 1.0f),
            "rock" => Mathf.Clamp(RockCoverage, 0.0f, 1.0f),
            "water" => Mathf.Clamp(WaterEdgeCoverage, 0.0f, 1.0f),
            _ => 0.0f,
        };

        private bool FootprintMatchesPalette(Vector2 tilePosition, string paletteKey)
        {
            Vector2 containingCellCenter = new(
                Mathf.Floor(tilePosition.X) + 0.5f,
                Mathf.Floor(tilePosition.Y) + 0.5f);

            return PaletteAt(tilePosition) == paletteKey
                && PaletteAt(containingCellCenter) == paletteKey
                && PaletteAt(tilePosition + new Vector2(0.25f, 0.0f)) == paletteKey
                && PaletteAt(tilePosition + new Vector2(-0.25f, 0.0f)) == paletteKey
                && PaletteAt(tilePosition + new Vector2(0.0f, 0.25f)) == paletteKey
                && PaletteAt(tilePosition + new Vector2(0.0f, -0.25f)) == paletteKey;
        }

        private string PaletteAt(Vector2 tilePosition)
        {
            // Authoritative water test, ahead of any terrain-kind mapping: a
            // prop must never end up standing in the sea or in a lake.
            if (_generator is not null && _generator.IsWaterAtPosition(tilePosition) && !AllowShallowWaterProps)
                return string.Empty;

            string cellKey = _cells is null ? string.Empty : PaletteKeyFor(Normalize(CellTerrainKindAt(tilePosition)));
            string generatorKey = _generator is null ? string.Empty : PaletteKeyFor(Normalize(GeneratorTerrainKindAt(tilePosition)));

            if (_cells is not null && string.IsNullOrEmpty(cellKey))
                return string.Empty;
            if (_generator is not null && string.IsNullOrEmpty(generatorKey))
                return string.Empty;
            if (_cells is not null && _generator is not null && cellKey != generatorKey)
                return string.Empty;

            return _cells is not null ? cellKey : generatorKey;
        }

        private static bool IsFarEnough(Vector2 tilePosition, IEnumerable<Vector2> placedTiles, float minDistanceSquared)
        {
            if (minDistanceSquared <= 0.0f)
                return true;

            foreach (Vector2 placed in placedTiles)
            {
                if (tilePosition.DistanceSquaredTo(placed) < minDistanceSquared)
                    return false;
            }

            return true;
        }

        private static float Hash01(int x, int y, int seed)
        {
            uint value = (uint)(x * 374761393) + (uint)(y * 668265263) + (uint)seed;
            value = (value ^ (value >> 13)) * 1274126177u;
            value ^= value >> 16;
            return (value & 0x00ffffffu) / 16777215.0f;
        }

        private bool HasAnyConfiguredPath()
            => new[]
            {
                GrassPrimaryPath, GrassSecondaryPath, GrassAccentPath,
                DesertPrimaryPath, DesertSecondaryPath, DesertAccentPath,
                MudPrimaryPath, MudSecondaryPath, MudAccentPath,
                RockPrimaryPath, RockSecondaryPath, SnowAccentPath,
                WaterEdgePrimaryPath, WaterEdgeSecondaryPath,
            }.Any(path => !string.IsNullOrWhiteSpace(path));

        // Water kinds deliberately fall through to the empty key, which is what
        // keeps plants, rocks and bushes out of the sea and out of lakes.
        private string PaletteKeyFor(string terrainKind) => terrainKind switch
        {
            "grass" or "grassland" or "dry_grass" or "plains" or "jungle" => "grass",
            "sand" or "desert" or "beach" => "desert",
            "mud" or "swamp" or "dirt" or "soil" => "mud",
            "rock" or "stone" or "gravel" or "snow" or "ice" or "tundra" => "rock",
            "shallow_water" when AllowShallowWaterProps => "water",
            _ => string.Empty,
        };
    }
}

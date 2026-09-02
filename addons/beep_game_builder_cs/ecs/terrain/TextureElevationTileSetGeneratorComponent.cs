using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Beep.ECS
{
    /// <summary>
    /// Generates a reference-style elevated 2D terrain atlas from one top
    /// texture and an optional cliff texture. The generated atlas is not a
    /// flat autotile mask: each tile region reserves space for a walkable top
    /// surface plus cliffs, stacked walls, ramps, stairs, and shadows.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TextureElevationTileSetGeneratorComponent : Node
    {
        public enum ElevationScenario
        {
            Hill,
            Mountain,
            Dune,
            Canyon,
            Snow,
            Volcano,
            Swamp
        }

        private enum TileRole
        {
            Top,
            Cliff,
            Stack,
            Ramp,
            Stair,
            Shadow
        }

        private readonly record struct TileSpec(
            string Id,
            TileRole Role,
            int Column,
            int Row,
            bool Walkable,
            bool Climbable,
            string Direction);

        [Signal] public delegate void AtlasGeneratedEventHandler(string atlasPath);

        [Export(PropertyHint.File, "*.png,*.webp,*.jpg,*.jpeg")]
        public string TopTexturePath { get; set; } = "";

        [Export(PropertyHint.File, "*.png,*.webp,*.jpg,*.jpeg")]
        public string CliffTexturePath { get; set; } = "";

        [Export(PropertyHint.File, "*.png,*.webp,*.jpg,*.jpeg")]
        public string CliffColumnTexturePath { get; set; } = "";

        [Export(PropertyHint.File, "*.png,*.webp,*.jpg,*.jpeg")]
        public string SideCliffTexturePath { get; set; } = "";

        [Export(PropertyHint.SaveFile, "*.png")]
        public string OutputAtlasPath { get; set; } = "res://addons/beep_game_builder_cs/generated/terrain/elevation_tileset.png";

        [Export(PropertyHint.SaveFile, "*.tres")]
        public string OutputTileSetPath { get; set; } = "res://addons/beep_game_builder_cs/generated/terrain/elevation_tileset.tres";

        [Export(PropertyHint.SaveFile, "*.json")]
        public string OutputManifestPath { get; set; } = "res://addons/beep_game_builder_cs/generated/terrain/elevation_tileset_manifest.json";

        [ExportGroup("Generation")]
        [Export] public ElevationScenario Scenario { get; set; } = ElevationScenario.Mountain;
        [Export(PropertyHint.Range, "16,256,1")] public int TileWidth { get; set; } = 64;
        [Export(PropertyHint.Range, "16,256,1")] public int TopHeight { get; set; } = 48;
        [Export(PropertyHint.Range, "8,256,1")] public int CliffHeight { get; set; } = 48;
        [Export] public bool SaveTileSetResource { get; set; } = true;
        [Export] public bool SaveManifest { get; set; } = true;
        [Export] public bool GenerateOnReady { get; set; } = false;
        [Export] public bool GenerateInEditor { get; set; } = false;
        [Export] public bool UseDirectTextureSampling { get; set; } = true;
        [Export] public bool PreserveCliffSourceLayout { get; set; } = true;
        [Export] public bool UseForestReferenceSheetLayout { get; set; } = false;
        [Export] public bool PreserveReferenceSheetOutputLayout { get; set; } = false;
        [Export(PropertyHint.Range, "0.25,8,0.05")] public float TextureRepeatsPerTile { get; set; } = 1.0f;

        [ExportGroup("Source Rects")]
        [Export] public Vector2I TopSourceOrigin { get; set; } = Vector2I.Zero;
        [Export] public Vector2I TopSourceSize { get; set; } = Vector2I.Zero;
        [Export] public Vector2I CliffColumnSourceOrigin { get; set; } = Vector2I.Zero;
        [Export] public Vector2I CliffColumnSourceSize { get; set; } = Vector2I.Zero;
        [Export] public Vector2I SideCliffSourceOrigin { get; set; } = Vector2I.Zero;
        [Export] public Vector2I SideCliffSourceSize { get; set; } = Vector2I.Zero;

        [ExportGroup("Look")]
        [Export(PropertyHint.Range, "0,0.4,0.01")] public float EdgeRoundness { get; set; } = 0.18f;
        [Export(PropertyHint.Range, "0,0.35,0.005")] public float OrganicEdgeAmount { get; set; } = 0.055f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float CliffShadeStrength { get; set; } = 0.30f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float TopHighlightStrength { get; set; } = 0.10f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float OutlineStrength { get; set; } = 0.18f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float DetailDensity { get; set; } = 0.45f;
        [Export] public int Seed { get; set; } = 27431;

        private const int AtlasColumns = 8;
        private const int AtlasRows = 4;

        private static readonly TileSpec[] TileSpecs =
        {
            new("top_full", TileRole.Top, 0, 0, true, false, ""),
            new("top_horizontal", TileRole.Top, 1, 0, true, false, "east_west"),
            new("top_vertical", TileRole.Top, 2, 0, true, false, "north_south"),
            new("top_small", TileRole.Top, 3, 0, true, false, ""),
            new("top_cap_north", TileRole.Top, 4, 0, true, false, "north"),
            new("top_cap_east", TileRole.Top, 5, 0, true, false, "east"),
            new("top_cap_south", TileRole.Top, 6, 0, true, false, "south"),
            new("top_cap_west", TileRole.Top, 7, 0, true, false, "west"),

            new("cliff_front_full", TileRole.Cliff, 0, 1, false, false, "south"),
            new("cliff_front_left_edge", TileRole.Cliff, 1, 1, false, false, "south_west"),
            new("cliff_front_right_edge", TileRole.Cliff, 2, 1, false, false, "south_east"),
            new("cliff_front_column", TileRole.Cliff, 3, 1, false, false, "south"),
            new("cliff_outer_left", TileRole.Cliff, 4, 1, false, false, "west"),
            new("cliff_outer_right", TileRole.Cliff, 5, 1, false, false, "east"),
            new("cliff_inner_left", TileRole.Cliff, 6, 1, false, false, "inner_west"),
            new("cliff_inner_right", TileRole.Cliff, 7, 1, false, false, "inner_east"),

            new("stack_front_full", TileRole.Stack, 0, 2, false, false, "south"),
            new("stack_front_left_edge", TileRole.Stack, 1, 2, false, false, "south_west"),
            new("stack_front_right_edge", TileRole.Stack, 2, 2, false, false, "south_east"),
            new("stack_front_column", TileRole.Stack, 3, 2, false, false, "south"),
            new("side_left_wall", TileRole.Cliff, 4, 2, false, false, "west"),
            new("side_right_wall", TileRole.Cliff, 5, 2, false, false, "east"),
            new("side_left_stack", TileRole.Stack, 6, 2, false, false, "west"),
            new("side_right_stack", TileRole.Stack, 7, 2, false, false, "east"),

            new("ramp_north", TileRole.Ramp, 0, 3, true, true, "north"),
            new("ramp_south", TileRole.Ramp, 1, 3, true, true, "south"),
            new("ramp_west", TileRole.Ramp, 2, 3, true, true, "west"),
            new("ramp_east", TileRole.Ramp, 3, 3, true, true, "east"),
            new("stair_south", TileRole.Stair, 4, 3, true, true, "south"),
            new("stair_north", TileRole.Stair, 5, 3, true, true, "north"),
            new("path_cut", TileRole.Ramp, 6, 3, true, true, ""),
            new("cliff_shadow", TileRole.Shadow, 7, 3, false, false, "")
        };

        public override void _Ready()
        {
            if (!GenerateOnReady)
                return;

            if (Engine.IsEditorHint() && !GenerateInEditor)
                return;

            CallDeferred(nameof(GenerateElevationTileSet));
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (string.IsNullOrWhiteSpace(TopTexturePath))
                return new[] { "TopTexturePath must point to the required top terrain texture." };
            if (string.IsNullOrWhiteSpace(OutputAtlasPath))
                return new[] { "OutputAtlasPath must be a PNG path." };
            if (TileWidth <= 0 || TopHeight <= 0 || CliffHeight <= 0)
                return new[] { "TileWidth, TopHeight, and CliffHeight must be greater than zero." };
            return Array.Empty<string>();
        }

        public string GenerateElevationTileSet()
        {
            Image? top = LoadImage(TopTexturePath);
            if (top is null || top.IsEmpty())
            {
                GD.PushWarning($"[{Name}] Could not load top terrain texture '{TopTexturePath}'.");
                return "";
            }

            Image? cliff = string.IsNullOrWhiteSpace(CliffTexturePath) ? null : LoadImage(CliffTexturePath);
            Image? cliffColumn = string.IsNullOrWhiteSpace(CliffColumnTexturePath) ? cliff : LoadImage(CliffColumnTexturePath);
            Image? sideCliff = string.IsNullOrWhiteSpace(SideCliffTexturePath) ? cliffColumn : LoadImage(SideCliffTexturePath);
            if (UseForestReferenceSheetLayout && PreserveReferenceSheetOutputLayout)
                return SaveReferenceSheetLayout(top);

            int tileWidth = Mathf.Max(16, TileWidth);
            int topHeight = Mathf.Max(16, TopHeight);
            int cliffHeight = Mathf.Max(8, CliffHeight);
            int regionHeight = topHeight + cliffHeight;

            top.Convert(Image.Format.Rgba8);
            cliff?.Convert(Image.Format.Rgba8);
            cliffColumn?.Convert(Image.Format.Rgba8);
            sideCliff?.Convert(Image.Format.Rgba8);

            var atlas = Image.CreateEmpty(AtlasColumns * tileWidth, AtlasRows * regionHeight, false, Image.Format.Rgba8);
            atlas.Fill(Colors.Transparent);

            foreach (TileSpec spec in TileSpecs)
                PaintTile(atlas, top, cliffColumn, sideCliff, spec, tileWidth, topHeight, cliffHeight);

            Error imageError = SavePng(atlas, OutputAtlasPath);
            if (imageError != Error.Ok)
            {
                GD.PushWarning($"[{Name}] Could not save elevation atlas '{OutputAtlasPath}': {imageError}.");
                return "";
            }

            if (SaveTileSetResource && !string.IsNullOrWhiteSpace(OutputTileSetPath))
                SaveTileSet(atlas, tileWidth, regionHeight);

            if (SaveManifest && !string.IsNullOrWhiteSpace(OutputManifestPath))
                SaveManifestJson(tileWidth, topHeight, cliffHeight);

            if (Engine.IsEditorHint())
                EditorInterface.Singleton?.GetResourceFilesystem()?.Scan();

            EmitSignal(SignalName.AtlasGenerated, OutputAtlasPath);
            GD.Print($"[{Name}] Generated elevation tileset atlas: {OutputAtlasPath}");
            return OutputAtlasPath;
        }

        public Godot.Collections.Array<Godot.Collections.Dictionary> GetTileManifest()
        {
            var result = new Godot.Collections.Array<Godot.Collections.Dictionary>();
            foreach (TileSpec spec in TileSpecs)
            {
                result.Add(new Godot.Collections.Dictionary
                {
                    ["id"] = spec.Id,
                    ["role"] = spec.Role.ToString().ToLowerInvariant(),
                    ["atlas"] = new Vector2I(spec.Column, spec.Row),
                    ["walkable"] = spec.Walkable,
                    ["climbable"] = spec.Climbable,
                    ["direction"] = spec.Direction
                });
            }

            return result;
        }

        private string SaveReferenceSheetLayout(Image source)
        {
            source.Convert(Image.Format.Rgba8);
            Error imageError = SavePng(source, OutputAtlasPath);
            if (imageError != Error.Ok)
            {
                GD.PushWarning($"[{Name}] Could not save reference-layout elevation atlas '{OutputAtlasPath}': {imageError}.");
                return "";
            }

            if (SaveManifest && !string.IsNullOrWhiteSpace(OutputManifestPath))
                SaveReferenceManifestJson(source.GetWidth(), source.GetHeight());

            if (Engine.IsEditorHint())
                EditorInterface.Singleton?.GetResourceFilesystem()?.Scan();

            EmitSignal(SignalName.AtlasGenerated, OutputAtlasPath);
            GD.Print($"[{Name}] Generated reference-layout elevation tileset atlas: {OutputAtlasPath}");
            return OutputAtlasPath;
        }

        private void PaintTile(Image atlas, Image top, Image? cliffColumn, Image? sideCliff, TileSpec spec, int tileWidth, int topHeight, int cliffHeight)
        {
            int ox = spec.Column * tileWidth;
            int oy = spec.Row * (topHeight + cliffHeight);
            if (UseForestReferenceSheetLayout && PaintForestReferenceTile(atlas, top, spec, ox, oy, tileWidth, topHeight, cliffHeight))
                return;

            switch (spec.Role)
            {
                case TileRole.Top:
                    PaintTopOnly(atlas, top, spec, ox, oy, tileWidth, topHeight);
                    break;
                case TileRole.Cliff:
                    PaintCliff(atlas, top, cliffColumn, sideCliff, spec, ox, oy, tileWidth, topHeight, cliffHeight, stacked: false);
                    break;
                case TileRole.Stack:
                    PaintCliff(atlas, top, cliffColumn, sideCliff, spec, ox, oy, tileWidth, topHeight, cliffHeight, stacked: true);
                    break;
                case TileRole.Ramp:
                    PaintRamp(atlas, top, cliffColumn, sideCliff, spec, ox, oy, tileWidth, topHeight, cliffHeight);
                    break;
                case TileRole.Stair:
                    PaintStair(atlas, top, cliffColumn, sideCliff, spec, ox, oy, tileWidth, topHeight, cliffHeight);
                    break;
                case TileRole.Shadow:
                    PaintShadow(atlas, ox, oy, tileWidth, topHeight, cliffHeight);
                    break;
            }
        }

        private void PaintTopOnly(Image atlas, Image top, TileSpec spec, int ox, int oy, int tileWidth, int topHeight)
        {
            for (int y = 0; y < topHeight; y++)
            {
                for (int x = 0; x < tileWidth; x++)
                {
                    float u = (x + 0.5f) / tileWidth;
                    float v = (y + 0.5f) / topHeight;
                    float alpha = TopMask(spec.Id, u, v);
                    if (alpha <= 0.01f)
                        continue;

                    Color colour = TopPixel(top, u, v, spec.Column + spec.Row * 17);
                    colour = AddTopDetails(colour, u, v, alpha);
                    atlas.SetPixel(ox + x, oy + y, WithAlpha(colour, alpha));
                }
            }

            DrawOrganicOutline(atlas, ox, oy, tileWidth, topHeight, spec.Id);
        }

        private void PaintCliff(Image atlas, Image top, Image? cliffColumn, Image? sideCliff, TileSpec spec, int ox, int oy, int tileWidth, int topHeight, int cliffHeight, bool stacked)
        {
            int topLipHeight = Mathf.Clamp(topHeight / 3, 10, topHeight);
            for (int y = 0; y < topLipHeight; y++)
            {
                for (int x = 0; x < tileWidth; x++)
                {
                    float u = (x + 0.5f) / tileWidth;
                    float v = (y + 0.5f) / topHeight;
                    float alpha = TopMask(TopMaskIdForCliff(spec.Id), u, v);
                    if (alpha <= 0.01f)
                        continue;

                    Color colour = AddTopDetails(TopPixel(top, u, v, spec.Column + 41), u, v, alpha);
                    atlas.SetPixel(ox + x, oy + y, WithAlpha(colour, alpha));
                }
            }

            int wallStart = Mathf.Max(6, topLipHeight - 5);
            for (int y = wallStart; y < topHeight + cliffHeight; y++)
            {
                for (int x = 0; x < tileWidth; x++)
                {
                    float u = (x + 0.5f) / tileWidth;
                    float wallV = (y - wallStart + 0.5f) / Mathf.Max(1, topHeight + cliffHeight - wallStart);
                    float alpha = CliffMask(spec.Id, u, wallV);
                    if (alpha <= 0.01f)
                        continue;

                    Image? cliffSource = UsesSideCliff(spec.Id) ? sideCliff : cliffColumn;
                    Vector2I rectOrigin = UsesSideCliff(spec.Id) ? SideCliffSourceOrigin : CliffColumnSourceOrigin;
                    Vector2I rectSize = UsesSideCliff(spec.Id) ? SideCliffSourceSize : CliffColumnSourceSize;
                    Color colour = CliffPixel(top, cliffSource, rectOrigin, rectSize, u, wallV, spec.Column + spec.Row * 31);
                    float sourceAlpha = Mathf.Clamp(colour.A, 0.0f, 1.0f);
                    if (sourceAlpha <= 0.01f)
                        continue;

                    colour = PreserveCliffSourceLayout && cliffSource is not null
                        ? AdjustBrightness(colour, stacked && wallV > 0.48f ? -0.08f : 0.0f)
                        : ApplyCliffShading(colour, u, wallV, stacked);
                    atlas.SetPixel(ox + x, oy + y, WithAlpha(colour, alpha * sourceAlpha));
                }
            }

            DrawCliffCracks(atlas, ox, oy + wallStart, tileWidth, topHeight + cliffHeight - wallStart, spec.Column + spec.Row * 13, stacked);
        }

        private void PaintRamp(Image atlas, Image top, Image? cliffColumn, Image? sideCliff, TileSpec spec, int ox, int oy, int tileWidth, int topHeight, int cliffHeight)
        {
            PaintCliff(atlas, top, cliffColumn, sideCliff, new TileSpec(spec.Id, TileRole.Cliff, spec.Column, spec.Row, spec.Walkable, spec.Climbable, spec.Direction), ox, oy, tileWidth, topHeight, cliffHeight, stacked: false);

            int regionHeight = topHeight + cliffHeight;
            for (int y = 0; y < regionHeight; y++)
            {
                for (int x = 0; x < tileWidth; x++)
                {
                    float u = (x + 0.5f) / tileWidth;
                    float v = (y + 0.5f) / regionHeight;
                    float ramp = RampMask(spec.Direction, u, v);
                    if (ramp <= 0.01f)
                        continue;

                    Color topColour = TopPixel(top, u, Mathf.Clamp(v, 0.0f, 1.0f), spec.Column + 300);
                    Color dirt = ScenarioRampColour(topColour);
                    Color existing = atlas.GetPixel(ox + x, oy + y);
                    atlas.SetPixel(ox + x, oy + y, AlphaOver(existing, WithAlpha(dirt, ramp * 0.96f)));
                }
            }
        }

        private void PaintStair(Image atlas, Image top, Image? cliffColumn, Image? sideCliff, TileSpec spec, int ox, int oy, int tileWidth, int topHeight, int cliffHeight)
        {
            PaintRamp(atlas, top, cliffColumn, sideCliff, spec, ox, oy, tileWidth, topHeight, cliffHeight);
            int regionHeight = topHeight + cliffHeight;
            int steps = 5;
            for (int i = 0; i < steps; i++)
            {
                float t = (i + 1) / (float)(steps + 1);
                int y = spec.Direction == "north"
                    ? Mathf.RoundToInt(Mathf.Lerp(regionHeight - 12, topHeight / 2, t))
                    : Mathf.RoundToInt(Mathf.Lerp(topHeight / 2, regionHeight - 12, t));
                DrawLine(atlas, ox + tileWidth / 4, oy + y, ox + (tileWidth * 3) / 4, oy + y + 1, new Color(0.12f, 0.13f, 0.12f, 0.46f));
            }
        }

        private void PaintShadow(Image atlas, int ox, int oy, int tileWidth, int topHeight, int cliffHeight)
        {
            int regionHeight = topHeight + cliffHeight;
            for (int y = 0; y < regionHeight; y++)
            {
                for (int x = 0; x < tileWidth; x++)
                {
                    float u = (x + 0.5f) / tileWidth;
                    float v = (y + 0.5f) / regionHeight;
                    float oval = 1.0f - Mathf.Pow((u - 0.5f) / 0.48f, 2.0f) - Mathf.Pow((v - 0.72f) / 0.22f, 2.0f);
                    if (oval <= 0.0f)
                        continue;

                    atlas.SetPixel(ox + x, oy + y, new Color(0.0f, 0.0f, 0.0f, Mathf.Clamp(oval * 0.34f, 0.0f, 0.34f)));
                }
            }
        }

        private bool PaintForestReferenceTile(Image atlas, Image sheet, TileSpec spec, int ox, int oy, int tileWidth, int topHeight, int cliffHeight)
        {
            Rect2I? source = ForestReferenceSourceFor(spec.Id);
            if (source is null)
                return false;

            int regionHeight = topHeight + cliffHeight;
            Rect2I target = spec.Role == TileRole.Top
                ? ForestTopTargetFor(spec.Id, ox, oy, tileWidth, topHeight)
                : new Rect2I(ox, oy, tileWidth, regionHeight);

            BlitScaled(sheet, source.Value, atlas, target);
            return true;
        }

        private static Rect2I ForestTopTargetFor(string id, int ox, int oy, int tileWidth, int topHeight)
        {
            if (id == "top_vertical")
            {
                int width = Mathf.Max(1, tileWidth / 2);
                return new Rect2I(ox + (tileWidth - width) / 2, oy, width, topHeight);
            }

            if (id == "top_horizontal")
                return new Rect2I(ox, oy + topHeight / 4, tileWidth, topHeight / 2);

            if (id == "top_small")
            {
                int width = Mathf.Max(1, tileWidth / 2);
                int height = Mathf.Max(1, topHeight / 2);
                return new Rect2I(ox + (tileWidth - width) / 2, oy + (topHeight - height) / 2, width, height);
            }

            return new Rect2I(ox, oy, tileWidth, topHeight);
        }

        private static Rect2I? ForestReferenceSourceFor(string id)
            => id switch
            {
                "top_full" => new Rect2I(320, 0, 128, 128),
                "top_horizontal" => new Rect2I(320, 128, 128, 64),
                "top_vertical" => new Rect2I(448, 0, 64, 128),
                "top_small" => new Rect2I(512, 128, 64, 64),
                "top_cap_north" => new Rect2I(320, 0, 128, 128),
                "top_cap_east" => new Rect2I(448, 0, 64, 128),
                "top_cap_south" => new Rect2I(320, 128, 128, 64),
                "top_cap_west" => new Rect2I(512, 0, 64, 128),

                "cliff_front_full" => new Rect2I(320, 128, 128, 256),
                "cliff_front_left_edge" => new Rect2I(320, 128, 128, 256),
                "cliff_front_right_edge" => new Rect2I(320, 128, 128, 256),
                "cliff_front_column" => new Rect2I(512, 128, 64, 256),
                "cliff_outer_left" => new Rect2I(0, 256, 128, 128),
                "cliff_outer_right" => new Rect2I(192, 256, 64, 128),
                "cliff_inner_left" => new Rect2I(0, 256, 128, 128),
                "cliff_inner_right" => new Rect2I(192, 256, 64, 128),

                "stack_front_full" => new Rect2I(320, 128, 128, 256),
                "stack_front_left_edge" => new Rect2I(320, 128, 128, 256),
                "stack_front_right_edge" => new Rect2I(320, 128, 128, 256),
                "stack_front_column" => new Rect2I(512, 128, 64, 256),
                "side_left_wall" => new Rect2I(0, 256, 128, 128),
                "side_right_wall" => new Rect2I(192, 256, 64, 128),
                "side_left_stack" => new Rect2I(0, 256, 128, 128),
                "side_right_stack" => new Rect2I(192, 256, 64, 128),

                "ramp_north" => new Rect2I(0, 256, 128, 128),
                "ramp_south" => new Rect2I(0, 256, 128, 128),
                "ramp_west" => new Rect2I(0, 256, 128, 128),
                "ramp_east" => new Rect2I(192, 256, 64, 128),
                "stair_south" => new Rect2I(320, 128, 128, 256),
                "stair_north" => new Rect2I(320, 128, 128, 256),
                "path_cut" => new Rect2I(0, 256, 128, 128),
                _ => null
            };

        private static void BlitScaled(Image source, Rect2I sourceRect, Image target, Rect2I targetRect)
        {
            Rect2I safeSource = ClampSourceRect(source, sourceRect);
            if (targetRect.Size.X <= 0 || targetRect.Size.Y <= 0 || safeSource.Size.X <= 0 || safeSource.Size.Y <= 0)
                return;

            for (int y = 0; y < targetRect.Size.Y; y++)
            {
                float v = targetRect.Size.Y <= 1 ? 0.0f : y / (float)(targetRect.Size.Y - 1);
                int sy = safeSource.Position.Y + Mathf.Clamp(Mathf.RoundToInt(v * (safeSource.Size.Y - 1)), 0, safeSource.Size.Y - 1);
                for (int x = 0; x < targetRect.Size.X; x++)
                {
                    float u = targetRect.Size.X <= 1 ? 0.0f : x / (float)(targetRect.Size.X - 1);
                    int sx = safeSource.Position.X + Mathf.Clamp(Mathf.RoundToInt(u * (safeSource.Size.X - 1)), 0, safeSource.Size.X - 1);
                    Color colour = source.GetPixel(sx, sy);
                    if (colour.A <= 0.01f)
                        continue;

                    int tx = targetRect.Position.X + x;
                    int ty = targetRect.Position.Y + y;
                    if (tx < 0 || ty < 0 || tx >= target.GetWidth() || ty >= target.GetHeight())
                        continue;

                    target.SetPixel(tx, ty, colour);
                }
            }
        }

        private static Rect2I ClampSourceRect(Image source, Rect2I rect)
        {
            int x = Mathf.Clamp(rect.Position.X, 0, Mathf.Max(0, source.GetWidth() - 1));
            int y = Mathf.Clamp(rect.Position.Y, 0, Mathf.Max(0, source.GetHeight() - 1));
            int w = Mathf.Clamp(rect.Size.X, 1, source.GetWidth() - x);
            int h = Mathf.Clamp(rect.Size.Y, 1, source.GetHeight() - y);
            return new Rect2I(x, y, w, h);
        }

        private float TopMask(string id, float u, float v)
        {
            (float minX, float maxX, float minY, float maxY) = id switch
            {
                "top_horizontal" => (0.02f, 0.98f, 0.31f, 0.69f),
                "top_vertical" => (0.31f, 0.69f, 0.02f, 0.98f),
                "top_small" => (0.26f, 0.74f, 0.26f, 0.74f),
                "top_cap_north" => (0.10f, 0.90f, 0.02f, 0.63f),
                "top_cap_east" => (0.37f, 0.98f, 0.10f, 0.90f),
                "top_cap_south" => (0.10f, 0.90f, 0.37f, 0.98f),
                "top_cap_west" => (0.02f, 0.63f, 0.10f, 0.90f),
                _ => (0.03f, 0.97f, 0.03f, 0.97f)
            };

            float rounded = RoundedRectMask(u, v, minX, maxX, minY, maxY, Mathf.Clamp(EdgeRoundness, 0.0f, 0.4f));
            float wobble = (SmoothValueNoise(u * 10.0f, v * 10.0f, Seed + id.GetHashCode()) - 0.5f) * OrganicEdgeAmount;
            return SmoothStep(0.43f, 0.58f, rounded + wobble);
        }

        private static string TopMaskIdForCliff(string id)
        {
            if (id.Contains("column", StringComparison.Ordinal))
                return "top_vertical";
            if (id.Contains("left", StringComparison.Ordinal))
                return "top_cap_west";
            if (id.Contains("right", StringComparison.Ordinal))
                return "top_cap_east";
            return "top_full";
        }

        private float CliffMask(string id, float u, float v)
        {
            (float minX, float maxX) = id switch
            {
                _ when id.Contains("left", StringComparison.Ordinal) => (0.02f, 0.70f),
                _ when id.Contains("right", StringComparison.Ordinal) => (0.30f, 0.98f),
                _ when id.Contains("column", StringComparison.Ordinal) => (0.28f, 0.72f),
                _ => (0.04f, 0.96f)
            };

            float side = SmoothStep(minX, minX + 0.045f, u) * (1.0f - SmoothStep(maxX - 0.045f, maxX, u));
            float bottom = 1.0f - SmoothStep(0.92f, 1.0f, v);
            return Mathf.Clamp(side * bottom, 0.0f, 1.0f);
        }

        private float RampMask(string direction, float u, float v)
        {
            float center = direction switch
            {
                "west" => u,
                "east" => 1.0f - u,
                "north" => v,
                "south" => 1.0f - v,
                _ => 1.0f - Mathf.Abs(u - 0.5f) * 1.6f
            };
            float width = 0.46f - (Mathf.Abs(center - 0.5f) * 0.34f);
            float lateral = direction is "north" or "south"
                ? Mathf.Abs(u - 0.5f)
                : Mathf.Abs(v - 0.55f);
            return SmoothStep(width + 0.08f, width - 0.02f, lateral);
        }

        private Color TopPixel(Image top, float u, float v, int salt)
        {
            Color colour = UseDirectTextureSampling
                ? SampleDirectTexture(top, TopSourceOrigin, TopSourceSize, u, v, salt, preferTopSurface: true)
                : SampleTopMaterial(top, u, v, salt);
            float grain = SmoothValueNoise(u * 18.0f, v * 18.0f, Seed + salt) - 0.5f;
            colour = AdjustBrightness(colour, (grain * 0.07f) + TopHighlightStrength * (1.0f - v) * 0.55f);
            return WithAlpha(colour, 1.0f);
        }

        private Color CliffPixel(Image top, Image? cliff, Vector2I sourceOrigin, Vector2I sourceSize, float u, float v, int salt)
        {
            Color source = cliff is null
                ? ScenarioGeneratedCliffColour(SampleTopMaterial(top, u, v, salt))
                : PreserveCliffSourceLayout
                    ? SampleSourceRegion(cliff, sourceOrigin, sourceSize, u, v)
                : UseDirectTextureSampling
                    ? SampleDirectTexture(cliff, sourceOrigin, sourceSize, u, v, salt, preferTopSurface: false)
                    : SampleOpaqueMaterial(cliff, u, v, salt);
            return source;
        }

        private Color ScenarioTopTint(Color colour) => Scenario switch
        {
            ElevationScenario.Dune => Blend(colour, new Color(0.77f, 0.62f, 0.34f), 0.28f),
            ElevationScenario.Canyon => Blend(colour, new Color(0.62f, 0.37f, 0.19f), 0.24f),
            ElevationScenario.Snow => Blend(AdjustBrightness(colour, 0.20f), new Color(0.86f, 0.92f, 0.94f), 0.46f),
            ElevationScenario.Volcano => Blend(AdjustBrightness(colour, -0.22f), new Color(0.16f, 0.13f, 0.11f), 0.38f),
            ElevationScenario.Swamp => Blend(AdjustBrightness(colour, -0.08f), new Color(0.13f, 0.28f, 0.12f), 0.28f),
            ElevationScenario.Mountain => AdjustSaturation(colour, 0.82f),
            _ => colour
        };

        private Color ScenarioGeneratedCliffColour(Color topColour) => Scenario switch
        {
            ElevationScenario.Dune => Blend(AdjustBrightness(topColour, -0.24f), new Color(0.60f, 0.47f, 0.28f), 0.46f),
            ElevationScenario.Canyon => Blend(AdjustBrightness(topColour, -0.30f), new Color(0.45f, 0.25f, 0.15f), 0.58f),
            ElevationScenario.Snow => Blend(AdjustBrightness(topColour, -0.18f), new Color(0.54f, 0.65f, 0.68f), 0.56f),
            ElevationScenario.Volcano => Blend(AdjustBrightness(topColour, -0.42f), new Color(0.08f, 0.07f, 0.07f), 0.62f),
            ElevationScenario.Swamp => Blend(AdjustBrightness(topColour, -0.34f), new Color(0.09f, 0.15f, 0.12f), 0.52f),
            _ => Blend(AdjustSaturation(AdjustBrightness(topColour, -0.20f), 0.42f), new Color(0.43f, 0.58f, 0.60f), 0.56f)
        };

        private Color ScenarioRampColour(Color topColour) => Scenario switch
        {
            ElevationScenario.Dune => Blend(topColour, new Color(0.84f, 0.68f, 0.37f), 0.48f),
            ElevationScenario.Snow => Blend(topColour, new Color(0.76f, 0.83f, 0.86f), 0.46f),
            ElevationScenario.Volcano => new Color(0.18f, 0.13f, 0.10f, 1.0f),
            ElevationScenario.Swamp => new Color(0.18f, 0.27f, 0.13f, 1.0f),
            _ => Blend(topColour, new Color(0.46f, 0.36f, 0.24f), 0.42f)
        };

        private Color AddTopDetails(Color colour, float u, float v, float alpha)
        {
            float density = Mathf.Clamp(DetailDensity, 0.0f, 1.0f);
            float detail = Scenario switch
            {
                ElevationScenario.Dune => Mathf.Max(0.0f, Mathf.Sin((u * 10.0f) + (v * 17.0f) + Seed) - 0.55f) * 0.15f,
                ElevationScenario.Mountain => CrackMark(u, v, 0.035f) * -0.18f,
                ElevationScenario.Canyon => Mathf.Sin(v * 34.0f) * 0.045f,
                ElevationScenario.Snow => Spot(u, v, Seed + 501, 8.0f, 0.08f) * 0.16f,
                ElevationScenario.Volcano => CrackMark(u, v, 0.035f) * 0.22f,
                ElevationScenario.Swamp => Spot(u, v, Seed + 503, 6.0f, 0.16f) * -0.12f,
                _ => Spot(u, v, Seed + 509, 8.0f, 0.12f) * -0.10f
            };
            return AdjustBrightness(colour, detail * density * alpha);
        }

        private Color ApplyCliffShading(Color colour, float u, float v, bool stacked)
        {
            float verticalShade = v * CliffShadeStrength;
            float groove = Mathf.Abs(Mathf.Sin((u * 24.0f) + (SmoothValueNoise(u * 6.0f, v * 5.0f, Seed + 700) * 3.0f)));
            float grooveShade = groove > 0.78f ? (groove - 0.78f) * -0.18f : 0.0f;
            float ledge = stacked && Mathf.Abs(v - 0.52f) < 0.035f ? 0.16f : 0.0f;
            return AdjustBrightness(colour, -verticalShade + grooveShade + ledge);
        }

        private void DrawOrganicOutline(Image image, int ox, int oy, int width, int height, string maskId)
        {
            Color outline = new Color(0.05f, 0.13f, 0.12f, Mathf.Clamp(OutlineStrength, 0.0f, 1.0f));
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    float u = (x + 0.5f) / width;
                    float v = (y + 0.5f) / height;
                    float alpha = TopMask(maskId, u, v);
                    if (alpha <= 0.25f)
                        continue;

                    bool nearEmpty =
                        TopMask(maskId, (x - 1 + 0.5f) / width, v) <= 0.25f ||
                        TopMask(maskId, (x + 1 + 0.5f) / width, v) <= 0.25f ||
                        TopMask(maskId, u, (y - 1 + 0.5f) / height) <= 0.25f ||
                        TopMask(maskId, u, (y + 1 + 0.5f) / height) <= 0.25f;
                    if (!nearEmpty)
                        continue;

                    image.SetPixel(ox + x, oy + y, AlphaOver(image.GetPixel(ox + x, oy + y), outline));
                }
            }
        }

        private void DrawCliffCracks(Image image, int ox, int oy, int width, int height, int salt, bool stacked)
        {
            for (int i = 0; i < 7; i++)
            {
                float seedX = TerrainGeometry.Hash01(i, salt, Seed + 811);
                int x = ox + Mathf.RoundToInt(Mathf.Lerp(width * 0.15f, width * 0.85f, seedX));
                int y0 = oy + Mathf.RoundToInt(Mathf.Lerp(height * 0.04f, height * 0.28f, TerrainGeometry.Hash01(i, salt, Seed + 823)));
                int y1 = oy + Mathf.RoundToInt(Mathf.Lerp(height * 0.58f, height * 0.92f, TerrainGeometry.Hash01(i, salt, Seed + 839)));
                Color crack = new Color(0.05f, 0.08f, 0.08f, stacked ? 0.08f : 0.05f);
                DrawLineOnOpaque(image, x, y0, x + Mathf.RoundToInt(Mathf.Lerp(-6, 6, TerrainGeometry.Hash01(i, salt, Seed + 853))), y1, crack);
            }
        }

        private static void DrawLine(Image image, int x0, int y0, int x1, int y1, Color colour)
            => DrawLine(image, x0, y0, x1, y1, colour, requireOpaqueTarget: false);

        private static void DrawLineOnOpaque(Image image, int x0, int y0, int x1, int y1, Color colour)
            => DrawLine(image, x0, y0, x1, y1, colour, requireOpaqueTarget: true);

        private static void DrawLine(Image image, int x0, int y0, int x1, int y1, Color colour, bool requireOpaqueTarget)
        {
            int dx = Mathf.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            int x = x0;
            int y = y0;

            while (true)
            {
                if (x >= 0 && y >= 0 && x < image.GetWidth() && y < image.GetHeight())
                {
                    Color current = image.GetPixel(x, y);
                    if (!requireOpaqueTarget || current.A > 0.75f)
                        image.SetPixel(x, y, AlphaOver(current, colour));
                }
                if (x == x1 && y == y1)
                    break;

                int e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x += sx;
                }
                if (e2 <= dx)
                {
                    err += dx;
                    y += sy;
                }
            }
        }

        private static Image? LoadImage(string path)
        {
            string disk = DiskPath(path);
            return File.Exists(disk) ? Image.LoadFromFile(disk) : null;
        }

        private static Error SavePng(Image image, string path)
        {
            string disk = DiskPath(path);
            EnsureDirectory(disk);
            return image.SavePng(disk);
        }

        private void SaveTileSet(Image atlas, int tileWidth, int regionHeight)
        {
            // The TileSet is saved with this texture EMBEDDED, so the mip chain
            // has to be built here or it never exists: nothing imports a texture
            // that lives inside a .tres, and a TileMapLayer asking for
            // LinearWithMipmaps against it falls back to plain linear without
            // saying so. Baking it in at generation time is what stops every
            // future consumer of this atlas aliasing at map zoom.
            atlas.GenerateMipmaps();
            var source = new TileSetAtlasSource
            {
                Texture = ImageTexture.CreateFromImage(atlas),
                TextureRegionSize = new Vector2I(tileWidth, regionHeight)
            };

            for (int y = 0; y < AtlasRows; y++)
            {
                for (int x = 0; x < AtlasColumns; x++)
                    source.CreateTile(new Vector2I(x, y));
            }

            var tileSet = new TileSet { TileSize = new Vector2I(tileWidth, regionHeight) };
            tileSet.AddSource(source, 0);
            string disk = DiskPath(OutputTileSetPath);
            EnsureDirectory(disk);
            Error err = ResourceSaver.Save(tileSet, OutputTileSetPath);
            if (err != Error.Ok)
                GD.PushWarning($"[{Name}] Could not save elevation TileSet '{OutputTileSetPath}': {err}.");
        }

        private void SaveManifestJson(int tileWidth, int topHeight, int cliffHeight)
        {
            string disk = DiskPath(OutputManifestPath);
            EnsureDirectory(disk);
            File.WriteAllText(disk, BuildManifestJson(tileWidth, topHeight, cliffHeight));
        }

        private void SaveReferenceManifestJson(int sheetWidth, int sheetHeight)
        {
            string disk = DiskPath(OutputManifestPath);
            EnsureDirectory(disk);
            File.WriteAllText(disk, BuildReferenceManifestJson(sheetWidth, sheetHeight));
        }

        private string BuildManifestJson(int tileWidth, int topHeight, int cliffHeight)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"atlas\": \"{Escape(OutputAtlasPath)}\",");
            sb.AppendLine($"  \"scenario\": \"{Scenario.ToString().ToLowerInvariant()}\",");
            sb.AppendLine($"  \"tile_width\": {tileWidth},");
            sb.AppendLine($"  \"top_height\": {topHeight},");
            sb.AppendLine($"  \"cliff_height\": {cliffHeight},");
            sb.AppendLine("  \"tiles\": [");
            for (int i = 0; i < TileSpecs.Length; i++)
            {
                TileSpec spec = TileSpecs[i];
                sb.Append("    { ");
                sb.Append($"\"id\": \"{Escape(spec.Id)}\", ");
                sb.Append($"\"role\": \"{spec.Role.ToString().ToLowerInvariant()}\", ");
                sb.Append($"\"atlas\": [{spec.Column}, {spec.Row}], ");
                sb.Append($"\"walkable\": {Bool(spec.Walkable)}, ");
                sb.Append($"\"climbable\": {Bool(spec.Climbable)}, ");
                sb.Append($"\"direction\": \"{Escape(spec.Direction)}\"");
                sb.Append(i == TileSpecs.Length - 1 ? " }\n" : " },\n");
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private string BuildReferenceManifestJson(int sheetWidth, int sheetHeight)
        {
            var roles = new (string Id, string Role, Rect2I Rect, bool Walkable, bool Climbable, string Direction)[]
            {
                ("top_full_left", "top", new Rect2I(0, 0, 128, 128), true, false, ""),
                ("top_vertical_left", "top", new Rect2I(128, 0, 64, 128), true, false, "north_south"),
                ("top_vertical_right", "top", new Rect2I(192, 0, 64, 128), true, false, "north_south"),
                ("top_horizontal_left", "top", new Rect2I(0, 128, 128, 64), true, false, "east_west"),
                ("top_small_left", "top", new Rect2I(192, 128, 64, 64), true, false, ""),
                ("side_cliff_left", "side_cliff", new Rect2I(0, 256, 128, 128), false, false, "west"),
                ("side_cliff_right", "side_cliff", new Rect2I(192, 256, 64, 128), false, false, "east"),
                ("top_full_elevated", "top", new Rect2I(320, 0, 128, 128), true, false, ""),
                ("top_vertical_elevated_left", "top", new Rect2I(448, 0, 64, 128), true, false, "north_south"),
                ("top_vertical_elevated_right", "top", new Rect2I(512, 0, 64, 128), true, false, "north_south"),
                ("front_lip", "top_edge", new Rect2I(320, 128, 128, 64), true, false, "south"),
                ("front_column_narrow", "cliff", new Rect2I(512, 128, 64, 256), false, false, "south"),
                ("front_columns_full", "cliff", new Rect2I(320, 192, 128, 192), false, false, "south"),
            };

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"atlas\": \"{Escape(OutputAtlasPath)}\",");
            sb.AppendLine("  \"layout\": \"forest_reference_sheet\",");
            sb.AppendLine($"  \"sheet_width\": {sheetWidth},");
            sb.AppendLine($"  \"sheet_height\": {sheetHeight},");
            sb.AppendLine("  \"tiles\": [");
            for (int i = 0; i < roles.Length; i++)
            {
                var role = roles[i];
                sb.Append("    { ");
                sb.Append($"\"id\": \"{Escape(role.Id)}\", ");
                sb.Append($"\"role\": \"{Escape(role.Role)}\", ");
                sb.Append($"\"rect\": [{role.Rect.Position.X}, {role.Rect.Position.Y}, {role.Rect.Size.X}, {role.Rect.Size.Y}], ");
                sb.Append($"\"walkable\": {Bool(role.Walkable)}, ");
                sb.Append($"\"climbable\": {Bool(role.Climbable)}, ");
                sb.Append($"\"direction\": \"{Escape(role.Direction)}\"");
                sb.Append(i == roles.Length - 1 ? " }\n" : " },\n");
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string DiskPath(string path)
            => path.StartsWith("res://", StringComparison.Ordinal) || path.StartsWith("user://", StringComparison.Ordinal)
                ? ProjectSettings.GlobalizePath(path)
                : path;

        private static void EnsureDirectory(string diskPath)
        {
            string? dir = Path.GetDirectoryName(diskPath);
            if (!string.IsNullOrEmpty(dir) && !DirAccess.DirExistsAbsolute(dir))
                DirAccess.MakeDirRecursiveAbsolute(dir);
        }

        private Color SampleTopMaterial(Image source, float u, float v, int salt)
            => SamplePreferred(source, u, v, salt, preferTopSurface: true);

        private Color SampleOpaqueMaterial(Image source, float u, float v, int salt)
            => SamplePreferred(source, u, v, salt, preferTopSurface: false);

        private Color SampleDirectTexture(Image source, Vector2I sourceOrigin, Vector2I sourceSize, float u, float v, int salt, bool preferTopSurface)
        {
            int width = source.GetWidth();
            int height = source.GetHeight();
            Rect2I rect = SourceRectFor(source, sourceOrigin, sourceSize);
            float repeats = Mathf.Max(0.25f, TextureRepeatsPerTile);
            float su = Fract((u * repeats) + TerrainGeometry.Hash01(salt, 0, Seed + 2101) * 0.17f);
            float sv = Fract((v * repeats) + TerrainGeometry.Hash01(0, salt, Seed + 2113) * 0.17f);
            Color direct = source.GetPixel(
                rect.Position.X + Mathf.Clamp(Mathf.FloorToInt(su * rect.Size.X), 0, rect.Size.X - 1),
                rect.Position.Y + Mathf.Clamp(Mathf.FloorToInt(sv * rect.Size.Y), 0, rect.Size.Y - 1));
            if (direct.A > 0.05f)
                return WithAlpha(direct, 1.0f);

            return SamplePreferred(source, sourceOrigin, sourceSize, u, v, salt, preferTopSurface);
        }

        private static Color SampleSourceRegion(Image source, Vector2I sourceOrigin, Vector2I sourceSize, float u, float v)
        {
            Rect2I rect = SourceRectFor(source, sourceOrigin, sourceSize);
            return source.GetPixel(
                rect.Position.X + Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp(u, 0.0f, 0.9999f) * rect.Size.X), 0, rect.Size.X - 1),
                rect.Position.Y + Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp(v, 0.0f, 0.9999f) * rect.Size.Y), 0, rect.Size.Y - 1));
        }

        private Color SamplePreferred(Image source, float u, float v, int salt, bool preferTopSurface)
            => SamplePreferred(source, Vector2I.Zero, Vector2I.Zero, u, v, salt, preferTopSurface);

        private Color SamplePreferred(Image source, Vector2I sourceOrigin, Vector2I sourceSize, float u, float v, int salt, bool preferTopSurface)
        {
            Rect2I rect = SourceRectFor(source, sourceOrigin, sourceSize);
            Color best = ScenarioFallbackColour();
            float bestScore = -1.0f;

            TryCandidate(source, rect, u, v, preferTopSurface, ref best, ref bestScore);

            // A user may provide a whole tileset sheet rather than a seamless
            // material tile. Skip transparent sheet gaps and reuse real pixels.
            for (int i = 0; i < 48; i++)
            {
                float su = Fract(u + TerrainGeometry.Hash01(i, salt, Seed + 1901) * 0.93f + (i * 0.037f));
                float sv = Fract(v + TerrainGeometry.Hash01(i, salt, Seed + 1913) * 0.89f + (i * 0.053f));
                TryCandidate(source, rect, su, sv, preferTopSurface, ref best, ref bestScore);
            }

            return WithAlpha(best, 1.0f);
        }

        private static void TryCandidate(Image source, Rect2I rect, float u, float v, bool preferTopSurface, ref Color best, ref float bestScore)
        {
            Color candidate = source.GetPixel(
                rect.Position.X + Mathf.Clamp(Mathf.FloorToInt(Fract(u) * rect.Size.X), 0, rect.Size.X - 1),
                rect.Position.Y + Mathf.Clamp(Mathf.FloorToInt(Fract(v) * rect.Size.Y), 0, rect.Size.Y - 1));
            float score = MaterialScore(candidate, preferTopSurface);
            if (score <= bestScore)
                return;

            best = WithAlpha(candidate, 1.0f);
            bestScore = score;
        }

        private static Rect2I SourceRectFor(Image source, Vector2I origin, Vector2I size)
        {
            int width = source.GetWidth();
            int height = source.GetHeight();
            Vector2I clampedOrigin = new(Mathf.Clamp(origin.X, 0, Mathf.Max(0, width - 1)), Mathf.Clamp(origin.Y, 0, Mathf.Max(0, height - 1)));
            Vector2I clampedSize = size.X <= 0 || size.Y <= 0
                ? new Vector2I(width - clampedOrigin.X, height - clampedOrigin.Y)
                : new Vector2I(Mathf.Clamp(size.X, 1, width - clampedOrigin.X), Mathf.Clamp(size.Y, 1, height - clampedOrigin.Y));
            return new Rect2I(clampedOrigin, clampedSize);
        }

        private static bool UsesSideCliff(string id)
            => id.Contains("side_", StringComparison.Ordinal)
                || id.Contains("outer_left", StringComparison.Ordinal)
                || id.Contains("outer_right", StringComparison.Ordinal)
                || id.Contains("inner_left", StringComparison.Ordinal)
                || id.Contains("inner_right", StringComparison.Ordinal);

        private static float MaterialScore(Color colour, bool preferTopSurface)
        {
            if (colour.A <= 0.05f)
                return -1.0f;

            float brightness = (colour.R + colour.G + colour.B) / 3.0f;
            if (brightness < 0.08f)
                return -1.0f;

            float max = Mathf.Max(colour.R, Mathf.Max(colour.G, colour.B));
            float min = Mathf.Min(colour.R, Mathf.Min(colour.G, colour.B));
            float saturation = max - min;
            float greenBias = Mathf.Clamp(colour.G - Mathf.Max(colour.R, colour.B), -1.0f, 1.0f);
            float nonBlack = Mathf.Clamp((brightness - 0.08f) / 0.24f, 0.0f, 1.0f);

            return preferTopSurface
                ? nonBlack + brightness + saturation * 0.35f + Mathf.Max(0.0f, greenBias) * 1.35f
                : nonBlack + saturation * 0.20f - Mathf.Max(0.0f, greenBias) * 0.25f;
        }

        private Color ScenarioFallbackColour() => Scenario switch
        {
            ElevationScenario.Dune => new Color(0.76f, 0.60f, 0.34f, 1.0f),
            ElevationScenario.Canyon => new Color(0.58f, 0.34f, 0.18f, 1.0f),
            ElevationScenario.Snow => new Color(0.84f, 0.90f, 0.92f, 1.0f),
            ElevationScenario.Volcano => new Color(0.16f, 0.14f, 0.12f, 1.0f),
            ElevationScenario.Swamp => new Color(0.16f, 0.30f, 0.13f, 1.0f),
            ElevationScenario.Mountain => new Color(0.42f, 0.53f, 0.50f, 1.0f),
            _ => new Color(0.42f, 0.62f, 0.32f, 1.0f)
        };

        private static float RoundedRectMask(float u, float v, float minX, float maxX, float minY, float maxY, float radius)
        {
            float x = Mathf.Clamp(u, minX + radius, maxX - radius);
            float y = Mathf.Clamp(v, minY + radius, maxY - radius);
            float dx = Mathf.Abs(u - x);
            float dy = Mathf.Abs(v - y);
            float inside = radius - Mathf.Sqrt((dx * dx) + (dy * dy));
            float bounds = u >= minX && u <= maxX && v >= minY && v <= maxY ? 1.0f : 0.0f;
            return Mathf.Clamp(SmoothStep(-0.025f, 0.025f, inside) * bounds, 0.0f, 1.0f);
        }

        private float CrackMark(float u, float v, float width)
        {
            float n = SmoothValueNoise(u * 6.0f, v * 6.0f, Seed + 1301);
            float line = Mathf.Abs(Mathf.Sin((u * 19.0f) - (v * 13.0f) + (n * 3.0f)));
            return line < width ? 1.0f - (line / width) : 0.0f;
        }

        private static float Spot(float u, float v, int seed, float scale, float threshold)
        {
            float value = SmoothValueNoise(u * scale, v * scale, seed);
            return value > 1.0f - threshold ? Mathf.Clamp((value - (1.0f - threshold)) / threshold, 0.0f, 1.0f) : 0.0f;
        }

        private static float SmoothValueNoise(float x, float y, int seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float tx = Smooth(x - x0);
            float ty = Smooth(y - y0);
            float top = Mathf.Lerp(TerrainGeometry.Hash01(x0, y0, seed), TerrainGeometry.Hash01(x0 + 1, y0, seed), tx);
            float bottom = Mathf.Lerp(TerrainGeometry.Hash01(x0, y0 + 1, seed), TerrainGeometry.Hash01(x0 + 1, y0 + 1, seed), tx);
            return Mathf.Lerp(top, bottom, ty);
        }

        private static float Smooth(float value)
            => value * value * (3.0f - (2.0f * value));

        private static float SmoothStep(float edge0, float edge1, float value)
        {
            if (Mathf.IsEqualApprox(edge0, edge1))
                return value < edge0 ? 0.0f : 1.0f;

            float t = Mathf.Clamp((value - edge0) / (edge1 - edge0), 0.0f, 1.0f);
            return Smooth(t);
        }

        private static int Wrap(int value, int size)
            => size <= 0 ? 0 : ((value % size) + size) % size;

        private static float Fract(float value)
            => value - Mathf.Floor(value);

        private static Color WithAlpha(Color colour, float alpha)
            => new(colour.R, colour.G, colour.B, Mathf.Clamp(alpha, 0.0f, 1.0f));

        private static Color AlphaOver(Color under, Color over)
        {
            float outA = over.A + under.A * (1.0f - over.A);
            if (outA <= 0.001f)
                return Colors.Transparent;

            return new Color(
                ((over.R * over.A) + (under.R * under.A * (1.0f - over.A))) / outA,
                ((over.G * over.A) + (under.G * under.A * (1.0f - over.A))) / outA,
                ((over.B * over.A) + (under.B * under.A * (1.0f - over.A))) / outA,
                outA);
        }

        private static Color Blend(Color a, Color b, float amount)
            => new(
                Mathf.Lerp(a.R, b.R, amount),
                Mathf.Lerp(a.G, b.G, amount),
                Mathf.Lerp(a.B, b.B, amount),
                Mathf.Lerp(a.A, b.A, amount));

        private static Color AdjustBrightness(Color colour, float amount)
            => new(
                Mathf.Clamp(colour.R + amount, 0.0f, 1.0f),
                Mathf.Clamp(colour.G + amount, 0.0f, 1.0f),
                Mathf.Clamp(colour.B + amount, 0.0f, 1.0f),
                colour.A);

        private static Color AdjustSaturation(Color colour, float saturation)
        {
            float gray = (colour.R * 0.299f) + (colour.G * 0.587f) + (colour.B * 0.114f);
            return new Color(
                Mathf.Clamp(Mathf.Lerp(gray, colour.R, saturation), 0.0f, 1.0f),
                Mathf.Clamp(Mathf.Lerp(gray, colour.G, saturation), 0.0f, 1.0f),
                Mathf.Clamp(Mathf.Lerp(gray, colour.B, saturation), 0.0f, 1.0f),
                colour.A);
        }

        private static string Escape(string value)
            => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

        private static string Bool(bool value) => value ? "true" : "false";
    }
}

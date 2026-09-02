using Godot;
using static Beep.ECS.GridDefinitionReader;

namespace Beep.ECS
{
    /// <summary>
    /// Data for one placeable building/prop/tool target in grid builder games.
    /// Menus can list these definitions and pass the selected one to placement.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridBuildDefinition : Resource
    {
        [Export] public string BuildId { get; set; } = "workshop";
        [Export] public string DisplayName { get; set; } = "Workshop";
        [Export] public string Category { get; set; } = "Buildings";
        [Export] public PackedScene? Scene { get; set; }
        [Export] public Texture2D? PreviewTexture { get; set; }
        [Export] public Vector2I Footprint { get; set; } = Vector2I.One;
        [Export(PropertyHint.Range, "0,600,0.01")] public float BuildSeconds { get; set; } = 0f;
        [Export] public string JobKind { get; set; } = "build";
        [Export] public bool BlocksNavigation { get; set; } = true;
        /// <summary>
        /// Whether the placed build occupies its footprint cells in placement.
        /// Separate from BlocksNavigation because they are different facts: a
        /// garden is walkable but you cannot build a second one on top of it.
        /// The old code used BlocksNavigation for both, so every walkable
        /// build could be stacked without limit on one cell. False is for the
        /// rare truly stackable decoration.
        /// </summary>
        [Export] public bool OccupiesCells { get; set; } = true;
        [Export] public bool SetZIndexFromY { get; set; } = true;
        /// <summary>
        /// Terrain kinds THIS build may stand on. Non-empty overrides the
        /// placement component's scene policy entirely - an offshore platform
        /// authorizes shallow_water for itself without opening water placement
        /// to every build. Empty defers to the scene's allowed/blocked lists.
        /// </summary>
        [Export] public Godot.Collections.Array<string> AllowedTerrainKinds { get; set; } = new();
        [Export] public Godot.Collections.Array Costs { get; set; } = new();

        public Vector2I EffectiveFootprint => new(Mathf.Max(1, Footprint.X), Mathf.Max(1, Footprint.Y));
        public float EffectiveBuildSeconds => Mathf.Max(0f, float.IsFinite(BuildSeconds) ? BuildSeconds : 0f);

        public bool HasPlayableSurface()
            => Scene != null || PreviewTexture != null;

        public static System.Collections.Generic.IEnumerable<GridBuildDefinition> Enumerate(Godot.Collections.Array builds)
        {
            foreach (Variant entry in builds)
                if (TryRead(entry, out GridBuildDefinition? definition) && definition != null)
                    yield return definition;
        }

        public static bool TryRead(Variant entry, out GridBuildDefinition? definition)
        {
            definition = null;
            if (entry.VariantType == Variant.Type.Dictionary)
            {
                if (!GridVariantReader.TryDictionary(entry, out Godot.Collections.Dictionary data))
                    return false;

                definition = new GridBuildDefinition
                {
                    BuildId = ReadString(data, "BuildId", "build_id", "workshop"),
                    DisplayName = ReadString(data, "DisplayName", "display_name", ""),
                    Category = ReadString(data, "Category", "category", "Buildings"),
                    Scene = ReadObject<PackedScene>(data, "Scene", "scene"),
                    PreviewTexture = ReadObject<Texture2D>(data, "PreviewTexture", "preview_texture"),
                    Footprint = ReadVector2I(data, "Footprint", "footprint", Vector2I.One),
                    BuildSeconds = ReadFloat(data, "BuildSeconds", "build_seconds", 0f),
                    JobKind = ReadString(data, "JobKind", "job_kind", "build"),
                    BlocksNavigation = ReadBool(data, "BlocksNavigation", "blocks_navigation", true),
                    OccupiesCells = ReadBool(data, "OccupiesCells", "occupies_cells", true),
                    SetZIndexFromY = ReadBool(data, "SetZIndexFromY", "set_z_index_from_y", true),
                    AllowedTerrainKinds = Strings(ReadArray(data, "AllowedTerrainKinds", "allowed_terrain_kinds")),
                    Costs = ReadArray(data, "Costs", "costs")
                };
                return !string.IsNullOrWhiteSpace(definition.BuildId);
            }

            if (entry.VariantType != Variant.Type.Object || entry.AsGodotObject() is not Resource resource)
                return false;

            if (resource is GridBuildDefinition typed)
            {
                definition = typed;
                return !string.IsNullOrWhiteSpace(typed.BuildId);
            }

            definition = new GridBuildDefinition
            {
                BuildId = ReadString(resource, "BuildId", "build_id", "workshop"),
                DisplayName = ReadString(resource, "DisplayName", "display_name", ""),
                Category = ReadString(resource, "Category", "category", "Buildings"),
                Scene = ReadObject<PackedScene>(resource, "Scene", "scene"),
                PreviewTexture = ReadObject<Texture2D>(resource, "PreviewTexture", "preview_texture"),
                Footprint = ReadVector2I(resource, "Footprint", "footprint", Vector2I.One),
                BuildSeconds = ReadFloat(resource, "BuildSeconds", "build_seconds", 0f),
                JobKind = ReadString(resource, "JobKind", "job_kind", "build"),
                BlocksNavigation = ReadBool(resource, "BlocksNavigation", "blocks_navigation", true),
                OccupiesCells = ReadBool(resource, "OccupiesCells", "occupies_cells", true),
                SetZIndexFromY = ReadBool(resource, "SetZIndexFromY", "set_z_index_from_y", true),
                AllowedTerrainKinds = Strings(ReadArray(resource, "AllowedTerrainKinds", "allowed_terrain_kinds")),
                Costs = ReadArray(resource, "Costs", "costs")
            };
            return !string.IsNullOrWhiteSpace(definition.BuildId);
        }

        private static Godot.Collections.Array<string> Strings(Godot.Collections.Array source)
        {
            var result = new Godot.Collections.Array<string>();
            foreach (Variant value in source)
            {
                string s = value.AsString();
                if (!string.IsNullOrWhiteSpace(s))
                    result.Add(s);
            }
            return result;
        }

        // Reading is delegated to GridDefinitionReader - the shared dual-key
        // (PascalCase / snake_case) reader all definition resources use.
    }
}

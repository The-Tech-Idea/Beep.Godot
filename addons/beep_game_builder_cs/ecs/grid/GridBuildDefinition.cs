using Godot;
using static Beep.ECS.GridDefinitionReader;

namespace Beep.ECS
{
    /// <summary>
    /// Data for one placeable building/prop/tool target in grid builder games.
    /// Menus can list these definitions and pass the selected one to placement.
    ///
    /// This is the addon's canonical <see cref="IGridSite"/>: it declares the
    /// three things you need to judge a site before commissioning it - the ground
    /// it takes, the material it needs, and how long it takes in turns.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridBuildDefinition : Resource, IGridSite
    {
        [Export] public string BuildId { get; set; } = "workshop";
        [Export] public string DisplayName { get; set; } = "Workshop";
        [Export] public string Category { get; set; } = "Buildings";
        [Export] public PackedScene? Scene { get; set; }
        [Export] public Texture2D? PreviewTexture { get; set; }
        [Export] public Vector2I Footprint { get; set; } = Vector2I.One;

        /// <summary>
        /// Turns of work this build takes. A turn is a day, so the same number
        /// means five end-turns in a turn-based game and five in-game days in a
        /// real-time one - authored once, correct on both axes. Zero means the
        /// build needs no work and finishes as soon as its materials arrive.
        /// </summary>
        [Export(PropertyHint.Range, "0,600,1")] public int BuildTurns { get; set; } = 0;
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

        /// <summary>
        /// Materials that must be PHYSICALLY DELIVERED to the site - via
        /// ITransporter, the same as any other cargo - before its build job
        /// is dispatched. Empty means the build needs nothing but Costs, paid
        /// from the wallet at placement, and starts exactly as it always has.
        /// Distinct from Costs on purpose: a cost is currency leaving a
        /// wallet the instant a build is confirmed; a required material is
        /// physical stock a hauler has to bring to the cell, which is why it
        /// is read by GridResourceAmount.Enumerate, same as Costs, but never
        /// touches GridResourceWalletComponent.
        /// </summary>
        [Export] public Godot.Collections.Array RequiredMaterials { get; set; } = new();

        /// <summary>
        /// Optional construction-stage scenes, shown one at a time as the
        /// build's job progresses (GridBuildStageVisualComponent) - a
        /// foundation, then framed, then roofed, then the finished Scene.
        /// Each is told the site's footprint, cell size and progress through
        /// IConstructionVisual, so one stage scene serves any build size.
        /// Empty means the feature is unused for this build: no stage swap,
        /// nothing instantiated beyond Scene itself.
        /// </summary>
        [Export] public Godot.Collections.Array<PackedScene> ConstructionStages { get; set; } = new();

        public Vector2I EffectiveFootprint => new(Mathf.Max(1, Footprint.X), Mathf.Max(1, Footprint.Y));
        public int EffectiveBuildTurns => Mathf.Max(0, BuildTurns);

        // ── IGridSite: the three facts, asked one way ──
        public Vector2I SiteFootprint => EffectiveFootprint;
        public Godot.Collections.Array SiteMaterials => RequiredMaterials;
        public int SiteTurns => EffectiveBuildTurns;

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
                    BuildTurns = ReadInt(data, "BuildTurns", "build_turns", 0),
                    JobKind = ReadString(data, "JobKind", "job_kind", "build"),
                    BlocksNavigation = ReadBool(data, "BlocksNavigation", "blocks_navigation", true),
                    OccupiesCells = ReadBool(data, "OccupiesCells", "occupies_cells", true),
                    SetZIndexFromY = ReadBool(data, "SetZIndexFromY", "set_z_index_from_y", true),
                    AllowedTerrainKinds = Strings(ReadArray(data, "AllowedTerrainKinds", "allowed_terrain_kinds")),
                    Costs = ReadArray(data, "Costs", "costs"),
                    RequiredMaterials = ReadArray(data, "RequiredMaterials", "required_materials"),
                    ConstructionStages = PackedScenes(ReadArray(data, "ConstructionStages", "construction_stages"))
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
                BuildTurns = ReadInt(resource, "BuildTurns", "build_turns", 0),
                JobKind = ReadString(resource, "JobKind", "job_kind", "build"),
                BlocksNavigation = ReadBool(resource, "BlocksNavigation", "blocks_navigation", true),
                OccupiesCells = ReadBool(resource, "OccupiesCells", "occupies_cells", true),
                SetZIndexFromY = ReadBool(resource, "SetZIndexFromY", "set_z_index_from_y", true),
                AllowedTerrainKinds = Strings(ReadArray(resource, "AllowedTerrainKinds", "allowed_terrain_kinds")),
                Costs = ReadArray(resource, "Costs", "costs"),
                RequiredMaterials = ReadArray(resource, "RequiredMaterials", "required_materials"),
                ConstructionStages = PackedScenes(ReadArray(resource, "ConstructionStages", "construction_stages"))
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

        private static Godot.Collections.Array<PackedScene> PackedScenes(Godot.Collections.Array source)
        {
            var result = new Godot.Collections.Array<PackedScene>();
            foreach (Variant value in source)
            {
                if (value.VariantType == Variant.Type.Object && value.AsGodotObject() is PackedScene scene)
                    result.Add(scene);
            }
            return result;
        }

        // Reading is delegated to GridDefinitionReader - the shared dual-key
        // (PascalCase / snake_case) reader all definition resources use.
    }
}

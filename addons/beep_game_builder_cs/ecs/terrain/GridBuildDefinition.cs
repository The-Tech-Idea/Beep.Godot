using Godot;

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
        [Export] public bool SetZIndexFromY { get; set; } = true;
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
                    SetZIndexFromY = ReadBool(data, "SetZIndexFromY", "set_z_index_from_y", true),
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
                SetZIndexFromY = ReadBool(resource, "SetZIndexFromY", "set_z_index_from_y", true),
                Costs = ReadArray(resource, "Costs", "costs")
            };
            return !string.IsNullOrWhiteSpace(definition.BuildId);
        }

        private static string ReadString(Godot.Collections.Dictionary data, string pascal, string snake, string fallback)
        {
            Variant value = ReadVariant(data, pascal, snake);
            return value.VariantType == Variant.Type.Nil ? fallback : value.AsString();
        }

        private static string ReadString(Resource resource, string pascal, string snake, string fallback)
        {
            Variant value = ReadVariant(resource, pascal, snake);
            return value.VariantType == Variant.Type.Nil ? fallback : value.AsString();
        }

        private static bool ReadBool(Godot.Collections.Dictionary data, string pascal, string snake, bool fallback)
        {
            Variant value = ReadVariant(data, pascal, snake);
            return GridVariantReader.Bool(value, fallback);
        }

        private static bool ReadBool(Resource resource, string pascal, string snake, bool fallback)
        {
            Variant value = ReadVariant(resource, pascal, snake);
            return GridVariantReader.Bool(value, fallback);
        }

        private static float ReadFloat(Godot.Collections.Dictionary data, string pascal, string snake, float fallback)
            => ReadFiniteFloat(ReadVariant(data, pascal, snake), fallback);

        private static float ReadFloat(Resource resource, string pascal, string snake, float fallback)
            => ReadFiniteFloat(ReadVariant(resource, pascal, snake), fallback);

        private static float ReadFiniteFloat(Variant value, float fallback)
        {
            return GridVariantReader.Float(value, fallback);
        }

        private static Vector2I ReadVector2I(Godot.Collections.Dictionary data, string pascal, string snake, Vector2I fallback)
            => ReadVector2I(ReadVariant(data, pascal, snake), fallback);

        private static Vector2I ReadVector2I(Resource resource, string pascal, string snake, Vector2I fallback)
            => ReadVector2I(ReadVariant(resource, pascal, snake), fallback);

        private static Vector2I ReadVector2I(Variant value, Vector2I fallback)
        {
            return GridVariantReader.Vector2I(value, fallback);
        }

        private static T? ReadObject<T>(Godot.Collections.Dictionary data, string pascal, string snake) where T : GodotObject
        {
            Variant value = ReadVariant(data, pascal, snake);
            return value.VariantType == Variant.Type.Object ? value.AsGodotObject() as T : null;
        }

        private static T? ReadObject<T>(Resource resource, string pascal, string snake) where T : GodotObject
        {
            Variant value = ReadVariant(resource, pascal, snake);
            return value.VariantType == Variant.Type.Object ? value.AsGodotObject() as T : null;
        }

        private static Godot.Collections.Array ReadArray(Godot.Collections.Dictionary data, string pascal, string snake)
        {
            Variant value = ReadVariant(data, pascal, snake);
            return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new Godot.Collections.Array();
        }

        private static Godot.Collections.Array ReadArray(Resource resource, string pascal, string snake)
        {
            Variant value = ReadVariant(resource, pascal, snake);
            return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new Godot.Collections.Array();
        }

        private static Variant ReadVariant(Godot.Collections.Dictionary data, string pascal, string snake)
        {
            if (data.ContainsKey(pascal))
                return data[pascal];
            return data.ContainsKey(snake) ? data[snake] : default;
        }

        private static Variant ReadVariant(Resource resource, string pascal, string snake)
        {
            Variant value = resource.Get(pascal);
            if (value.VariantType != Variant.Type.Nil)
                return value;
            return resource.Get(snake);
        }
    }
}

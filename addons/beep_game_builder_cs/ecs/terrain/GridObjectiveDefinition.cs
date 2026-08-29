using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Authored data for one settlement/grid objective. Runtime progress lives
    /// on GridObjectiveTrackerComponent so this resource can be reused safely.
    /// </summary>
    [GlobalClass]
    public partial class GridObjectiveDefinition : Resource
    {
        [Export] public string ObjectiveId { get; set; } = "clear_land";
        [Export] public string DisplayName { get; set; } = "Clear land";
        [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
        [Export(PropertyHint.Range, "1,9999,1")] public int TargetCount { get; set; } = 1;
        [Export] public bool AutoComplete { get; set; } = true;
        [Export] public bool ActiveOnStart { get; set; } = true;
        [Export] public bool HiddenUntilActive { get; set; } = false;

        public string NormalizedId()
            => Normalize(ObjectiveId);

        public static string Normalize(string value)
            => string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant().Replace(' ', '_');

        public int EffectiveTargetCount => Mathf.Max(1, TargetCount);

        public static System.Collections.Generic.IEnumerable<GridObjectiveDefinition> Enumerate(Godot.Collections.Array objectives)
        {
            foreach (Variant entry in objectives)
                if (TryRead(entry, out GridObjectiveDefinition? definition) && definition != null)
                    yield return definition;
        }

        public static bool TryRead(Variant entry, out GridObjectiveDefinition? definition)
        {
            definition = null;
            if (entry.VariantType == Variant.Type.Dictionary)
            {
                if (!GridVariantReader.TryDictionary(entry, out Godot.Collections.Dictionary data))
                    return false;

                definition = new GridObjectiveDefinition
                {
                    ObjectiveId = ReadString(data, "ObjectiveId", "objective_id", ""),
                    DisplayName = ReadString(data, "DisplayName", "display_name", ""),
                    Description = ReadString(data, "Description", "description", ""),
                    TargetCount = ReadInt(data, "TargetCount", "target_count", 1),
                    AutoComplete = ReadBool(data, "AutoComplete", "auto_complete", true),
                    ActiveOnStart = ReadBool(data, "ActiveOnStart", "active_on_start", true),
                    HiddenUntilActive = ReadBool(data, "HiddenUntilActive", "hidden_until_active", false)
                };
                return !string.IsNullOrWhiteSpace(definition.ObjectiveId);
            }

            if (entry.VariantType != Variant.Type.Object || entry.AsGodotObject() is not Resource resource)
                return false;
            if (resource is GridObjectiveDefinition typed)
            {
                definition = typed;
                return !string.IsNullOrWhiteSpace(typed.ObjectiveId);
            }

            definition = new GridObjectiveDefinition
            {
                ObjectiveId = ReadString(resource, "ObjectiveId", "objective_id", ""),
                DisplayName = ReadString(resource, "DisplayName", "display_name", ""),
                Description = ReadString(resource, "Description", "description", ""),
                TargetCount = ReadInt(resource, "TargetCount", "target_count", 1),
                AutoComplete = ReadBool(resource, "AutoComplete", "auto_complete", true),
                ActiveOnStart = ReadBool(resource, "ActiveOnStart", "active_on_start", true),
                HiddenUntilActive = ReadBool(resource, "HiddenUntilActive", "hidden_until_active", false)
            };
            return !string.IsNullOrWhiteSpace(definition.ObjectiveId);
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

        private static int ReadInt(Godot.Collections.Dictionary data, string pascal, string snake, int fallback)
            => ReadInt(ReadVariant(data, pascal, snake), fallback);

        private static int ReadInt(Resource resource, string pascal, string snake, int fallback)
            => ReadInt(ReadVariant(resource, pascal, snake), fallback);

        private static int ReadInt(Variant value, int fallback)
        {
            return GridVariantReader.Int(value, fallback);
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

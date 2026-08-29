using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Crop data for GridCropCatalogComponent. Use one resource per crop so farming
    /// games can configure maturity, valid seasons, regrowth, and harvest yield in
    /// the Inspector instead of hard-coding tool behavior.
    /// </summary>
    [GlobalClass]
    public partial class GridCropDefinition : Resource
    {
        [Export] public string CropId { get; set; } = "turnip";
        [Export] public string DisplayName { get; set; } = "Turnip";
        [Export(PropertyHint.Range, "0,365,1")] public int DaysToMature { get; set; } = 4;
        [Export(PropertyHint.Range, "-1,365,1")] public int RegrowDays { get; set; } = -1;
        [Export] public string SeedItemId { get; set; } = "turnip_seed";
        [Export] public string YieldItemId { get; set; } = "turnip";
        [Export(PropertyHint.Range, "1,999,1")] public int YieldCount { get; set; } = 1;
        [Export] public bool Spring { get; set; } = true;
        [Export] public bool Summer { get; set; } = false;
        [Export] public bool Fall { get; set; } = false;
        [Export] public bool Winter { get; set; } = false;

        public int EffectiveDaysToMature => Mathf.Max(0, DaysToMature);
        public int EffectiveRegrowDays => Mathf.Max(-1, RegrowDays);
        public int EffectiveYieldCount => Mathf.Max(1, YieldCount);

        public bool CanPlantIn(GridCalendarComponent.GridSeason season)
            => season switch
            {
                GridCalendarComponent.GridSeason.Spring => Spring,
                GridCalendarComponent.GridSeason.Summer => Summer,
                GridCalendarComponent.GridSeason.Fall => Fall,
                GridCalendarComponent.GridSeason.Winter => Winter,
                _ => false
            };

        public static System.Collections.Generic.IEnumerable<GridCropDefinition> Enumerate(Godot.Collections.Array crops)
        {
            foreach (Variant entry in crops)
                if (TryRead(entry, out GridCropDefinition? crop) && crop != null)
                    yield return crop;
        }

        public static bool TryRead(Variant entry, out GridCropDefinition? crop)
        {
            crop = null;
            if (entry.VariantType == Variant.Type.Dictionary)
            {
                if (!GridVariantReader.TryDictionary(entry, out Godot.Collections.Dictionary data))
                    return false;

                crop = new GridCropDefinition
                {
                    CropId = ReadString(data, "CropId", "crop_id", "turnip"),
                    DisplayName = ReadString(data, "DisplayName", "display_name", ""),
                    DaysToMature = ReadInt(data, "DaysToMature", "days_to_mature", 4),
                    RegrowDays = ReadInt(data, "RegrowDays", "regrow_days", -1),
                    SeedItemId = ReadString(data, "SeedItemId", "seed_item_id", ""),
                    YieldItemId = ReadString(data, "YieldItemId", "yield_item_id", ""),
                    YieldCount = ReadInt(data, "YieldCount", "yield_count", 1),
                    Spring = ReadBool(data, "Spring", "spring", true),
                    Summer = ReadBool(data, "Summer", "summer", false),
                    Fall = ReadBool(data, "Fall", "fall", false),
                    Winter = ReadBool(data, "Winter", "winter", false)
                };
                return !string.IsNullOrWhiteSpace(crop.CropId);
            }

            if (entry.VariantType != Variant.Type.Object || entry.AsGodotObject() is not Resource resource)
                return false;
            if (resource is GridCropDefinition typed)
            {
                crop = typed;
                return !string.IsNullOrWhiteSpace(typed.CropId);
            }

            crop = new GridCropDefinition
            {
                CropId = ReadString(resource, "CropId", "crop_id", "turnip"),
                DisplayName = ReadString(resource, "DisplayName", "display_name", ""),
                DaysToMature = ReadInt(resource, "DaysToMature", "days_to_mature", 4),
                RegrowDays = ReadInt(resource, "RegrowDays", "regrow_days", -1),
                SeedItemId = ReadString(resource, "SeedItemId", "seed_item_id", ""),
                YieldItemId = ReadString(resource, "YieldItemId", "yield_item_id", ""),
                YieldCount = ReadInt(resource, "YieldCount", "yield_count", 1),
                Spring = ReadBool(resource, "Spring", "spring", true),
                Summer = ReadBool(resource, "Summer", "summer", false),
                Fall = ReadBool(resource, "Fall", "fall", false),
                Winter = ReadBool(resource, "Winter", "winter", false)
            };
            return !string.IsNullOrWhiteSpace(crop.CropId);
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

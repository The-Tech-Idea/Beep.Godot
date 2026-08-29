using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Lookup table for farming crop definitions. Pair with GridToolActionComponent
    /// so the plant tool can read maturity days and season restrictions from data.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridCropCatalogComponent : Node
    {
        [Export] public Godot.Collections.Array Crops { get; set; } = new();
        [Export] public bool AllowUnknownCrops { get; set; } = true;

        public GridCropDefinition? FindCrop(string cropId)
        {
            if (string.IsNullOrWhiteSpace(cropId))
                return null;

            foreach (GridCropDefinition crop in GridCropDefinition.Enumerate(Crops))
            {
                if (crop == null)
                    continue;

                if (string.Equals(crop.CropId, cropId.Trim(), System.StringComparison.OrdinalIgnoreCase))
                    return crop;
            }

            return null;
        }

        public bool CanPlant(string cropId, GridCalendarComponent.GridSeason season)
        {
            GridCropDefinition? crop = FindCrop(cropId);
            return crop == null ? AllowUnknownCrops : crop.CanPlantIn(season);
        }

        public int DaysToMature(string cropId, int fallback)
        {
            GridCropDefinition? crop = FindCrop(cropId);
            return crop == null ? Mathf.Max(0, fallback) : crop.EffectiveDaysToMature;
        }

        public string YieldItem(string cropId)
        {
            GridCropDefinition? crop = FindCrop(cropId);
            return crop?.YieldItemId ?? cropId;
        }

        public int YieldCount(string cropId)
        {
            GridCropDefinition? crop = FindCrop(cropId);
            return crop == null ? 1 : crop.EffectiveYieldCount;
        }

        public Godot.Collections.Array<string> CropIdsForSeason(GridCalendarComponent.GridSeason season)
        {
            var ids = new Godot.Collections.Array<string>();
            foreach (GridCropDefinition crop in GridCropDefinition.Enumerate(Crops))
            {
                if (crop != null && crop.CanPlantIn(season))
                    ids.Add(crop.CropId);
            }
            return ids;
        }
    }
}

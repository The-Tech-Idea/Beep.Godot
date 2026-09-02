using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Resource id plus quantity, used by grid build costs and starting wallets.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridResourceAmount : Resource
    {
        [Export] public string ResourceId { get; set; } = "wood";
        [Export(PropertyHint.Range, "0,999999,1")] public int Amount { get; set; } = 1;

        public static IEnumerable<(string ResourceId, int Amount)> Enumerate(Godot.Collections.Array amounts)
        {
            foreach (Variant entry in amounts)
            {
                if (TryRead(entry, out string resourceId, out int amount))
                    yield return (resourceId, amount);
            }
        }

        public static bool TryRead(Variant entry, out string resourceId, out int amount)
        {
            resourceId = "";
            amount = 0;

            if (entry.VariantType == Variant.Type.Dictionary)
            {
                if (!GridVariantReader.TryDictionary(entry, out Godot.Collections.Dictionary dictionary))
                    return false;

                resourceId = GridDefinitionReader.ReadString(dictionary, "ResourceId", "resource_id", "");
                amount = GridDefinitionReader.ReadInt(dictionary, "Amount", "amount", 0);
                return !string.IsNullOrWhiteSpace(resourceId);
            }

            if (entry.VariantType != Variant.Type.Object || entry.AsGodotObject() is not Resource resource)
                return false;

            resourceId = resource is GridResourceAmount typed
                ? typed.ResourceId
                : GridDefinitionReader.ReadString(resource, "ResourceId", "resource_id", "");
            amount = resource is GridResourceAmount typedAmount
                ? typedAmount.Amount
                : GridDefinitionReader.ReadInt(resource, "Amount", "amount", 0);

            return !string.IsNullOrWhiteSpace(resourceId);
        }
    }
}

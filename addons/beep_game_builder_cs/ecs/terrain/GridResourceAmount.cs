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

                resourceId = DictionaryString(dictionary, "ResourceId", "resource_id");
                amount = DictionaryInt(dictionary, "Amount", "amount");
                return !string.IsNullOrWhiteSpace(resourceId);
            }

            if (entry.VariantType != Variant.Type.Object || entry.AsGodotObject() is not Resource resource)
                return false;

            resourceId = resource is GridResourceAmount typed
                ? typed.ResourceId
                : ResourceString(resource, "ResourceId", "resource_id");
            amount = resource is GridResourceAmount typedAmount
                ? typedAmount.Amount
                : ResourceInt(resource, "Amount", "amount");

            return !string.IsNullOrWhiteSpace(resourceId);
        }

        private static string DictionaryString(Godot.Collections.Dictionary dictionary, string pascalName, string snakeName)
        {
            if (dictionary.ContainsKey(pascalName))
                return dictionary[pascalName].AsString();
            return dictionary.ContainsKey(snakeName) ? dictionary[snakeName].AsString() : "";
        }

        private static int DictionaryInt(Godot.Collections.Dictionary dictionary, string pascalName, string snakeName)
        {
            if (dictionary.ContainsKey(pascalName))
                return ReadInt(dictionary[pascalName]);
            return dictionary.ContainsKey(snakeName) ? ReadInt(dictionary[snakeName]) : 0;
        }

        private static string ResourceString(Resource resource, string pascalName, string snakeName)
        {
            Variant value = resource.Get(pascalName);
            if (value.VariantType == Variant.Type.String)
                return value.AsString();

            value = resource.Get(snakeName);
            return value.VariantType == Variant.Type.String ? value.AsString() : "";
        }

        private static int ResourceInt(Resource resource, string pascalName, string snakeName)
        {
            Variant value = resource.Get(pascalName);
            if (CanReadInt(value))
                return ReadInt(value);

            value = resource.Get(snakeName);
            return CanReadInt(value) ? ReadInt(value) : 0;
        }

        private static bool CanReadInt(Variant value)
            => value.VariantType is Variant.Type.Int or Variant.Type.Float or Variant.Type.String;

        private static int ReadInt(Variant value)
        {
            return GridVariantReader.Int(value, 0);
        }
    }
}

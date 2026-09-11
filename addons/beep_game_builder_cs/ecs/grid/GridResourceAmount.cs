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

        internal static bool TryTotals(Godot.Collections.Array amounts, out Dictionary<string, int> totals)
        {
            // Canonical resource id (GridIds.Normalize: trim, lower, ' '/'-' -> '_'), the one form the
            // wallet, storage and reservation stores all key on (DUP-14). Keying by a space-kept
            // Trim().ToLowerInvariant() here is what let Wallet.Spend commit a debit to a phantom
            // "iron ore" entry while the balance lived under "iron_ore". A default ordinal dict now
            // suffices - the lower-invariant canonicalisation already folds case, so a second
            // OrdinalIgnoreCase rule would be a duplicate case policy.
            totals = new();
            foreach ((string resourceId, int amount) in Enumerate(amounts))
            {
                if (amount <= 0) continue;
                string id = GridIds.Normalize(resourceId);
                if (id.Length == 0) continue;
                totals.TryGetValue(id, out int existing);
                long sum = (long)existing + amount;
                if (sum > int.MaxValue) return false;
                totals[id] = (int)sum;
            }
            return true;
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

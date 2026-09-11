using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// A stationary cargo hold: a tank, a silo, a warehouse, a pipeline
    /// buffer. It holds material, answers the IGridCargoHold contract, and
    /// does nothing else - which is the point: an extractor fills it, a
    /// hauler draws from it, a pipeline segment hands through it, all via
    /// Load/Unload and GridTransportManagerComponent.Transfer, and none of
    /// them need to know it is a tank.
    ///
    /// Attach under a placed building beside its GridObjectComponent. Give
    /// each storage its own SaveKey; the contents are world state and
    /// round-trip through saves.
    ///
    /// AllowedResourceIds/AllowedResourceTags decide what CanAccept lets in;
    /// the tag list, when Catalog is wired, accepts by resource TYPE
    /// (ResourceDefinition.Tags) instead of one id at a time.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridStorageComponent : Node, IStorage, ISaveable
    {
        [Signal] public delegate void StorageChangedEventHandler(string resourceId, int stored, int currentLoad);

        [Export] public bool ParticipatesInSave { get; set; } = true;
        [Export] public string SaveKey { get; set; } = "grid_storage.state";

        /// <summary>Total units the storage takes, across every resource in it.</summary>
        [Export(PropertyHint.Range, "1,999999,1")] public int Capacity { get; set; } = 200;

        /// <summary>
        /// Resource ids this storage accepts; empty (together with
        /// AllowedResourceTags) accepts anything. Checked before
        /// AllowedResourceTags - an exact id listed here is always accepted,
        /// catalog or not.
        /// </summary>
        [Export] public Godot.Collections.Array<string> AllowedResourceIds { get; set; } = new();

        /// <summary>
        /// The shared resource-type catalog, consulted for AllowedResourceTags
        /// - optional. Without it, only AllowedResourceIds (or accepting
        /// anything, when both lists are empty) decides.
        /// </summary>
        [Export] public ResourceCatalog? Catalog { get; set; }

        /// <summary>
        /// Resource TAGS this storage accepts, checked against Catalog -
        /// e.g. "ore" accepts any catalog resource tagged "ore" without
        /// listing every ore id by hand, so a resource the catalog adds
        /// later is storable here with no scene edit. Applies ON TOP OF
        /// AllowedResourceIds, never instead of it. Only checked when
        /// Catalog is wired; an id the catalog does not recognize can never
        /// match a tag, which is what closes a typo'd resource id out.
        /// </summary>
        [Export] public Godot.Collections.Array<string> AllowedResourceTags { get; set; } = new();

        // Keyed by the canonical GridIds.Normalize id (DUP-14), the one form the wallet, cost totals
        // and reservations share - so a cost of "Iron Ore" and a stored "iron_ore" are the same
        // resource. A default ordinal dict is right: the normaliser already lower-cases, so an
        // OrdinalIgnoreCase comparer would be a second, redundant case rule (and would still miss the
        // space-vs-dash difference the normaliser folds).
        private readonly Dictionary<string, int> _stored = new();

        public override void _Ready()
        {
            if (!Engine.IsEditorHint() && ParticipatesInSave)
                AddToGroup(SaveableHelper.Group);
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            ClearMaterialClaims();
            if (ParticipatesInSave)
                RemoveFromGroup(SaveableHelper.Group);
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (AllowedResourceTags.Count > 0 && Catalog == null)
                return new[] { "AllowedResourceTags is set but Catalog is empty - tag filtering has no effect, and any id not also listed in AllowedResourceIds will be rejected." };
            return Array.Empty<string>();
        }

        public int CurrentLoad
        {
            get
            {
                int total = 0;
                foreach (int amount in _stored.Values)
                    total += amount;
                return total;
            }
        }

        int ILoadPort.Capacity => Mathf.Max(1, Capacity);

        public bool CanAccept(string resourceId)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
                return false;
            if (AllowedResourceIds.Count == 0 && AllowedResourceTags.Count == 0)
                return true;

            return AcceptsResourceType(GridIds.Normalize(resourceId));
        }

        /// <summary>
        /// HOOK: the acceptance rule once at least one of AllowedResourceIds/
        /// AllowedResourceTags is configured (an unconfigured storage accepts
        /// everything and never reaches this method - see CanAccept). The
        /// default checks the exact-id list first, then - only when Catalog
        /// is wired - whether the catalog's definition for the id carries any
        /// of AllowedResourceTags. Override to replace the rule entirely
        /// (category-based, a per-project scheme) without touching Load/Unload.
        /// The <paramref name="resourceId"/> arrives already canonicalised through
        /// GridIds.Normalize (DUP-14); an override that compares against author strings
        /// must normalise its own side too.
        /// </summary>
        protected virtual bool AcceptsResourceType(string resourceId)
        {
            foreach (string allowed in AllowedResourceIds)
            {
                if (GridIds.Normalize(allowed) == resourceId)
                    return true;
            }

            if (Catalog != null && AllowedResourceTags.Count > 0
                && Catalog.Find(resourceId) is { } definition)
            {
                foreach (string tag in AllowedResourceTags)
                {
                    if (definition.HasTag(tag))
                        return true;
                }
            }

            return false;
        }

        public int Load(string resourceId, int amount)
        {
            if (amount <= 0 || !CanAccept(resourceId))
                return 0;

            string id = GridIds.Normalize(resourceId);
            int space = Mathf.Max(0, Mathf.Max(1, Capacity) - CurrentLoad);
            int taken = Mathf.Min(space, amount);
            if (taken <= 0)
                return 0;

            _stored[id] = Stored(id) + taken;
            EmitSignal(SignalName.StorageChanged, id, _stored[id], CurrentLoad);
            return taken;
        }

        public int Unload(string resourceId, int amount)
        {
            if (amount <= 0 || string.IsNullOrWhiteSpace(resourceId))
                return 0;

            string id = GridIds.Normalize(resourceId);
            int held = Stored(id);
            int released = Mathf.Min(amount, Available(id));
            if (released <= 0)
                return 0;

            int remaining = held - released;
            if (remaining <= 0)
                _stored.Remove(id);
            else
                _stored[id] = remaining;
            EmitSignal(SignalName.StorageChanged, id, remaining, CurrentLoad);
            return released;
        }

        public int Stored(string resourceId)
            => _stored.TryGetValue(GridIds.Normalize(resourceId), out int amount) ? amount : 0;

        public bool CanProvide(Godot.Collections.Array amounts)
        {
            if (!GridResourceAmount.TryTotals(amounts, out var totals)) return false;
            foreach ((string id, int amount) in totals)
                if (Available(id) < amount) return false;
            return true;
        }

        public bool TryConsume(Godot.Collections.Array amounts)
        {
            if (!GridResourceAmount.TryTotals(amounts, out var totals)) return false;
            foreach ((string id, int amount) in totals)
                if (Available(id) < amount) return false;
            foreach ((string id, int amount) in totals)
            {
                int remaining = Stored(id) - amount;
                if (remaining == 0) _stored.Remove(id);
                else _stored[id] = remaining;
            }
            foreach (string id in totals.Keys)
                EmitSignal(SignalName.StorageChanged, id, Stored(id), CurrentLoad);
            return true;
        }

        public Godot.Collections.Array<string> StoredIds()
        {
            var ids = new Godot.Collections.Array<string>();
            foreach (string id in _stored.Keys)
                ids.Add(id);
            return ids;
        }

        public Godot.Collections.Dictionary CaptureState()
        {
            var contents = new Godot.Collections.Dictionary();
            foreach ((string id, int amount) in _stored)
                contents[id] = amount;
            return new Godot.Collections.Dictionary { ["contents"] = contents };
        }

        public void RestoreState(Godot.Collections.Dictionary state)
        {
            ClearMaterialClaims();
            _stored.Clear();
            if (!state.ContainsKey("contents")
                || !GridVariantReader.TryDictionary(state["contents"], out Godot.Collections.Dictionary contents))
                return;

            foreach (Variant key in contents.Keys)
            {
                // Normalise on load so an older save written with space-kept keys ("Iron Ore")
                // migrates to the canonical form without a separate migration pass (DUP-14).
                string id = GridIds.Normalize(key.AsString());
                int amount = GridVariantReader.Int(contents[key], 0);
                if (id.Length > 0 && amount > 0)
                    _stored[id] = amount;
            }
        }

        // Explicit interface implementations, so the cargo Load(resourceId,
        // amount) is the only "Load" Godot's name-based Call dispatch can see
        // - an overload against ISaveable.Load(state) would make every duck
        // hand-off ambiguous.
        void ISaveable.Save(GameBuilder.GameStateData state)
        {
            if (!string.IsNullOrWhiteSpace(SaveKey))
                state.GameData[SaveKey] = CaptureState();
        }

        void ISaveable.Load(GameBuilder.GameStateData state)
        {
            if (string.IsNullOrWhiteSpace(SaveKey))
                return;

            if (state.GameData.TryGetValue(SaveKey, out Variant value)
                && GridVariantReader.TryDictionary(value, out Godot.Collections.Dictionary saved))
                RestoreState(saved);
        }
    }
}

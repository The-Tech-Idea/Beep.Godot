using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// What an entity is currently wearing/wielding — runtime state, one item per
    /// <see cref="EquipSlot"/>. It CONTRIBUTES the equipped items' stat modifiers to the entity's
    /// <see cref="StatsComponent"/> and withdraws them on unequip; it exposes no DamageBonus/
    /// DefenseBonus accessors, because nothing asks it anything — whoever computes damage reads the
    /// entity's stats. That is why a new stat never needs a new accessor here.
    ///
    /// Blind — no parent-type requirement; equipment is data on any node. Implements ISaveable
    /// (persists slot → item id, re-resolves via the catalog); opt a single entity's component in
    /// with <see cref="ParticipatesInSave"/>, since GameStateData is player-centric.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class EquipmentComponent : GameplayComponent, ISaveable
    {
        /// <summary>Items to equip on ready, so a template can ship an entity pre-armed.</summary>
        [Export] public GameEquipment[] StartingEquipment { get; set; } = System.Array.Empty<GameEquipment>();

        /// <summary>Include this entity's equipment in saves. Tick it on the player only.</summary>
        [Export] public bool ParticipatesInSave { get; set; } = false;

        [Signal] public delegate void EquippedEventHandler(int slot, GameEquipment item);
        [Signal] public delegate void UnequippedEventHandler(int slot, GameEquipment item);

        private readonly Dictionary<EquipSlot, GameEquipment> _slots = new();
        private StatsComponent? _stats;

        /// <summary>The weapon in MainHand, or null — the common query.</summary>
        public GameWeapon? MainWeapon => Get(EquipSlot.MainHand) as GameWeapon;

        public override void _Ready()
        {
            base._Ready();
            if (ParticipatesInSave) AddToGroup(SaveableHelper.Group);
            _stats = GetSiblingComponent<StatsComponent>();
            if (Engine.IsEditorHint()) return;
            foreach (var item in StartingEquipment)
                if (item != null) Equip(item);
        }

        /// <summary>Equip an item into its slot, returning whatever it displaced (or null). Its
        /// modifiers begin contributing immediately.</summary>
        public GameEquipment? Equip(GameEquipment item)
        {
            if (item == null) return null;
            var displaced = Unequip(item.Slot);
            _slots[item.Slot] = item;
            Contribute(item);
            EmitSignal(SignalName.Equipped, (int)item.Slot, item);
            return displaced;
        }

        /// <summary>Remove whatever is in a slot, returning it (or null). Its modifiers are
        /// withdrawn by source, so the entity's stats fall back to exactly what they were.</summary>
        public GameEquipment? Unequip(EquipSlot slot)
        {
            if (!_slots.TryGetValue(slot, out var item)) return null;
            _slots.Remove(slot);
            _stats?.RemoveBySource(item);
            EmitSignal(SignalName.Unequipped, (int)slot, item);
            return item;
        }

        public GameEquipment? Get(EquipSlot slot) => _slots.TryGetValue(slot, out var i) ? i : null;

        /// <summary>Every currently equipped piece, for consumers that aggregate across slots —
        /// e.g. ResistanceComponent multiplying in each piece's per-type resistance.</summary>
        public IEnumerable<GameEquipment> EquippedItems => _slots.Values;

        private void Contribute(GameEquipment item)
        {
            // Authored Modifiers are shared on the .tres, so DUPLICATE them per wearer (two entities
            // holding the same sword must not share modifier state, and ticking must not
            // double-decrement one instance). Intrinsic modifiers are built fresh per call, so they
            // are added directly. Both carry Source = item, so Unequip removes exactly these by identity.
            var toAdd = new List<StatModifier>();
            foreach (var mod in item.Modifiers)
                if (mod != null) { var c = (StatModifier)mod.Duplicate(); c.Source = item; toAdd.Add(c); }
            foreach (var mod in item.GetIntrinsicModifiers())
                if (mod != null) { mod.Source = item; toAdd.Add(mod); }

            if (toAdd.Count == 0) return;
            if (_stats == null)
            {
                GD.PushWarning(
                    $"[{Name}] '{item.DisplayName}' contributes {toAdd.Count} stat modifier(s) " +
                    "but there is no sibling StatsComponent — they do nothing. Add a StatsComponent to this entity.");
                return;
            }
            foreach (var m in toAdd) _stats.AddModifier(m);
        }

        // ── ISaveable ──
        public void Save(GameBuilder.GameStateData state)
        {
            var dict = new Godot.Collections.Dictionary();
            foreach (var (slot, item) in _slots)
                dict[slot.ToString()] = item.Id;
            state.GameData["equipment"] = dict;
        }

        public void Load(GameBuilder.GameStateData state)
        {
            // Withdraw whatever _Ready equipped, then restore the saved loadout.
            foreach (var slot in new List<EquipSlot>(_slots.Keys)) Unequip(slot);
            if (!state.GameData.TryGetValue("equipment", out var v)) return;
            var dict = v.AsGodotDictionary();
            foreach (var key in dict.Keys)
            {
                if (System.Enum.TryParse<EquipSlot>(key.AsString(), out _)
                    && GameItemCatalog.Resolve(dict[key].AsString()) is GameEquipment item)
                    Equip(item);
            }
        }
    }
}

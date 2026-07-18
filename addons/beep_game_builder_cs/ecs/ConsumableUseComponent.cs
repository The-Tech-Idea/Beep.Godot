using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Uses consumable items — the missing consumer for <see cref="GameConsumable"/>
    /// and <see cref="GameLiquid"/>. Their <c>HealAmount</c>/<c>StatusEffectId</c> were
    /// authorable but inert: nothing read them, so a potion or food was just data. Attach to a
    /// character alongside its Health/StatusEffect/Inventory.
    ///
    /// <see cref="Use"/> applies a consumable's effects; <see cref="UseFromInventory"/> finds one
    /// in the sibling inventory, uses it, and removes it. WHEN the game calls these (a hotbar key,
    /// a right-click) is the game's — this provides the mechanism the item tree was missing.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ConsumableUseComponent : GameplayComponent
    {
        /// <summary>Default status-effect duration for a consumable that names an effect but no
        /// duration (GameLiquid has no Duration field of its own).</summary>
        [Export] public float DefaultEffectDuration { get; set; } = 5f;

        [Signal] public delegate void ConsumedEventHandler(string itemId);

        private HealthComponent? _health;
        private StatusEffectComponent? _statusEffects;
        private InventoryComponent? _inventory;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            _health = GetSiblingComponent<HealthComponent>();
            _statusEffects = GetSiblingComponent<StatusEffectComponent>();
            _inventory = GetSiblingComponent<InventoryComponent>();
        }

        /// <summary>Apply an item's consumable effects (heal + timed status). Returns false if the
        /// item isn't a usable consumable/liquid.</summary>
        public bool Use(GameItem? item)
        {
            switch (item)
            {
                case GameConsumable c:
                    ApplyEffects(c.HealAmount, c.StatusEffectId, c.Duration);
                    EmitSignal(SignalName.Consumed, c.Id);
                    return true;
                case GameLiquid { IsDrinkable: true } l:
                    ApplyEffects(l.HealAmount, l.StatusEffectId, DefaultEffectDuration);
                    EmitSignal(SignalName.Consumed, l.Id);
                    return true;
                default:
                    return false;   // weapons, armor, non-drinkable liquids (fuel/oil) aren't consumed
            }
        }

        /// <summary>Use a consumable held in the sibling inventory, then remove one from the bag.
        /// Returns false if there's no inventory, the item isn't held, or it isn't consumable.</summary>
        public bool UseFromInventory(string itemId)
        {
            if (_inventory == null)
            {
                GD.PushWarning($"[{Name}] UseFromInventory needs a sibling InventoryComponent — none found.");
                return false;
            }
            var item = FindItem(itemId);
            if (item == null || !Use(item)) return false;
            _inventory.RemoveItem(itemId);
            return true;
        }

        private void ApplyEffects(float healAmount, string statusEffectId, float duration)
        {
            if (healAmount > 0f)
            {
                if (_health != null) _health.Heal(healAmount);
                else GD.PushWarning($"[{Name}] Consumable heals {healAmount} but there is no sibling HealthComponent to heal.");
            }
            if (!string.IsNullOrEmpty(statusEffectId))
            {
                if (_statusEffects != null) _statusEffects.ApplyEffect(statusEffectId, duration > 0f ? duration : DefaultEffectDuration);
                else GD.PushWarning($"[{Name}] Consumable applies '{statusEffectId}' but there is no sibling StatusEffectComponent.");
            }
        }

        private GameItem? FindItem(string itemId)
        {
            if (_inventory?.Slots == null) return null;
            foreach (var slot in _inventory.Slots)
                if (slot?.Item != null && slot.Item.Id == itemId)
                    return slot.Item;
            return null;
        }
    }
}

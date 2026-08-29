using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Survival mechanic: hunger, thirst, and stamina tracking.
    /// Hunger/thirst decrease over time, faster when moving or in extreme temperatures.
    /// Stamina regenerates during rest. Critical levels apply negative status effects.
    /// Integrates with StatusEffectComponent for debuffs and TemperatureComponent
    /// for temperature-based drain modifiers.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class HungerStaminaComponent : GameplayComponent
    {
        [ExportGroup("Current Values (0-100)")]
        [Export] public float CurrentHunger { get; set; } = 100f;
        [Export] public float CurrentThirst { get; set; } = 100f;
        [Export] public float CurrentStamina { get; set; } = 100f;

        [ExportGroup("Depletion Rates")]
        [Export] public float HungerDepletePerSecond { get; set; } = 2f;    // % per second when idle
        [Export] public float ThirstDepletePerSecond { get; set; } = 3f;   // % per second
        [Export] public float StaminaDepleteWhenMoving { get; set; } = 15f;  // % per second while moving
        [Export] public float MovementThreshold { get; set; } = 10f;  // velocity magnitude to trigger "moving"

        [ExportGroup("Recovery Rates")]
        [Export] public float HungerRecoverPerSecond { get; set; } = 5f;   // % per second with food
        [Export] public float ThirstRecoverPerSecond { get; set; } = 8f;   // % per second with water
        [Export] public float StaminaRecoverPerSecond { get; set; } = 25f; // % per second at rest

        [ExportGroup("Critical Thresholds")]
        [Export] public float HungerCriticalLevel { get; set; } = 20f;  // Apply hungry debuff below this
        [Export] public float ThirstCriticalLevel { get; set; } = 15f;  // Apply thirsty debuff below this
        [Export] public float StaminaCriticalLevel { get; set; } = 10f; // Can't run below this

        [ExportGroup("Temperature Integration")]
        [Export] public bool TemperatureAffectsHunger { get; set; } = true;
        /// <summary>Whether overheating accelerates thirst drain. Separate from
        /// <see cref="TemperatureAffectsHunger"/> — thirst used to ride the hunger flag, so turning
        /// hunger's temperature effect off silently also disabled overheat thirst.</summary>
        [Export] public bool TemperatureAffectsThirst { get; set; } = true;
        [Export] public float ColdHungerMultiplier { get; set; } = 1.5f;    // 150% drain in cold
        [Export] public float OverheatThirstMultiplier { get; set; } = 1.5f; // 150% drain when overheating

        [Signal] public delegate void HungerChangedEventHandler(float value);
        [Signal] public delegate void ThirstChangedEventHandler(float value);
        [Signal] public delegate void StaminaChangedEventHandler(float value);
        [Signal] public delegate void HungerCriticalEventHandler();
        [Signal] public delegate void ThirstCriticalEventHandler();
        [Signal] public delegate void StaminaCriticalEventHandler();

        private TemperatureComponent? _temperature;
        private StatusEffectComponent? _statusEffects;
        private CharacterBody2D? _body;
        private bool _hungerDebuffActive;
        private bool _thirstDebuffActive;
        // Last emitted values, so the *Changed signals fire only on an actual change — not every
        // frame while a value is pinned at 0/100 (starved, or resting at full stamina).
        private float _lastHunger = float.NaN;
        private float _lastThirst = float.NaN;
        private float _lastStamina = float.NaN;

        public float EffectiveHungerDepletePerSecond => NonNegative(HungerDepletePerSecond);
        public float EffectiveThirstDepletePerSecond => NonNegative(ThirstDepletePerSecond);
        public float EffectiveStaminaDepleteWhenMoving => NonNegative(StaminaDepleteWhenMoving);
        public float EffectiveMovementThreshold => NonNegative(MovementThreshold);
        public float EffectiveHungerRecoverPerSecond => NonNegative(HungerRecoverPerSecond);
        public float EffectiveThirstRecoverPerSecond => NonNegative(ThirstRecoverPerSecond);
        public float EffectiveStaminaRecoverPerSecond => NonNegative(StaminaRecoverPerSecond);
        public float EffectiveHungerCriticalLevel => ClampPercent(HungerCriticalLevel, 20f);
        public float EffectiveThirstCriticalLevel => ClampPercent(ThirstCriticalLevel, 15f);
        public float EffectiveStaminaCriticalLevel => ClampPercent(StaminaCriticalLevel, 10f);
        public float EffectiveColdHungerMultiplier => NonNegative(ColdHungerMultiplier);
        public float EffectiveOverheatThirstMultiplier => NonNegative(OverheatThirstMultiplier);

        public override void _Ready()
        {
            base._Ready();
            _temperature = GetSiblingComponent<TemperatureComponent>();
            _statusEffects = GetSiblingComponent<StatusEffectComponent>();
            _body = GetParent() as CharacterBody2D;
            if (!Engine.IsEditorHint() && _body == null)
                GD.PushWarning($"[{Name}] HungerStaminaComponent has no CharacterBody2D parent, so movement-based drain (faster hunger/thirst when moving, stamina spend) can't detect motion — 'moving' stays false. Parent it to the body if you want movement to matter.");
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint() || !IsActive) return;

            float dt = double.IsFinite(delta) ? Mathf.Max(0f, (float)delta) : 0f;
            NormalizeValues();
            bool isMoving = _body != null && IsFinite(_body.Velocity) && _body.Velocity.Length() > EffectiveMovementThreshold;

            // Apply depletion
            ApplyHungerDepletion(dt, isMoving);
            ApplyThirstDepletion(dt, isMoving);
            ApplyStaminaDepletion(dt, isMoving);

            // Clamp values
            NormalizeValues();

            // Emit only when the value actually changed (skips the frames where it's clamped).
            EmitChangedSignals();

            // Check critical levels
            CheckCriticalLevels();
        }

        private void ApplyHungerDepletion(float dt, bool isMoving)
        {
            float depleteRate = EffectiveHungerDepletePerSecond;
            if (isMoving) depleteRate *= 1.3f;  // 30% more hunger when active

            // Cold increases hunger
            float tempModifier = 1.0f;
            if (TemperatureAffectsHunger && _temperature != null)
            {
                tempModifier = _temperature.GetTemperatureState() switch
                {
                    TemperatureComponent.TemperatureState.Cold => EffectiveColdHungerMultiplier,
                    TemperatureComponent.TemperatureState.Frozen => EffectiveColdHungerMultiplier * 1.5f,
                    _ => 1.0f
                };
            }

            CurrentHunger -= depleteRate * tempModifier * dt;
        }

        private void ApplyThirstDepletion(float dt, bool isMoving)
        {
            float depleteRate = EffectiveThirstDepletePerSecond;
            if (isMoving) depleteRate *= 1.3f;  // 30% more thirst when active

            // Overheating increases thirst
            float tempModifier = 1.0f;
            if (TemperatureAffectsThirst && _temperature != null)
            {
                tempModifier = _temperature.GetTemperatureState() switch
                {
                    TemperatureComponent.TemperatureState.Overheating => EffectiveOverheatThirstMultiplier,
                    TemperatureComponent.TemperatureState.HeatStroke => EffectiveOverheatThirstMultiplier * 2f,
                    _ => 1.0f
                };
            }

            CurrentThirst -= depleteRate * tempModifier * dt;
        }

        private void ApplyStaminaDepletion(float dt, bool isMoving)
        {
            if (isMoving)
            {
                CurrentStamina -= EffectiveStaminaDepleteWhenMoving * dt;
            }
            else
            {
                // Regenerate stamina at rest
                CurrentStamina += EffectiveStaminaRecoverPerSecond * dt;
            }
        }

        private void CheckCriticalLevels()
        {
            // Hunger critical
            if (CurrentHunger <= EffectiveHungerCriticalLevel && !_hungerDebuffActive)
            {
                _hungerDebuffActive = true;
                _statusEffects?.ApplyEffect("hungry", 999f, 0.5f, isBuff: false,
                    stackBehavior: StatusEffectComponent.StackBehavior.Refresh);
                EmitSignal(SignalName.HungerCritical);
            }
            else if (CurrentHunger > EffectiveHungerCriticalLevel && _hungerDebuffActive)
            {
                _hungerDebuffActive = false;
                _statusEffects?.RemoveEffect("hungry");
            }

            // Thirst critical
            if (CurrentThirst <= EffectiveThirstCriticalLevel && !_thirstDebuffActive)
            {
                _thirstDebuffActive = true;
                _statusEffects?.ApplyEffect("thirsty", 999f, 0.5f, isBuff: false,
                    stackBehavior: StatusEffectComponent.StackBehavior.Refresh);
                EmitSignal(SignalName.ThirstCritical);
            }
            else if (CurrentThirst > EffectiveThirstCriticalLevel && _thirstDebuffActive)
            {
                _thirstDebuffActive = false;
                _statusEffects?.RemoveEffect("thirsty");
            }

            // Stamina critical — latch like hunger/thirst so it fires once on crossing the
            // threshold, not every frame it stays low (which spammed any SFX/UI listener).
            if (CurrentStamina <= EffectiveStaminaCriticalLevel)
            {
                if (!_staminaCriticalActive) { _staminaCriticalActive = true; EmitSignal(SignalName.StaminaCritical); }
            }
            else _staminaCriticalActive = false;
        }

        private bool _staminaCriticalActive;

        /// <summary>Consume food to restore hunger.</summary>
        public void ConsumeFood(float hungerRestore)
        {
            if (!float.IsFinite(hungerRestore) || hungerRestore <= 0f) return;
            NormalizeValues();
            CurrentHunger = Mathf.Clamp(CurrentHunger + hungerRestore, 0f, 100f);
            EmitChangedSignals();
            CheckCriticalLevels();
        }

        /// <summary>Drink water to restore thirst.</summary>
        public void DrinkWater(float thirstRestore)
        {
            if (!float.IsFinite(thirstRestore) || thirstRestore <= 0f) return;
            NormalizeValues();
            CurrentThirst = Mathf.Clamp(CurrentThirst + thirstRestore, 0f, 100f);
            EmitChangedSignals();
            CheckCriticalLevels();
        }

        /// <summary>Rest to restore stamina (called by rest mechanic).</summary>
        public void Rest(float duration)
        {
            if (!float.IsFinite(duration) || duration <= 0f) return;
            NormalizeValues();
            CurrentStamina = Mathf.Clamp(CurrentStamina + (EffectiveStaminaRecoverPerSecond * duration), 0f, 100f);
            EmitChangedSignals();
            CheckCriticalLevels();
        }

        /// <summary>Spend stamina for an action (e.g. a dash/sprint). Refuses — returns false —
        /// when exhausted or the cost can't be paid, so an ability can actually gate on stamina.
        /// This is the reader the "can't run below this" / IsExhausted stamina system was missing;
        /// nothing consumed stamina for a discrete action before, so the gate never applied.</summary>
        public bool TryConsumeStamina(float amount)
        {
            if (!float.IsFinite(amount)) return false;
            if (amount <= 0f) return true;
            NormalizeValues();
            if (IsExhausted || CurrentStamina < amount) return false;
            CurrentStamina -= amount;
            _lastStamina = CurrentStamina;
            EmitSignal(SignalName.StaminaChanged, CurrentStamina);
            // Route through the same latch CheckCriticalLevels uses, so StaminaCritical stays
            // edge-triggered and doesn't double-fire with the _Process check next frame.
            if (CurrentStamina <= EffectiveStaminaCriticalLevel && !_staminaCriticalActive)
            {
                _staminaCriticalActive = true;
                EmitSignal(SignalName.StaminaCritical);
            }
            return true;
        }

        private void NormalizeValues()
        {
            CurrentHunger = ClampPercent(CurrentHunger, 100f);
            CurrentThirst = ClampPercent(CurrentThirst, 100f);
            CurrentStamina = ClampPercent(CurrentStamina, 100f);
        }

        private void EmitChangedSignals()
        {
            if (!Mathf.IsEqualApprox(CurrentHunger, _lastHunger))
            {
                _lastHunger = CurrentHunger;
                EmitSignal(SignalName.HungerChanged, CurrentHunger);
            }
            if (!Mathf.IsEqualApprox(CurrentThirst, _lastThirst))
            {
                _lastThirst = CurrentThirst;
                EmitSignal(SignalName.ThirstChanged, CurrentThirst);
            }
            if (!Mathf.IsEqualApprox(CurrentStamina, _lastStamina))
            {
                _lastStamina = CurrentStamina;
                EmitSignal(SignalName.StaminaChanged, CurrentStamina);
            }
        }

        public bool IsHungry => CurrentHunger <= EffectiveHungerCriticalLevel;
        public bool IsThirsty => CurrentThirst <= EffectiveThirstCriticalLevel;
        public bool IsExhausted => CurrentStamina <= EffectiveStaminaCriticalLevel;

        private static float NonNegative(float value) => float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;

        private static float ClampPercent(float value, float fallback) => float.IsFinite(value) ? Mathf.Clamp(value, 0f, 100f) : fallback;

        private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}

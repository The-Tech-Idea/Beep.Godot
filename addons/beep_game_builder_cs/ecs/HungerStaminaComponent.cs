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

        public override void _Ready()
        {
            base._Ready();
            _temperature = GetSiblingComponent<TemperatureComponent>();
            _statusEffects = GetSiblingComponent<StatusEffectComponent>();
            _body = GetParent() as CharacterBody2D;
        }

        public override void _Process(double delta)
        {
            if (!IsActive) return;

            float dt = (float)delta;
            bool isMoving = _body != null && _body.Velocity.Length() > MovementThreshold;

            // Apply depletion
            ApplyHungerDepletion(dt, isMoving);
            ApplyThirstDepletion(dt, isMoving);
            ApplyStaminaDepletion(dt, isMoving);

            // Clamp values
            CurrentHunger = Mathf.Clamp(CurrentHunger, 0f, 100f);
            CurrentThirst = Mathf.Clamp(CurrentThirst, 0f, 100f);
            CurrentStamina = Mathf.Clamp(CurrentStamina, 0f, 100f);

            // Emit signals
            EmitSignal(SignalName.HungerChanged, CurrentHunger);
            EmitSignal(SignalName.ThirstChanged, CurrentThirst);
            EmitSignal(SignalName.StaminaChanged, CurrentStamina);

            // Check critical levels
            CheckCriticalLevels();
        }

        private void ApplyHungerDepletion(float dt, bool isMoving)
        {
            float depleteRate = HungerDepletePerSecond;
            if (isMoving) depleteRate *= 1.3f;  // 30% more hunger when active

            // Cold increases hunger
            float tempModifier = 1.0f;
            if (TemperatureAffectsHunger && _temperature != null)
            {
                tempModifier = _temperature.GetTemperatureState() switch
                {
                    TemperatureComponent.TemperatureState.Cold => ColdHungerMultiplier,
                    TemperatureComponent.TemperatureState.Frozen => ColdHungerMultiplier * 1.5f,
                    _ => 1.0f
                };
            }

            CurrentHunger -= depleteRate * tempModifier * dt;
        }

        private void ApplyThirstDepletion(float dt, bool isMoving)
        {
            float depleteRate = ThirstDepletePerSecond;
            if (isMoving) depleteRate *= 1.3f;  // 30% more thirst when active

            // Overheating increases thirst
            float tempModifier = 1.0f;
            if (TemperatureAffectsHunger && _temperature != null)
            {
                tempModifier = _temperature.GetTemperatureState() switch
                {
                    TemperatureComponent.TemperatureState.Overheating => OverheatThirstMultiplier,
                    TemperatureComponent.TemperatureState.HeatStroke => OverheatThirstMultiplier * 2f,
                    _ => 1.0f
                };
            }

            CurrentThirst -= depleteRate * tempModifier * dt;
        }

        private void ApplyStaminaDepletion(float dt, bool isMoving)
        {
            if (isMoving)
            {
                CurrentStamina -= StaminaDepleteWhenMoving * dt;
            }
            else
            {
                // Regenerate stamina at rest
                CurrentStamina += StaminaRecoverPerSecond * dt;
            }
        }

        private void CheckCriticalLevels()
        {
            // Hunger critical
            if (CurrentHunger <= HungerCriticalLevel && !_hungerDebuffActive)
            {
                _hungerDebuffActive = true;
                _statusEffects?.ApplyEffect("hungry", 999f, 0.5f, isBuff: false,
                    stackBehavior: StatusEffectComponent.StackBehavior.Refresh);
                EmitSignal(SignalName.HungerCritical);
            }
            else if (CurrentHunger > HungerCriticalLevel && _hungerDebuffActive)
            {
                _hungerDebuffActive = false;
                _statusEffects?.RemoveEffect("hungry");
            }

            // Thirst critical
            if (CurrentThirst <= ThirstCriticalLevel && !_thirstDebuffActive)
            {
                _thirstDebuffActive = true;
                _statusEffects?.ApplyEffect("thirsty", 999f, 0.5f, isBuff: false,
                    stackBehavior: StatusEffectComponent.StackBehavior.Refresh);
                EmitSignal(SignalName.ThirstCritical);
            }
            else if (CurrentThirst > ThirstCriticalLevel && _thirstDebuffActive)
            {
                _thirstDebuffActive = false;
                _statusEffects?.RemoveEffect("thirsty");
            }

            // Stamina critical
            if (CurrentStamina <= StaminaCriticalLevel)
            {
                EmitSignal(SignalName.StaminaCritical);
            }
        }

        /// <summary>Consume food to restore hunger.</summary>
        public void ConsumeFood(float hungerRestore)
        {
            CurrentHunger = Mathf.Min(CurrentHunger + hungerRestore, 100f);
        }

        /// <summary>Drink water to restore thirst.</summary>
        public void DrinkWater(float thirstRestore)
        {
            CurrentThirst = Mathf.Min(CurrentThirst + thirstRestore, 100f);
        }

        /// <summary>Rest to restore stamina (called by rest mechanic).</summary>
        public void Rest(float duration)
        {
            CurrentStamina = Mathf.Min(CurrentStamina + (StaminaRecoverPerSecond * duration), 100f);
        }

        public bool IsHungry => CurrentHunger <= HungerCriticalLevel;
        public bool IsThirsty => CurrentThirst <= ThirstCriticalLevel;
        public bool IsExhausted => CurrentStamina <= StaminaCriticalLevel;
    }
}

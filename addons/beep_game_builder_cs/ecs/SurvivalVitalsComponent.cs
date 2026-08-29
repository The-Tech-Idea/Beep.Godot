using Godot;
using System.Globalization;

namespace Beep.ECS
{
    /// <summary>
    /// The survival genre's simulation: health, hunger, thirst and stamina, and the way they
    /// feed each other.
    ///
    /// This exists because <c>SurvivalHudComponent</c> registered all four readouts as
    /// <c>Placeholder(...)</c> — the HUD showed whatever text was typed into the scene, so the
    /// numbers a player saw were invented and never changed. Eight of the ten genres are in that
    /// state; this is the second genre (after <see cref="CityEconomyComponent"/>) to get a real
    /// one, and it follows that component's shape deliberately so the rest can follow the same
    /// pattern rather than each inventing its own.
    ///
    /// The interesting part of a survival sim is not four independent bars — it is the coupling:
    /// thirst runs roughly twice as fast as hunger, an empty bar does not stop at zero but starts
    /// costing health, and stamina refuses to regenerate while you are starving or parched. Four
    /// bars that only count down are a progress bar, not a survival loop.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class SurvivalVitalsComponent : GameplayComponent, ISaveable
    {
        /// <summary>Join the save walk. Declared here rather than inherited — implementing
        /// ISaveable is not enough on its own; a component must also be in the saveables group
        /// or the walk never finds it.</summary>
        [Export] public bool ParticipatesInSave { get; set; } = true;

        // ── Tuning ────────────────────────────────────────────────────────────────────────
        [Export] public float MaxHealth { get; set; } = 100f;
        [Export] public float MaxHunger { get; set; } = 100f;
        [Export] public float MaxThirst { get; set; } = 100f;
        [Export] public float MaxStamina { get; set; } = 100f;

        /// <summary>Real seconds for a full hunger bar to empty from full while idle.</summary>
        [Export] public float SecondsToStarve { get; set; } = 900f;

        /// <summary>Thirst runs faster than hunger — the standard survival relationship, and what
        /// makes water the resource a player plans around.</summary>
        [Export] public float ThirstRateMultiplier { get; set; } = 2.1f;

        /// <summary>Health lost per second while a vital is at zero.</summary>
        [Export] public float StarvationDamagePerSecond { get; set; } = 0.9f;

        /// <summary>Health regained per second when fed, watered and not exhausted.</summary>
        [Export] public float RegenPerSecond { get; set; } = 0.45f;

        [Export] public float StaminaDrainPerSecond { get; set; } = 12f;
        [Export] public float StaminaRecoverPerSecond { get; set; } = 8f;

        /// <summary>Below this fraction a vital is "low" — the threshold the HUD colours on.</summary>
        [Export(PropertyHint.Range, "0.05,0.5,0.01")] public float LowThreshold { get; set; } = 0.25f;

        // ── State ─────────────────────────────────────────────────────────────────────────
        public float Health { get; private set; }
        public float Hunger { get; private set; }
        public float Thirst { get; private set; }
        public float Stamina { get; private set; }

        /// <summary>True while the player is spending stamina (sprinting, swimming, chopping).
        /// Driven by gameplay; the sim only decides what it costs.</summary>
        public bool Exerting { get; set; }

        public bool IsDead => Health <= 0f;
        public bool IsStarving => Hunger <= 0f;
        public bool IsParched => Thirst <= 0f;
        public bool IsExhausted => Stamina <= 0f;

        public float EffectiveMaxHealth => AtLeast(MaxHealth, 1f);
        public float EffectiveMaxHunger => AtLeast(MaxHunger, 1f);
        public float EffectiveMaxThirst => AtLeast(MaxThirst, 1f);
        public float EffectiveMaxStamina => AtLeast(MaxStamina, 1f);
        public float EffectiveSecondsToStarve => NonNegative(SecondsToStarve);
        public float EffectiveThirstRateMultiplier => NonNegative(ThirstRateMultiplier);
        public float EffectiveStarvationDamagePerSecond => NonNegative(StarvationDamagePerSecond);
        public float EffectiveRegenPerSecond => NonNegative(RegenPerSecond);
        public float EffectiveStaminaDrainPerSecond => NonNegative(StaminaDrainPerSecond);
        public float EffectiveStaminaRecoverPerSecond => NonNegative(StaminaRecoverPerSecond);
        public float EffectiveLowThreshold => float.IsFinite(LowThreshold) ? Mathf.Clamp(LowThreshold, 0.01f, 1f) : 0.25f;

        public float HealthFraction => ClampFinite(Health / EffectiveMaxHealth, 0f, 1f, 1f);
        public float HungerFraction => ClampFinite(Hunger / EffectiveMaxHunger, 0f, 1f, 1f);
        public float ThirstFraction => ClampFinite(Thirst / EffectiveMaxThirst, 0f, 1f, 1f);
        public float StaminaFraction => ClampFinite(Stamina / EffectiveMaxStamina, 0f, 1f, 1f);

        [Signal] public delegate void VitalsChangedEventHandler();
        /// <summary>Raised once per transition, not per frame — a HUD toast that fired every
        /// frame while a bar sat at zero would bury the screen.</summary>
        [Signal] public delegate void VitalCriticalEventHandler(string vital);
        [Signal] public delegate void DiedEventHandler();

        private bool _wasStarving, _wasParched, _wasDead;
        private float _accum;

        /// <summary>Emit at most this often. The sim runs per frame, but a HUD that relays every
        /// frame does 60 string allocations a second for a number that moves once a second.</summary>
        private const float EmitInterval = 0.25f;

        public override void _Ready()
        {
            base._Ready();
            Health = EffectiveMaxHealth;
            Hunger = EffectiveMaxHunger;
            Thirst = EffectiveMaxThirst;
            Stamina = EffectiveMaxStamina;
            NormalizeVitals();
            if (ParticipatesInSave) AddToGroup(SaveableHelper.Group);
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint() || !IsActive || IsDead) return;
            float dt = double.IsFinite(delta) ? Mathf.Max(0f, (float)delta) : 0f;
            NormalizeVitals();

            // Rates derived from SecondsToStarve so one exported number tunes the whole loop —
            // four independent per-second rates drift apart the moment anyone edits one.
            float hungerRate = EffectiveSecondsToStarve <= 0f ? 0f : EffectiveMaxHunger / EffectiveSecondsToStarve;
            Hunger = Mathf.Max(0f, Hunger - hungerRate * dt);
            Thirst = Mathf.Max(0f, Thirst - hungerRate * EffectiveThirstRateMultiplier * dt);

            // Stamina will not recover while a vital is empty. Without this the player can
            // sprint indefinitely on an empty stomach and hunger stops meaning anything.
            if (Exerting)
                Stamina = Mathf.Max(0f, Stamina - EffectiveStaminaDrainPerSecond * dt);
            else if (!IsStarving && !IsParched)
                Stamina = Mathf.Min(EffectiveMaxStamina, Stamina + EffectiveStaminaRecoverPerSecond * dt);

            int empty = (IsStarving ? 1 : 0) + (IsParched ? 1 : 0);
            if (empty > 0)
                Health = Mathf.Max(0f, Health - EffectiveStarvationDamagePerSecond * empty * dt);
            else if (!IsExhausted)
                Health = Mathf.Min(EffectiveMaxHealth, Health + EffectiveRegenPerSecond * dt);

            NormalizeVitals();

            RaiseTransitions();

            _accum += dt;
            if (_accum >= EmitInterval)
            {
                _accum = 0f;
                EmitSignal(SignalName.VitalsChanged);
            }
        }

        /// <summary>Edge-triggered alerts. Level-triggered ones would fire every frame.</summary>
        private void RaiseTransitions()
        {
            if (IsStarving != _wasStarving)
            {
                _wasStarving = IsStarving;
                if (IsStarving) EmitSignal(SignalName.VitalCritical, "hunger");
            }
            if (IsParched != _wasParched)
            {
                _wasParched = IsParched;
                if (IsParched) EmitSignal(SignalName.VitalCritical, "thirst");
            }
            if (IsDead && !_wasDead)
            {
                _wasDead = true;
                EmitSignal(SignalName.VitalsChanged);
                EmitSignal(SignalName.Died);
            }
        }

        // ── Gameplay API ──────────────────────────────────────────────────────────────────
        public void Eat(float amount)
        {
            if (!float.IsFinite(amount) || amount <= 0f) return;
            NormalizeVitals();
            Hunger = Mathf.Min(EffectiveMaxHunger, Hunger + amount);
            Changed();
        }

        public void Drink(float amount)
        {
            if (!float.IsFinite(amount) || amount <= 0f) return;
            NormalizeVitals();
            Thirst = Mathf.Min(EffectiveMaxThirst, Thirst + amount);
            Changed();
        }

        public void Heal(float amount)
        {
            if (!float.IsFinite(amount) || amount <= 0f || IsDead) return;
            NormalizeVitals();
            Health = Mathf.Min(EffectiveMaxHealth, Health + amount);
            Changed();
        }

        private void Changed()
        {
            NormalizeVitals();
            RaiseTransitions();
            EmitSignal(SignalName.VitalsChanged);
        }

        /// <summary>Apply damage. Returns true if this killed the player, so a caller can react
        /// without re-reading state and racing the signal.</summary>
        public bool Damage(float amount)
        {
            if (!float.IsFinite(amount) || amount <= 0f || IsDead) return false;
            NormalizeVitals();
            Health = Mathf.Max(0f, Health - amount);
            Changed();
            return IsDead;
        }

        /// <summary>Restore everything — respawn, or a full night's rest.</summary>
        public void Restore()
        {
            Health = EffectiveMaxHealth; Hunger = EffectiveMaxHunger; Thirst = EffectiveMaxThirst; Stamina = EffectiveMaxStamina;
            _wasStarving = _wasParched = _wasDead = false;
            EmitSignal(SignalName.VitalsChanged);
        }

        // ── Persistence ───────────────────────────────────────────────────────────────────
        private const string KHealth = "survival.health";
        private const string KHunger = "survival.hunger";
        private const string KThirst = "survival.thirst";
        private const string KStamina = "survival.stamina";

        public void Save(GameBuilder.GameStateData state)
        {
            NormalizeVitals();
            state.GameData[KHealth] = Health;
            state.GameData[KHunger] = Hunger;
            state.GameData[KThirst] = Thirst;
            state.GameData[KStamina] = Stamina;
        }

        public void Load(GameBuilder.GameStateData state)
        {
            var d = state.GameData;
            if (d.TryGetValue(KHealth, out var h)) Health = ClampFinite(ReadFloat(h, EffectiveMaxHealth), 0f, EffectiveMaxHealth, EffectiveMaxHealth);
            if (d.TryGetValue(KHunger, out var u)) Hunger = ClampFinite(ReadFloat(u, EffectiveMaxHunger), 0f, EffectiveMaxHunger, EffectiveMaxHunger);
            if (d.TryGetValue(KThirst, out var t)) Thirst = ClampFinite(ReadFloat(t, EffectiveMaxThirst), 0f, EffectiveMaxThirst, EffectiveMaxThirst);
            if (d.TryGetValue(KStamina, out var s)) Stamina = ClampFinite(ReadFloat(s, EffectiveMaxStamina), 0f, EffectiveMaxStamina, EffectiveMaxStamina);
            NormalizeVitals();

            // Recomputed, never restored: a save that carried "was starving" could suppress the
            // alert for a state the loaded numbers no longer describe.
            _wasStarving = IsStarving;
            _wasParched = IsParched;
            _wasDead = IsDead;
            EmitSignal(SignalName.VitalsChanged);
        }

        private void NormalizeVitals()
        {
            Health = ClampFinite(Health, 0f, EffectiveMaxHealth, EffectiveMaxHealth);
            Hunger = ClampFinite(Hunger, 0f, EffectiveMaxHunger, EffectiveMaxHunger);
            Thirst = ClampFinite(Thirst, 0f, EffectiveMaxThirst, EffectiveMaxThirst);
            Stamina = ClampFinite(Stamina, 0f, EffectiveMaxStamina, EffectiveMaxStamina);
        }

        private static float NonNegative(float value) => float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;

        private static float AtLeast(float value, float minimum) => float.IsFinite(value) ? Mathf.Max(minimum, value) : minimum;

        private static float ClampFinite(float value, float min, float max, float fallback)
            => float.IsFinite(value) ? Mathf.Clamp(value, min, max) : fallback;

        private static float ReadFloat(Variant value, float fallback)
        {
            switch (value.VariantType)
            {
                case Variant.Type.Int:
                case Variant.Type.Float:
                {
                    double raw = value.AsDouble();
                    return double.IsFinite(raw) ? (float)raw : fallback;
                }
                case Variant.Type.String:
                    return float.TryParse(value.AsString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
                        && float.IsFinite(parsed)
                            ? parsed
                            : fallback;
                default:
                    return fallback;
            }
        }
    }
}

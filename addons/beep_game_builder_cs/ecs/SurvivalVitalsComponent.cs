using Godot;

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

        public float HealthFraction => MaxHealth <= 0f ? 0f : Health / MaxHealth;
        public float HungerFraction => MaxHunger <= 0f ? 0f : Hunger / MaxHunger;
        public float ThirstFraction => MaxThirst <= 0f ? 0f : Thirst / MaxThirst;
        public float StaminaFraction => MaxStamina <= 0f ? 0f : Stamina / MaxStamina;

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
            Health = MaxHealth;
            Hunger = MaxHunger;
            Thirst = MaxThirst;
            Stamina = MaxStamina;
            if (ParticipatesInSave) AddToGroup(SaveableHelper.Group);
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint() || IsDead) return;
            float dt = (float)delta;

            // Rates derived from SecondsToStarve so one exported number tunes the whole loop —
            // four independent per-second rates drift apart the moment anyone edits one.
            float hungerRate = SecondsToStarve <= 0f ? 0f : MaxHunger / SecondsToStarve;
            Hunger = Mathf.Max(0f, Hunger - hungerRate * dt);
            Thirst = Mathf.Max(0f, Thirst - hungerRate * ThirstRateMultiplier * dt);

            // Stamina will not recover while a vital is empty. Without this the player can
            // sprint indefinitely on an empty stomach and hunger stops meaning anything.
            if (Exerting)
                Stamina = Mathf.Max(0f, Stamina - StaminaDrainPerSecond * dt);
            else if (!IsStarving && !IsParched)
                Stamina = Mathf.Min(MaxStamina, Stamina + StaminaRecoverPerSecond * dt);

            int empty = (IsStarving ? 1 : 0) + (IsParched ? 1 : 0);
            if (empty > 0)
                Health = Mathf.Max(0f, Health - StarvationDamagePerSecond * empty * dt);
            else if (!IsExhausted)
                Health = Mathf.Min(MaxHealth, Health + RegenPerSecond * dt);

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
            if (amount <= 0f) return;
            Hunger = Mathf.Min(MaxHunger, Hunger + amount);
            Changed();
        }

        public void Drink(float amount)
        {
            if (amount <= 0f) return;
            Thirst = Mathf.Min(MaxThirst, Thirst + amount);
            Changed();
        }

        public void Heal(float amount)
        {
            if (amount <= 0f || IsDead) return;
            Health = Mathf.Min(MaxHealth, Health + amount);
            Changed();
        }

        private void Changed()
        {
            RaiseTransitions();
            EmitSignal(SignalName.VitalsChanged);
        }

        /// <summary>Apply damage. Returns true if this killed the player, so a caller can react
        /// without re-reading state and racing the signal.</summary>
        public bool Damage(float amount)
        {
            if (amount <= 0f || IsDead) return false;
            Health = Mathf.Max(0f, Health - amount);
            Changed();
            return IsDead;
        }

        /// <summary>Restore everything — respawn, or a full night's rest.</summary>
        public void Restore()
        {
            Health = MaxHealth; Hunger = MaxHunger; Thirst = MaxThirst; Stamina = MaxStamina;
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
            state.GameData[KHealth] = Health;
            state.GameData[KHunger] = Hunger;
            state.GameData[KThirst] = Thirst;
            state.GameData[KStamina] = Stamina;
        }

        public void Load(GameBuilder.GameStateData state)
        {
            var d = state.GameData;
            if (d.TryGetValue(KHealth, out var h)) Health = Mathf.Clamp((float)h.AsDouble(), 0f, MaxHealth);
            if (d.TryGetValue(KHunger, out var u)) Hunger = Mathf.Clamp((float)u.AsDouble(), 0f, MaxHunger);
            if (d.TryGetValue(KThirst, out var t)) Thirst = Mathf.Clamp((float)t.AsDouble(), 0f, MaxThirst);
            if (d.TryGetValue(KStamina, out var s)) Stamina = Mathf.Clamp((float)s.AsDouble(), 0f, MaxStamina);

            // Recomputed, never restored: a save that carried "was starving" could suppress the
            // alert for a state the loaded numbers no longer describe.
            _wasStarving = IsStarving;
            _wasParched = IsParched;
            _wasDead = IsDead;
            EmitSignal(SignalName.VitalsChanged);
        }
    }
}

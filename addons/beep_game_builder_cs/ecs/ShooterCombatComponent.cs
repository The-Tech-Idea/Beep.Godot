using Godot;
using System.Globalization;

namespace Beep.ECS
{
    /// <summary>
    /// The shooter genre's combat state: the weapon's magazine and reserve, and the wave the
    /// player is fighting.
    ///
    /// <c>ShooterHudComponent</c> registered Ammo and Wave as <c>Placeholder(...)</c>, so both
    /// showed whatever text was typed into the scene and never moved. Fourth genre to get a real
    /// one, following <see cref="CityEconomyComponent"/>, <see cref="SurvivalVitalsComponent"/>
    /// and <see cref="RpgPartyComponent"/>.
    ///
    /// What makes it a weapon rather than a counter:
    ///  - reloading takes TIME and can be interrupted; a reload that completes instantly removes
    ///    the only real cost of firing
    ///  - a reload moves only what the reserve actually holds, so a partial magazine is possible
    ///  - firing is refused while reloading or empty rather than silently going negative
    ///  - waves scale their enemy count, so wave 10 is not wave 1 with a different label
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ShooterCombatComponent : GameplayComponent, ISaveable
    {
        /// <summary>Join the save walk. Declared per-component, not inherited.</summary>
        [Export] public bool ParticipatesInSave { get; set; } = true;

        // ── Tuning ────────────────────────────────────────────────────────────────────────
        [Export] public int MagazineSize { get; set; } = 30;
        [Export] public int MaxReserve { get; set; } = 240;
        [Export] public float ReloadSeconds { get; set; } = 1.8f;

        [Export] public int BaseEnemiesPerWave { get; set; } = 6;
        /// <summary>Extra enemies added per wave — linear, so difficulty is readable.</summary>
        [Export] public int EnemiesAddedPerWave { get; set; } = 3;

        /// <summary>Ammo returned to the reserve when a wave is cleared, as a fraction of a
        /// magazine. Without a resupply the run ends by attrition rather than by skill.</summary>
        [Export(PropertyHint.Range, "0,4,0.25")] public float WaveResupplyMagazines { get; set; } = 1.5f;

        /// <summary>Below this fraction of a magazine the HUD warns.</summary>
        [Export(PropertyHint.Range, "0.05,0.5,0.01")] public float LowThreshold { get; set; } = 0.25f;

        // ── State ─────────────────────────────────────────────────────────────────────────
        public int Magazine { get; private set; }
        public int Reserve { get; private set; }
        public int Wave { get; private set; } = 1;
        public int EnemiesRemaining { get; private set; }
        public int EffectiveMagazineSize => Mathf.Max(1, MagazineSize);
        public int EffectiveMaxReserve => Mathf.Max(0, MaxReserve);
        public float EffectiveReloadSeconds => Mathf.Max(0f, float.IsFinite(ReloadSeconds) ? ReloadSeconds : 0f);
        public int EffectiveBaseEnemiesPerWave => Mathf.Max(1, BaseEnemiesPerWave);
        public int EffectiveEnemiesAddedPerWave => Mathf.Max(0, EnemiesAddedPerWave);
        public float EffectiveWaveResupplyMagazines => Mathf.Max(0f, float.IsFinite(WaveResupplyMagazines) ? WaveResupplyMagazines : 0f);
        public float EffectiveLowThreshold => Mathf.Clamp(float.IsFinite(LowThreshold) ? LowThreshold : 0.25f, 0.01f, 1f);

        public int EnemiesInWave => EffectiveBaseEnemiesPerWave + EffectiveEnemiesAddedPerWave * (Mathf.Max(1, Wave) - 1);

        public bool IsReloading { get; private set; }
        /// <summary>0..1 progress through the current reload, for a HUD ring or bar.</summary>
        public float ReloadProgress => IsReloading && EffectiveReloadSeconds > 0f
            ? Mathf.Clamp(_reloadElapsed / EffectiveReloadSeconds, 0f, 1f) : 0f;

        public bool IsEmpty => Magazine <= 0;
        public bool IsOutOfAmmo => Magazine <= 0 && Reserve <= 0;
        public float MagazineFraction => (float)Mathf.Clamp(Magazine, 0, EffectiveMagazineSize) / EffectiveMagazineSize;
        public float WaveFraction => EnemiesInWave <= 0 ? 0f
            : 1f - (float)EnemiesRemaining / EnemiesInWave;

        [Signal] public delegate void AmmoChangedEventHandler();
        [Signal] public delegate void WaveChangedEventHandler(int wave);
        [Signal] public delegate void WaveClearedEventHandler(int wave);
        [Signal] public delegate void ReloadStateChangedEventHandler(bool reloading);

        private float _reloadElapsed;

        public override void _Ready()
        {
            base._Ready();
            Magazine = EffectiveMagazineSize;
            Reserve = EffectiveMaxReserve;
            EnemiesRemaining = EnemiesInWave;
            if (ParticipatesInSave) AddToGroup(SaveableHelper.Group);
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint() || !IsActive || !IsReloading) return;

            _reloadElapsed += DeltaSeconds(delta);
            // Emitted every frame ONLY while reloading, so a HUD progress ring can animate.
            EmitSignal(SignalName.AmmoChanged);
            if (_reloadElapsed < EffectiveReloadSeconds) return;

            // Move only what the reserve actually holds — a partial magazine is a real state,
            // and topping up to full regardless would make the reserve meaningless.
            int want = EffectiveMagazineSize - Magazine;
            int moved = Mathf.Min(want, Reserve);
            Magazine += moved;
            Reserve -= moved;

            IsReloading = false;
            _reloadElapsed = 0f;
            EmitSignal(SignalName.ReloadStateChanged, false);
            EmitSignal(SignalName.AmmoChanged);
        }

        // ── Weapon API ────────────────────────────────────────────────────────────────────

        /// <summary>Fire one round. Returns false — changing nothing — when empty or mid-reload,
        /// so a caller can play a dry-fire click instead of discovering a negative magazine.</summary>
        public bool Fire()
        {
            if (!IsActive || IsReloading || Magazine <= 0) return false;
            Magazine--;
            EmitSignal(SignalName.AmmoChanged);
            return true;
        }

        /// <summary>Begin a reload. Refused when already reloading, already full, or the reserve
        /// is empty — each of those would otherwise start a timer that achieves nothing.</summary>
        public bool BeginReload()
        {
            if (!IsActive || IsReloading || Magazine >= EffectiveMagazineSize || Reserve <= 0) return false;
            if (EffectiveReloadSeconds <= 0f)
            {
                int want = EffectiveMagazineSize - Magazine;
                int moved = Mathf.Min(want, Reserve);
                Magazine += moved;
                Reserve -= moved;
                EmitSignal(SignalName.AmmoChanged);
                return moved > 0;
            }
            IsReloading = true;
            _reloadElapsed = 0f;
            EmitSignal(SignalName.ReloadStateChanged, true);
            EmitSignal(SignalName.AmmoChanged);
            return true;
        }

        /// <summary>Interrupt a reload — sprinting, taking a hit, swapping weapons. The rounds
        /// are NOT transferred, which is what makes interruption a real cost.</summary>
        public void CancelReload()
        {
            if (!IsReloading) return;
            IsReloading = false;
            _reloadElapsed = 0f;
            EmitSignal(SignalName.ReloadStateChanged, false);
            EmitSignal(SignalName.AmmoChanged);
        }

        public void AddAmmo(int rounds)
        {
            if (rounds <= 0) return;
            Reserve = Mathf.Min(EffectiveMaxReserve, Reserve + rounds);
            EmitSignal(SignalName.AmmoChanged);
        }

        // ── Waves ─────────────────────────────────────────────────────────────────────────

        /// <summary>Register a kill. Clearing the wave advances and resupplies.</summary>
        public void RegisterKill(int count = 1)
        {
            if (EnemiesRemaining <= 0) return;
            EnemiesRemaining = Mathf.Max(0, EnemiesRemaining - Mathf.Max(1, count));
            EmitSignal(SignalName.AmmoChanged);   // the wave readout shares the HUD refresh
            if (EnemiesRemaining > 0) return;

            EmitSignal(SignalName.WaveCleared, Wave);
            Wave++;
            EnemiesRemaining = EnemiesInWave;
            AddAmmo(Mathf.RoundToInt(EffectiveMagazineSize * EffectiveWaveResupplyMagazines));
            EmitSignal(SignalName.WaveChanged, Wave);
        }

        /// <summary>Restart at wave 1 with a full loadout.</summary>
        public void ResetRun()
        {
            CancelReload();
            Wave = 1;
            EnemiesRemaining = EnemiesInWave;
            Magazine = EffectiveMagazineSize;
            Reserve = EffectiveMaxReserve;
            EmitSignal(SignalName.WaveChanged, Wave);
            EmitSignal(SignalName.AmmoChanged);
        }

        // ── Persistence ───────────────────────────────────────────────────────────────────
        private const string KMag = "shooter.magazine";
        private const string KRes = "shooter.reserve";
        private const string KWave = "shooter.wave";
        private const string KLeft = "shooter.enemies_left";

        public void Save(GameBuilder.GameStateData state)
        {
            state.GameData[KMag] = Mathf.Clamp(Magazine, 0, EffectiveMagazineSize);
            state.GameData[KRes] = Mathf.Clamp(Reserve, 0, EffectiveMaxReserve);
            state.GameData[KWave] = Mathf.Max(1, Wave);
            state.GameData[KLeft] = Mathf.Clamp(EnemiesRemaining, 0, EnemiesInWave);
            // IsReloading is deliberately not saved: a reload is an in-progress action, and
            // restoring one would resume a timer the player never started this session.
        }

        public void Load(GameBuilder.GameStateData state)
        {
            var d = state.GameData;
            if (d.TryGetValue(KWave, out var w)) Wave = Mathf.Max(1, ReadInt(w, Wave));
            if (d.TryGetValue(KMag, out var m)) Magazine = Mathf.Clamp(ReadInt(m, Magazine), 0, EffectiveMagazineSize);
            if (d.TryGetValue(KRes, out var r)) Reserve = Mathf.Clamp(ReadInt(r, Reserve), 0, EffectiveMaxReserve);
            // Clamped against the LOADED wave's size, so a save cannot carry more enemies than
            // its own wave defines.
            if (d.TryGetValue(KLeft, out var e)) EnemiesRemaining = Mathf.Clamp(ReadInt(e, EnemiesRemaining), 0, EnemiesInWave);

            IsReloading = false;
            _reloadElapsed = 0f;
            EmitSignal(SignalName.WaveChanged, Wave);
            EmitSignal(SignalName.AmmoChanged);
        }

        private static float DeltaSeconds(double delta) =>
            double.IsFinite(delta) ? Mathf.Max(0f, (float)delta) : 0f;

        private static int ReadInt(Variant value, int fallback)
        {
            switch (value.VariantType)
            {
                case Variant.Type.Int:
                    return value.AsInt32();
                case Variant.Type.Float:
                {
                    double raw = value.AsDouble();
                    return double.IsFinite(raw) ? Mathf.RoundToInt((float)raw) : fallback;
                }
                case Variant.Type.String:
                    return int.TryParse(value.AsString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                        ? parsed
                        : fallback;
                default:
                    return fallback;
            }
        }
    }
}

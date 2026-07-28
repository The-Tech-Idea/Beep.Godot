using Godot;

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
        public int EnemiesInWave => BaseEnemiesPerWave + EnemiesAddedPerWave * (Wave - 1);

        public bool IsReloading { get; private set; }
        /// <summary>0..1 progress through the current reload, for a HUD ring or bar.</summary>
        public float ReloadProgress => IsReloading && ReloadSeconds > 0f
            ? Mathf.Clamp(_reloadElapsed / ReloadSeconds, 0f, 1f) : 0f;

        public bool IsEmpty => Magazine <= 0;
        public bool IsOutOfAmmo => Magazine <= 0 && Reserve <= 0;
        public float MagazineFraction => MagazineSize <= 0 ? 0f : (float)Magazine / MagazineSize;
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
            Magazine = MagazineSize;
            Reserve = MaxReserve;
            EnemiesRemaining = EnemiesInWave;
            if (ParticipatesInSave) AddToGroup(SaveableHelper.Group);
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint() || !IsReloading) return;

            _reloadElapsed += (float)delta;
            // Emitted every frame ONLY while reloading, so a HUD progress ring can animate.
            EmitSignal(SignalName.AmmoChanged);
            if (_reloadElapsed < ReloadSeconds) return;

            // Move only what the reserve actually holds — a partial magazine is a real state,
            // and topping up to full regardless would make the reserve meaningless.
            int want = MagazineSize - Magazine;
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
            if (IsReloading || Magazine <= 0) return false;
            Magazine--;
            EmitSignal(SignalName.AmmoChanged);
            return true;
        }

        /// <summary>Begin a reload. Refused when already reloading, already full, or the reserve
        /// is empty — each of those would otherwise start a timer that achieves nothing.</summary>
        public bool BeginReload()
        {
            if (IsReloading || Magazine >= MagazineSize || Reserve <= 0) return false;
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
            Reserve = Mathf.Min(MaxReserve, Reserve + rounds);
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
            AddAmmo(Mathf.RoundToInt(MagazineSize * WaveResupplyMagazines));
            EmitSignal(SignalName.WaveChanged, Wave);
        }

        /// <summary>Restart at wave 1 with a full loadout.</summary>
        public void ResetRun()
        {
            CancelReload();
            Wave = 1;
            EnemiesRemaining = EnemiesInWave;
            Magazine = MagazineSize;
            Reserve = MaxReserve;
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
            state.GameData[KMag] = Magazine;
            state.GameData[KRes] = Reserve;
            state.GameData[KWave] = Wave;
            state.GameData[KLeft] = EnemiesRemaining;
            // IsReloading is deliberately not saved: a reload is an in-progress action, and
            // restoring one would resume a timer the player never started this session.
        }

        public void Load(GameBuilder.GameStateData state)
        {
            var d = state.GameData;
            if (d.TryGetValue(KWave, out var w)) Wave = Mathf.Max(1, w.AsInt32());
            if (d.TryGetValue(KMag, out var m)) Magazine = Mathf.Clamp(m.AsInt32(), 0, MagazineSize);
            if (d.TryGetValue(KRes, out var r)) Reserve = Mathf.Clamp(r.AsInt32(), 0, MaxReserve);
            // Clamped against the LOADED wave's size, so a save cannot carry more enemies than
            // its own wave defines.
            if (d.TryGetValue(KLeft, out var e)) EnemiesRemaining = Mathf.Clamp(e.AsInt32(), 0, EnemiesInWave);

            IsReloading = false;
            _reloadElapsed = 0f;
            EmitSignal(SignalName.WaveChanged, Wave);
            EmitSignal(SignalName.AmmoChanged);
        }
    }
}

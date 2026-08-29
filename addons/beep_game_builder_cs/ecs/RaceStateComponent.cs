using Godot;
using System.Collections.Generic;
using System.Globalization;

namespace Beep.ECS
{
    /// <summary>
    /// The racing genre's session state: speed, lap, position and lap timing.
    ///
    /// <c>RacingHudComponent</c> registered all four as <c>Placeholder(...)</c>, so every readout
    /// showed whatever text was typed into the scene. Fifth genre to get a real one.
    ///
    /// What makes it a race rather than four readouts:
    ///  - the lap clock RUNS, per frame, and splits on each crossing rather than being reset
    ///  - a best lap is kept, and a lap that beats it is announced — that is the whole feedback
    ///    loop of a time trial
    ///  - position is derived from progress against rivals, not stored, so it cannot disagree
    ///    with the race
    ///  - the final lap and finish are distinct states; a race that just keeps counting laps has
    ///    no ending
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class RaceStateComponent : GameplayComponent, ISaveable
    {
        /// <summary>Join the save walk. Declared per-component, not inherited.</summary>
        [Export] public bool ParticipatesInSave { get; set; } = true;

        // ── Tuning ────────────────────────────────────────────────────────────────────────
        [Export] public int TotalLaps { get; set; } = 3;
        [Export] public int RivalCount { get; set; } = 7;

        /// <summary>Top speed in the unit the HUD shows.</summary>
        [Export] public float MaxSpeed { get; set; } = 240f;
        [Export] public string SpeedUnit { get; set; } = "km/h";

        // ── State ─────────────────────────────────────────────────────────────────────────
        /// <summary>Current speed. Driven by the vehicle; the sim only reports and clamps it.</summary>
        public float Speed
        {
            get => _speed;
            set
            {
                float v = Mathf.Clamp(FiniteOr(value, 0f), 0f, EffectiveMaxSpeed);
                if (Mathf.IsEqualApprox(v, _speed)) return;
                _speed = v;
                EmitSignal(SignalName.RaceChanged);
            }
        }
        private float _speed;

        public int Lap { get; private set; } = 1;
        public int Position { get; private set; } = 1;
        public float LapTime { get; private set; }
        public float TotalTime { get; private set; }
        /// <summary>Best completed lap this session, or -1 when none is finished yet.</summary>
        public float BestLap { get; private set; } = -1f;
        public bool Finished { get; private set; }

        /// <summary>True on the last lap — the cue every racing HUD flashes.</summary>
        public bool IsFinalLap => !Finished && Lap >= EffectiveTotalLaps;

        public int EffectiveTotalLaps => Mathf.Max(1, TotalLaps);
        public int EffectiveRivalCount => Mathf.Clamp(RivalCount, 0, 64);
        public float EffectiveMaxSpeed => Mathf.Max(0f, FiniteOr(MaxSpeed, 0f));

        public float SpeedFraction => EffectiveMaxSpeed <= 0f ? 0f : Mathf.Clamp(_speed / EffectiveMaxSpeed, 0f, 1f);
        public float RaceFraction => Mathf.Clamp((Lap - 1 + LapProgress) / EffectiveTotalLaps, 0f, 1f);

        /// <summary>0..1 through the current lap. Driven by the track; used for position.</summary>
        public float LapProgress
        {
            get => _lapProgress;
            set { _lapProgress = Mathf.Clamp(FiniteOr(value, 0f), 0f, 1f); RecomputePosition(); }
        }
        private float _lapProgress;

        /// <summary>Rival progress in total laps completed (fractional). The race owns these;
        /// position is DERIVED from them so it can never disagree with the field.</summary>
        private readonly List<float> _rivals = new();

        public IReadOnlyList<float> Rivals => _rivals;

        public string FormattedLapTime => Format(LapTime);
        public string FormattedBestLap => BestLap < 0f ? "--:--.---" : Format(BestLap);

        /// <summary>m:ss.mmm — the format every racing HUD uses. A bare seconds count is
        /// unreadable at a glance while driving.</summary>
        public static string Format(float seconds)
        {
            if (!float.IsFinite(seconds) || seconds < 0f) return "--:--.---";
            int total = Mathf.FloorToInt(seconds);
            int ms = Mathf.FloorToInt((seconds - total) * 1000f);
            return $"{total / 60}:{total % 60:00}.{ms:000}";
        }

        [Signal] public delegate void RaceChangedEventHandler();
        [Signal] public delegate void LapCompletedEventHandler(int lap, float lapTime);
        // Named NewBestLap, not BestLap: the source generator emits an event member per signal,
        // which would collide with the BestLap property that holds the time.
        [Signal] public delegate void NewBestLapEventHandler(float lapTime);
        [Signal] public delegate void RaceFinishedEventHandler(float totalTime);

        public override void _Ready()
        {
            base._Ready();
            ResetRivals();
            if (ParticipatesInSave) AddToGroup(SaveableHelper.Group);
        }

        private void ResetRivals()
        {
            _rivals.Clear();
            for (int i = 0; i < EffectiveRivalCount; i++) _rivals.Add(0f);
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint() || Finished) return;
            // The clock RUNS. A lap time that only updates when something else happens is not a
            // clock, and this is the readout a player watches most.
            float dt = DeltaSeconds(delta);
            LapTime += dt;
            TotalTime += dt;
            EmitSignal(SignalName.RaceChanged);
        }

        /// <summary>Report a rival's total progress in laps (2.5 = halfway through lap 3).</summary>
        public void SetRivalProgress(int index, float lapsCompleted)
        {
            if (index < 0 || index >= _rivals.Count) return;
            _rivals[index] = Mathf.Max(0f, FiniteOr(lapsCompleted, 0f));
            RecomputePosition();
        }

        private void RecomputePosition()
        {
            float mine = Lap - 1 + _lapProgress;
            int ahead = 0;
            foreach (float r in _rivals) if (r > mine) ahead++;
            int p = ahead + 1;
            if (p == Position) return;
            Position = p;
            EmitSignal(SignalName.RaceChanged);
        }

        /// <summary>Cross the finish line: split the lap, bank a best, advance or finish.</summary>
        public void CompleteLap()
        {
            if (Finished) return;

            float split = LapTime;
            EmitSignal(SignalName.LapCompleted, Lap, split);

            // Strictly less-than, so an identical time does not re-announce a best lap.
            if (BestLap < 0f || split < BestLap)
            {
                BestLap = split;
                EmitSignal(SignalName.NewBestLap, split);
            }

            if (Lap >= EffectiveTotalLaps)
            {
                Finished = true;
                _speed = 0f;
                EmitSignal(SignalName.RaceFinished, TotalTime);
                EmitSignal(SignalName.RaceChanged);
                return;
            }

            Lap++;
            LapTime = 0f;          // split, not a reset of the total
            _lapProgress = 0f;
            RecomputePosition();
            EmitSignal(SignalName.RaceChanged);
        }

        public void RestartRace()
        {
            Lap = 1; Position = 1; LapTime = 0f; TotalTime = 0f;
            BestLap = -1f; Finished = false; _speed = 0f; _lapProgress = 0f;
            ResetRivals();
            EmitSignal(SignalName.RaceChanged);
        }

        // ── Persistence ───────────────────────────────────────────────────────────────────
        private const string KLap = "racing.lap";
        private const string KTotal = "racing.total_time";
        private const string KBest = "racing.best_lap";
        private const string KFinished = "racing.finished";

        public void Save(GameBuilder.GameStateData state)
        {
            state.GameData[KLap] = Mathf.Clamp(Lap, 1, EffectiveTotalLaps);
            state.GameData[KTotal] = Mathf.Max(0f, FiniteOr(TotalTime, 0f));
            state.GameData[KBest] = BestLap < 0f ? -1f : Mathf.Max(0f, FiniteOr(BestLap, -1f));
            state.GameData[KFinished] = Finished;
            // Speed, LapTime and rival positions are live race state, not progress. Restoring a
            // mid-lap clock would resume a lap the player is not driving.
        }

        public void Load(GameBuilder.GameStateData state)
        {
            var d = state.GameData;
            if (d.TryGetValue(KLap, out var l)) Lap = Mathf.Clamp(ReadInt(l, Lap), 1, EffectiveTotalLaps);
            if (d.TryGetValue(KTotal, out var t)) TotalTime = Mathf.Max(0f, VariantFloat(t, 0f));
            if (d.TryGetValue(KBest, out var b))
            {
                float v = VariantFloat(b, -1f);
                BestLap = v < 0f ? -1f : v;
            }
            if (d.TryGetValue(KFinished, out var f)) Finished = ReadBool(f, Finished);

            _speed = 0f;
            LapTime = 0f;
            _lapProgress = 0f;
            ResetRivals();
            RecomputePosition();
            EmitSignal(SignalName.RaceChanged);
        }

        private static float DeltaSeconds(double delta) =>
            double.IsFinite(delta) ? Mathf.Max(0f, (float)delta) : 0f;

        private static float FiniteOr(float value, float fallback) =>
            float.IsFinite(value) ? value : fallback;

        private static float VariantFloat(Variant value, float fallback)
        {
            switch (value.VariantType)
            {
                case Variant.Type.Int:
                case Variant.Type.Float:
                {
                    double d = value.AsDouble();
                    return double.IsFinite(d) ? (float)d : fallback;
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

        private static bool ReadBool(Variant value, bool fallback)
        {
            switch (value.VariantType)
            {
                case Variant.Type.Bool:
                    return value.AsBool();
                case Variant.Type.Int:
                    return value.AsInt32() != 0;
                case Variant.Type.String:
                    return bool.TryParse(value.AsString(), out bool parsed) ? parsed : fallback;
                default:
                    return fallback;
            }
        }
    }
}

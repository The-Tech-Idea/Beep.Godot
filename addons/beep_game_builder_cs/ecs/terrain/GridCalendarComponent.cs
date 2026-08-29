using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Simple grid-game calendar for farming and settlement loops. It advances
    /// GridCellDataComponent crops by day, tracks season/year, and can run from
    /// real seconds or be advanced manually from a sleep/end-day screen.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridCalendarComponent : Node, ISaveable
    {
        public enum GridSeason
        {
            Spring,
            Summer,
            Fall,
            Winter
        }

        [Signal] public delegate void DayAdvancedEventHandler(int day, int season, int year);
        [Signal] public delegate void SeasonChangedEventHandler(int season, int year);
        [Signal] public delegate void YearChangedEventHandler(int year);

        [Export] public bool ParticipatesInSave { get; set; } = true;
        [Export] public string SaveKeyPrefix { get; set; } = "grid_calendar";
        [Export] public NodePath CellDataPath { get; set; } = new("");
        [Export] public bool AutoAdvance { get; set; } = false;
        [Export(PropertyHint.Range, "1,3600,1")] public float SecondsPerDay { get; set; } = 45f;
        [Export(PropertyHint.Range, "1,120,1")] public int DaysPerSeason { get; set; } = 28;
        [Export(PropertyHint.Range, "1,9999,1")] public int Year { get; private set; } = 1;
        [Export(PropertyHint.Range, "1,120,1")] public int DayOfSeason { get; private set; } = 1;
        [Export] public GridSeason Season { get; private set; } = GridSeason.Spring;

        public int AbsoluteDay { get; private set; } = 1;
        public float EffectiveSecondsPerDay => PositiveFinite(SecondsPerDay, 45f);
        public int EffectiveDaysPerSeason => Mathf.Max(1, DaysPerSeason);
        public float DayProgress
        {
            get
            {
                float seconds = EffectiveSecondsPerDay;
                float clock = float.IsFinite(_dayClock) ? Mathf.Max(0f, _dayClock) : 0f;
                return Mathf.Clamp(clock / seconds, 0f, 1f);
            }
        }

        private GridCellDataComponent? _cells;
        private float _dayClock;

        public override void _Ready()
        {
            ResolveReferences();
            if (!Engine.IsEditorHint() && ParticipatesInSave)
                AddToGroup(SaveableHelper.Group);
            SetProcess(!Engine.IsEditorHint());
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            if (ParticipatesInSave)
                RemoveFromGroup(SaveableHelper.Group);
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (!float.IsFinite(SecondsPerDay) || SecondsPerDay <= 0f)
                return new[] { "SecondsPerDay must be a finite value greater than zero." };
            if (DaysPerSeason <= 0)
                return new[] { "DaysPerSeason must be greater than zero." };
            return System.Array.Empty<string>();
        }

        public override void _Process(double delta)
        {
            if (!AutoAdvance)
                return;

            float step = DeltaSeconds(delta);
            if (step <= 0f)
                return;

            float seconds = EffectiveSecondsPerDay;
            _dayClock = (float.IsFinite(_dayClock) ? Mathf.Max(0f, _dayClock) : 0f) + step;
            if (_dayClock < seconds)
                return;

            int days = Mathf.Max(1, Mathf.FloorToInt(_dayClock / seconds));
            _dayClock -= days * seconds;
            AdvanceDay(days);
        }

        public void AdvanceDay(int days = 1)
        {
            int count = Mathf.Max(1, days);
            for (int i = 0; i < count; i++)
                AdvanceOneDay();
        }

        public void SetDate(int year, GridSeason season, int dayOfSeason)
        {
            Year = Mathf.Max(1, year);
            Season = season;
            int seasonLength = EffectiveDaysPerSeason;
            DayOfSeason = Mathf.Clamp(dayOfSeason, 1, seasonLength);
            AbsoluteDay = ((Year - 1) * seasonLength * 4) + ((int)Season * seasonLength) + DayOfSeason;
            _dayClock = 0f;
        }

        public Godot.Collections.Dictionary CaptureState()
            => new()
            {
                ["absolute_day"] = AbsoluteDay,
                ["year"] = Year,
                ["season"] = (int)Season,
                ["day_of_season"] = DayOfSeason,
                ["day_clock"] = float.IsFinite(_dayClock) ? Mathf.Max(0f, _dayClock) : 0f,
                ["days_per_season"] = EffectiveDaysPerSeason
            };

        public void RestoreState(Godot.Collections.Dictionary state)
        {
            int daysPerSeason = DictInt(state, "days_per_season", DaysPerSeason);
            DaysPerSeason = Mathf.Max(1, daysPerSeason);
            Year = Mathf.Max(1, DictInt(state, "year", 1));
            Season = (GridSeason)Mathf.Clamp(DictInt(state, "season", 0), 0, 3);
            DayOfSeason = Mathf.Clamp(DictInt(state, "day_of_season", 1), 1, EffectiveDaysPerSeason);
            AbsoluteDay = Mathf.Max(1, DictInt(state, "absolute_day", AbsoluteDayFromDate()));
            _dayClock = NonNegativeFinite(DictFloat(state, "day_clock", 0f));
        }

        public string DisplayDate()
            => $"Year {Year}, {Season} {DayOfSeason}";

        public void Save(GameBuilder.GameStateData state)
        {
            if (string.IsNullOrWhiteSpace(SaveKeyPrefix))
                return;

            state.GameData[$"{SaveKeyPrefix}.state"] = CaptureState();
        }

        public void Load(GameBuilder.GameStateData state)
        {
            if (string.IsNullOrWhiteSpace(SaveKeyPrefix))
                return;

            string key = $"{SaveKeyPrefix}.state";
            if (state.GameData.TryGetValue(key, out Variant value)
                && GridVariantReader.TryDictionary(value, out Godot.Collections.Dictionary saved))
                RestoreState(saved);
        }

        private void AdvanceOneDay()
        {
            ResolveReferences();
            _cells?.AdvanceDay();

            AbsoluteDay++;
            DayOfSeason++;

            int seasonLength = EffectiveDaysPerSeason;
            bool seasonChanged = false;
            bool yearChanged = false;
            if (DayOfSeason > seasonLength)
            {
                DayOfSeason = 1;
                Season = (GridSeason)(((int)Season + 1) % 4);
                seasonChanged = true;
                if (Season == GridSeason.Spring)
                {
                    Year++;
                    yearChanged = true;
                }
            }

            EmitSignal(SignalName.DayAdvanced, DayOfSeason, (int)Season, Year);
            if (seasonChanged)
                EmitSignal(SignalName.SeasonChanged, (int)Season, Year);
            if (yearChanged)
                EmitSignal(SignalName.YearChanged, Year);
        }

        private int AbsoluteDayFromDate()
        {
            int seasonLength = EffectiveDaysPerSeason;
            return ((Year - 1) * seasonLength * 4) + ((int)Season * seasonLength) + DayOfSeason;
        }

        private void ResolveReferences()
        {
            if (_cells == null || !GodotObject.IsInstanceValid(_cells))
                _cells = !CellDataPath.IsEmpty
                    ? GetNodeOrNull<GridCellDataComponent>(CellDataPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridCellDataComponent>(GetTree()?.CurrentScene) : null;
        }

        private static int DictInt(Godot.Collections.Dictionary dict, string key, int fallback)
            => GridVariantReader.Int(dict, key, fallback);

        private static float DictFloat(Godot.Collections.Dictionary dict, string key, float fallback)
            => GridVariantReader.Float(dict, key, fallback);

        private static float DeltaSeconds(double delta)
            => double.IsFinite(delta) && delta > 0.0 ? (float)Mathf.Min(delta, 86400.0) : 0f;

        private static float PositiveFinite(float value, float fallback)
            => float.IsFinite(value) && value > 0f ? value : fallback;

        private static float NonNegativeFinite(float value)
            => float.IsFinite(value) && value > 0f ? value : 0f;
    }
}

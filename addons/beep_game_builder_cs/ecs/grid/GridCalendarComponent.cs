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
        [Export(PropertyHint.Range, "1,120,1")] public int DaysPerSeason { get; set; } = 28;
        [Export(PropertyHint.Range, "1,9999,1")] public int Year { get; private set; } = 1;
        [Export(PropertyHint.Range, "1,120,1")] public int DayOfSeason { get; private set; } = 1;
        [Export] public GridSeason Season { get; private set; } = GridSeason.Spring;

        public int AbsoluteDay { get; private set; } = 1;
        public int EffectiveDaysPerSeason => Mathf.Max(1, DaysPerSeason);

        private GridCellDataComponent? _cells;

        public override void _Ready()
        {
            ResolveReferences();
            if (!Engine.IsEditorHint() && ParticipatesInSave)
                AddToGroup(SaveableHelper.Group);
            SetProcess(false);
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            if (ParticipatesInSave)
                RemoveFromGroup(SaveableHelper.Group);
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (DaysPerSeason <= 0)
                return new[] { "DaysPerSeason must be greater than zero." };
            return System.Array.Empty<string>();
        }

        // No _Process and no clock of its own. The calendar DERIVES from the
        // game clock: GridWorkClockComponent calls AdvanceDay when a day rolls
        // over, exactly as Freeciv advances the year inside end_turn. It used to
        // carry an AutoAdvance accumulator with its own SecondsPerDay, which was
        // a second owner of "what day is it" - enable it alongside the clock and
        // the day advances twice.

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
            // The date changed even though no day "passed": the HUD refreshes
            // its labels only on this signal, and without it a jumped or
            // loaded date stayed on screen as the old one until the next tick.
            EmitSignal(SignalName.DayAdvanced, DayOfSeason, (int)Season, Year);
        }

        public Godot.Collections.Dictionary CaptureState()
            => new()
            {
                ["absolute_day"] = AbsoluteDay,
                ["year"] = Year,
                ["season"] = (int)Season,
                ["day_of_season"] = DayOfSeason,
                // No day_clock: the fraction of a day in progress belongs to the
                // game clock, which persists it once. Storing it here too would
                // make the same fact restorable from two places.
                ["days_per_season"] = EffectiveDaysPerSeason
            };

        public void RestoreState(Godot.Collections.Dictionary state)
        {
            int daysPerSeason = GridVariantReader.Int(state, "days_per_season", DaysPerSeason);
            DaysPerSeason = Mathf.Max(1, daysPerSeason);
            Year = Mathf.Max(1, GridVariantReader.Int(state, "year", 1));
            Season = (GridSeason)Mathf.Clamp(GridVariantReader.Int(state, "season", 0), 0, 3);
            DayOfSeason = Mathf.Clamp(GridVariantReader.Int(state, "day_of_season", 1), 1, EffectiveDaysPerSeason);
            AbsoluteDay = Mathf.Max(1, GridVariantReader.Int(state, "absolute_day", AbsoluteDayFromDate()));
            // See SetDate: the restored date has to reach the HUD's labels.
            EmitSignal(SignalName.DayAdvanced, DayOfSeason, (int)Season, Year);
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
            => EntityComponent.Resolve(this, CellDataPath, ref _cells);



    }
}

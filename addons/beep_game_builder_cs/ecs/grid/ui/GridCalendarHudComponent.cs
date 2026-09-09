using Godot;
using System;
using Beep.ECS.UI.Kit;

namespace Beep.ECS
{
    /// <summary>
    /// Compact HUD panel for GridCalendarComponent. It shows the current date,
    /// optional day progress, and an optional advance-day button for farming and
    /// settlement loops.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridCalendarHudComponent : GridPanelComponent
    {
        [Signal] public delegate void AdvanceDayRequestedEventHandler();

        [Export] public NodePath CalendarPath { get; set; } = new("");
        [Export] public NodePath DateLabelPath { get; set; } = new("");
        [Export] public NodePath DayProgressPath { get; set; } = new("");
        [Export] public NodePath AdvanceButtonPath { get; set; } = new("");
        [Export] public bool ShowProgress { get; set; } = true;
        [Export] public bool ShowAdvanceButton { get; set; } = true;
        [Export] public string AdvanceButtonText { get; set; } = "Next Day";
        [Export] public Vector2 PanelMinimumSize { get; set; } = new(184, 82);

        private GridCalendarComponent? _calendar;
        private Label? _date;
        private ProgressBar? _progress;
        private Button? _advanceButton;
        private Button? _connectedAdvanceButton;

        public override void _Ready()
        {
            ResolveReferences();
            if (!Engine.IsEditorHint() || BuildInEditor)
                CallDeferred(nameof(RebuildHud));

            if (!Engine.IsEditorHint() && _calendar != null)
            {
                _calendar.DayAdvanced += OnDayAdvanced;
                _calendar.SeasonChanged += OnSeasonChanged;
                _calendar.YearChanged += OnYearChanged;
            }

            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            if (_calendar != null && GodotObject.IsInstanceValid(_calendar))
            {
                _calendar.DayAdvanced -= OnDayAdvanced;
                _calendar.SeasonChanged -= OnSeasonChanged;
                _calendar.YearChanged -= OnYearChanged;
            }

            DisconnectAdvanceButton();
        }

        public override void _Process(double delta)
        {
            // The only thing that changes continuously is the day-progress
            // fill, and only while the calendar advances on real time. Date
            // changes arrive through the calendar's signals; running the full
            // RefreshHud (labels, button wiring) every frame was waste.
            if (!ShowProgress || _progress == null)
                return;

            // The day fraction belongs to the clock, not the calendar - the
            // calendar knows which day it is, not how far through it we are.
            GridWorkClockComponent? workClock = ResolveWorkClock();
            if (workClock == null)
                return;

            _progress.Value = Mathf.RoundToInt(workClock.DayProgress01 * 100f);
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (CalendarPath.IsEmpty)
                return new[] { "CalendarPath should point to a GridCalendarComponent." };
            if (!GenerateControlsWhenPathsEmpty && !HasAuthoredControls())
                return new[] { "Set DateLabelPath/DayProgressPath/AdvanceButtonPath, add scene-authored Date/DayProgress/AdvanceDay children, or enable GenerateControlsWhenPathsEmpty." };
            return Array.Empty<string>();
        }

        public void RebuildHud()
        {
            ResolveReferences();
            if (BindExistingControls())
            {
                RefreshHud();
                return;
            }

            if (!GenerateControlsWhenPathsEmpty)
                return;

            ClearChildren();

            var panel = new PanelContainer
            {
                Name = "GeneratedCalendarHud",
                CustomMinimumSize = PanelMinimumSize,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            AddChild(panel);
            SetEditedOwner(panel);

            var layout = new VBoxContainer
            {
                Name = "Content",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            KitChrome.SetConstantOverrideIfChanged(layout, "separation", 4);
            panel.AddChild(layout);
            SetEditedOwner(layout);

            _date = new Label
            {
                Name = "Date",
                HorizontalAlignment = HorizontalAlignment.Center,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
            };
            KitChrome.SetColorOverrideIfChanged(_date, "font_color", Colors.White);
            layout.AddChild(_date);
            SetEditedOwner(_date);

            _progress = new ProgressBar
            {
                Name = "DayProgress",
                MinValue = 0,
                MaxValue = 100,
                ShowPercentage = false,
                CustomMinimumSize = new Vector2(0, 8),
                Visible = ShowProgress
            };
            layout.AddChild(_progress);
            SetEditedOwner(_progress);

            _advanceButton = new Button
            {
                Name = "AdvanceDay",
                Text = AdvanceButtonText,
                CustomMinimumSize = new Vector2(112, 30),
                Visible = ShowAdvanceButton
            };
            ConnectAdvanceButton();
            layout.AddChild(_advanceButton);
            SetEditedOwner(_advanceButton);

            RefreshHud();
        }

        public void RefreshHud()
        {
            ResolveReferences();

            if (_date != null)
                _date.Text = DateText();
            if (_progress != null)
            {
                _progress.Visible = ShowProgress;
                _progress.Value = Mathf.RoundToInt(DayProgress01() * 100f);
            }
            if (_advanceButton != null)
            {
                _advanceButton.Text = AdvanceButtonText;
                _advanceButton.Visible = ShowAdvanceButton;
                _advanceButton.Disabled = _calendar == null;
                ConnectAdvanceButton();
            }
        }

        public bool RequestAdvanceDay()
        {
            ResolveReferences();
            EmitSignal(SignalName.AdvanceDayRequested);
            if (_calendar == null)
            {
                RefreshHud();
                return false;
            }

            _calendar.AdvanceDay();
            RefreshHud();
            return true;
        }

        public string DateText()
        {
            ResolveReferences();
            return _calendar?.DisplayDate() ?? "Date unavailable";
        }

        public float DayProgress01()
            => ResolveWorkClock()?.DayProgress01 ?? 0f;

        /// <summary>
        /// The work clock, which owns the fraction of a day in progress. Found
        /// scene-wide; a scene with none simply shows no progress rather than
        /// inventing one.
        /// </summary>
        private GridWorkClockComponent? ResolveWorkClock()
            => EntityComponent.Resolve(this, new NodePath(""), ref _workClock);

        private void OnDayAdvanced(int day, int season, int year) => RefreshHud();
        private void OnSeasonChanged(int season, int year) => RefreshHud();
        private void OnYearChanged(int year) => RefreshHud();

        private GridWorkClockComponent? _workClock;

        private void ResolveReferences()
            => EntityComponent.Resolve(this, CalendarPath, ref _calendar);

        public bool UsesSceneControls()
            => !DateLabelPath.IsEmpty || !DayProgressPath.IsEmpty || !AdvanceButtonPath.IsEmpty
            || FindDateLabel() != null || FindDayProgress() != null || FindAdvanceButton() != null;

        private bool BindExistingControls()
        {
            if (!UsesSceneControls())
                return false;

            Label? date = FindDateLabel();
            ProgressBar? progress = FindDayProgress();
            Button? advanceButton = FindAdvanceButton();

            if (date == null)
                return false;
            if (ShowProgress && progress == null)
                return false;
            if (ShowAdvanceButton && advanceButton == null)
                return false;

            _date = date;
            _progress = progress;
            _advanceButton = advanceButton;
            ConnectAdvanceButton();
            return true;
        }

        private bool HasAuthoredControls()
        {
            if (FindDateLabel() == null)
                return false;
            if (ShowProgress && FindDayProgress() == null)
                return false;
            if (ShowAdvanceButton && FindAdvanceButton() == null)
                return false;
            return true;
        }

        private Label? FindDateLabel() => FindControl<Label>(DateLabelPath, "Date");

        private ProgressBar? FindDayProgress() => FindControl<ProgressBar>(DayProgressPath, "DayProgress");

        private Button? FindAdvanceButton() => FindControl<Button>(AdvanceButtonPath, "AdvanceDay");

        private void ConnectAdvanceButton()
        {
            if (_advanceButton == null)
                return;
            if (_connectedAdvanceButton == _advanceButton)
                return;

            DisconnectAdvanceButton();
            _advanceButton.Pressed += OnAdvanceButtonPressed;
            _connectedAdvanceButton = _advanceButton;
        }

        private void DisconnectAdvanceButton()
        {
            if (_connectedAdvanceButton != null && GodotObject.IsInstanceValid(_connectedAdvanceButton))
                _connectedAdvanceButton.Pressed -= OnAdvanceButtonPressed;

            _connectedAdvanceButton = null;
        }

        private void OnAdvanceButtonPressed() => RequestAdvanceDay();

        private void ClearChildren()
        {
            DisconnectAdvanceButton();
            foreach (Node child in GetChildren())
                child.QueueFree();
            _date = null;
            _progress = null;
            _advanceButton = null;
            _connectedAdvanceButton = null;
        }

    }
}

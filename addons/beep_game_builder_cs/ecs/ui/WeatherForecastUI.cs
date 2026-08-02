using Godot;
using Beep.ECS.UI.Kit;

namespace Beep.ECS
{
    /// <summary>
    /// Weather forecast display UI. Shows a 7-day weather prediction with icons,
    /// temperature, and wind speed.
    ///
    /// Attach to a Control node in the HUD. Forecast data is generated from a
    /// WeatherForecast resource (deterministic based on in-game day).
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class WeatherForecastUI : Control
    {
        [ExportGroup("Forecast")]
        [Export] public Beep.GameBuilder.WeatherForecast? ForecastData { get; set; }
        [Export] public int CurrentDay { get; set; } = 0;
        [Export] public PackedScene? ForecastItemScene { get; set; }

        [ExportGroup("Visual")]
        // Weather reads as a ROLE from the palette, not fixed Yellow/Gray/CornflowerBlue.
        // Those literals were why every card stayed the same washed-out grey in all 50 skins.
        private Color ClearColor => Beep.ECS.UI.UiSurface.Semantic(this, Beep.ECS.UI.UiSurface.Role.Warning);
        private Color CloudyColor => Beep.ECS.UI.UiSurface.Semantic(this, Beep.ECS.UI.UiSurface.Role.Neutral);
        private Color RainyColor => Beep.ECS.UI.UiSurface.Semantic(this, Beep.ECS.UI.UiSurface.Role.Info);
        private Color StormyColor => Beep.ECS.UI.UiSurface.Semantic(this, Beep.ECS.UI.UiSurface.Role.Accent2);

        [ExportGroup("Layout")]
        /// <summary>How many cards fit per row, computed from the space this control actually
        /// has. It was a fixed export, so a 7-day forecast wrapped to a second row no matter how
        /// wide the strip was — and the second row fell off the bottom of the HUD.</summary>
        private int ItemsPerRow
        {
            get
            {
                float avail = Size.X > 1f ? Size.X : CustomMinimumSize.X;
                float per = ItemSize.X + ItemSpacing;
                if (avail <= 1f || per <= 1f) return 4;
                return Mathf.Clamp(Mathf.FloorToInt((avail + ItemSpacing) / per),
                                   1, Mathf.Max(1, ForecastData?.DaysForward?.Length ?? 7));
            }
        }
        [Export] public Vector2 ItemSize { get; set; } = new(72, 88);
        [Export] public float ItemSpacing { get; set; } = 8f;

        private VBoxContainer? _forecastContainer;
        private Button? _toggle;
        private Godot.Control? _slide;      // clips the forecast so it can slide out from under
        private Tween? _tween;
        private bool _open;

        /// <summary>Start with the forecast tucked away behind its button.</summary>
        [Export] public bool StartCollapsed { get; set; } = true;
        [Export] public float SlideSeconds { get; set; } = 0.22f;
        private HBoxContainer? _currentRowContainer;

        public override void _Ready()
        {
            base._Ready();
            // SetupUI builds the forecast container and its rows. This is [Tool] and sits
            // in the genre main scenes, so without the guard opening one in the editor
            // fills it with runtime-only children.
            if (Engine.IsEditorHint()) return;
            // Hide + skip building when the genre disables the forecast.
            if (Beep.GameBuilder.GameInfo.Instance is { } info && !info.EnableWeatherForecast)
            {
                Visible = false;
                return;
            }
            SetupUI();
            _open = !StartCollapsed;
        }

        private void SetupUI()
        {
            // A weather BUTTON that slides the forecast open, rather than seven cards parked
            // on the HUD permanently. The button is a plain themed Button and the cards are
            // built from the live theme, so the whole widget skins with the genre.
            var root = new VBoxContainer { Name = "WeatherRoot" };
            root.AddThemeConstantOverride("separation", 4);
            root.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(root);

            _toggle = new KitPushButton
            {
                Name = "WeatherToggle",
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
                ToggleMode = true,
            };
            _toggle.CustomMinimumSize = new Vector2(Beep.ECS.UI.UiSurface.FontSize(this) * 7.5f,
                                                    Beep.ECS.UI.UiSurface.FontSize(this) * 2.0f);
            _toggle.AddThemeFontSizeOverride("font_size", Beep.ECS.UI.UiSurface.FontSize(this, Beep.ECS.UI.UiSurface.TextRole.Caption));
            _toggle.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            _toggle.Pressed += () => SetOpen(!_open);

            // ClipContents on a wrapper is what makes it a SLIDE rather than a pop: the cards
            // keep their real size and are revealed by the wrapper growing.
            _slide = new Godot.Control { Name = "Slide", ClipContents = true };
            _slide.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            // Cards ABOVE the button. This widget is anchored to the bottom of the HUD, so a
            // forecast added below the button grows straight off the bottom of the screen.
            root.AddChild(_slide);
            root.AddChild(_toggle);

            _forecastContainer = new VBoxContainer { Name = "ForecastContainer" };
            _forecastContainer.AddThemeConstantOverride("separation", (int)ItemSpacing);
            _slide.AddChild(_forecastContainer);

            // A null ForecastData used to leave the panel permanently empty and silent — the
            // addon ships no default .tres and none of the genre mains set one. Since the
            // resource can generate its own forecast, fall back to a working default instead
            // of rendering nothing (the repo's "prefer a working default" rule).
            if (ForecastData == null)
            {
                ForecastData = new Beep.GameBuilder.WeatherForecast();
                GD.PushWarning($"[{Name}] No ForecastData assigned — using a self-generated default forecast. Assign a WeatherForecast resource to control it.");
            }
            ForecastData.GenerateForecast(CurrentDay);

            // Populate forecast items
            RefreshForecast();
        }

        public void RefreshForecast()
        {
            if (ForecastData == null || _forecastContainer == null) return;

            foreach (var child in _forecastContainer.GetChildren())
            {
                _forecastContainer.RemoveChild(child);
                child.QueueFree();
            }
            _currentRowContainer = null;

            for (int i = 0; i < ForecastData.DaysForward.Length; i++)
            {
                // Create new row every ItemsPerRow items
                if (i % ItemsPerRow == 0)
                {
                    _currentRowContainer = new HBoxContainer
                    {
                        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
                    };
                    _currentRowContainer.AddThemeConstantOverride("separation", (int)ItemSpacing);
                    _forecastContainer.AddChild(_currentRowContainer);
                }

                var dayData = ForecastData.DaysForward[i];
                var itemPanel = CreateForecastItem(i, dayData);
                _currentRowContainer?.AddChild(itemPanel);
            }

            RefreshToggleLabel();
            // Deferred: the cards were just added, so their combined minimum size is only
            // correct after the container has sorted on the next frame.
            CallDeferred(nameof(SettleOpenState));
        }

        /// <summary>Apply the current open/closed height without animating — used after a
        /// rebuild, where a tween from the old height would look like a glitch.</summary>
        private void SettleOpenState()
        {
            if (_slide == null || _forecastContainer == null) return;
            _slide.CustomMinimumSize = new Vector2(
                _slide.CustomMinimumSize.X,
                _open ? _forecastContainer.GetCombinedMinimumSize().Y : 0f);
            RefreshToggleLabel();
        }

        /// <summary>Open or close the forecast, animating the clip height.</summary>
        public void SetOpen(bool open)
        {
            _open = open;
            if (_toggle != null) _toggle.SetPressedNoSignal(open);
            if (_slide == null || _forecastContainer == null) return;

            float target = open ? _forecastContainer.GetCombinedMinimumSize().Y : 0f;
            _tween?.Kill();
            _tween = CreateTween();
            _tween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            _tween.TweenProperty(_slide, "custom_minimum_size:y", target, SlideSeconds);
        }

        /// <summary>Label the button with the CURRENT conditions, so a closed forecast still
        /// tells the player what the weather is.</summary>
        private void RefreshToggleLabel()
        {
            if (_toggle == null) return;
            var days = ForecastData?.DaysForward;
            string txt = days is { Length: > 0 }
                ? $"{WeatherGlyph(days[0].WeatherType)}  {days[0].Temperature:0}°C"
                : "Weather";
            _toggle.Text = $"{txt}   {(_open ? "▴" : "▾")}";
        }

        private static string WeatherGlyph(string weatherType) => weatherType switch
        {
            "Clear" => "☀",
            "Cloudy" => "☁",
            "Rain" or "Rainy" => "☂",
            "Storm" or "Stormy" => "⚡",
            _ => "☁",
        };

        private Godot.Control CreateForecastItem(int dayIndex, Beep.GameBuilder.WeatherData dayData)
        {
            return new KitWeatherForecastCard
            {
                CustomMinimumSize = ItemSize,
                Name = $"Day{dayIndex}",
                MouseFilter = MouseFilterEnum.Ignore,
                DayText = $"Day {dayIndex + 1}",
                WeatherGlyph = GetWeatherIcon(dayData.WeatherType),
                TemperatureText = $"{dayData.Temperature:F0}°C",
                WindText = $"Wind {dayData.WindSpeed:F1}",
                WeatherRole = GetWeatherRole(dayData.WeatherType),
            };
        }

        // Keys match the names WeatherForecast stamps into WeatherData.WeatherType (Clear/Cloudy/Rain/
        // Storm) — the generator used to emit "Rainy"/"Stormy", so these lookups fell through to the
        // default color/icon. Legacy names kept as aliases for any hand-authored data.
        private Color GetWeatherColor(string weatherType) => weatherType switch
        {
            "Clear" => ClearColor,
            "Cloudy" => CloudyColor,
            "Rain" or "Rainy" => RainyColor,
            "Storm" or "Stormy" => StormyColor,
            _ => Beep.ECS.UI.UiSurface.Semantic(this, Beep.ECS.UI.UiSurface.Role.Neutral)
        };

        private static Beep.ECS.UI.UiSurface.Role GetWeatherRole(string weatherType) => weatherType switch
        {
            "Clear" => Beep.ECS.UI.UiSurface.Role.Warning,
            "Cloudy" => Beep.ECS.UI.UiSurface.Role.Neutral,
            "Rain" or "Rainy" => Beep.ECS.UI.UiSurface.Role.Info,
            "Storm" or "Stormy" => Beep.ECS.UI.UiSurface.Role.Accent2,
            _ => Beep.ECS.UI.UiSurface.Role.Neutral,
        };

        private string GetWeatherIcon(string weatherType) => weatherType switch
        {
            "Clear" => "☀️",
            "Cloudy" => "☁️",
            "Rain" or "Rainy" => "🌧️",
            "Storm" or "Stormy" => "⛈️",
            _ => "?"
        };

        /// <summary>
        /// Update the forecast display (call after changing CurrentDay).
        /// </summary>
        public void UpdateForecast(int newDay)
        {
            CurrentDay = newDay;
            if (ForecastData != null)
                ForecastData.GenerateForecast(CurrentDay);
            RefreshForecast();
        }
    }
}

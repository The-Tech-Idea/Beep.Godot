using Godot;

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
        [Export] public Color ClearColor { get; set; } = Colors.Yellow;
        [Export] public Color CloudyColor { get; set; } = Colors.Gray;
        [Export] public Color RainyColor { get; set; } = Colors.CornflowerBlue;
        [Export] public Color StormyColor { get; set; } = Colors.DarkSlateBlue;

        [ExportGroup("Layout")]
        [Export] public int ItemsPerRow { get; set; } = 4;
        [Export] public Vector2 ItemSize { get; set; } = new(80, 100);
        [Export] public float ItemSpacing { get; set; } = 10f;

        private VBoxContainer? _forecastContainer;
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
        }

        private void SetupUI()
        {
            // Create main container
            _forecastContainer = new VBoxContainer { Name = "ForecastContainer" };
            _forecastContainer.AddThemeConstantOverride("separation", (int)ItemSpacing);
            AddChild(_forecastContainer);

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
        }

        private PanelContainer CreateForecastItem(int dayIndex, Beep.GameBuilder.WeatherData dayData)
        {
            var panel = new PanelContainer
            {
                CustomMinimumSize = ItemSize,
                Name = $"Day{dayIndex}"
            };

            // Create a simple StyleBox background
            var styleBox = new StyleBoxFlat
            {
                BgColor = GetWeatherColor(dayData.WeatherType),
                BorderColor = Colors.Black,
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2
            };
            panel.AddThemeStyleboxOverride("panel", styleBox);

            // Content layout
            var vbox = new VBoxContainer { Name = "Content" };
            panel.AddChild(vbox);

            // Day label
            var dayLabel = new Label
            {
                Text = $"Day {dayIndex + 1}",
                CustomMinimumSize = new Vector2(0, 20),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            vbox.AddChild(dayLabel);

            // Weather icon (text for now, could be replaced with TextureRect)
            var weatherLabel = new Label
            {
                Text = GetWeatherIcon(dayData.WeatherType),
                CustomMinimumSize = new Vector2(0, 30),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            vbox.AddChild(weatherLabel);

            // Temperature
            var tempLabel = new Label
            {
                Text = $"{dayData.Temperature:F0}°C",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            vbox.AddChild(tempLabel);

            // Wind speed
            var windLabel = new Label
            {
                Text = $"💨 {dayData.WindSpeed:F1}",
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = Colors.LightGray
            };
            vbox.AddChild(windLabel);

            return panel;
        }

        private Color GetWeatherColor(string weatherType) => weatherType switch
        {
            "Clear" => ClearColor,
            "Cloudy" => CloudyColor,
            "Rainy" => RainyColor,
            "Stormy" => StormyColor,
            _ => Colors.White
        };

        private string GetWeatherIcon(string weatherType) => weatherType switch
        {
            "Clear" => "☀️",
            "Cloudy" => "☁️",
            "Rainy" => "🌧️",
            "Stormy" => "⛈️",
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

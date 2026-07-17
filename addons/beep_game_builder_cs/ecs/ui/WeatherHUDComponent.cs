using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Weather heads-up display binder. Attach as a child of a HUD Control
    /// (typically inside a CanvasLayer). Discovers a
    /// <see cref="global::Beep.ECS.WeatherSystemComponent"/> and updates named
    /// child Labels every frame / on signal. Pure binding — no layout; the
    /// scene's nodes provide the layout.
    ///
    /// Optional child nodes (by name; create only the ones you need):
    ///   "WeatherLabel"   — current weather name (e.g. "Heavy Rain").
    ///   "IntensityLabel" — intensity percentage (e.g. "85%").
    ///   "ForecastLabel"  — countdown to next weather under AutoCycle (e.g. "0:42").
    ///   "SeasonLabel"    — current season.
    ///   "TimeLabel"      — time of day in HH:MM (requires day-night enabled).
    ///   "WindLabel"      — wind strength summary.
    ///
    /// Missing nodes are silently skipped. Icons: if a child "WeatherIcon"
    /// (TextureRect) exists, its texture is swapped from the
    /// <c>WeatherIcons</c> array (indexed by weather enum value).
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class WeatherHUDComponent : UIComponent
    {
        /// <summary>WeatherSystemComponent to read. Leave null to auto-find in the scene.</summary>
        [Export] public NodePath? WeatherSystemPath { get; set; }

        [ExportGroup("Optional Label Paths (relative to parent)")]
        [Export] public NodePath WeatherLabelPath { get; set; } = "WeatherLabel";
        [Export] public NodePath IntensityLabelPath { get; set; } = "IntensityLabel";
        [Export] public NodePath ForecastLabelPath { get; set; } = "ForecastLabel";
        [Export] public NodePath SeasonLabelPath { get; set; } = "SeasonLabel";
        [Export] public NodePath TimeLabelPath { get; set; } = "TimeLabel";
        [Export] public NodePath WindLabelPath { get; set; } = "WindLabel";

        [ExportGroup("Optional Icon")]
        [Export] public NodePath WeatherIconPath { get; set; } = "WeatherIcon";
        /// <summary>
        /// Textures indexed by WeatherType enum value (Clear=0, Cloudy=1, ...).
        /// Assign in order; the HUD shows the one matching the current weather.
        /// </summary>
        [Export] public Texture2D?[] WeatherIcons { get; set; } = new Texture2D?[10];

        // ── Display strings per weather type ──
        private static readonly string[] WeatherDisplayNames =
        {
            "Clear", "Cloudy", "Heavy Rain", "Snow", "Storm",
            "Fog", "Sandstorm", "Hail", "Leaf Fall", "Heatwave"
        };
        private static readonly string[] SeasonDisplayNames =
        {
            "Spring", "Summer", "Autumn", "Winter"
        };

        // ── Resolved nodes ──
        private Label? _weather;
        private Label? _intensity;
        private Label? _forecast;
        private Label? _season;
        private Label? _time;
        private Label? _wind;
        private TextureRect? _icon;
        private global::Beep.ECS.WeatherSystemComponent? _ws;
        // Time-of-day and season moved out of the weather system to their own authorities;
        // the HUD reads each from its owner.
        private global::Beep.ECS.DayNightCycleComponent? _dayNight;
        private global::Beep.ECS.SeasonalComponent? _seasonalComp;

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(Bind));
        }

        public void Bind()
        {
            if (GetParent() is not Node parent) return;
            _weather = parent.GetNodeOrNull<Label>(WeatherLabelPath);
            _intensity = parent.GetNodeOrNull<Label>(IntensityLabelPath);
            _forecast = parent.GetNodeOrNull<Label>(ForecastLabelPath);
            _season = parent.GetNodeOrNull<Label>(SeasonLabelPath);
            _time = parent.GetNodeOrNull<Label>(TimeLabelPath);
            _wind = parent.GetNodeOrNull<Label>(WindLabelPath);
            _icon = parent.GetNodeOrNull<TextureRect>(WeatherIconPath);

            // Discover the weather system.
            if (WeatherSystemPath != null)
                _ws = parent.GetNodeOrNull<global::Beep.ECS.WeatherSystemComponent>(WeatherSystemPath);
            _ws ??= FindWeatherSystem();

            if (_ws == null)
            {
                GD.PushWarning("[WeatherHUD] No WeatherSystemComponent found in scene.");
                return;
            }

            // Day/night and season now live in their own components. Optional — the HUD
            // just omits those fields if they're absent.
            _dayNight = global::Beep.ECS.EntityComponent.FindComponent<global::Beep.ECS.DayNightCycleComponent>(GetTree()?.Root, true);
            _seasonalComp = global::Beep.ECS.EntityComponent.FindComponent<global::Beep.ECS.SeasonalComponent>(GetTree()?.Root, true);

            // React to weather/season changes immediately; the forecast/intensity/
            // time values are polled in _Process because they change continuously.
            _ws.WeatherChanged += OnWeatherChanged;
            if (_seasonalComp != null) _seasonalComp.SeasonChanged += OnSeasonChanged;
            OnWeatherChanged((int)_ws.CurrentWeather);
            RefreshAll();
        }

        public override void _Process(double delta)
        {
            if (_ws == null || !IsActive) return;
            RefreshAll();
        }

        private void RefreshAll()
        {
            if (_ws == null) return;

            // Intensity percentage.
            if (_intensity != null)
                _intensity.Text = $"{(int)(_ws.WeatherIntensity * 100f)}%";

            // Forecast countdown (only meaningful under AutoCycle).
            if (_forecast != null)
            {
                double remain = _ws.TimeToNextWeather;
                _forecast.Text = remain > 0
                    ? $"{(int)remain / 60}:{(int)remain % 60:00}"
                    : "—";
            }

            // Time of day in HH:MM, from the day-night cycle if one is present.
            if (_time != null && _dayNight != null)
            {
                float t = _dayNight.TimeOfDay;
                _time.Text = $"{(int)t:00}:{(int)((t - (int)t) * 60f):00}";
            }

            // Wind summary.
            if (_wind != null)
            {
                float strength = _ws.WindForce.Length();
                _wind.Text = strength < 0.1f ? "Calm"
                    : strength < 0.5f ? "Light"
                    : strength < 1.5f ? "Breezy"
                    : "Strong";
            }
        }

        private void OnWeatherChanged(int type)
        {
            if (_weather != null && type >= 0 && type < WeatherDisplayNames.Length)
                _weather.Text = WeatherDisplayNames[type];
            if (_icon != null && WeatherIcons != null
                && type >= 0 && type < WeatherIcons.Length
                && WeatherIcons[type] != null)
                _icon.Texture = WeatherIcons[type];
        }

        private void OnSeasonChanged(int season)
        {
            if (_season != null && season >= 0 && season < SeasonDisplayNames.Length)
                _season.Text = SeasonDisplayNames[season];
        }

        private global::Beep.ECS.WeatherSystemComponent? FindWeatherSystem()
        {
            var tree = GetTree();
            if (tree == null) return null;
            foreach (var n in tree.GetNodesInGroup("weather_system"))
                if (n is global::Beep.ECS.WeatherSystemComponent w) return w;
            return tree.Root.FindChild("WeatherSystemComponent", true, false)
                as global::Beep.ECS.WeatherSystemComponent;
        }
    }
}

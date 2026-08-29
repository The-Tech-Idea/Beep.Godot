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
        [Export] public NodePath RootPath { get; set; } = new("");
        [Export] public NodePath SlidePath { get; set; } = new("");
        [Export] public NodePath ForecastContainerPath { get; set; } = new("");
        [Export] public NodePath ToggleButtonPath { get; set; } = new("");
        [Export] public bool BuildInEditor { get; set; } = true;
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = false;
        /// <summary>How many cards fit per row, computed from the space this control actually
        /// has. It was a fixed export, so a 7-day forecast wrapped to a second row no matter how
        /// wide the strip was — and the second row fell off the bottom of the HUD.</summary>
        private int ItemsPerRow
        {
            get
            {
                float avail = Size.X > 1f ? Size.X : CustomMinimumSize.X;
                Vector2 itemSize = EffectiveItemSize;
                float spacing = EffectiveItemSpacing;
                float per = itemSize.X + spacing;
                if (avail <= 1f || per <= 1f) return 4;
                return Mathf.Clamp(Mathf.FloorToInt((avail + spacing) / per),
                                   1, Mathf.Max(1, ForecastData?.DaysForward?.Length ?? 7));
            }
        }
        [Export] public Vector2 ItemSize { get; set; } = new(72, 88);
        [Export] public float ItemSpacing { get; set; } = 8f;
        public Vector2 EffectiveItemSize => new(
            float.IsFinite(ItemSize.X) ? Mathf.Clamp(ItemSize.X, 32f, 240f) : 72f,
            float.IsFinite(ItemSize.Y) ? Mathf.Clamp(ItemSize.Y, 32f, 240f) : 88f);
        public float EffectiveItemSpacing => float.IsFinite(ItemSpacing) ? Mathf.Clamp(ItemSpacing, 0f, 64f) : 8f;

        private VBoxContainer? _forecastContainer;
        private Button? _toggle;
        private Godot.Control? _slide;      // clips the forecast so it can slide out from under
        private VBoxContainer? _root;
        private bool _createdRoot;
        private Tween? _tween;
        private bool _open;

        /// <summary>Start with the forecast tucked away behind its button.</summary>
        [Export] public bool StartCollapsed { get; set; } = true;
        [Export] public float SlideSeconds { get; set; } = 0.22f;
        public float EffectiveSlideSeconds => float.IsFinite(SlideSeconds) ? Mathf.Max(0f, SlideSeconds) : 0f;
        private HBoxContainer? _currentRowContainer;

        public override void _Ready()
        {
            base._Ready();
            // Hide + skip building when the genre disables the forecast.
            if (!Engine.IsEditorHint() && Beep.GameBuilder.GameInfo.Instance is { } info && !info.EnableWeatherForecast)
            {
                Visible = false;
                return;
            }
            if (!Engine.IsEditorHint() || BuildInEditor)
                CallDeferred(nameof(SetupUI));
            UpdateConfigurationWarnings();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (!GenerateControlsWhenPathsEmpty && !HasAuthoredControls())
                return new[] { "Set SlidePath, ForecastContainerPath, and ToggleButtonPath to authored controls, add the WeatherRoot/Slide/ForecastContainer/WeatherToggle child nodes, or enable GenerateControlsWhenPathsEmpty." };
            return System.Array.Empty<string>();
        }

        private void SetupUI()
        {
            if (!BindExistingControls())
            {
                if (!GenerateControlsWhenPathsEmpty)
                    return;

                BuildGeneratedSurface();
            }

            if (_slide == null || _forecastContainer == null || _toggle == null)
                return;

            StyleControls();
            _open = !StartCollapsed;
            PrepareForecastData();
            RefreshForecast();
        }

        private void BuildGeneratedSurface()
        {
            // A weather BUTTON that slides the forecast open, rather than seven cards parked
            // on the HUD permanently. The button is a plain themed Button and the cards are
            // built from the live theme, so the whole widget skins with the genre.
            _createdRoot = true;
            _root = new VBoxContainer { Name = "WeatherRoot" };
            _root.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(_root);
            SetEditedOwner(_root);

            _toggle = new KitPushButton
            {
                Name = "WeatherToggle",
                ToggleMode = true,
            };

            // ClipContents on a wrapper is what makes it a SLIDE rather than a pop: the cards
            // keep their real size and are revealed by the wrapper growing.
            _slide = new Godot.Control { Name = "Slide", ClipContents = true };

            // Cards ABOVE the button. This widget is anchored to the bottom of the HUD, so a
            // forecast added below the button grows straight off the bottom of the screen.
            _root.AddChild(_slide);
            _root.AddChild(_toggle);
            SetEditedOwner(_slide);
            SetEditedOwner(_toggle);

            _forecastContainer = new VBoxContainer { Name = "ForecastContainer" };
            _slide.AddChild(_forecastContainer);
            SetEditedOwner(_forecastContainer);
        }

        private bool BindExistingControls()
        {
            _createdRoot = false;
            _root = FindRoot();
            _slide = FindSlide();
            _forecastContainer = FindForecastContainer();
            _toggle = FindToggleButton();
            return _slide != null && _forecastContainer != null && _toggle != null;
        }

        public bool UsesSceneControls()
            => HasAuthoredControls();

        private bool HasAuthoredControls()
            => FindSlide() != null && FindForecastContainer() != null && FindToggleButton() != null;

        private VBoxContainer? FindRoot()
        {
            if (!RootPath.IsEmpty && GetNodeOrNull<VBoxContainer>(RootPath) is { } pathRoot)
                return pathRoot;

            if (FindChild("WeatherRoot", recursive: true, owned: false) is VBoxContainer childRoot)
                return childRoot;

            return GetParent()?.FindChild("WeatherRoot", recursive: true, owned: false) as VBoxContainer;
        }

        private Godot.Control? FindSlide()
        {
            if (!SlidePath.IsEmpty && GetNodeOrNull<Godot.Control>(SlidePath) is { } pathSlide)
                return pathSlide;

            if (GetNodeOrNull<Godot.Control>("WeatherRoot/Slide") is { } localSlide)
                return localSlide;

            if (FindChild("Slide", recursive: true, owned: false) is Godot.Control childSlide)
                return childSlide;

            return GetParent()?.FindChild("Slide", recursive: true, owned: false) as Godot.Control;
        }

        private VBoxContainer? FindForecastContainer()
        {
            if (!ForecastContainerPath.IsEmpty && GetNodeOrNull<VBoxContainer>(ForecastContainerPath) is { } pathContainer)
                return pathContainer;

            if (GetNodeOrNull<VBoxContainer>("WeatherRoot/Slide/ForecastContainer") is { } localContainer)
                return localContainer;

            if (FindChild("ForecastContainer", recursive: true, owned: false) is VBoxContainer childContainer)
                return childContainer;

            return GetParent()?.FindChild("ForecastContainer", recursive: true, owned: false) as VBoxContainer;
        }

        private Button? FindToggleButton()
        {
            if (!ToggleButtonPath.IsEmpty && GetNodeOrNull<Button>(ToggleButtonPath) is { } pathToggle)
                return pathToggle;

            if (GetNodeOrNull<Button>("WeatherRoot/WeatherToggle") is { } localToggle)
                return localToggle;

            if (FindChild("WeatherToggle", recursive: true, owned: false) is Button childToggle)
                return childToggle;

            return GetParent()?.FindChild("WeatherToggle", recursive: true, owned: false) as Button;
        }

        private void StyleControls()
        {
            if (_root != null)
            {
                KitChrome.SetConstantOverrideIfChanged(_root, "separation", 4);
                _root.SetAnchorsPreset(LayoutPreset.FullRect);
            }

            if (_slide != null)
            {
                _slide.ClipContents = true;
                _slide.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            }

            if (_toggle != null)
            {
                _toggle.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
                _toggle.ToggleMode = true;
                _toggle.CustomMinimumSize = new Vector2(Beep.ECS.UI.UiSurface.FontSize(this) * 7.5f,
                                                        Beep.ECS.UI.UiSurface.FontSize(this) * 2.0f);
                KitChrome.SetFontSizeOverrideIfChanged(_toggle, "font_size", Beep.ECS.UI.UiSurface.FontSize(this, Beep.ECS.UI.UiSurface.TextRole.Caption));
                _toggle.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                if (!_toggle.IsConnected(Button.SignalName.Pressed, Callable.From(OnTogglePressed)))
                    _toggle.Pressed += OnTogglePressed;
            }

            if (_forecastContainer != null)
                KitChrome.SetConstantOverrideIfChanged(_forecastContainer, "separation", Mathf.RoundToInt(EffectiveItemSpacing));
        }

        private void PrepareForecastData()
        {
            // A null ForecastData used to leave the panel permanently empty and silent — the
            // addon ships no default .tres and none of the genre mains set one. Since the
            // resource can generate its own forecast, fall back to a working default instead
            // of rendering nothing (the repo's "prefer a working default" rule).
            if (ForecastData == null)
            {
                ForecastData = new Beep.GameBuilder.WeatherForecast();
                if (!Engine.IsEditorHint())
                    GD.PushWarning($"[{Name}] No ForecastData assigned — using a self-generated default forecast. Assign a WeatherForecast resource to control it.");
            }
            ForecastData.GenerateForecast(CurrentDay);
        }

        private void OnTogglePressed() => SetOpen(!_open);

        public void RefreshForecast()
        {
            if (ForecastData == null || _forecastContainer == null) return;

            foreach (var child in _forecastContainer.GetChildren())
            {
                _forecastContainer.RemoveChild(child);
                child.QueueFree();
            }
            _currentRowContainer = null;

            var days = ForecastData.DaysForward ?? System.Array.Empty<Beep.GameBuilder.WeatherData>();
            for (int i = 0; i < days.Length; i++)
            {
                // Create new row every ItemsPerRow items
                if (i % ItemsPerRow == 0)
                {
                    _currentRowContainer = new HBoxContainer
                    {
                        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
                    };
                    KitChrome.SetConstantOverrideIfChanged(_currentRowContainer, "separation", Mathf.RoundToInt(EffectiveItemSpacing));
                    _forecastContainer.AddChild(_currentRowContainer);
                }

                var dayData = days[i] ?? new Beep.GameBuilder.WeatherData();
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
            float seconds = EffectiveSlideSeconds;
            if (seconds <= 0f)
            {
                _slide.CustomMinimumSize = new Vector2(_slide.CustomMinimumSize.X, target);
                return;
            }
            _tween = CreateTween();
            _tween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            _tween.TweenProperty(_slide, "custom_minimum_size:y", target, seconds);
        }

        /// <summary>Label the button with the CURRENT conditions, so a closed forecast still
        /// tells the player what the weather is.</summary>
        private void RefreshToggleLabel()
        {
            if (_toggle == null) return;
            var days = ForecastData?.DaysForward;
            string txt = days is { Length: > 0 }
                ? $"{WeatherGlyph(days[0]?.EffectiveWeatherType ?? "Clear")}  {days[0]?.EffectiveTemperature ?? 20f:0}°C"
                : "Weather";
            _toggle.Text = $"{txt}   {(_open ? "▴" : "▾")}";
        }

        private static string WeatherGlyph(string weatherType) => weatherType switch
        {
            "Clear" => "☀",
            "Cloudy" => "☁",
            "Rain" or "Rainy" => "☂",
            "Snow" => "❄",
            "Storm" or "Stormy" => "⚡",
            "Fog" => "≋",
            "Sandstorm" => "☰",
            "Hail" => "◇",
            "LeafFall" => "✦",
            "Heatwave" => "♨",
            _ => "☁",
        };

        private Godot.Control CreateForecastItem(int dayIndex, Beep.GameBuilder.WeatherData dayData)
        {
            if (ForecastItemScene != null)
            {
                Node instance = ForecastItemScene.Instantiate();
                if (instance is Godot.Control control)
                {
                    ConfigureForecastItem(control, dayIndex, dayData);
                    return control;
                }

                instance.QueueFree();
                GD.PushWarning($"[{Name}] ForecastItemScene must instantiate a Control; got '{instance.GetType().Name}'. Falling back to KitWeatherForecastCard.");
            }

            var card = new KitWeatherForecastCard();
            ConfigureForecastItem(card, dayIndex, dayData);
            return card;
        }

        private void ConfigureForecastItem(Godot.Control control, int dayIndex, Beep.GameBuilder.WeatherData dayData)
        {
            control.CustomMinimumSize = EffectiveItemSize;
            control.Name = $"Day{dayIndex}";
            control.MouseFilter = MouseFilterEnum.Ignore;

            string day = $"Day {dayIndex + 1}";
            string weatherType = dayData.EffectiveWeatherType;
            string glyph = GetWeatherIcon(weatherType);
            string temperature = $"{dayData.EffectiveTemperature:F0}°C";
            string wind = $"Wind {dayData.EffectiveWindSpeed:F1}";

            if (control is KitWeatherForecastCard card)
            {
                card.DayText = day;
                card.WeatherGlyph = glyph;
                card.TemperatureText = temperature;
                card.WindText = wind;
                card.WeatherRole = GetWeatherRole(weatherType);
                return;
            }

            SetForecastLabel(control, "Day", day);
            SetForecastLabel(control, "DayText", day);
            SetForecastLabel(control, "Weather", glyph);
            SetForecastLabel(control, "WeatherGlyph", glyph);
            SetForecastLabel(control, "Temperature", temperature);
            SetForecastLabel(control, "TemperatureText", temperature);
            SetForecastLabel(control, "Wind", wind);
            SetForecastLabel(control, "WindText", wind);
        }

        private static void SetForecastLabel(Node root, string name, string text)
        {
            if (root.FindChild(name, recursive: true, owned: false) is Label label)
            {
                label.Text = text;
                label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                label.ClipText = true;
            }
        }

        // Keys match the names WeatherForecast stamps into WeatherData.WeatherType (Clear/Cloudy/Rain/
        // Storm) — the generator used to emit "Rainy"/"Stormy", so these lookups fell through to the
        // default color/icon. Legacy names kept as aliases for any hand-authored data.
        private Color GetWeatherColor(string weatherType) => weatherType switch
        {
            "Clear" => ClearColor,
            "Cloudy" => CloudyColor,
            "Rain" or "Rainy" => RainyColor,
            "Snow" => RainyColor,
            "Storm" or "Stormy" => StormyColor,
            "Fog" => CloudyColor,
            "Sandstorm" => StormyColor,
            "Hail" => RainyColor,
            "LeafFall" => ClearColor,
            "Heatwave" => ClearColor,
            _ => Beep.ECS.UI.UiSurface.Semantic(this, Beep.ECS.UI.UiSurface.Role.Neutral)
        };

        private static Beep.ECS.UI.UiSurface.Role GetWeatherRole(string weatherType) => weatherType switch
        {
            "Clear" => Beep.ECS.UI.UiSurface.Role.Warning,
            "Cloudy" => Beep.ECS.UI.UiSurface.Role.Neutral,
            "Rain" or "Rainy" => Beep.ECS.UI.UiSurface.Role.Info,
            "Snow" => Beep.ECS.UI.UiSurface.Role.Info,
            "Storm" or "Stormy" => Beep.ECS.UI.UiSurface.Role.Accent2,
            "Fog" => Beep.ECS.UI.UiSurface.Role.Neutral,
            "Sandstorm" => Beep.ECS.UI.UiSurface.Role.Accent2,
            "Hail" => Beep.ECS.UI.UiSurface.Role.Info,
            "LeafFall" => Beep.ECS.UI.UiSurface.Role.Warning,
            "Heatwave" => Beep.ECS.UI.UiSurface.Role.Warning,
            _ => Beep.ECS.UI.UiSurface.Role.Neutral,
        };

        private string GetWeatherIcon(string weatherType) => weatherType switch
        {
            "Clear" => "☀️",
            "Cloudy" => "☁️",
            "Rain" or "Rainy" => "🌧️",
            "Snow" => "❄️",
            "Storm" or "Stormy" => "⛈️",
            "Fog" => "🌫️",
            "Sandstorm" => "≋",
            "Hail" => "◇",
            "LeafFall" => "🍂",
            "Heatwave" => "♨",
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

        public override void _ExitTree()
        {
            base._ExitTree();
            _tween?.Kill();
            _tween = null;
            if (_toggle != null && GodotObject.IsInstanceValid(_toggle))
                _toggle.Pressed -= OnTogglePressed;
            if (_createdRoot && _root != null && GodotObject.IsInstanceValid(_root))
                _root.QueueFree();
            _toggle = null;
            _slide = null;
            _forecastContainer = null;
            _root = null;
        }

        private void SetEditedOwner(Node node)
        {
            if (!Engine.IsEditorHint())
                return;

            node.Owner = GetTree()?.EditedSceneRoot;
        }
    }
}

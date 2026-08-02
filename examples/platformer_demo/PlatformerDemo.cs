using Beep.ECS;
using Beep.ECS.UI;
using Beep.ECS.UI.Kit;
using Godot;

namespace Beep.Examples;

/// <summary>
/// A small playable platformer example that uses the addon's authored platformer scene.
/// The scene instance supplies the controller, HUD, atmosphere, level loader, pickups and
/// weather; this script only adds demo terrain and a finish gate so the template can be
/// played directly from the examples folder.
/// </summary>
public partial class PlatformerDemo : Node
{
    [Signal] public delegate void DemoReadyEventHandler();
    [Signal] public delegate void GoalReachedEventHandler();

    private Node2D? _game;
    private CharacterBody2D? _player;
    private Node2D? _levelHost;
    private WeatherSystemComponent? _weather;
    private KitLabel? _weatherStatus;
    private KitSlider? _weatherIntensity;
    private bool _built;

    public override void _Ready()
    {
        EnsureInputActions();
        CallDeferred(nameof(BuildDemo));
    }

    private async void BuildDemo()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        _game = GetNodeOrNull<Node2D>("PlatformerMain");
        _player = GetNodeOrNull<CharacterBody2D>("PlatformerMain/Player");
        _levelHost = GetNodeOrNull<Node2D>("PlatformerMain/LevelContainer");
        _weather = GetNodeOrNull<WeatherSystemComponent>("PlatformerMain/Atmosphere/Weather");

        if (_game == null || _player == null || _levelHost == null)
        {
            GD.PushError("[PlatformerDemo] The platformer template did not expose PlatformerMain, Player and LevelContainer.");
            return;
        }

        var level = await WaitForLoadedLevel();
        if (level == null)
        {
            GD.PushError("[PlatformerDemo] The platformer template did not load a level into LevelContainer.");
            return;
        }

        BuildTerrain(level);
        AddGoal(level);
        AddInstructions();
        AddWeatherControls();

        _player.GlobalPosition = new Vector2(160, 360);
        _player.Velocity = Vector2.Zero;
        _built = true;
        EmitSignal(SignalName.DemoReady);
    }

    private void BuildTerrain(Node2D level)
    {
        if (level.GetNodeOrNull("DemoTerrain") != null) return;

        var terrain = new Node2D { Name = "DemoTerrain" };
        level.AddChild(terrain);

        AddPlatform(terrain, "Ground", new Vector2(640, 488), new Vector2(1180, 48), new Color(0.18f, 0.34f, 0.27f));
        AddPlatform(terrain, "StepA", new Vector2(360, 392), new Vector2(220, 28), new Color(0.24f, 0.45f, 0.30f));
        AddPlatform(terrain, "StepB", new Vector2(650, 322), new Vector2(220, 28), new Color(0.31f, 0.52f, 0.34f));
        AddPlatform(terrain, "StepC", new Vector2(945, 260), new Vector2(240, 28), new Color(0.37f, 0.60f, 0.36f));
        AddPlatform(terrain, "GoalLedge", new Vector2(1220, 392), new Vector2(260, 28), new Color(0.43f, 0.65f, 0.39f));
    }

    private async System.Threading.Tasks.Task<Node2D?> WaitForLoadedLevel()
    {
        if (_levelHost == null) return null;
        for (int i = 0; i < 12; i++)
        {
            if (_levelHost.GetChildCount() > 0 && _levelHost.GetChild(0) is Node2D loaded)
                return loaded;
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        return _levelHost;
    }

    private static void AddPlatform(Node parent, string name, Vector2 center, Vector2 size, Color color)
    {
        var body = new StaticBody2D { Name = name, Position = center };
        parent.AddChild(body);

        var shape = new CollisionShape2D
        {
            Name = "Collision",
            Shape = new RectangleShape2D { Size = size },
        };
        body.AddChild(shape);

        var visual = new Polygon2D
        {
            Name = "Visual",
            Color = color,
            Polygon = new Vector2[]
            {
                new(-size.X * 0.5f, -size.Y * 0.5f),
                new(size.X * 0.5f, -size.Y * 0.5f),
                new(size.X * 0.5f, size.Y * 0.5f),
                new(-size.X * 0.5f, size.Y * 0.5f),
            },
        };
        body.AddChild(visual);
    }

    private void AddGoal(Node2D level)
    {
        if (level.GetNodeOrNull("DemoGoal") != null) return;

        var goal = new Area2D { Name = "DemoGoal", Position = new Vector2(1220, 330) };
        goal.BodyEntered += OnGoalBodyEntered;
        level.AddChild(goal);

        goal.AddChild(new CollisionShape2D
        {
            Name = "Collision",
            Shape = new RectangleShape2D { Size = new Vector2(44, 96) },
        });

        goal.AddChild(new Polygon2D
        {
            Name = "Flag",
            Color = new Color(1.0f, 0.78f, 0.22f),
            Polygon = new Vector2[] { new(-8, -48), new(34, -30), new(-8, -12) },
        });

        goal.AddChild(new Polygon2D
        {
            Name = "Pole",
            Color = new Color(0.95f, 0.95f, 0.88f),
            Polygon = new Vector2[] { new(-12, -48), new(-6, -48), new(-6, 48), new(-12, 48) },
        });
    }

    private void AddInstructions()
    {
        if (_game == null || _game.GetNodeOrNull("DemoHelp") != null) return;

        var layer = new CanvasLayer { Name = "DemoHelp", Layer = 5 };
        _game.AddChild(layer);

        var root = new MarginContainer { Name = "Root", MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AnchorLeft = 0.5f;
        root.AnchorRight = 0.5f;
        root.AnchorTop = 1.0f;
        root.AnchorBottom = 1.0f;
        root.OffsetLeft = -300.0f;
        root.OffsetRight = 300.0f;
        root.OffsetTop = -74.0f;
        root.OffsetBottom = -24.0f;
        root.AddThemeConstantOverride("margin_bottom", 24);
        layer.AddChild(root);

        var panel = new KitPanelContainer
        {
            Name = "HelpPanel",
            CustomMinimumSize = new Vector2(600, 46),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        root.AddChild(panel);

        var label = new KitLabel
        {
            Name = "HelpText",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = "Platformer demo: move with A/D or arrows, jump with Space, collect coins, reach the flag.",
            Role = UiSurface.TextRole.Caption,
        };
        panel.AddChild(label);
    }

    private void AddWeatherControls()
    {
        if (_game == null || _game.GetNodeOrNull("DemoWeatherControls") != null) return;

        var layer = new CanvasLayer { Name = "DemoWeatherControls", Layer = 6 };
        _game.AddChild(layer);

        var root = new MarginContainer
        {
            Name = "Root",
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
        root.AnchorLeft = 1.0f;
        root.AnchorRight = 1.0f;
        root.AnchorTop = 0.0f;
        root.AnchorBottom = 0.0f;
        root.OffsetLeft = -310.0f;
        root.OffsetRight = -22.0f;
        root.OffsetTop = 126.0f;
        root.OffsetBottom = 304.0f;
        layer.AddChild(root);

        var panel = new KitPanelContainer
        {
            Name = "WeatherPanel",
            Title = "Weather",
            ExtraPadding = new Vector2(8, 8),
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
        root.AddChild(panel);

        var body = new VBoxContainer
        {
            Name = "Body",
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
        body.AddThemeConstantOverride("separation", 8);
        panel.AddChild(body);

        _weatherStatus = new KitLabel
        {
            Name = "Status",
            Text = WeatherStatusText(),
            Role = UiSurface.TextRole.Caption,
            CustomMinimumSize = new Vector2(236, 24),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        body.AddChild(_weatherStatus);

        var picker = new KitOptionButton
        {
            Name = "WeatherPicker",
            CustomMinimumSize = new Vector2(236, 34),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        foreach (WeatherSystemComponent.WeatherType type in System.Enum.GetValues(typeof(WeatherSystemComponent.WeatherType)))
            picker.AddItem(type.ToString(), (int)type);
        picker.Selected = Mathf.Max(0, (int)(_weather?.CurrentWeather ?? WeatherSystemComponent.WeatherType.Clear));
        picker.ItemSelected += index =>
        {
            if (_weather == null) return;
            int id = picker.GetItemId((int)index);
            var type = (WeatherSystemComponent.WeatherType)id;
            _weather.AutoCycle = false;
            _weather.TransitionTo(type, 0.8f, (float)(_weatherIntensity?.Value ?? 1.0));
            UpdateWeatherStatus(type);
        };
        body.AddChild(picker);

        var sliderRow = new HBoxContainer
        {
            Name = "IntensityRow",
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
        sliderRow.AddThemeConstantOverride("separation", 8);
        body.AddChild(sliderRow);

        sliderRow.AddChild(new KitLabel
        {
            Name = "IntensityLabel",
            Text = "Intensity",
            Role = UiSurface.TextRole.Small,
            CustomMinimumSize = new Vector2(66, 28),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });

        _weatherIntensity = new KitSlider
        {
            Name = "Intensity",
            CustomMinimumSize = new Vector2(160, 28),
            MouseFilter = Control.MouseFilterEnum.Stop,
            MinValue = 0.15,
            MaxValue = 1.0,
            Step = 0.05,
            Value = Mathf.Clamp(_weather?.WeatherIntensity ?? 1.0f, 0.15f, 1.0f),
        };
        _weatherIntensity.ValueChanged += value =>
        {
            if (_weather == null) return;
            _weather.TransitionTo(_weather.CurrentWeather, 0.35f, (float)value);
            UpdateWeatherStatus(_weather.CurrentWeather);
        };
        sliderRow.AddChild(_weatherIntensity);

        var quick = new HBoxContainer
        {
            Name = "QuickButtons",
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
        quick.AddThemeConstantOverride("separation", 6);
        body.AddChild(quick);

        AddWeatherButton(quick, "Clear", WeatherSystemComponent.WeatherType.Clear, UiSurface.Role.Success);
        AddWeatherButton(quick, "Rain", WeatherSystemComponent.WeatherType.Rain, UiSurface.Role.Info);
        AddWeatherButton(quick, "Storm", WeatherSystemComponent.WeatherType.Storm, UiSurface.Role.Warning);
    }

    private void AddWeatherButton(Node parent, string text, WeatherSystemComponent.WeatherType type, UiSurface.Role role)
    {
        var button = new KitPushButton
        {
            Name = $"{text}Button",
            Text = text,
            Accent = role,
            CustomMinimumSize = new Vector2(74, 32),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        button.Pressed += () =>
        {
            if (_weather == null) return;
            _weather.AutoCycle = false;
            _weather.TransitionTo(type, 0.8f, (float)(_weatherIntensity?.Value ?? 1.0));
            UpdateWeatherStatus(type);
        };
        parent.AddChild(button);
    }

    private string WeatherStatusText()
    {
        if (_weather == null) return "Weather system not found";
        return $"{_weather.CurrentWeather}  {Mathf.RoundToInt(_weather.WeatherIntensity * 100f)}%";
    }

    private void UpdateWeatherStatus(WeatherSystemComponent.WeatherType type)
    {
        if (_weatherStatus == null) return;
        float value = (float)(_weatherIntensity?.Value ?? _weather?.WeatherIntensity ?? 1.0f);
        _weatherStatus.Text = $"{type}  {Mathf.RoundToInt(value * 100f)}%";
    }

    private void OnGoalBodyEntered(Node2D body)
    {
        if (body != _player) return;
        EmitSignal(SignalName.GoalReached);
        if (_game != null)
            EntityComponent.FindComponent<GameFlowComponent>(_game, true)?.TriggerLevelComplete();
    }

    private static void EnsureInputActions()
    {
        AddKey("move_left", Key.A, Key.Left);
        AddKey("move_right", Key.D, Key.Right);
        AddKey("jump", Key.Space, Key.W, Key.Up);
        AddKey("pause", Key.Escape, Key.P);
    }

    private static void AddKey(string action, params Key[] keys)
    {
        if (!InputMap.HasAction(action))
            InputMap.AddAction(action);

        foreach (Key key in keys)
        {
            var ev = new InputEventKey { Keycode = key };
            if (!InputMap.ActionHasEvent(action, ev))
                InputMap.ActionAddEvent(action, ev);
        }
    }

    public bool IsBuilt => _built;
    public CharacterBody2D? Player => _player;
    public Node2D? LevelHost => _levelHost;
}

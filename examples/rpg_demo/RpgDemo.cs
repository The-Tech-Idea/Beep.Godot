using Beep.ECS;
using Beep.ECS.UI;
using Beep.ECS.UI.Kit;
using Godot;
using System.Collections.Generic;

namespace Beep.Examples;

/// <summary>
/// Playable RPG/top-down example built on the addon's RPG main scene.
/// Uses copied Kenney roguelike spritesheets from examples/rpg_demo/assets/.
/// </summary>
public partial class RpgDemo : Node
{
    [Signal] public delegate void DemoReadyEventHandler();

    private const string TileSheetPath = "res://examples/rpg_demo/assets/roguelikeSheet_transparent.png";
    private const string CharSheetPath = "res://examples/rpg_demo/assets/roguelikeChar_transparent.png";

    private Node2D? _game;
    private CharacterBody2D? _player;
    private Node2D? _level;
    private WeatherSystemComponent? _weather;
    private KitLabel? _questText;
    private KitLabel? _weatherText;
    private int _herbs;

    public override void _EnterTree()
    {
        EnsureInputActions();
    }

    public override void _Ready()
    {
        CallDeferred(nameof(BuildDemo));
    }

    private async void BuildDemo()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        _game = GetNodeOrNull<Node2D>("RPGMain");
        _player = GetNodeOrNull<CharacterBody2D>("RPGMain/Player");
        _level = GetNodeOrNull<Node2D>("RPGMain/LevelContainer");
        _weather = GetNodeOrNull<WeatherSystemComponent>("RPGMain/Atmosphere/Weather");
        if (_game == null || _player == null || _level == null)
        {
            GD.PushError("[RpgDemo] RPG template did not expose RPGMain, Player and LevelContainer.");
            return;
        }

        SkinCatalog.SetActiveSkin("rpg", "fantasy", "", "");
        BuildWorld(_level);
        SetupPlayer();
        SetupWeather();
        AddHudOverlay();
        AddWeatherControls();

        EmitSignal(SignalName.DemoReady);
    }

    private void BuildWorld(Node2D level)
    {
        if (level.GetNodeOrNull("DemoVillage") != null) return;
        foreach (Node child in level.GetChildren())
            child.QueueFree();

        var village = new Node2D { Name = "DemoVillage" };
        level.AddChild(village);

        var tileSheet = LoadTexture(TileSheetPath);
        var chars = LoadTexture(CharSheetPath);
        if (tileSheet == null || chars == null)
        {
            GD.PushError("[RpgDemo] Failed to load copied Kenney spritesheets.");
            return;
        }
        var map = new VillageMap
        {
            Name = "VillageMap",
            TileSheet = tileSheet,
            Position = new Vector2(-640, -360),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        village.AddChild(map);

        AddSolid(village, "NorthFence", new Vector2(0, -338), new Vector2(1190, 26));
        AddSolid(village, "SouthFence", new Vector2(0, 338), new Vector2(1190, 26));
        AddSolid(village, "WestFence", new Vector2(-594, 0), new Vector2(26, 690));
        AddSolid(village, "EastFence", new Vector2(594, 0), new Vector2(26, 690));
        AddSolid(village, "InnCollision", new Vector2(-280, -158), new Vector2(194, 126));
        AddSolid(village, "ShopCollision", new Vector2(235, -152), new Vector2(194, 126));
        AddSolid(village, "WellCollision", new Vector2(18, 74), new Vector2(76, 62));
        AddSolid(village, "TreeA", new Vector2(-510, 130), new Vector2(70, 72));
        AddSolid(village, "TreeB", new Vector2(508, 152), new Vector2(70, 72));

        AddNpc(village, chars, "Village Elder", new Vector2(-92, -42), new Vector2I(0, 3));
        AddNpc(village, chars, "Guard", new Vector2(320, 48), new Vector2I(0, 0));
        AddNpc(village, chars, "Merchant", new Vector2(-372, 82), new Vector2I(0, 5));

        AddHerb(village, tileSheet, new Vector2(-464, 238));
        AddHerb(village, tileSheet, new Vector2(416, 246));
        AddHerb(village, tileSheet, new Vector2(86, -238));
    }

    private void SetupPlayer()
    {
        if (_player == null) return;
        _player.GlobalPosition = new Vector2(-432, 36);
        _player.Velocity = Vector2.Zero;

        if (_player.GetNodeOrNull<CollisionShape2D>("DemoCollision") == null)
        {
            _player.AddChild(new CollisionShape2D
            {
                Name = "DemoCollision",
                Shape = new CapsuleShape2D { Radius = 13, Height = 34 },
                Position = new Vector2(0, 5),
            });
        }

        if (_player.GetNodeOrNull<Sprite2D>("DemoSprite") == null)
        {
            var tex = LoadTexture(CharSheetPath);
            if (tex == null) return;
            _player.AddChild(new Sprite2D
            {
                Name = "DemoSprite",
                Texture = tex,
                RegionEnabled = true,
                RegionRect = Atlas(0, 1),
                Scale = new Vector2(2.4f, 2.4f),
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                ZIndex = 20,
            });
        }

        var camera = _player.GetNodeOrNull<Camera2D>("Camera2D");
        if (camera == null)
        {
            camera = new Camera2D { Name = "Camera2D", Enabled = true, Zoom = new Vector2(1.35f, 1.35f) };
            _player.AddChild(camera);
        }
        camera.Enabled = true;
        camera.MakeCurrent();
    }

    private void SetupWeather()
    {
        if (_weather == null) return;
        _weather.CurrentWeather = WeatherSystemComponent.WeatherType.Rain;
        _weather.ViewMode = WeatherSystemComponent.WeatherViewMode.RpgTopDown;
        _weather.TopDownView = true;
        _weather.CloudMode = WeatherSystemComponent.CloudRender.None;
        _weather.ParticleCount = 420;
        _weather.SetWeather(WeatherSystemComponent.WeatherType.Rain);
    }

    private void AddHudOverlay()
    {
        if (_game == null) return;
        _questText = _game.GetNodeOrNull<KitLabel>("HUD/Root/QuestBox/QuestLabel");
        UpdateQuest();
    }

    private void AddWeatherControls()
    {
        if (_game == null || _game.GetNodeOrNull("RpgWeatherControls") != null) return;
        var layer = new CanvasLayer { Name = "RpgWeatherControls", Layer = 8 };
        _game.AddChild(layer);

        var root = new MarginContainer
        {
            Name = "Root",
            AnchorLeft = 0,
            AnchorRight = 0,
            OffsetLeft = 22,
            OffsetRight = 344,
            OffsetTop = 24,
            OffsetBottom = 198,
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
        layer.AddChild(root);

        root.AddChild(new CollapsiblePanelComponent
        {
            Name = "Collapse",
            Title = "Weather",
            ParticipatesInSave = false,
        });
        var panel = new KitPanelContainer { Name = "WeatherPanel", Title = "Weather", ExtraPadding = new Vector2(8, 8) };
        root.AddChild(panel);
        var box = new VBoxContainer { Name = "Body" };
        box.AddThemeConstantOverride("separation", 7);
        panel.AddChild(box);

        _weatherText = new KitLabel
        {
            Name = "WeatherStatus",
            Role = UiSurface.TextRole.Caption,
            CustomMinimumSize = new Vector2(284, 24),
        };
        box.AddChild(_weatherText);

        var row = new HBoxContainer { Name = "Buttons" };
        row.AddThemeConstantOverride("separation", 6);
        box.AddChild(row);
        AddWeatherButton(row, "Clear", WeatherSystemComponent.WeatherType.Clear);
        AddWeatherButton(row, "Rain", WeatherSystemComponent.WeatherType.Rain);
        AddWeatherButton(row, "Snow", WeatherSystemComponent.WeatherType.Snow);
        AddWeatherButton(row, "Storm", WeatherSystemComponent.WeatherType.Storm);

        var row2 = new HBoxContainer { Name = "Buttons2" };
        row2.AddThemeConstantOverride("separation", 6);
        box.AddChild(row2);
        AddWeatherButton(row2, "Hail", WeatherSystemComponent.WeatherType.Hail);
        AddWeatherButton(row2, "Sand", WeatherSystemComponent.WeatherType.Sandstorm);
        AddWeatherButton(row2, "Leaves", WeatherSystemComponent.WeatherType.LeafFall);

        UpdateWeatherStatus();
    }

    private void AddWeatherButton(BoxContainer row, string text, WeatherSystemComponent.WeatherType type)
    {
        var button = new KitPushButton
        {
            Text = text,
            CustomMinimumSize = new Vector2(64, 30),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        button.Pressed += () =>
        {
            _weather?.SetWeather(type);
            UpdateWeatherStatus();
        };
        row.AddChild(button);
    }

    private void AddNpc(Node parent, Texture2D texture, string label, Vector2 position, Vector2I atlas)
    {
        var npc = new Node2D { Name = label.Replace(" ", ""), Position = position };
        parent.AddChild(npc);
        npc.AddChild(new Sprite2D
        {
            Name = "Sprite",
            Texture = texture,
            RegionEnabled = true,
            RegionRect = Atlas(atlas.X, atlas.Y),
            Scale = new Vector2(2.4f, 2.4f),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            ZIndex = 15,
        });
        npc.AddChild(new Label
        {
            Name = "Name",
            Text = label,
            Position = new Vector2(-42, -46),
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(84, 18),
        });
    }

    private void AddHerb(Node parent, Texture2D texture, Vector2 position)
    {
        var herb = new Area2D { Name = "Herb", Position = position };
        parent.AddChild(herb);
        herb.BodyEntered += body =>
        {
            if (body != _player || !GodotObject.IsInstanceValid(herb)) return;
            _herbs++;
            herb.QueueFree();
            UpdateQuest();
        };
        herb.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 24 } });
        herb.AddChild(new Sprite2D
        {
            Texture = texture,
            RegionEnabled = true,
            RegionRect = Atlas(41, 19),
            Scale = new Vector2(2.6f, 2.6f),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            ZIndex = 12,
        });
    }

    private static void AddSolid(Node parent, string name, Vector2 center, Vector2 size)
    {
        var body = new StaticBody2D { Name = name, Position = center };
        parent.AddChild(body);
        body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = size } });
    }

    private void UpdateQuest()
    {
        if (_questText == null) return;
        _questText.Text = _herbs >= 3
            ? "Herbs collected. Return to the elder."
            : $"Collect village herbs: {_herbs}/3";
    }

    private void UpdateWeatherStatus()
    {
        if (_weatherText == null || _weather == null) return;
        _weatherText.Text = $"{_weather.CurrentWeatherName} / RPG top-down";
    }

    private static Rect2 Atlas(int col, int row) => new(col * 17, row * 17, 16, 16);

    private static Texture2D? LoadTexture(string path)
    {
        Texture2D? texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        if (texture != null) return texture;

        string global = ProjectSettings.GlobalizePath(path);
        if (!FileAccess.FileExists(global))
            return null;

        Image image = Image.LoadFromFile(global);
        return image != null && !image.IsEmpty()
            ? ImageTexture.CreateFromImage(image)
            : null;
    }

    private static void EnsureInputActions()
    {
        AddKeyAction("move_left", Key.A, Key.Left);
        AddKeyAction("move_right", Key.D, Key.Right);
        AddKeyAction("move_up", Key.W, Key.Up);
        AddKeyAction("move_down", Key.S, Key.Down);
        AddKeyAction("inventory", Key.I);
        AddKeyAction("character", Key.C);
        AddKeyAction("quests", Key.J);
    }

    private static void AddKeyAction(string name, params Key[] keys)
    {
        if (!InputMap.HasAction(name))
            InputMap.AddAction(name);
        foreach (var key in keys)
        {
            var ev = new InputEventKey { Keycode = key };
            if (!InputMap.ActionHasEvent(name, ev))
                InputMap.ActionAddEvent(name, ev);
        }
    }

    private sealed partial class VillageMap : Node2D
    {
        public Texture2D? TileSheet { get; set; }
        private const int Tile = 48;
        private readonly Dictionary<char, Vector2I> _tiles = new()
        {
            ['g'] = new Vector2I(1, 1),
            ['G'] = new Vector2I(4, 17),
            ['p'] = new Vector2I(1, 27),
            ['w'] = new Vector2I(2, 14),
            ['f'] = new Vector2I(43, 23),
            ['r'] = new Vector2I(20, 17),
            ['b'] = new Vector2I(31, 15),
            ['s'] = new Vector2I(35, 24),
        };

        private readonly string[] _map =
        {
            "fffffffffffffffffffffffff",
            "fggggggggggpppgggggggggf",
            "fgggrrrggggpppgggwwwgggf",
            "fgggrrrggggpppgggwwwgggf",
            "fgggrrrggggpppgggggggggf",
            "fggggggggggpppgggggggggf",
            "fggggggggggpppppppppgggf",
            "fggggggggggggggggpppgggf",
            "fgggwwggggggbbbggpppgggf",
            "fgggwwggggggbbbggpppgggf",
            "fggggggggggggggggpppgggf",
            "fgggggggGGGGGGGggpppgggf",
            "fgggssssgggggggggpppgggf",
            "fgggssssgggggggggpppgggf",
            "fggggggggggggggggggggggf",
            "fffffffffffffffffffffffff",
        };

        public override void _Draw()
        {
            if (TileSheet == null) return;
            for (int y = 0; y < _map.Length; y++)
            {
                for (int x = 0; x < _map[y].Length; x++)
                {
                    char key = _map[y][x];
                    Vector2I atlas = _tiles.TryGetValue(key, out var tile) ? tile : _tiles['g'];
                    Rect2 dst = new(x * Tile, y * Tile, Tile, Tile);
                    DrawTextureRectRegion(TileSheet, dst, Atlas(atlas.X, atlas.Y));
                }
            }
        }
    }
}

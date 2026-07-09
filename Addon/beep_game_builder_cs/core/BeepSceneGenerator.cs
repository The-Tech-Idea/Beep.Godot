using Godot;

public static class BeepSceneGenerator
{
    private static CollisionShape2D ColRect(Vector2 size)
    {
        var c = new CollisionShape2D { Name = "CollisionShape2D" };
        c.Shape = new RectangleShape2D { Size = size }; return c;
    }

    private static ColorRect PlaceholderSprite(Vector2 size)
    {
        return new ColorRect { Name = "PlaceholderVisual", Size = size, Position = -size / 2f };
    }

    private static string SaveScene(Node root, string path)
    {
        var packed = new PackedScene(); packed.Pack(root);
        ResourceSaver.Save(packed, path); return path;
    }

    public static string CreateMainScene(string path = "res://scenes/main/main.tscn")
    {
        var root = new Node2D { Name = "Main" };
        AddOwned(root, new Node2D { Name = "World" });
        AddOwned(root, new Marker2D { Name = "PlayerSpawn" });
        AddOwned(root, new Node2D { Name = "NPCs" });
        AddOwned(root, new Node2D { Name = "Projectiles" });
        AddOwned(root, new Node2D { Name = "Effects" });
        AddOwned(root, new Camera2D { Name = "Camera2D" });
        AddOwned(root, new CanvasLayer { Name = "UI" });
        return SaveScene(root, path);
    }

    private static void AddOwned(Node parent, Node child) { parent.AddChild(child); child.Owner = parent; }

    public static string CreateTopDownPlayerScene(string scriptPath, string outPath = "res://scenes/player/player_top_down.tscn")
    {
        var p = new CharacterBody2D { Name = "Player" };
        AddOwned(p, PlaceholderSprite(new Vector2(32, 32)));
        AddOwned(p, ColRect(new Vector2(24, 24)));
        p.SetScript(ResourceLoader.Load<Script>(scriptPath));
        return SaveScene(p, outPath);
    }

    public static string CreatePlatformerPlayerScene(string scriptPath, string outPath = "res://scenes/player/player_platformer.tscn")
    {
        var p = new CharacterBody2D { Name = "PlatformerPlayer" };
        AddOwned(p, PlaceholderSprite(new Vector2(32, 48)));
        AddOwned(p, ColRect(new Vector2(28, 44)));
        p.SetScript(ResourceLoader.Load<Script>(scriptPath));
        return SaveScene(p, outPath);
    }

    public static string CreateRobotNpcScene(string scriptPath, string outPath = "res://scenes/npc/robot_npc.tscn")
    {
        var npc = new CharacterBody2D { Name = "RobotNPC" };
        AddOwned(npc, PlaceholderSprite(new Vector2(32, 32)));
        AddOwned(npc, ColRect(new Vector2(24, 24)));
        var det = new Area2D { Name = "DetectionArea" };
        var ds = new CollisionShape2D { Shape = new CircleShape2D { Radius = 96 } };
        AddOwned(det, ds); AddOwned(npc, det);
        npc.SetScript(ResourceLoader.Load<Script>(scriptPath));
        return SaveScene(npc, outPath);
    }

    public static string CreateMainMenu(string path = "res://scenes/ui/main_menu.tscn")
    {
        var root = new Control { Name = "MainMenu" }; root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var vb = new VBoxContainer { Name = "MenuVBox" }; vb.SetAnchorsPreset(Control.LayoutPreset.Center);
        vb.AddThemeConstantOverride("separation", 16); AddOwned(root, vb);
        var t = new Label { Name = "TitleLabel", Text = "Game Title", HorizontalAlignment = HorizontalAlignment.Center };
        AddOwned(vb, t);
        AddOwned(vb, new Button { Name = "StartButton", Text = "Start Game" });
        AddOwned(vb, new Button { Name = "OptionsButton", Text = "Options" });
        AddOwned(vb, new Button { Name = "QuitButton", Text = "Quit" });
        return SaveScene(root, path);
    }

    public static string CreatePauseMenu(string path = "res://scenes/ui/pause_menu.tscn")
    {
        var root = new Control { Name = "PauseMenu", Visible = false }; root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var panel = new PanelContainer { Name = "Panel", CustomMinimumSize = new Vector2(280, 300) };
        panel.SetAnchorsPreset(Control.LayoutPreset.Center); AddOwned(root, panel);
        var vb = new VBoxContainer(); vb.AddThemeConstantOverride("separation", 12); AddOwned(panel, vb);
        AddOwned(vb, new Label { Text = "Paused", HorizontalAlignment = HorizontalAlignment.Center });
        AddOwned(vb, new Button { Text = "Resume" });
        AddOwned(vb, new Button { Text = "Restart" });
        AddOwned(vb, new Button { Text = "Main Menu" });
        AddOwned(vb, new Button { Text = "Quit" });
        return SaveScene(root, path);
    }

    /// <summary>Copy a scene template (.tscn) from templates/scenes to the project.</summary>
    public static string CreateTemplateScene(string templateName, string targetPath = null)
    {
        targetPath ??= $"res://scenes/templates/{templateName}.tscn";
        var srcPath = $"res://addons/beep_game_builder_cs/templates/scenes/{templateName}.tscn";

        var dir = targetPath.GetBaseDir();
        if (!DirAccess.DirExistsAbsolute(dir)) DirAccess.MakeDirRecursiveAbsolute(dir);

        var packed = ResourceLoader.Load<PackedScene>(srcPath);
        if (packed == null) return $"Failed to load: {srcPath}";

        ResourceSaver.Save(packed, targetPath);
        return targetPath;
    }
}

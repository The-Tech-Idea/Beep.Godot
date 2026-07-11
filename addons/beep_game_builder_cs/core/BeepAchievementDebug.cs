using Godot;
using System;
using System.Collections.Generic;

namespace Beep.GameBuilder;

/// <summary>Achievement system. Define achievements with progress, unlock conditions, and notifications.</summary>
public static class BeepAchievementSystem
{
    private static Dictionary<string, Achievement> _achievements = new();
    private static List<string> _unlockedThisSession = new();
    public static Action<Achievement> AchievementUnlocked;

    public class Achievement
    {
        public string Id, Title, Description;
        public Texture2D Icon;
        public bool Unlocked;
        public int Progress, Target;
        public bool HasProgress => Target > 0;
    }

    public static void Register(Achievement ach) { _achievements[ach.Id] = ach; }
    public static Achievement Get(string id) => _achievements.GetValueOrDefault(id);

    public static void SetProgress(string id, int progress)
    {
        if (!_achievements.TryGetValue(id, out var ach) || ach.Unlocked) return;
        ach.Progress = Mathf.Min(progress, ach.Target);
        if (ach.Progress >= ach.Target) Unlock(id);
    }

    public static void IncrementProgress(string id) => SetProgress(id, (Get(id)?.Progress ?? 0) + 1);

    public static void Unlock(string id)
    {
        if (!_achievements.TryGetValue(id, out var ach) || ach.Unlocked) return;
        ach.Unlocked = true; ach.Progress = ach.Target;
        _unlockedThisSession.Add(id);
        AchievementUnlocked?.Invoke(ach);
    }

    public static bool IsUnlocked(string id) => _achievements.TryGetValue(id, out var a) && a.Unlocked;
    public static void Save(string path = "user://achievements.cfg")
    {
        var cfg = new ConfigFile();
        foreach (var (id, ach) in _achievements) cfg.SetValue(id, "unlocked", ach.Unlocked);
        cfg.Save(path);
    }
    public static void Load(string path = "user://achievements.cfg")
    {
        var cfg = new ConfigFile();
        if (cfg.Load(path) != Error.Ok) return;
        foreach (var id in cfg.GetSections())
            if (_achievements.TryGetValue(id, out var ach))
                ach.Unlocked = (bool)cfg.GetValue(id, "unlocked");
    }
}

/// <summary>Simple game analytics helper. Track events with timestamps for debugging/balancing.</summary>
public static class BeepAnalyticsHelper
{
    private static List<Event> _events = new();
    private static bool _enabled = true;

    public struct Event { public string Name; public Dictionary<string, object> Data; public float Time; }

    public static void Track(string name, Dictionary<string, object> data = null)
    {
        if (!_enabled) return;
        _events.Add(new Event { Name = name, Data = data, Time = Time.GetTicksMsec() / 1000f });
    }

    public static void TrackSimple(string name, params (string key, object value)[] pairs)
    {
        var dict = new Dictionary<string, object>();
        foreach (var (k, v) in pairs) dict[k] = v;
        Track(name, dict);
    }

    public static int Count(string name) => _events.FindAll(e => e.Name == name).Count;
    public static List<Event> GetAll(string name) => _events.FindAll(e => e.Name == name);
    public static void Clear() => _events.Clear();
    public static void SetEnabled(bool enabled) => _enabled = enabled;

    public static Dictionary<string, int> Summary()
    {
        var summary = new Dictionary<string, int>();
        foreach (var e in _events)
            summary[e.Name] = summary.GetValueOrDefault(e.Name, 0) + 1;
        return summary;
    }
}

/// <summary>In-game debug console. Register commands, open/close with tilde key, execute commands.</summary>
[Tool]
public partial class BeepDebugConsole : Godot.Control
{
    private RichTextLabel _output;
    private LineEdit _input;
    private VBoxContainer _box;
    private Dictionary<string, Action<string[]>> _commands = new();
    private bool _open;
    private List<string> _history = new();
    private int _historyIdx = -1;

    [Export] public int MaxLines { get; set; } = 500;
    [Export] public Color BgColor { get; set; } = new(0,0,0,0.85f);

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;

        _box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _box.SetAnchorsPreset(LayoutPreset.TopWide);
        _box.CustomMinimumSize = new Vector2(0, 300);
        _box.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = BgColor, ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 4, ContentMarginBottom = 4 });

        _output = new RichTextLabel { ScrollFollowing = true, SizeFlagsVertical = SizeFlags.ExpandFill };
        _box.AddChild(_output);

        _input = new LineEdit { PlaceholderText = "Type command..." };
        _input.TextSubmitted += ExecuteCommand;
        _box.AddChild(_input);

        AddChild(_box);

        // Default commands
        RegisterCommand("help", _ => { Log("[b]Commands:[/b] " + string.Join(", ", _commands.Keys)); });
        RegisterCommand("clear", _ => _output.Clear());
        RegisterCommand("fps", _ => Log($"[color=cyan]FPS: {Engine.GetFramesPerSecond()}[/color]"));
        RegisterCommand("time", _ => Log($"[color=cyan]Time: {Time.GetTimeStringFromSystem()}[/color]"));
    }

    public void RegisterCommand(string name, Action<string[]> handler) => _commands[name.ToLower()] = handler;
    public void UnregisterCommand(string name) => _commands.Remove(name.ToLower());

    public void Log(string message) { _output.AppendText(message + "\n"); Trim(); }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey k && k.Keycode == Key.Quoteleft && k.Pressed && !k.Echo)
        {
            Toggle();
            AcceptEvent();
        }
        if (_open && e is InputEventKey k2 && k2.Pressed)
        {
            if (k2.Keycode == Key.Up) { if (_history.Count > 0) { _historyIdx = Mathf.Min(_historyIdx + 1, _history.Count - 1); _input.Text = _history[_history.Count - 1 - _historyIdx]; _input.CaretColumn = _input.Text.Length; } }
            else if (k2.Keycode == Key.Down) { _historyIdx = Mathf.Max(_historyIdx - 1, -1); _input.Text = _historyIdx < 0 ? "" : _history[_history.Count - 1 - _historyIdx]; _input.CaretColumn = _input.Text.Length; }
            AcceptEvent();
        }
    }

    public void Toggle()
    {
        _open = !_open; Visible = _open;
        if (_open) _input.GrabFocus();
    }

    private void ExecuteCommand(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _history.Add(text); _historyIdx = -1;
        Log($"[color=gray]> {text}[/color]");
        var parts = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        var cmd = parts[0].ToLower();
        var args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

        if (_commands.TryGetValue(cmd, out var handler))
            handler(args);
        else
            Log($"[color=red]Unknown command: {cmd}[/color]");
        _input.Clear();
    }

    private void Trim()
    {
        var lines = _output.Text.Split('\n');
        if (lines.Length <= MaxLines + 10) return;
        var keep = new string[MaxLines];
        Array.Copy(lines, lines.Length - MaxLines, keep, 0, MaxLines);
        _output.Text = string.Join("\n", keep);
    }
}

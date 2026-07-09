using Godot;
using System;
using System.Collections.Generic;

/// <summary>Typed configuration manager. Auto-saves/loads config sections as Godot Variants.</summary>
public static class BeepConfigManager
{
    private static ConfigFile _cfg = new();
    private static string _path = "user://beep_config.cfg";
    private static Dictionary<string, Action> _watchers = new();

    public static void SetPath(string path) { _path = path; Load(); }

    public static void Load() { var err = _cfg.Load(_path); if (err != Error.Ok) _cfg = new ConfigFile(); }
    public static void Save() => _cfg.Save(_path);

    public static T Get<T>(string section, string key, T defaultValue = default)
    {
        if (_cfg.HasSectionKey(section, key))
            return (T)Convert.ChangeType(_cfg.GetValue(section, key).Obj, typeof(T));
        return defaultValue;
    }

    public static void Set<T>(string section, string key, T value)
    {
        _cfg.SetValue(section, key, Variant.From(value));
        Save();
        Notify($"{section}.{key}");
    }

    public static bool Has(string section, string key) => _cfg.HasSectionKey(section, key);
    public static void Erase(string section) { _cfg.EraseSection(section); Save(); }

    public static void Watch(string sectionKey, Action callback)
    {
        _watchers[sectionKey] = callback;
    }

    private static void Notify(string sectionKey)
    {
        if (_watchers.TryGetValue(sectionKey, out var cb)) cb?.Invoke();
    }
}

/// <summary>Fighting-game style input buffer. Records inputs with timestamps for combo detection.</summary>
public class BeepInputBuffer
{
    private struct Entry { public string Action; public float Time; }
    private List<Entry> _buffer = new();
    private float _window;

    public BeepInputBuffer(float windowSeconds = 0.5f) => _window = windowSeconds;

    public void Record(string action) => _buffer.Add(new Entry { Action = action, Time = Time.GetTicksMsec() / 1000f });

    /// <summary>Check if a sequence of actions was pressed in order within the window.</summary>
    public bool MatchSequence(params string[] actions)
    {
        Cleanup();
        int idx = 0;
        for (int i = _buffer.Count - 1; i >= 0 && idx < actions.Length; i--)
        {
            if (_buffer[i].Action == actions[actions.Length - 1 - idx])
                idx++;
        }
        if (idx == actions.Length && _buffer.Count > 0)
        {
            float startTime = 0;
            int found = 0;
            for (int i = 0; i < _buffer.Count && found < actions.Length; i++)
            {
                if (_buffer[i].Action == actions[found]) { if (found == 0) startTime = _buffer[i].Time; found++; }
            }
            return (_buffer[_buffer.Count - 1].Time - startTime) <= _window * actions.Length;
        }
        return false;
    }

    /// <summary>Check if the last input matches.</summary>
    public bool LastWas(string action) => _buffer.Count > 0 && _buffer[_buffer.Count - 1].Action == action;

    public void Clear() => _buffer.Clear();

    private void Cleanup()
    {
        float now = Time.GetTicksMsec() / 1000f;
        _buffer.RemoveAll(e => now - e.Time > _window * 3);
    }
}

using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Keyboard shortcut / hotkey manager. Register actions with keys and callbacks.
/// Auto-processes input in _Input and fires callbacks.
/// </summary>
public static class BeepKeybindManager
{
    private static List<Keybind> _bindings = new();
    private static bool _enabled = true;

    public class Keybind
    {
        public string Id;
        public string Label;
        public Key Key;
        public Key Modifiers;
        public Action Action;
        public bool Ctrl => Modifiers.HasFlag(Key.Ctrl);
        public bool Shift => Modifiers.HasFlag(Key.Shift);
        public bool Alt => Modifiers.HasFlag(Key.Alt);
        public string DisplayString
        {
            get
            {
                var s = "";
                if (Ctrl) s += "Ctrl+";
                if (Shift) s += "Shift+";
                if (Alt) s += "Alt+";
                s += Key.ToString();
                return s;
            }
        }
    }

    /// <summary>Register a keybind.</summary>
    public static Keybind Register(string id, string label, Key key, Action action,
        bool ctrl = false, bool shift = false, bool alt = false)
    {
        var kb = new Keybind
        {
            Id = id, Label = label, Key = key, Action = action,
            Modifiers = (ctrl ? Key.Ctrl : 0) |
                        (shift ? Key.Shift : 0) |
                        (alt ? Key.Alt : 0)
        };
        _bindings.Add(kb);
        return kb;
    }

    /// <summary>Unregister a keybind by id.</summary>
    public static void Unregister(string id) => _bindings.RemoveAll(k => k.Id == id);

    /// <summary>Rebind a keybind to a new key.</summary>
    public static void Rebind(string id, Key newKey)
    {
        var kb = _bindings.Find(k => k.Id == id);
        if (kb != null) kb.Key = newKey;
    }

    /// <summary>Process input. Call from any Node's _Input or _UnhandledInput.</summary>
    public static bool ProcessInput(InputEvent e)
    {
        if (!_enabled) return false;
        if (e is InputEventKey ek && ek.Pressed)
        {
            foreach (var kb in _bindings)
            {
                if (ek.Keycode == kb.Key &&
                    ek.CtrlPressed == kb.Ctrl &&
                    ek.ShiftPressed == kb.Shift &&
                    ek.AltPressed == kb.Alt)
                {
                    kb.Action?.Invoke();
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>Enable/disable all keybinds.</summary>
    public static void SetEnabled(bool enabled) => _enabled = enabled;

    /// <summary>Get all registered keybinds (for settings UI).</summary>
    public static IReadOnlyList<Keybind> GetAll() => _bindings.AsReadOnly();

    /// <summary>Generate a help text string of all keybinds.</summary>
    public static string GetHelpText()
    {
        var text = "";
        foreach (var kb in _bindings)
            text += $"{kb.DisplayString,-20} {kb.Label}\n";
        return text;
    }

    /// <summary>Clear all keybinds.</summary>
    public static void Clear() => _bindings.Clear();
}

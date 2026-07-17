using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Keybind manager component. Tracks game-instance keybinds and routes input.
    /// Attach to a scene (or use as autoload) to capture keyboard input and fire
    /// keybind callbacks. Supports runtime rebinding and persistence via ISaveable.
    ///
    /// Example:
    /// var mgr = GetNode&lt;KeybindManagerComponent&gt;("KeybindMgr");
    /// mgr.Register("jump", Key.Space, () => player.Jump());
    /// mgr.Register("pause", Key.Escape, () => GetTree().Paused = true);
    /// mgr.KeybindTriggered += (id) => GD.Print($"Triggered: {id}");
    /// // Player presses Space → fires player.Jump() and emits KeybindTriggered("jump")
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KeybindManagerComponent : UIComponent, ISaveable
    {
        [Export] public bool CaptureInput { get; set; } = true;

        [Signal] public delegate void KeybindTriggeredEventHandler(string keybindId);
        [Signal] public delegate void KeybindReboundEventHandler(string keybindId, string newKeyDisplay);

        private class RegisteredKeybind
        {
            public string Id;
            public string Label;
            public Key Key;
            public Key Modifiers;
            public Action? Action;

            public bool Matches(InputEventKey evt)
            {
                return evt.Keycode == Key &&
                       evt.CtrlPressed == ((Modifiers & Key.Ctrl) != 0) &&
                       evt.ShiftPressed == ((Modifiers & Key.Shift) != 0) &&
                       evt.AltPressed == ((Modifiers & Key.Alt) != 0);
            }

            public string GetDisplayString()
            {
                var s = "";
                if ((Modifiers & Key.Ctrl) != 0) s += "Ctrl+";
                if ((Modifiers & Key.Shift) != 0) s += "Shift+";
                if ((Modifiers & Key.Alt) != 0) s += "Alt+";
                s += Key.ToString();
                return s;
            }
        }

        private Dictionary<string, RegisteredKeybind> _keybinds = new();
        private bool _enabled = true;

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!IsActive || !CaptureInput || !_enabled || Engine.IsEditorHint()) return;

            if (@event is InputEventKey ek && ek.Pressed)
            {
                foreach (var (id, kb) in _keybinds)
                {
                    if (kb.Matches(ek))
                    {
                        kb.Action?.Invoke();
                        EmitSignal(SignalName.KeybindTriggered, id);
                        GetTree()?.Root.SetInputAsHandled();
                        break;
                    }
                }
            }
        }

        /// <summary>Register a keybind with a callback.</summary>
        public void Register(string id, string label, Key key, Action? action = null,
            bool ctrl = false, bool shift = false, bool alt = false)
        {
            var kb = new RegisteredKeybind
            {
                Id = id,
                Label = label,
                Key = key,
                Action = action,
                Modifiers = (ctrl ? Key.Ctrl : 0) |
                           (shift ? Key.Shift : 0) |
                           (alt ? Key.Alt : 0)
            };
            _keybinds[id] = kb;
        }

        /// <summary>Unregister a keybind.</summary>
        public void Unregister(string id) => _keybinds.Remove(id);

        /// <summary>Rebind a keybind to a new key.</summary>
        public void Rebind(string id, Key newKey)
        {
            if (_keybinds.TryGetValue(id, out var kb))
            {
                kb.Key = newKey;
                EmitSignal(SignalName.KeybindRebound, id, kb.GetDisplayString());
            }
        }

        /// <summary>Set the callback for a registered keybind.</summary>
        public void SetAction(string id, Action? action)
        {
            if (_keybinds.TryGetValue(id, out var kb))
                kb.Action = action;
        }

        /// <summary>Enable/disable all keybinds.</summary>
        public void SetEnabled(bool enabled) => _enabled = enabled;

        /// <summary>Get the current key for a keybind.</summary>
        public Key? GetKey(string id) => _keybinds.TryGetValue(id, out var kb) ? kb.Key : null;

        /// <summary>Get the display string for a keybind.</summary>
        public string? GetDisplayString(string id) => _keybinds.TryGetValue(id, out var kb) ? kb.GetDisplayString() : null;

        /// <summary>Get all registered keybind IDs.</summary>
        public IEnumerable<string> GetAllKeybindIds() => _keybinds.Keys;

        /// <summary>Clear all keybinds.</summary>
        public void Clear() => _keybinds.Clear();

        // ── ISaveable Implementation ──
        public void Save(GameBuilder.GameStateData state)
        {
            // Store custom keybinds to custom data (map id → key string)
            var keybindMap = new Godot.Collections.Dictionary();
            foreach (var (id, kb) in _keybinds)
                keybindMap[id] = kb.Key.ToString();
            state.GameData["keybinds"] = keybindMap;
        }

        public void Load(GameBuilder.GameStateData state)
        {
            if (state.GameData.TryGetValue("keybinds", out var keybindsObj))
            {
                // Restore keybinds from the saved map
                var keybindMap = keybindsObj.AsGodotDictionary();
                foreach (var key in keybindMap.Keys)
                {
                    var id = key.AsString();
                    var keyStr = keybindMap[key].AsString();
                    if (_keybinds.TryGetValue(id, out var kb))
                    {
                        // Try to parse the key string back to enum
                        if (System.Enum.TryParse<Key>(keyStr, out var parsedKey))
                            kb.Key = parsedKey;
                    }
                }
            }
        }
    }
}

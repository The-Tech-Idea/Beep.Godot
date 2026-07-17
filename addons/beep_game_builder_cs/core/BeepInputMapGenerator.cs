using Godot;
using System.Collections.Generic;

namespace Beep.GameBuilder;

public static class BeepInputMapGenerator
{
    public static List<string> SetupDefaultInput()
    {
        AddKey("move_up", Key.W); AddKey("move_up", Key.Up);
        AddKey("move_down", Key.S); AddKey("move_down", Key.Down);
        AddKey("move_left", Key.A); AddKey("move_left", Key.Left);
        AddKey("move_right", Key.D); AddKey("move_right", Key.Right);
        AddKey("jump", Key.Space);
        AddKey("attack", Key.J); AddMouse("attack", MouseButton.Left);
        AddKey("interact", Key.E);
        AddKey("dash", Key.Shift);
        AddKey("crouch", Key.Ctrl);
        AddKey("pause", Key.Escape);
        AddKey("ui_accept", Key.Enter); AddKey("ui_accept", Key.Space);
        AddKey("ui_cancel", Key.Escape);
        AddJoy("jump", JoyButton.A); AddJoy("attack", JoyButton.X);
        AddJoy("interact", JoyButton.B); AddJoy("dash", JoyButton.LeftShoulder);
        AddJoy("crouch", JoyButton.RightShoulder);
        AddJoy("pause", JoyButton.Start); AddJoy("ui_accept", JoyButton.A);
        AddJoy("ui_cancel", JoyButton.B);

        // Genre screen actions. GenreScreenComponent opens a genre's own screens (inventory,
        // crafting, deck builder…) on these; without them registered, the component has
        // nothing to listen for. Registered for every genre rather than per-genre: an action
        // no scene listens for costs nothing, and a missing one silently breaks the screen.
        // Keys chosen to not collide with the gameplay bindings above — note `attack` is
        // already on J, so the quest log takes L. Escape (pause + ui_cancel) and Space
        // (jump + ui_accept) are shared deliberately; these must not add to that.
        AddKey("inventory", Key.I); AddJoy("inventory", JoyButton.Y);
        AddKey("crafting", Key.C);
        AddKey("character", Key.P);
        AddKey("quests", Key.L);
        AddKey("map", Key.M);
        AddKey("build", Key.B);
        AddKey("research", Key.R);
        AddKey("codex", Key.K);

        var actions = new List<string> { "move_up","move_down","move_left","move_right","jump","attack","interact","dash","crouch","pause","ui_accept","ui_cancel",
                                         "inventory","crafting","character","quests","map","build","research","codex" };

        // The InputMap calls above only mutate the *runtime* singleton. Godot rebuilds
        // InputMap from ProjectSettings on every launch and never writes back, so without
        // this every generated game lost its controls the moment the editor restarted —
        // while the log still said "Input map configured".
        foreach (string action in actions) Persist(action);

        // Don't call ProjectSettings.Save() here — the generator saves once at the end.
        return actions;
    }

    /// <summary>Mirror an action from the runtime InputMap into ProjectSettings, which is
    /// what persists to project.godot. Reads the events back out of InputMap so the dedup
    /// in AddKey/AddMouse/AddJoy stays the single source of truth.</summary>
    private static void Persist(string action)
    {
        if (!InputMap.HasAction(action)) return;

        var events = new Godot.Collections.Array();
        foreach (var e in InputMap.ActionGetEvents(action)) events.Add(e);

        ProjectSettings.SetSetting($"input/{action}", new Godot.Collections.Dictionary
        {
            ["deadzone"] = InputMap.ActionGetDeadzone(action),
            ["events"] = events,
        });
    }

    private static void AddKey(string action, Key key)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action);
        foreach (var e in InputMap.ActionGetEvents(action))
            if (e is InputEventKey ke && ke.Keycode == key) return;
        InputMap.ActionAddEvent(action, new InputEventKey { Keycode = key });
    }

    private static void AddMouse(string action, MouseButton btn)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action);
        foreach (var e in InputMap.ActionGetEvents(action))
            if (e is InputEventMouseButton mb && mb.ButtonIndex == btn) return;
        InputMap.ActionAddEvent(action, new InputEventMouseButton { ButtonIndex = btn });
    }

    private static void AddJoy(string action, JoyButton btn)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action);
        foreach (var e in InputMap.ActionGetEvents(action))
            if (e is InputEventJoypadButton jb && jb.ButtonIndex == btn) return;
        InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = btn });
    }
}

using Godot;
using System.Collections.Generic;

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
        AddKey("pause", Key.Escape);
        AddKey("ui_accept", Key.Enter); AddKey("ui_accept", Key.Space);
        AddKey("ui_cancel", Key.Escape);
        AddJoy("jump", JoyButton.A); AddJoy("attack", JoyButton.X);
        AddJoy("interact", JoyButton.B); AddJoy("dash", JoyButton.LeftShoulder);
        AddJoy("pause", JoyButton.Start); AddJoy("ui_accept", JoyButton.A);
        AddJoy("ui_cancel", JoyButton.B);
        ProjectSettings.Save();
        return new List<string> { "move_up","move_down","move_left","move_right","jump","attack","interact","dash","pause","ui_accept","ui_cancel" };
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

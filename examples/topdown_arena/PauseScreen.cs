using Godot;

namespace Beep.Examples;

/// <summary>Pause overlay. Layout is `ui/pause.tscn`.</summary>
public partial class PauseScreen : Control
{
    [Signal] public delegate void ResumeEventHandler();
    [Signal] public delegate void RestartEventHandler();
    [Signal] public delegate void MenuEventHandler();

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Wire("ResumeButton", () => EmitSignal(SignalName.Resume));
        Wire("RestartButton", () => EmitSignal(SignalName.Restart));
        Wire("MenuButton", () => EmitSignal(SignalName.Menu));
    }

    private void Wire(string name, System.Action onPressed)
    {
        if (FindChild(name, true, false) is Button b) b.Pressed += onPressed;
        else GD.PushWarning($"[PauseScreen] pause.tscn has no {name} — that action is unreachable.");
    }
}

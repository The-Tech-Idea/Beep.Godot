using Godot;

namespace Beep.Examples;

/// <summary>Win/lose card. Layout is `ui/result.tscn`; this only fills it in and reports clicks.</summary>
public partial class ResultScreen : Control
{
    [Signal] public delegate void AgainEventHandler();
    [Signal] public delegate void MenuEventHandler();

    private Label _title = null!, _body = null!;

    public override void _Ready()
    {
        // Runs while the tree is paused -- the game pauses BEFORE showing this, so without it the
        // buttons would be inert and the card would never lay out.
        ProcessMode = ProcessModeEnum.Always;

        _title = FindChild("TitleLabel", true, false) as Label
                 ?? throw new System.InvalidOperationException("result.tscn has no TitleLabel");
        _body = FindChild("BodyLabel", true, false) as Label
                ?? throw new System.InvalidOperationException("result.tscn has no BodyLabel");

        if (FindChild("AgainButton", true, false) is Button again)
            again.Pressed += () => EmitSignal(SignalName.Again);
        else GD.PushWarning("[ResultScreen] result.tscn has no AgainButton — nothing restarts.");

        if (FindChild("MenuButton", true, false) is Button menu)
            menu.Pressed += () => EmitSignal(SignalName.Menu);
        else GD.PushWarning("[ResultScreen] result.tscn has no MenuButton — cannot leave the run.");
    }

    public void Show(bool won, int score, int collected, int total)
    {
        _title.Text = won ? "ARENA CLEARED" : "YOU DIED";
        _body.Text = won
            ? $"All {total} coins.   Score {score}."
            : $"{collected}/{total} coins.   Score {score}.";
        Visible = true;
    }
}

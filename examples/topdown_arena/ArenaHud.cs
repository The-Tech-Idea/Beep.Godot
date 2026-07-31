using Godot;
using Beep.ECS.UI.Kit;

namespace Beep.Examples;

/// <summary>
/// The HUD's behaviour. Its LAYOUT is `ui/hud.tscn` — a KitPanel with two captions and a KitMeter,
/// all editable in the editor.
///
/// Worth copying: nothing here picks a colour, corner radius, outline weight or font. All of it
/// comes from the active skin via the ThemePresetComponent in the scene, so changing the theme
/// restyles the whole HUD — including the silhouette and the type hierarchy.
/// </summary>
public partial class ArenaHud : Control
{
    private Label _score = null!, _coins = null!;
    private KitMeter _health = null!;

    public override void _Ready()
    {
        // BY NAME, scene-wide. A path would hard-code the layout and break the moment someone
        // wraps the rows in another container.
        _score = FindChild("ScoreLabel", true, false) as Label
                 ?? throw new System.InvalidOperationException("hud.tscn has no ScoreLabel");
        _coins = FindChild("CoinsLabel", true, false) as Label
                 ?? throw new System.InvalidOperationException("hud.tscn has no CoinsLabel");
        _health = FindChild("HealthMeter", true, false) as KitMeter
                  ?? throw new System.InvalidOperationException("hud.tscn has no HealthMeter");
    }

    public void Set(int score, int collected, int total, float hp, float maxHp)
    {
        _score.Text = $"SCORE {score}";
        _coins.Text = $"COINS {collected}/{total}";
        // KitMeter.Value is a RATIO in 0..1 -- it has no MaxValue, and assigning raw health would
        // clamp to 1 and read as full forever.
        _health.Value = maxHp <= 0f ? 0f : Mathf.Clamp(hp / maxHp, 0f, 1f);
    }
}

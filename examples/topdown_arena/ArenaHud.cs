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
    private KitLabelValue _score = null!, _coins = null!;
    private KitMeter _health = null!;

    public override void _Ready()
    {
        // BY NAME, scene-wide. A path would hard-code the layout and break the moment someone
        // wraps the rows in another container.
        _score = FindChild("ScoreRow", true, false) as KitLabelValue
                 ?? throw new System.InvalidOperationException("hud.tscn has no ScoreRow");
        _coins = FindChild("CoinsRow", true, false) as KitLabelValue
                 ?? throw new System.InvalidOperationException("hud.tscn has no CoinsRow");
        _health = FindChild("HealthMeter", true, false) as KitMeter
                  ?? throw new System.InvalidOperationException("hud.tscn has no HealthMeter");
    }

    public void Set(int score, int collected, int total, float hp, float maxHp)
    {
        // Label and VALUE are separate fields, so the number takes the accent colour and the
        // BeepValue size while the caption stays quiet. Concatenating them into one caption made
        // the score a footnote -- same size, same colour, no grid between the two rows.
        _score.Value = score.ToString();
        _coins.Value = $"{collected}/{total}";
        // KitMeter.Value is a RATIO in 0..1 -- it has no MaxValue, and assigning raw health would
        // clamp to 1 and read as full forever.
        _health.Value = maxHp <= 0f ? 0f : Mathf.Clamp(hp / maxHp, 0f, 1f);
    }
}

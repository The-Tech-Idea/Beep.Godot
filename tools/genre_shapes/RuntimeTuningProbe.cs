using Godot;
using Beep.ECS;
using Beep.GameBuilder;

/// Closes the second half of the tuning chain: GameInfo -> the components.
///
/// GameInfo.Instance resolves through GameApp.Instance, which looks up the ABSOLUTE path
/// /root/GameApp. Four attempts to fake that by adding a GameApp node failed, because a node
/// parented anywhere lands at /root/Something/GameApp. The running scene's ROOT, however, sits at
/// exactly /root/<its name> — so a probe scene whose root IS the GameApp satisfies the lookup with
/// no autoload and no generated project.
public partial class RuntimeTuningProbe : Node
{
    public override void _Ready() => _ = Run();

    private async System.Threading.Tasks.Task Run()
    {
        int bad = 0;
        var app = GetParent() as GameApp;
        if (app == null) { GD.Print("rt:     FAIL parent is not a GameApp"); GetTree().Quit(1); return; }

        // survival's real numbers: a 14-day season and a 12C ambient, against export defaults of
        // 7 and 20. Distinct on purpose — equal values cannot tell wired from inert.
        app.Info = new GameInfo { EnableSeasons = true, DaysPerSeason = 14.0,
                                  EnableTemperature = true, AmbientTemperature = 12f };
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        bool ok = GameInfo.Instance != null;
        GD.Print($"rt:     {(ok ? "ok  " : "FAIL")} GameInfo.Instance resolves via /root/GameApp");
        if (!ok) { GD.Print("rt:     FAIL (1)"); GetTree().Quit(1); return; }

        var season = new SeasonalComponent();
        AddChild(season);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ok = Mathf.IsEqualApprox((float)season.DaysPerSeason, 14f);
        GD.Print($"rt:     {(ok ? "ok  " : "FAIL")} SeasonalComponent.DaysPerSeason = "
               + $"{season.DaysPerSeason:0} (GameInfo 14, export default 7)");
        if (!ok) bad++;

        var temp = new TemperatureComponent();
        AddChild(temp);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ok = Mathf.IsEqualApprox(temp.AmbientTemp, 12f);
        GD.Print($"rt:     {(ok ? "ok  " : "FAIL")} TemperatureComponent.AmbientTemp = "
               + $"{temp.AmbientTemp:0}C (GameInfo 12, export default 20)");
        if (!ok) bad++;

        var authored = new SeasonalComponent { DaysPerSeason = 21.0 };
        AddChild(authored);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ok = Mathf.IsEqualApprox((float)authored.DaysPerSeason, 21f);
        GD.Print($"rt:     {(ok ? "ok  " : "FAIL")} an inspector-authored 21 still beats "
               + $"GameInfo's 14 -> {authored.DaysPerSeason:0}");
        if (!ok) bad++;

        GD.Print($"rt:     {(bad == 0 ? "PASS" : $"FAIL ({bad})")}");
        GetTree().Quit(bad == 0 ? 0 : 1);
    }
}

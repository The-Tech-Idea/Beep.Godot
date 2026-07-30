using Godot;
using Beep.GameBuilder;

/// Prove BeepSceneDrift reports every outcome. A check only ever observed taking its
/// "nothing generated" branch is not evidence that the comparison works.
///
/// Tests the HELPER, not the dock: BeepGameBuilderDock builds an EditorResourcePicker in _Ready,
/// so instantiating it headlessly segfaults.
public partial class DriftProbe : Node
{
    public override void _Ready()
    {
        const string T = "res://tmp/drift/templates";
        const string G = "res://tmp/drift/scenes";
        foreach (string d in new[] { T, G, $"{G}/sub" }) DirAccess.MakeDirRecursiveAbsolute(d);

        Write($"{T}/same.tscn",   "[gd_scene format=3]\n");
        Write($"{G}/same.tscn",   "[gd_scene format=3]\n");
        Write($"{T}/behind.tscn", "[gd_scene format=3]\nNEW\n");
        Write($"{G}/behind.tscn", "[gd_scene format=3]\nOLD\n");
        Write($"{G}/mine.tscn",   "[gd_scene format=3]\n");
        Write($"{G}/sub/same.tscn", "[gd_scene format=3]\n");   // duplicate basename

        int fails = 0;
        var r = BeepSceneDrift.Compare(T, G);
        fails += Expect(r.HasGeneratedProject, true, "sees the project");
        fails += Expect(r.UpToDate, 1, "one up to date");
        fails += Expect(r.Drifted.Count, 1, "one drifted");
        fails += Expect(r.Drifted.Contains("behind.tscn"), true, "names the drifted one");
        fails += Expect(r.OwnScreens, 1, "one own screen");
        fails += Expect(r.Ambiguous.Count, 1, "duplicate basename reported");

        var empty = BeepSceneDrift.Compare(T, "res://tmp/drift/nope");
        fails += Expect(empty.HasGeneratedProject, false, "empty project detected");
        fails += Expect(BeepSceneDrift.Describe(empty).Count, 1, "empty project explained");

        GD.Print($"drift: {(fails == 0 ? "PASS" : $"FAIL ({fails})")}");
        foreach (string l in BeepSceneDrift.Describe(r)) GD.Print("drift:   " + l);
        GetTree().Quit(fails == 0 ? 0 : 1);
    }

    private static int Expect(object got, object want, string what)
    {
        bool ok = got.Equals(want);
        GD.Print($"drift: [{(ok ? "ok " : "FAIL")}] {what}: got {got}, want {want}");
        return ok ? 0 : 1;
    }

    private static void Write(string path, string text)
    {
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        f?.StoreString(text);
    }
}

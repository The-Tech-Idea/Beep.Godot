using Godot;
using System.Collections.Generic;

public static class BeepValidator
{
    public static List<string> Validate()
    {
        var msgs = new List<string>();
        var folders = new[] { "res://scenes/main","res://scenes/player","res://scenes/npc","res://scenes/ui","res://scenes/effects","res://scenes/projectiles","res://scripts/player","res://scripts/npc","res://scripts/managers","res://assets/shaders","res://assets/particles","res://autoload" };
        foreach (var f in folders) msgs.Add((DirAccess.DirExistsAbsolute(f) ? "OK: " : "Missing: ") + f);
        foreach (var a in new[] { "move_up","move_down","move_left","move_right","jump","attack","interact","dash","pause","ui_accept","ui_cancel" })
            msgs.Add((InputMap.HasAction(a) ? "Input OK: " : "Input missing: ") + a);
        var main = (string)ProjectSettings.GetSetting("application/run/main_scene", "");
        msgs.Add("Main scene: " + (main.Length > 0 ? main : "Not set"));
        foreach (var fp in new[] { "res://scenes/main/main.tscn","res://scenes/player/player_top_down.tscn","res://scenes/npc/robot_npc.tscn","res://scenes/ui/main_menu.tscn","res://scenes/ui/pause_menu.tscn" })
            msgs.Add((FileAccess.FileExists(fp) ? "OK: " : "Missing: ") + fp);
        foreach (var sp in new[] { "res://scripts/managers/scene_manager.gd","res://scripts/managers/save_manager.gd","res://scripts/managers/audio_manager.gd" })
            msgs.Add((FileAccess.FileExists(sp) ? "OK: " : "Missing: ") + sp);
        msgs.Add(FileAccess.FileExists("res://export_presets.cfg") ? "OK: export presets" : "WARNING: no export_presets.cfg");
        return msgs;
    }

    public static List<string> FixSafeIssues()
    {
        var msgs = new List<string>();
        var folders = new[] { "res://scenes/main","res://scenes/player","res://scenes/npc","res://scenes/ui","res://scenes/effects/particles","res://scenes/projectiles","res://scripts/player","res://scripts/npc","res://scripts/managers","res://assets/shaders","res://assets/particles","res://autoload" };
        foreach (var f in folders)
            if (!DirAccess.DirExistsAbsolute(f)) { DirAccess.MakeDirRecursiveAbsolute(f); msgs.Add("Fixed: " + f); }
        if (msgs.Count == 0) msgs.Add("No issues to fix.");
        return msgs;
    }

    public static void WriteReport(string path)
    {
        var lines = Validate();
        var md = "# Validation Report\n\n| Status | Item |\n|--------|------|\n";
        foreach (var l in lines)
        {
            var status = l.StartsWith("OK:") ? "OK" : l.StartsWith("ERROR:") ? "ERROR" : l.StartsWith("WARNING:") ? "WARNING" : l.StartsWith("Missing:") ? "MISSING" : "";
            var parts = l.Split(": ", 2);
            var display = parts.Length > 1 ? parts[1] : l;
            md += $"| {status} | {display} |\n";
        }
        BeepFileUtils.SafeWriteText(path, md, true);
    }
}

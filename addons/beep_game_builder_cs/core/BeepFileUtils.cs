using Godot;
using System;
using System.Collections.Generic;

namespace Beep.GameBuilder;

public static class BeepFileUtils
{
    public static Action<string> LogCallback = _ => { };
    public static Action<string> ErrorCallback = _ => { };

    public static void Log(string msg) => LogCallback(msg);
    public static void LogError(string msg) => ErrorCallback(msg);

    public static void EnsureDir(string path)
    {
        if (!DirAccess.DirExistsAbsolute(path))
            DirAccess.MakeDirRecursiveAbsolute(path);
    }

    public static bool SafeWriteText(string path, string content, bool overwrite = false)
    {
        if (!overwrite && Godot.FileAccess.FileExists(path)) { Log($"Skipped (exists): {path}"); return false; }
        var dir = path.GetBaseDir(); EnsureDir(dir);
        using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
        if (f == null) { LogError($"Could not write: {path}"); return false; }
        f.StoreString(content);
        // UID assignment is handled by Godot's own filesystem scan (RefreshFilesystem)
        // — hand-rolling uid:// strings Godot doesn't recognize as registered UIDs.
        Log($"Created: {path}"); return true;
    }

    public static string ReadText(string path)
    {
        if (!Godot.FileAccess.FileExists(path)) return "";
        using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        return f?.GetAsText() ?? "";
    }

    public static bool FileExists(string path) => Godot.FileAccess.FileExists(path);
    public static bool DirExists(string path) => DirAccess.DirExistsAbsolute(path);

    public static void RefreshFilesystem()
    {
        // EditorInterface.Singleton is null outside the editor. StampProject/CreateProject are public and
        // MCP-reachable, so this can be called at runtime — guard rather than NRE there.
        if (Engine.IsEditorHint())
            EditorInterface.Singleton.GetResourceFilesystem().Scan();
    }

    public static void SaveCurrentScene() =>
        EditorInterface.Singleton.SaveScene();

    public static Godot.Collections.Dictionary LoadJson(string path)
    {
        if (!Godot.FileAccess.FileExists(path)) { LogError($"JSON not found: {path}"); return new(); }
        using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        if (f == null) return new();
        var text = f.GetAsText();
        var json = new Json();
        if (json.Parse(text) != Error.Ok) { LogError($"JSON parse error: {path}"); return new(); }
        return json.Data.AsGodotDictionary();
    }
}

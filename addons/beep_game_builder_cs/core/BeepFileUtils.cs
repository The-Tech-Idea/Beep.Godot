using Godot;
using System;
using System.Collections.Generic;

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
        // Generate .uid file so Godot 4.x recognizes the resource in FileSystem
        EnsureUid(path);
        Log($"Created: {path}"); return true;
    }

    /// <summary>Create a .uid file if one doesn't exist, so Godot 4.x shows the file in FileSystem.</summary>
    private static void EnsureUid(string path)
    {
        var uidPath = path + ".uid";
        if (Godot.FileAccess.FileExists(uidPath)) return;
        string uid = "uid://" + Guid.NewGuid().ToString("N")[..13];
        using var uf = Godot.FileAccess.Open(uidPath, Godot.FileAccess.ModeFlags.Write);
        if (uf != null) uf.StoreString(uid);
    }

    public static string ReadText(string path)
    {
        if (!Godot.FileAccess.FileExists(path)) return "";
        using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        return f?.GetAsText() ?? "";
    }

    public static bool FileExists(string path) => Godot.FileAccess.FileExists(path);
    public static bool DirExists(string path) => DirAccess.DirExistsAbsolute(path);

    public static void RefreshFilesystem() =>
        EditorInterface.Singleton.GetResourceFilesystem().Scan();

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

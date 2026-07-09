using Godot;
using System;
using System.Collections.Generic;

/// <summary>Object pool for frequently created/destroyed UI nodes (damage numbers, bullets, particles).</summary>
public class BeepPoolManager<T> where T : Node
{
    private Queue<T> _pool = new();
    private PackedScene _scene;
    private Node _parent;
    private int _maxSize = 50;

    public BeepPoolManager(PackedScene scene, Node parent, int preload = 5, int maxSize = 50)
    {
        _scene = scene;
        _parent = parent;
        _maxSize = maxSize;
        for (int i = 0; i < preload; i++) Return(CreateNew());
    }

    public T Get()
    {
        if (_pool.Count > 0)
        {
            var obj = _pool.Dequeue();
            if (GodotObject.IsInstanceValid(obj)) { obj.SetProcess(true); obj.SetPhysicsProcess(true); return obj; }
        }
        return _pool.Count + 1 <= _maxSize ? CreateNew() : null;
    }

    public void Return(T obj)
    {
        if (obj == null || !GodotObject.IsInstanceValid(obj)) return;
        obj.SetProcess(false);
        obj.SetPhysicsProcess(false);
        if (obj is CanvasItem ci) ci.Visible = false;
        _pool.Enqueue(obj);
    }

    public void ReturnDeferred(T obj)
    {
        if (obj == null || !GodotObject.IsInstanceValid(obj)) return;
        obj.SetProcess(false);
        obj.SetPhysicsProcess(false);
        if (obj is CanvasItem ci) ci.Visible = false;
        _pool.Enqueue(obj);
    }

    public int Available => _pool.Count;

    private T CreateNew()
    {
        var inst = _scene.Instantiate<T>();
        _parent.AddChild(inst);
        return inst;
    }
}

/// <summary>Simple save/load manager using Godot ConfigFile. Works with Variant-compatible types.</summary>
public static class BeepSaveManager
{
    private static ConfigFile _config = new();
    private static string _path = "user://beep_save.cfg";

    public static void SetPath(string path) => _path = path;

    public static void Save<T>(string section, string key, T value)
    {
        _config.SetValue(section, key, Variant.From(value));
        _config.Save(_path);
    }

    public static T Load<T>(string section, string key, T defaultValue = default)
    {
        if (_config.HasSectionKey(section, key))
        {
            var v = _config.GetValue(section, key);
            return (T)Convert.ChangeType(v.Obj, typeof(T));
        }
        return defaultValue;
    }

    public static bool HasKey(string section, string key) => _config.HasSectionKey(section, key);
    public static void DeleteSection(string section) { _config.EraseSection(section); _config.Save(_path); }
    public static void DeleteAll() => DirAccess.RemoveAbsolute(_path);

    public static void LoadFile() { var err = _config.Load(_path); if (err != Error.Ok) _config = new ConfigFile(); }

    public static string[] GetSections() => _config.GetSections();
    public static string[] GetKeys(string section) => _config.GetSectionKeys(section);
}

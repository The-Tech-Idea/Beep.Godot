using Godot;
using System.Collections.Generic;

public static class BeepProjectGenerator
{
    public static List<string> CreateStandardFolders()
    {
        var folders = new List<string>
        {
            "res://scenes/main","res://scenes/player","res://scenes/npc","res://scenes/ui",
            "res://scenes/effects","res://scenes/projectiles",
            "res://scripts/player","res://scripts/npc","res://scripts/managers",
            "res://scripts/ui","res://scripts/effects","res://scripts/projectiles",
            "res://assets/sprites","res://assets/audio","res://assets/fonts",
            "res://assets/shaders","res://assets/particles",
            "res://resources/materials","res://resources/themes","res://resources/data",
            "res://autoload",
        };
        foreach (var f in folders) BeepFileUtils.EnsureDir(f);
        return folders;
    }
}

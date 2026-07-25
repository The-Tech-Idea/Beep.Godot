using System.Text.Json.Nodes;
using Godot;

namespace GodotMcp;

/// <summary>
/// PARTIAL: Phase 4 — editor lifecycle and play control.
///
/// The gates (dotnet build, validate_scenes.sh) run on the host and live in the MCP
/// server. What only Godot can do is here: make the editor re-import, reload scripts,
/// save, and actually PLAY something.
///
/// That last one matters most. This repo's honesty rule exists because nothing could ever
/// run the game:
///   "Neither gate runs the game. Compile-clean + validator-PASS says the code loads,
///    not that it works."
/// play.scene + view.capture + log.tail is the first combination in this project's history
/// that can answer "does it actually work".
///
/// See docs/mcp/PHASE_4_AUTONOMY.md.
/// </summary>
public partial class GodotMcpBridgeController
{
    private bool TryExecuteLifecycle(string method, JsonObject p, out JsonNode? result)
    {
        switch (method)
        {
            case "editor.rescan_filesystem": result = EditorRescan(); return true;
            case "editor.reload_scripts": result = EditorReloadScripts(); return true;
            case "editor.save_all": result = EditorSaveAll(); return true;
            case "play.scene": result = PlayScene(p); return true;
            case "play.current": result = PlayCurrent(); return true;
            case "play.stop": result = PlayStop(); return true;
            case "play.state": result = PlayState(); return true;
            default: result = null; return false;
        }
    }

    /// <summary>Re-import the filesystem.
    ///
    /// Godot will not load a file it has not imported, so freshly written assets are
    /// invisible to ResourceLoader until this runs — which looks exactly like the thing
    /// that wrote them having done nothing. Baking 208 textures and then finding every
    /// slot still falling back to procedural is that failure.</summary>
    private JsonNode EditorRescan()
    {
#if TOOLS
        RequireWrites();
        var fs = EditorInterface.Singleton?.GetResourceFilesystem()
            ?? throw new McpBridgeException("NOT_EDITOR", "No EditorFileSystem — this is not an editor session.");
        fs.Scan();
        return new JsonObject { ["scanning"] = true, ["note"] = "Scan is asynchronous; give it a moment before loading new files." };
#else
        throw new McpBridgeException("NOT_EDITOR", "editor.rescan_filesystem is editor-only.");
#endif
    }

    private JsonNode EditorReloadScripts()
    {
#if TOOLS
        RequireWrites();
        // C# reload is driven by the build; this refreshes what the editor holds.
        EditorInterface.Singleton?.GetResourceFilesystem()?.Scan();
        return new JsonObject
        {
            ["reloaded"] = true,
            ["note"] = "For C#, run the build gate (beep_gate_build) first — Godot only registers a [GlobalClass] it has compiled.",
        };
#else
        throw new McpBridgeException("NOT_EDITOR", "editor.reload_scripts is editor-only.");
#endif
    }

    private JsonNode EditorSaveAll()
    {
#if TOOLS
        RequireWrites();
        EditorInterface.Singleton?.SaveAllScenes();
        return new JsonObject { ["saved"] = true };
#else
        throw new McpBridgeException("NOT_EDITOR", "editor.save_all is editor-only.");
#endif
    }

    /// <summary>Play a specific scene. Combine with view.capture and log.tail to find out
    /// whether it works, rather than whether it loads.</summary>
    private JsonNode PlayScene(JsonObject p)
    {
#if TOOLS
        RequireWrites();
        string scene = RequiredString(p, "scene");
        if (!ResourceLoader.Exists(scene))
            throw new McpBridgeException("RESOURCE_NOT_FOUND", $"No scene at '{scene}'.");

        var ei = EditorInterface.Singleton
            ?? throw new McpBridgeException("NOT_EDITOR", "play.scene is editor-only.");
        ei.PlayCustomScene(scene);
        return new JsonObject
        {
            ["playing"] = scene,
            ["note"] = "The running game connects as the 'runtime' role; give it a second, then use runtime tools.",
        };
#else
        throw new McpBridgeException("NOT_EDITOR", "play.scene is editor-only.");
#endif
    }

    private JsonNode PlayCurrent()
    {
#if TOOLS
        RequireWrites();
        var ei = EditorInterface.Singleton
            ?? throw new McpBridgeException("NOT_EDITOR", "play.current is editor-only.");
        var root = ei.GetEditedSceneRoot()
            ?? throw McpBridgeException.NoSceneOpen();
        ei.PlayCurrentScene();
        return new JsonObject { ["playing"] = root.SceneFilePath };
#else
        throw new McpBridgeException("NOT_EDITOR", "play.current is editor-only.");
#endif
    }

    private JsonNode PlayStop()
    {
#if TOOLS
        RequireWrites();
        EditorInterface.Singleton?.StopPlayingScene();
        return new JsonObject { ["stopped"] = true };
#else
        throw new McpBridgeException("NOT_EDITOR", "play.stop is editor-only.");
#endif
    }

    private JsonNode PlayState()
    {
#if TOOLS
        var ei = EditorInterface.Singleton;
        return new JsonObject
        {
            ["playing"] = ei?.IsPlayingScene() ?? false,
            ["scene"] = ei?.IsPlayingScene() == true ? ei.GetPlayingScene() : null,
        };
#else
        return new JsonObject { ["playing"] = false, ["note"] = "Not an editor session." };
#endif
    }
}

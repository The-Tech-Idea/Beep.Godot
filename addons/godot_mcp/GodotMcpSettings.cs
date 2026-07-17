using System;
using Godot;
using Environment = System.Environment;

namespace GodotMcp;

public static class GodotMcpSettings
{
    public const string Version = "0.2.0";
    public const string BridgeName = "godot-mcp-csharp";

    public const string Url = "godot_mcp/bridge/url";
    public const string Token = "godot_mcp/bridge/token";
    public const string AutoConnectEditor = "godot_mcp/bridge/auto_connect_editor";
    public const string AutoConnectRuntime = "godot_mcp/bridge/auto_connect_runtime";
    public const string ReconnectSeconds = "godot_mcp/bridge/reconnect_seconds";
    public const string VerboseLogging = "godot_mcp/bridge/verbose_logging";

    public const string AllowEditorWrites = "godot_mcp/security/allow_editor_writes";
    public const string AllowRuntimeWrites = "godot_mcp/security/allow_runtime_writes";
    public const string AllowNodeMethodCalls = "godot_mcp/security/allow_node_method_calls";
    public const string ScreenshotDirectory = "godot_mcp/runtime/screenshot_directory";

    public const string DefaultUrl = "ws://127.0.0.1:8789";
    public const string DefaultScreenshotDirectory = "user://mcp_screenshots";

    public static void EnsureProjectSettings()
    {
        bool dirty = false;

        // Migrate a stale URL from an older version, but only when it actually differs.
        // This used to write unconditionally, which re-saved project.godot on every
        // launch and made an open editor prompt "reload from disk?" each time.
        if (ProjectSettings.HasSetting(Url))
        {
            if (ProjectSettings.GetSetting(Url).AsString() != DefaultUrl)
            {
                ProjectSettings.SetSetting(Url, DefaultUrl);
                dirty = true;
            }
        }
        else
        {
            ProjectSettings.SetSetting(Url, DefaultUrl);
            dirty = true;
        }

        dirty |= EnsureString(Token, GenerateTokenIfNeeded());
        dirty |= EnsureBool(AutoConnectEditor, true);
        dirty |= EnsureBool(AutoConnectRuntime, true);
        dirty |= EnsureFloat(ReconnectSeconds, 2.0f);
        dirty |= EnsureBool(VerboseLogging, true);

        // Safe defaults: reads and connection work immediately; writes require explicit opt-in.
        dirty |= EnsureBool(AllowEditorWrites, false);
        dirty |= EnsureBool(AllowRuntimeWrites, false);
        dirty |= EnsureBool(AllowNodeMethodCalls, false);
        dirty |= EnsureString(ScreenshotDirectory, DefaultScreenshotDirectory);

        // Persist ONLY from the editor, and only when something actually changed.
        // This also runs from the GodotMcpRuntime autoload inside the running game,
        // where writing project.godot would dirty the file under the open editor
        // (and wouldn't persist in an exported build anyway).
        if (dirty && Engine.IsEditorHint())
            ProjectSettings.Save();
    }

    public static string GetUrl()
    {
        string? env = Environment.GetEnvironmentVariable("GODOT_MCP_BRIDGE_URL");
        if (!string.IsNullOrWhiteSpace(env))
            return env.Trim();
        return GetString(Url, DefaultUrl);
    }

    public static string GetToken()
    {
        string? env = Environment.GetEnvironmentVariable("GODOT_MCP_BRIDGE_TOKEN");
        if (!string.IsNullOrWhiteSpace(env))
            return env.Trim();
        return GetString(Token, string.Empty);
    }

    public static bool GetBool(string key, bool fallback)
    {
        if (!ProjectSettings.HasSetting(key))
            return fallback;
        return ProjectSettings.GetSetting(key).AsBool();
    }

    public static float GetFloat(string key, float fallback)
    {
        if (!ProjectSettings.HasSetting(key))
            return fallback;
        return (float)ProjectSettings.GetSetting(key).AsDouble();
    }

    public static string GetString(string key, string fallback)
    {
        if (!ProjectSettings.HasSetting(key))
            return fallback;
        string value = ProjectSettings.GetSetting(key).AsString();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    public static void SetString(string key, string value)
    {
        ProjectSettings.SetSetting(key, value ?? string.Empty);
        SaveIfEditor();
    }

    public static void SetBool(string key, bool value)
    {
        ProjectSettings.SetSetting(key, value);
        SaveIfEditor();
    }

    /// <summary>Persist project.godot only from the editor. Writing it from the running
    /// game changes the file under an open editor, which triggers a "reload from disk?"
    /// prompt — and it wouldn't persist in an exported build anyway.</summary>
    private static void SaveIfEditor()
    {
        if (Engine.IsEditorHint())
            ProjectSettings.Save();
    }

    private static string GenerateTokenIfNeeded()
    {
        if (ProjectSettings.HasSetting(Token))
        {
            string existing = ProjectSettings.GetSetting(Token).AsString();
            if (!string.IsNullOrWhiteSpace(existing))
                return existing;
        }
        return Guid.NewGuid().ToString("N");
    }

    /// <summary>Seed the setting if absent. Returns true when it actually wrote, so the
    /// caller only saves project.godot when something really changed.
    /// AddPropertyInfo is editor metadata only and never dirties the file.</summary>
    private static bool EnsureString(string key, string defaultValue)
    {
        bool wrote = false;
        if (!ProjectSettings.HasSetting(key))
        {
            ProjectSettings.SetSetting(key, defaultValue);
            wrote = true;
        }

        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            ["name"] = key,
            ["type"] = (int)Variant.Type.String
        });
        return wrote;
    }

    private static bool EnsureBool(string key, bool defaultValue)
    {
        bool wrote = false;
        if (!ProjectSettings.HasSetting(key))
        {
            ProjectSettings.SetSetting(key, defaultValue);
            wrote = true;
        }

        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            ["name"] = key,
            ["type"] = (int)Variant.Type.Bool
        });
        return wrote;
    }

    private static bool EnsureFloat(string key, float defaultValue)
    {
        bool wrote = false;
        if (!ProjectSettings.HasSetting(key))
        {
            ProjectSettings.SetSetting(key, defaultValue);
            wrote = true;
        }

        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            ["name"] = key,
            ["type"] = (int)Variant.Type.Float
        });
        return wrote;
    }
}

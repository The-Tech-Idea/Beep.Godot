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
        // Force-write URL so upgrades pick up the correct port (overrides stale cached value)
        ProjectSettings.SetSetting(Url, DefaultUrl);
        EnsureString(Token, GenerateTokenIfNeeded());
        EnsureBool(AutoConnectEditor, true);
        EnsureBool(AutoConnectRuntime, true);
        EnsureFloat(ReconnectSeconds, 2.0f);
        EnsureBool(VerboseLogging, true);

        // Safe defaults: reads and connection work immediately; writes require explicit opt-in.
        EnsureBool(AllowEditorWrites, false);
        EnsureBool(AllowRuntimeWrites, false);
        EnsureBool(AllowNodeMethodCalls, false);
        EnsureString(ScreenshotDirectory, DefaultScreenshotDirectory);
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
        ProjectSettings.Save();
    }

    public static void SetBool(string key, bool value)
    {
        ProjectSettings.SetSetting(key, value);
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

    private static void EnsureString(string key, string defaultValue)
    {
        if (!ProjectSettings.HasSetting(key))
            ProjectSettings.SetSetting(key, defaultValue);

        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            ["name"] = key,
            ["type"] = (int)Variant.Type.String
        });
    }

    private static void EnsureBool(string key, bool defaultValue)
    {
        if (!ProjectSettings.HasSetting(key))
            ProjectSettings.SetSetting(key, defaultValue);

        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            ["name"] = key,
            ["type"] = (int)Variant.Type.Bool
        });
    }

    private static void EnsureFloat(string key, float defaultValue)
    {
        if (!ProjectSettings.HasSetting(key))
            ProjectSettings.SetSetting(key, defaultValue);

        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            ["name"] = key,
            ["type"] = (int)Variant.Type.Float
        });
    }
}

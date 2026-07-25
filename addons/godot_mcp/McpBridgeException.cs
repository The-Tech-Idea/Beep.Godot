using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace GodotMcp;

/// <summary>
/// An error an agent can act on.
///
/// Before this, every failure came back as `{ok:false, error:"…", error_type:"…"}` — a
/// human sentence and a C# exception name. An agent could only regex it, and could not
/// tell a deliberate refusal (writes disabled) from a genuine crash, or learn what to do
/// next. Every failure the bridge raises on purpose now carries a stable <see cref="Code"/>
/// plus a <see cref="Fix"/> naming the next action.
///
/// The old two fields are still emitted alongside, so an older client keeps working.
/// </summary>
public sealed class McpBridgeException : Exception
{
    public string Code { get; }
    public string? Fix { get; }
    public IReadOnlyDictionary<string, object?>? Detail { get; }

    public McpBridgeException(string code, string message, string? fix = null,
                              IReadOnlyDictionary<string, object?>? detail = null)
        : base(message)
    {
        Code = code;
        Fix = fix;
        Detail = detail;
    }

    public JsonObject ToDetailJson()
    {
        var obj = new JsonObject();
        if (Detail is null) return obj;
        foreach (var kv in Detail)
        {
            obj[kv.Key] = kv.Value switch
            {
                null => null,
                string s => JsonValue.Create(s),
                bool b => JsonValue.Create(b),
                int i => JsonValue.Create(i),
                long l => JsonValue.Create(l),
                float f => JsonValue.Create(f),
                double d => JsonValue.Create(d),
                JsonNode n => n.DeepClone(),
                _ => JsonValue.Create(kv.Value.ToString()),
            };
        }
        return obj;
    }

    // ── the code table. Keep in step with docs/mcp/PHASE_1_SAFE_WRITES.md ──

    public static class Codes
    {
        public const string WriteDisabled = "WRITE_DISABLED";
        public const string NoSceneOpen = "NO_SCENE_OPEN";
        public const string NodeNotFound = "NODE_NOT_FOUND";
        public const string UnknownProperty = "UNKNOWN_PROPERTY";
        public const string SnakeCaseExport = "SNAKE_CASE_EXPORT";
        public const string TypeMismatch = "TYPE_MISMATCH";
        public const string StillReferenced = "STILL_REFERENCED";
        public const string MethodUnknown = "METHOD_UNKNOWN";
        public const string InvalidParams = "INVALID_PARAMS";
        public const string BatchAborted = "BATCH_ABORTED";
        public const string NotSupported = "NOT_SUPPORTED";
    }

    // ── constructors for the cases the bridge actually raises ──

    public static McpBridgeException WriteDisabled(string role, string setting) => new(
        Codes.WriteDisabled,
        $"Writes are disabled for the {role} role.",
        $"Enable {setting} in Project Settings (or the MCP dock) and retry.",
        new Dictionary<string, object?> { ["role"] = role, ["setting"] = setting });

    public static McpBridgeException NoSceneOpen() => new(
        Codes.NoSceneOpen,
        "No scene is open in the editor.",
        "Open a scene in Godot, or call beep.open_scene first.");

    public static McpBridgeException NodeNotFound(string path) => new(
        Codes.NodeNotFound,
        $"Node not found: {path}",
        "Call tree.serialize (or beep.inspect_scene) to see the real node paths.",
        new Dictionary<string, object?> { ["path"] = path });

    public static McpBridgeException UnknownProperty(string type, string property) => new(
        Codes.UnknownProperty,
        $"'{property}' is not a registered property on {type} — the value would be discarded.",
        "Call node.list_properties for the exact names this node accepts.",
        new Dictionary<string, object?> { ["type"] = type, ["property"] = property });

    /// <summary>The trap that cost this repo 67 dead assignments across 33 scenes: Godot
    /// registers a C# [Export] under its exact PascalCase name, so the snake_case spelling
    /// matches nothing, is dropped, and the scene still saves and loads.</summary>
    public static McpBridgeException SnakeCaseExport(string given, string pascal) => new(
        Codes.SnakeCaseExport,
        $"'{given}' is a C# [Export] written snake_case. Godot silently DROPS that spelling — the assignment would look successful and do nothing.",
        $"Use '{pascal}'.",
        new Dictionary<string, object?> { ["given"] = given, ["expected"] = pascal });

    public static McpBridgeException TypeMismatch(string property, string expected, string got) => new(
        Codes.TypeMismatch,
        $"'{property}' expects {expected}, but the value converted to {got}.",
        "Check node.list_properties for the property's Variant type.",
        new Dictionary<string, object?> { ["property"] = property, ["expected"] = expected, ["got"] = got });

    public static McpBridgeException StillReferenced(string path, string referrers) => new(
        Codes.StillReferenced,
        $"'{path}' is still referenced by a NodePath export on: {referrers}. Removing it would leave those resolving to null — silently.",
        "Clear or repoint those exports first.",
        new Dictionary<string, object?> { ["path"] = path, ["referrers"] = referrers });

    public static McpBridgeException InvalidParams(string message, string? fix = null) =>
        new(Codes.InvalidParams, message, fix);
}

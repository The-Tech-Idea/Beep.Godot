using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Godot;

namespace GodotMcp;

/// <summary>
/// PARTIAL: Phase 1 — safe writes.
///
/// Undo-backed node edits, batching, dry run, and the capability block. The originals in
/// GodotMcpBridgeController mutated the scene directly with no undo entry, one property
/// per round trip, no way to preview, and no way to tell afterwards what actually landed.
/// See docs/mcp/PHASE_1_SAFE_WRITES.md.
/// </summary>
public partial class GodotMcpBridgeController
{
    /// <summary>Dispatch for the Phase 1 methods. Returns false when the method is not
    /// one of ours, so ExecuteMethod can fall through to its own switch.</summary>
    /// <summary>The editor's undo manager, which lives on the EditorPlugin rather than on
    /// EditorInterface. Null at runtime and in a non-tools build, in which case edits apply
    /// directly and report undoable:false.</summary>
    private object? EditorUndoManager()
    {
#if TOOLS
        return _editorPlugin?.GetUndoRedo();
#else
        return null;
#endif
    }

    private bool TryExecuteSafeWrite(string method, JsonObject p, out JsonNode? result)
    {
        switch (method)
        {
            case "bridge.batch": result = Batch(p); return true;
            case "bridge.capabilities": result = Capabilities(); return true;
            case "node.set_property_safe": result = SetNodePropertySafe(p); return true;
            default: result = null; return false;
        }
    }

    // ════════════════════════════════════════════════════════════════
    // Batch — many ops, ONE undo entry
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Run an ordered list of operations inside a single undo action.
    ///
    /// Restyling one screen is 30–60 property writes. One at a time that is 60 round
    /// trips, 60 separate (previously non-existent) undo entries, and no way to describe
    /// the state if it fails halfway. Here it is one request, one undo step, and a
    /// per-op result array so a failure names the exact index.
    ///
    /// params: { label?, atomic? (default true), dry_run? (default false), ops: [ {method, params} ] }
    /// </summary>
    private JsonNode Batch(JsonObject p)
    {
        if (p["ops"] is not JsonArray ops)
            throw McpBridgeException.InvalidParams("bridge.batch needs an 'ops' array.",
                "Pass ops: [{ method: 'node.set_property', params: {...} }, ...]");

        bool atomic = p["atomic"]?.GetValue<bool>() ?? true;
        bool dryRun = p["dry_run"]?.GetValue<bool>() ?? false;
        string label = p["label"]?.GetValue<string>() ?? $"batch of {ops.Count}";

        if (!dryRun) RequireWrites();

        var results = new JsonArray();
        int failed = -1;
        JsonObject? firstError = null;

        // A dry run must not open an undo action at all — nothing may be recorded.
        using var scope = dryRun ? null : McpUndoScope.Begin(label, EditorUndoManager());

        for (int i = 0; i < ops.Count; i++)
        {
            var op = ops[i] as JsonObject;
            string opMethod = op?["method"]?.GetValue<string>() ?? "";
            var opParams = op?["params"] as JsonObject ?? new JsonObject();

            try
            {
                if (string.IsNullOrEmpty(opMethod))
                    throw McpBridgeException.InvalidParams($"ops[{i}] has no 'method'.");

                JsonNode? r = dryRun
                    ? Validate(opMethod, opParams)
                    : ExecuteInScope(opMethod, opParams, scope!);

                results.Add(new JsonObject { ["index"] = i, ["method"] = opMethod, ["ok"] = true, ["result"] = r });
            }
            catch (Exception ex)
            {
                var err = ErrorJson(ex);
                err["index"] = i;
                err["method"] = opMethod;
                err["ok"] = false;
                results.Add(err);
                failed = i;
                firstError ??= err;
                if (atomic) break;
            }
        }

        if (atomic && failed >= 0 && !dryRun)
        {
            // Nothing is committed. The scene is exactly as it was before op 0.
            scope!.Discard();
            return new JsonObject
            {
                ["ok"] = false,
                ["aborted_at"] = failed,
                ["committed"] = false,
                ["code"] = McpBridgeException.Codes.BatchAborted,
                ["error"] = $"Batch aborted at op {failed}; nothing was applied.",
                ["fix"] = "Fix that op, or pass atomic:false to apply the others anyway.",
                ["results"] = results
            };
        }

        return new JsonObject
        {
            ["ok"] = failed < 0,
            ["dry_run"] = dryRun,
            ["committed"] = !dryRun && (failed < 0 || !atomic),
            ["undoable"] = !dryRun && (scope?.IsUndoable ?? false),
            ["count"] = ops.Count,
            ["failed_count"] = failed < 0 ? 0 : CountFailures(results),
            ["results"] = results
        };
    }

    private static int CountFailures(JsonArray results)
    {
        int n = 0;
        foreach (var r in results)
            if (r is JsonObject o && o["ok"]?.GetValue<bool>() == false) n++;
        return n;
    }

    /// <summary>Apply one op inside an open undo scope.</summary>
    private JsonNode? ExecuteInScope(string method, JsonObject p, McpUndoScope scope) => method switch
    {
        "node.set_property" or "node.set_property_safe" => SetPropertyUndoable(p, scope),
        "node.create" => CreateNodeUndoable(p, scope),
        "node.delete" => DeleteNodeUndoable(p, scope),
        "node.reparent" => ReparentNodeUndoable(p, scope),
        // Anything else runs through the normal dispatcher. It will not be undoable, so
        // say so rather than implying the whole batch can be stepped back.
        _ => WrapNonUndoable(method, p),
    };

    private JsonNode? WrapNonUndoable(string method, JsonObject p)
    {
        JsonNode? r = ExecuteMethod(method, p);
        return new JsonObject { ["result"] = r, ["undoable"] = false };
    }

    // ════════════════════════════════════════════════════════════════
    // Dry run — validate without mutating
    // ════════════════════════════════════════════════════════════════

    /// <summary>Answer "would this work?" without touching anything. Same guards as the
    /// real write, so a green dry run means the write will land.</summary>
    private JsonNode Validate(string method, JsonObject p)
    {
        switch (method)
        {
            case "node.set_property":
            case "node.set_property_safe":
            {
                Node node = RequireNode(p);
                string property = RequiredString(p, "property");
                McpWriteGuard.ValidateProperty(node, property);
                return new JsonObject
                {
                    ["would_set"] = property,
                    ["on"] = node.GetPath().ToString(),
                    ["current"] = McpJson.FromVariant(node.Get(property)),
                };
            }
            case "node.create":
            {
                string type = RequiredString(p, "type");
                if (!ClassDB.ClassExists(type))
                    throw McpBridgeException.InvalidParams($"Unknown node type '{type}'.", "Check the spelling against Godot's class list.");
                if (!ClassDB.CanInstantiate(type))
                    throw McpBridgeException.InvalidParams($"'{type}' cannot be instantiated (abstract or a singleton).");
                return new JsonObject { ["would_create"] = type };
            }
            case "node.delete":
            {
                Node node = RequireNode(p);
                McpWriteGuard.EnsureNotReferenced(GetCurrentSceneRoot(), node);
                return new JsonObject { ["would_delete"] = node.GetPath().ToString() };
            }
            case "node.reparent":
            {
                Node node = RequireNode(p);
                string np = RequiredString(p, "new_parent_path");
                if (ResolveNode(np) is null) throw McpBridgeException.NodeNotFound(np);
                return new JsonObject { ["would_reparent"] = node.GetPath().ToString(), ["under"] = np };
            }
            default:
                return new JsonObject
                {
                    ["validated"] = false,
                    ["note"] = $"'{method}' has no dry-run validator; it would be executed for real outside a dry run."
                };
        }
    }

    // ════════════════════════════════════════════════════════════════
    // Undo-backed single writes
    // ════════════════════════════════════════════════════════════════

    /// <summary>node.set_property, but validated and undoable. Registered as
    /// `node.set_property_safe` so the original stays available for anything relying on
    /// its exact behaviour.</summary>
    private JsonNode SetNodePropertySafe(JsonObject p)
    {
        RequireWrites();
        if (p["dry_run"]?.GetValue<bool>() == true) return Validate("node.set_property", p);

        using var scope = McpUndoScope.Begin($"set {RequiredString(p, "property")}", EditorUndoManager());
        return SetPropertyUndoable(p, scope) ?? new JsonObject();
    }

    private JsonNode? SetPropertyUndoable(JsonObject p, McpUndoScope scope)
    {
        Node node = RequireNode(p);
        string property = RequiredString(p, "property");

        // Refuse rather than discard. Godot's Set() on an unknown name is a silent no-op
        // and the bridge used to answer "updated: true" for it.
        McpWriteGuard.ValidateProperty(node, property);

        JsonNode? value = p["value"];
        Variant variant = McpJson.ToVariant(value);
        Variant before = node.Get(property);

        scope.SetProperty(node, property, variant);

        return new JsonObject
        {
            ["path"] = node.GetPath().ToString(),
            ["property"] = property,
            ["before"] = McpJson.FromVariant(before),
            ["value"] = value?.DeepClone(),
            ["updated"] = true,
            ["undoable"] = scope.IsUndoable,
        };
    }

    private JsonNode? CreateNodeUndoable(JsonObject p, McpUndoScope scope)
    {
        string type = RequiredString(p, "type");
        if (!ClassDB.ClassExists(type) || !ClassDB.CanInstantiate(type))
            throw McpBridgeException.InvalidParams($"'{type}' is not an instantiable node type.");

        string parentPath = p["parent_path"]?.GetValue<string>() ?? p["parent"]?.GetValue<string>()
                            ?? GetCurrentSceneRoot().GetPath().ToString();
        Node parent = ResolveNode(parentPath) ?? throw McpBridgeException.NodeNotFound(parentPath);

        if ((GodotObject)ClassDB.Instantiate(new StringName(type)) is not Node node)
            throw McpBridgeException.InvalidParams($"'{type}' did not construct as a Node.");

        node.Name = p["name"]?.GetValue<string>() ?? type;
        Node owner = GetCurrentSceneRoot();
        scope.AddChild(parent, node, owner);

        return new JsonObject
        {
            ["created"] = true,
            ["name"] = node.Name.ToString(),
            ["type"] = type,
            ["parent"] = parent.GetPath().ToString(),
            ["undoable"] = scope.IsUndoable,
        };
    }

    private JsonNode? DeleteNodeUndoable(JsonObject p, McpUndoScope scope)
    {
        Node node = RequireNode(p);
        Node root = GetCurrentSceneRoot();
        if (node == root)
            throw McpBridgeException.InvalidParams("Refusing to delete the scene root.");

        McpWriteGuard.EnsureNotReferenced(root, node);

        string path = node.GetPath().ToString();
        Node parent = node.GetParent() ?? throw McpBridgeException.InvalidParams($"'{path}' has no parent.");
        scope.RemoveChild(parent, node, root);

        return new JsonObject { ["deleted"] = true, ["path"] = path, ["undoable"] = scope.IsUndoable };
    }

    private JsonNode? ReparentNodeUndoable(JsonObject p, McpUndoScope scope)
    {
        Node node = RequireNode(p);
        string newParentPath = RequiredString(p, "new_parent_path");
        Node newParent = ResolveNode(newParentPath) ?? throw McpBridgeException.NodeNotFound(newParentPath);
        Node oldParent = node.GetParent() ?? throw McpBridgeException.InvalidParams("Node has no parent to move from.");

        scope.Reparent(node, oldParent, newParent);

        return new JsonObject
        {
            ["path"] = node.GetPath().ToString(),
            ["new_parent"] = newParent.GetPath().ToString(),
            ["undoable"] = scope.IsUndoable,
        };
    }

    // ════════════════════════════════════════════════════════════════
    // Capabilities — so an agent never has to guess
    // ════════════════════════════════════════════════════════════════

    private JsonNode Capabilities()
    {
        var methods = new JsonArray();
        foreach (string m in KnownMethods) methods.Add(m);

        return new JsonObject
        {
            ["bridge"] = GodotMcpSettings.BridgeName,
            ["version"] = GodotMcpSettings.Version,
            ["role"] = _role,
            ["godot_version"] = Engine.GetVersionInfo().ToString(),
            ["editor_hint"] = Engine.IsEditorHint(),
            ["methods"] = methods,
            ["project_commands"] = ToJsonArray(McpCommandRegistry.CommandNames()),
            ["project_states"] = ToJsonArray(McpCommandRegistry.StateNames()),
            ["security"] = new JsonObject
            {
                ["allow_editor_writes"] = GodotMcpSettings.GetBool(GodotMcpSettings.AllowEditorWrites, false),
                ["allow_runtime_writes"] = GodotMcpSettings.GetBool(GodotMcpSettings.AllowRuntimeWrites, false),
                ["allow_node_method_calls"] = GodotMcpSettings.GetBool(GodotMcpSettings.AllowNodeMethodCalls, false),
            },
            ["features"] = new JsonObject
            {
                ["batch"] = true,
                ["dry_run"] = true,
                // Only the editor has an undo manager; a runtime write can never be stepped back.
                ["undo"] = Engine.IsEditorHint(),
                ["structured_errors"] = true,
            },
            ["error_codes"] = new JsonArray(
                McpBridgeException.Codes.WriteDisabled, McpBridgeException.Codes.NoSceneOpen,
                McpBridgeException.Codes.NodeNotFound, McpBridgeException.Codes.UnknownProperty,
                McpBridgeException.Codes.SnakeCaseExport, McpBridgeException.Codes.TypeMismatch,
                McpBridgeException.Codes.StillReferenced, McpBridgeException.Codes.MethodUnknown,
                McpBridgeException.Codes.InvalidParams, McpBridgeException.Codes.BatchAborted,
                McpBridgeException.Codes.NotSupported),
        };
    }

    private static readonly string[] KnownMethods =
    {
        "ping", "status.get", "bridge.capabilities", "bridge.batch",
        "tree.serialize", "scene.current", "editor.selection.get", "editor.selection.set",
        "node.get", "node.list_properties", "node.set_property", "node.set_property_safe",
        "node.call_method", "node.create", "node.delete", "node.reparent",
        "shader.attach_canvas_item", "shader.set_uniform",
        "tween.property", "particles.create_2d", "projectile.sample_arc_2d", "sprite.move_to",
        "runtime.pause", "runtime.resume", "runtime.screenshot", "input.action",
        "game.command", "game.state", "project.setting.get", "project.setting.set",
    };

    // ════════════════════════════════════════════════════════════════
    // Structured error envelope
    // ════════════════════════════════════════════════════════════════

    /// <summary>Build the error body. Keeps `error` and `error_type` so an older client
    /// still works, and adds `code`/`fix`/`detail` for one that can use them.</summary>
    internal static JsonObject ErrorJson(Exception ex)
    {
        var obj = new JsonObject
        {
            ["error"] = ex.Message,
            ["error_type"] = ex.GetType().Name,
        };
        if (ex is McpBridgeException m)
        {
            obj["code"] = m.Code;
            if (m.Fix != null) obj["fix"] = m.Fix;
            var detail = m.ToDetailJson();
            if (detail.Count > 0) obj["detail"] = detail;
        }
        return obj;
    }
}

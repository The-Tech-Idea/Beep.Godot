using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;

namespace GodotMcp;

/// <summary>
/// PARTIAL: Phase 3 — perception.
///
/// An agent editing UI has been working blind. It could read the node tree — a description
/// of intent — but not the pixels, which are the actual product. Every layout defect this
/// repo has shipped was invisible in the tree and obvious on screen: a 0-height back
/// button, a title at body size, a background covering the pattern meant to sit on it.
///
/// It also could not read Godot's own voice. GD.PushWarning is the mechanism this
/// framework uses for everything that would otherwise fail silently, and the whole point
/// is lost if the only reader is a human watching the Output panel.
///
/// See docs/mcp/PHASE_3_PERCEPTION.md.
/// </summary>
public partial class GodotMcpBridgeController
{
    private bool TryExecutePerception(string method, JsonObject p, out JsonNode? result)
    {
        switch (method)
        {
            case "view.capture": result = ViewCapture(p); return true;
            case "view.layout": result = ViewLayout(p); return true;
            case "log.tail": result = LogTail(p); return true;
            case "log.mark": result = LogMark(); return true;
            case "scene.snapshot": result = SceneSnapshot(p); return true;
            case "scene.diff": result = SceneDiff(p); return true;
            default: result = null; return false;
        }
    }

    // ════════════════════════════════════════════════════════════════
    // Capture
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// PNG of what is on screen, inline as base64 — an agent can look at base64; it cannot
    /// look at a file path.
    ///
    /// target "node" crops to a Control's global rect, which is the one that turns
    /// "the screen looks wrong" into "this header is wrong" and keeps the payload small.
    /// </summary>
    private JsonNode ViewCapture(JsonObject p)
    {
        string target = p["target"]?.GetValue<string>() ?? "viewport";
        int cap = p["max_width"]?.GetValue<int>() ?? 1280;

        Viewport vp = GetViewport()
            ?? throw new McpBridgeException("NO_VIEWPORT", "No viewport is available in this process.");
        Image image = vp.GetTexture()?.GetImage()
            ?? throw new McpBridgeException("NO_VIEWPORT", "The viewport produced no image.");

        var meta = new JsonObject
        {
            ["target"] = target,
            ["role"] = _role,
            ["source_size"] = $"{image.GetWidth()}x{image.GetHeight()}",
        };

        if (target == "node")
        {
            string path = RequiredString(p, "node");
            Node node = ResolveNode(path) ?? throw McpBridgeException.NodeNotFound(path);
            if (node is not Godot.Control control)
                throw McpBridgeException.InvalidParams($"'{path}' is a {node.GetType().Name}, not a Control — there is no rect to crop to.");

            Rect2 r = control.GetGlobalRect();
            var region = new Rect2I(
                Mathf.Clamp((int)r.Position.X, 0, image.GetWidth()),
                Mathf.Clamp((int)r.Position.Y, 0, image.GetHeight()),
                Mathf.Clamp((int)r.Size.X, 1, image.GetWidth()),
                Mathf.Clamp((int)r.Size.Y, 1, image.GetHeight()));

            // A zero-size Control is itself the bug worth reporting — cropping to it would
            // return an empty image and look like the capture failed.
            if (r.Size.X < 1 || r.Size.Y < 1)
                throw new McpBridgeException("EMPTY_RECT",
                    $"'{path}' has a {r.Size.X}x{r.Size.Y} rect — there is nothing to capture.",
                    "That is usually the defect: check custom_minimum_size and size_flags. view.layout flags these.");

            image = image.GetRegion(region);
            meta["rect"] = $"{region.Position.X},{region.Position.Y} {region.Size.X}x{region.Size.Y}";
        }

        if (cap > 0 && image.GetWidth() > cap)
        {
            int h = Mathf.Max(1, (int)(image.GetHeight() * (cap / (float)image.GetWidth())));
            image.Resize(cap, h, Image.Interpolation.Bilinear);
        }

        meta["format"] = "png";
        meta["width"] = image.GetWidth();
        meta["height"] = image.GetHeight();
        meta["base64"] = Convert.ToBase64String(image.SavePngToBuffer());
        return meta;
    }

    // ════════════════════════════════════════════════════════════════
    // Layout introspection — the numbers a screenshot cannot give you
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Rects, sizing and anchors for a Control subtree, with the three problems that
    /// have actually gone wrong here flagged explicitly: zero width, zero height, and a
    /// child whose rect escapes its parent.
    ///
    /// A back button shipped at custom_minimum_size (120, 0) across every genre screen —
    /// invisible in the tree, obvious here.
    /// </summary>
    private JsonNode ViewLayout(JsonObject p)
    {
        string path = p["node"]?.GetValue<string>() ?? ".";
        Node start = path is "." or "" ? GetCurrentSceneRoot()
            : ResolveNode(path) ?? throw McpBridgeException.NodeNotFound(path);

        bool recursive = p["recursive"]?.GetValue<bool>() ?? true;
        var entries = new JsonArray();
        var problems = new JsonArray();
        Node root = GetCurrentSceneRoot();

        Walk(start, root, entries, problems, recursive);

        return new JsonObject
        {
            ["node"] = start.GetPath().ToString(),
            ["count"] = entries.Count,
            ["problems"] = problems,
            ["controls"] = entries,
        };

        static void Walk(Node node, Node root, JsonArray entries, JsonArray problems, bool recursive)
        {
            if (node is Godot.Control c)
            {
                Rect2 r = c.GetGlobalRect();
                string p = root.GetPathTo(c).ToString();

                entries.Add(new JsonObject
                {
                    ["path"] = p,
                    ["class"] = c.GetType().Name,
                    ["rect"] = new JsonObject
                    {
                        ["x"] = r.Position.X, ["y"] = r.Position.Y,
                        ["w"] = r.Size.X, ["h"] = r.Size.Y,
                    },
                    ["min_size"] = $"{c.CustomMinimumSize.X}x{c.CustomMinimumSize.Y}",
                    ["size_flags"] = $"h={c.SizeFlagsHorizontal} v={c.SizeFlagsVertical}",
                    ["visible"] = c.Visible,
                    ["visible_in_tree"] = c.IsVisibleInTree(),
                });

                if (c.IsVisibleInTree())
                {
                    if (r.Size.Y < 1)
                        problems.Add(Problem(p, "ZERO_HEIGHT",
                            $"{c.GetType().Name} has height {r.Size.Y}. A control with no height is invisible and unclickable — usually custom_minimum_size was set as (w, 0)."));
                    if (r.Size.X < 1)
                        problems.Add(Problem(p, "ZERO_WIDTH",
                            $"{c.GetType().Name} has width {r.Size.X}."));

                    if (c.GetParent() is Godot.Control parent && parent.IsVisibleInTree())
                    {
                        Rect2 pr = parent.GetGlobalRect();
                        if (pr.Size.X >= 1 && pr.Size.Y >= 1 &&
                            (r.Position.X + r.Size.X > pr.Position.X + pr.Size.X + 1 ||
                             r.Position.Y + r.Size.Y > pr.Position.Y + pr.Size.Y + 1))
                            problems.Add(Problem(p, "OVERFLOWS_PARENT",
                                $"{c.GetType().Name} extends past {parent.GetType().Name} — content is clipped or spilling."));
                    }
                }
            }
            if (!recursive) return;
            foreach (var child in node.GetChildren()) Walk(child, root, entries, problems, true);
        }

        static JsonObject Problem(string path, string code, string message) =>
            new() { ["path"] = path, ["code"] = code, ["message"] = message };
    }

    // ════════════════════════════════════════════════════════════════
    // Logs — so PushWarning reaches the agent, not just the Output panel
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Read Godot's own log file.
    ///
    /// Godot has no public C# hook for intercepting the log, so this reads the file the
    /// engine already writes (debug/file_logging/log_path, default user://logs/godot.log).
    /// That has one large advantage over an in-process buffer: it contains everything from
    /// before the bridge connected, including the warnings emitted during project load.
    /// </summary>
    private JsonNode LogTail(JsonObject p)
    {
        string level = (p["level"]?.GetValue<string>() ?? "all").ToLowerInvariant();
        int limit = Mathf.Clamp(p["limit"]?.GetValue<int>() ?? 100, 1, 2000);
        int since = p["since_line"]?.GetValue<int>() ?? 0;

        string logPath = LogFilePath();
        if (!Godot.FileAccess.FileExists(logPath))
            return new JsonObject
            {
                ["path"] = logPath,
                ["available"] = false,
                ["note"] = "Godot is not writing a log file. Enable debug/file_logging/enable_file_logging in Project Settings.",
                ["entries"] = new JsonArray(),
            };

        using var f = Godot.FileAccess.Open(logPath, Godot.FileAccess.ModeFlags.Read);
        if (f is null)
            throw new McpBridgeException("LOG_UNREADABLE", $"Could not open '{logPath}': {Godot.FileAccess.GetOpenError()}");

        var all = f.GetAsText().Split('\n');
        var entries = new JsonArray();

        for (int i = Mathf.Max(since, 0); i < all.Length; i++)
        {
            string line = all[i].TrimEnd('\r');
            if (line.Length == 0) continue;
            string lvl = ClassifyLogLine(line);
            if (level != "all" && lvl != level) continue;
            entries.Add(new JsonObject { ["line"] = i, ["level"] = lvl, ["text"] = line });
        }

        // Keep the tail — the most recent lines are the ones that explain what just happened.
        while (entries.Count > limit) entries.RemoveAt(0);

        return new JsonObject
        {
            ["path"] = logPath,
            ["available"] = true,
            ["total_lines"] = all.Length,
            ["level"] = level,
            ["count"] = entries.Count,
            ["entries"] = entries,
        };
    }

    /// <summary>Current end-of-log line number, to pass back as `since_line`.
    ///
    /// This replaces the `log.clear` the plan called for: the log file is Godot's, it is
    /// shared with the engine's own writer, and truncating a user's log to make a read
    /// convenient is a destructive answer to a bookkeeping problem. A marker does the same
    /// job — "show me only what happened after this point" — without deleting anything.</summary>
    private JsonNode LogMark()
    {
        string logPath = LogFilePath();
        int lines = 0;
        if (Godot.FileAccess.FileExists(logPath))
        {
            using var f = Godot.FileAccess.Open(logPath, Godot.FileAccess.ModeFlags.Read);
            if (f is not null) lines = f.GetAsText().Split('\n').Length;
        }
        return new JsonObject
        {
            ["path"] = logPath,
            ["mark"] = lines,
            ["usage"] = "Pass this as log.tail's since_line to see only what happens after now.",
        };
    }

    private static string LogFilePath()
    {
        var setting = ProjectSettings.GetSetting("debug/file_logging/log_path");
        string path = setting.VariantType == Variant.Type.Nil ? "" : setting.AsString();
        return string.IsNullOrEmpty(path) ? "user://logs/godot.log" : path;
    }

    private static string ClassifyLogLine(string line)
    {
        // Godot's own prefixes. SCRIPT ERROR / USER ERROR come from push_error and from
        // C# exceptions; WARNING from push_warning, which is this framework's main voice.
        if (line.StartsWith("ERROR", StringComparison.Ordinal)
            || line.StartsWith("SCRIPT ERROR", StringComparison.Ordinal)
            || line.StartsWith("USER ERROR", StringComparison.Ordinal)) return "error";
        if (line.StartsWith("WARNING", StringComparison.Ordinal)
            || line.StartsWith("USER WARNING", StringComparison.Ordinal)) return "warning";
        return "info";
    }

    // ════════════════════════════════════════════════════════════════
    // Snapshot / diff — "did only what I intended change?"
    // ════════════════════════════════════════════════════════════════

    private static readonly Dictionary<string, Dictionary<string, string>> _snapshots = new();

    /// <summary>Record the open scene's shape under a label.</summary>
    private JsonNode SceneSnapshot(JsonObject p)
    {
        string label = p["label"]?.GetValue<string>() ?? "default";
        Node root = GetCurrentSceneRoot();
        var flat = Flatten(root);
        _snapshots[label] = flat;
        return new JsonObject { ["label"] = label, ["nodes"] = flat.Count, ["scene"] = root.SceneFilePath };
    }

    /// <summary>What changed between two snapshots (or a snapshot and now).
    ///
    /// After a 40-op batch, "what actually changed" should be answerable without
    /// re-reading the whole tree — and it is the check that catches a batch doing more
    /// than it claimed.</summary>
    private JsonNode SceneDiff(JsonObject p)
    {
        string fromLabel = p["from"]?.GetValue<string>() ?? "default";
        if (!_snapshots.TryGetValue(fromLabel, out var from))
            throw McpBridgeException.InvalidParams($"No snapshot labelled '{fromLabel}'.",
                "Call scene.snapshot before the change you want to measure.");

        Dictionary<string, string> to = p["to"]?.GetValue<string>() is { Length: > 0 } toLabel
            ? (_snapshots.TryGetValue(toLabel, out var t) ? t
               : throw McpBridgeException.InvalidParams($"No snapshot labelled '{toLabel}'."))
            : Flatten(GetCurrentSceneRoot());

        var added = new JsonArray();
        var removed = new JsonArray();
        var changed = new JsonArray();

        foreach (var kv in to)
            if (!from.ContainsKey(kv.Key)) added.Add(kv.Key);
        foreach (var kv in from)
        {
            if (!to.TryGetValue(kv.Key, out var now)) { removed.Add(kv.Key); continue; }
            if (now != kv.Value)
                changed.Add(new JsonObject { ["path"] = kv.Key, ["before"] = kv.Value, ["after"] = now });
        }

        return new JsonObject
        {
            ["from"] = fromLabel,
            ["added"] = added,
            ["removed"] = removed,
            ["changed"] = changed,
            ["total_changes"] = added.Count + removed.Count + changed.Count,
        };
    }

    /// <summary>Flatten a scene to path → fingerprint. Deliberately shallow: type, name and
    /// the Control geometry that layout bugs live in. A full property dump would make every
    /// diff enormous and bury the change that matters.</summary>
    private static Dictionary<string, string> Flatten(Node root)
    {
        var map = new Dictionary<string, string>();
        Collect(root, root, map);
        return map;

        static void Collect(Node root, Node node, Dictionary<string, string> map)
        {
            string path = node == root ? "." : root.GetPathTo(node).ToString();
            string fingerprint = node.GetType().Name;
            if (node is Godot.Control c)
            {
                Rect2 r = c.GetGlobalRect();
                fingerprint += $" rect={r.Position.X},{r.Position.Y},{r.Size.X},{r.Size.Y}" +
                               $" min={c.CustomMinimumSize.X}x{c.CustomMinimumSize.Y}" +
                               $" vis={c.Visible}";
            }
            map[path] = fingerprint;
            foreach (var child in node.GetChildren()) Collect(root, child, map);
        }
    }
}

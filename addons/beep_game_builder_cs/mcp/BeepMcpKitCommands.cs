using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using GodotMcp;
using Beep.ECS.UI.Kit;

namespace Beep.GameBuilder
{
    /// <summary>
    /// The Game UI Kit's MCP surface.
    ///
    /// The kit shipped 32 widgets and the bridge knew NOTHING about it — an agent could add a
    /// legacy ECS component through <c>beep.add_component</c> but had no way to discover a kit
    /// widget, ask what it replaces, or convert a screen onto it. Every migration therefore had to
    /// be done by hand-editing .tscn text, which is how a 108-button sweep ended up being audited
    /// one scene at a time.
    ///
    ///   read          — kit_widgets, kit_scene_audit
    ///   editor write  — kit_convert_scene   (allow_editor_writes)
    ///
    /// `kit_convert_scene` is deliberately ADDITIVE and reports what it did: it attaches
    /// KitPushButton / KitPanelContainer to existing nodes and never retypes or reparents one.
    /// Both derive from the Godot control they replace, so `text`, `Pressed +=`, Find&lt;Button&gt;
    /// and every other typed lookup keep working — that property is the only reason a bulk
    /// conversion is safe, and it is why nothing here offers a "replace with KitButton" mode.
    /// </summary>
    public static partial class BeepMcpKitCommands
    {
        /// <summary>Godot type -> the kit script that drops in for it, with no retype.</summary>
        private static readonly (string Godot, string Script, string Widget)[] DropIns =
        {
            ("Button", "res://addons/beep_game_builder_cs/ecs/ui/kit/KitPushButton.cs", "KitPushButton"),
            ("PanelContainer", "res://addons/beep_game_builder_cs/ecs/ui/kit/KitPanelContainer.cs", "KitPanelContainer"),
        };

        public static void Register()
        {
            McpCommandRegistry.RegisterCommand("beep.kit_widgets", _ => Widgets());
            McpCommandRegistry.RegisterCommand("beep.kit_scene_audit", args => Audit(Str(args, "scene")));
            McpCommandRegistry.RegisterCommand("beep.kit_template_audit", _ => AuditTemplates());
            McpCommandRegistry.RegisterCommand("beep.kit_convert_scene", args =>
                Convert(Str(args, "scene"), Bool(args, "dry_run", true)));
        }

        public static void Unregister() => McpCommandRegistry.UnregisterPrefix("beep.kit_");

        // ── read ────────────────────────────────────────────────────────────────────────

        /// <summary>Every kit widget, what it is for, and which Godot control it drops in for.</summary>
        private static JsonObject Widgets()
        {
            var arr = new JsonArray();
            foreach (var (name, purpose, replaces) in Catalogue)
            {
                arr.Add(new JsonObject
                {
                    ["widget"] = name,
                    ["purpose"] = purpose,
                    ["drops_in_for"] = replaces,
                    ["script"] = $"res://addons/beep_game_builder_cs/ecs/ui/kit/{name}.cs",
                });
            }
            return new JsonObject
            {
                ["widgets"] = arr,
                ["count"] = arr.Count,
                ["note"] = "Only KitPushButton and KitPanelContainer are DROP-INS: they derive "
                         + "from Button/PanelContainer, so a scene keeps its node types and every "
                         + "typed lookup. The rest derive from KitControl and are for new screens.",
            };
        }

        /// <summary>What a scene still has that the kit could take over.</summary>
        private static JsonObject Audit(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath) || !FileAccess.FileExists(scenePath))
                return Err($"scene not found: '{scenePath}'");

            string text = ReadAll(scenePath);
            var counts = new JsonObject();
            int convertible = 0;
            foreach (var (godot, _, widget) in DropIns)
            {
                int total = Occurrences(text, $"type=\"{godot}\"");
                int already = CountConverted(text, widget);
                counts[godot] = new JsonObject
                {
                    ["nodes"] = total,
                    ["already_kit"] = already,
                    ["convertible"] = total - already,
                };
                convertible += total - already;
            }
            return new JsonObject
            {
                ["scene"] = scenePath,
                ["controls"] = counts,
                ["convertible"] = convertible,
            };
        }

        private static JsonObject AuditTemplates()
        {
            var audit = BeepSceneTemplateAudit.AuditAll();
            var issues = new JsonArray();
            foreach (var issue in audit.Issues)
            {
                issues.Add(new JsonObject
                {
                    ["scene"] = issue.Scene,
                    ["line"] = issue.Line,
                    ["kind"] = issue.Kind,
                    ["detail"] = issue.Detail,
                });
            }

            return new JsonObject
            {
                ["ok"] = audit.Ok,
                ["scene_count"] = audit.SceneCount,
                ["issue_count"] = audit.Issues.Count,
                ["issues"] = issues,
            };
        }

        // ── editor write ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Attach kit scripts to a scene's generic controls.
        ///
        /// Defaults to dry_run: a bulk .tscn rewrite is exactly the kind of change that looks
        /// fine in the file and is wrong on screen, so the caller has to ask for it explicitly and
        /// should render the scene afterwards.
        /// </summary>
        private static JsonObject Convert(string scenePath, bool dryRun)
        {
            if (!dryRun && !GodotMcpSettings.GetBool(GodotMcpSettings.AllowEditorWrites, false))
                return Err("editor writes are disabled (godot_mcp/security/allow_editor_writes).");
            if (string.IsNullOrEmpty(scenePath) || !FileAccess.FileExists(scenePath))
                return Err($"scene not found: '{scenePath}'");

            string text = ReadAll(scenePath);
            var changed = new JsonArray();
            int n = 0;

            foreach (var (godot, script, widget) in DropIns)
            {
                string eid = "kit_" + godot.ToLowerInvariant();
                var blocks = text.Split("\n\n").ToList();
                int here = 0;
                for (int i = 0; i < blocks.Count; i++)
                {
                    string b = blocks[i];
                    if (!b.StartsWith("[node ") || !b.Contains($"type=\"{godot}\"")) continue;
                    if (b.Contains("script = ")) continue;          // already scripted: not ours
                    blocks[i] = b.TrimEnd('\n') + $"\nscript = ExtResource(\"{eid}\")";
                    here++;
                }
                if (here == 0) continue;

                text = string.Join("\n\n", blocks);
                if (!text.Contains($"id=\"{eid}\""))
                {
                    text = BumpLoadSteps(text, 1);
                    int at = text.IndexOf("[ext_resource", System.StringComparison.Ordinal);
                    if (at < 0) at = text.IndexOf("\n\n", System.StringComparison.Ordinal) + 2;
                    text = text.Insert(at,
                        $"[ext_resource type=\"Script\" path=\"{script}\" id=\"{eid}\"]\n");
                }
                changed.Add(new JsonObject { ["from"] = godot, ["to"] = widget, ["nodes"] = here });
                n += here;
            }

            if (n > 0 && !dryRun)
            {
                using var f = FileAccess.Open(scenePath, FileAccess.ModeFlags.Write);
                if (f == null) return Err($"cannot write '{scenePath}'");
                f.StoreString(text);
            }

            return new JsonObject
            {
                ["scene"] = scenePath,
                ["dry_run"] = dryRun,
                ["converted"] = n,
                ["changes"] = changed,
                ["next"] = n > 0 && !dryRun
                    ? "Render the scene and LOOK. A converted button that renders blank still "
                      + "compiles and still passes validate_scenes.sh."
                    : "",
            };
        }

        // ── helpers ─────────────────────────────────────────────────────────────────────

        private static readonly (string, string, string)[] Catalogue =
        {
            ("KitPushButton", "Button with kit chrome; drop-in", "Button"),
            ("KitPanelContainer", "PanelContainer with kit chrome; drop-in", "PanelContainer"),
            ("KitButton", "Button with overhanging attachments (badges)", "-"),
            ("KitPanel", "frame + recessed well + overhanging banner", "-"),
            ("KitLabelValue", "welded label/value pair, opposite polarity", "-"),
            ("KitMeter", "segmented bar; track in the fill's own hue", "ProgressBar"),
            ("KitRadialMeter", "ring gauge", "-"),
            ("KitSlider", "slider with a chunky bar knob", "HSlider"),
            ("KitToggle", "on/off switch (the game checkbox)", "CheckBox"),
            ("KitArrowSelector", "< Option > pager; games have no dropdowns", "OptionButton"),
            ("KitTabStrip", "tabs with weld/pill/elevate selection", "TabBar"),
            ("KitSlotGrid", "inventory grid; selection drawn OUTSIDE the slot", "-"),
            ("KitGemSlot", "single socket, cut into its host", "-"),
            ("KitTree", "skill tree; colour on branch OR state, never both", "-"),
            ("KitLevelPath", "serpentine level map", "-"),
            ("KitNodeCard", "card + welded footer (status 0.19x / action 0.10x)", "-"),
            ("KitRow", "list row; selection is a fill", "-"),
            ("KitCurrencyBar", "resource capsules with overhanging icon caps", "-"),
            ("KitChip", "rarity / count / dot / status / lock", "-"),
            ("KitAvatarFrame", "portrait with a badge straddling the rim", "-"),
            ("KitIconButton", "square icon button; locked has no hover", "-"),
            ("KitSegmentedIconGroup", "welded radio group", "-"),
            ("KitPager", "step and jump-to-end pagers", "-"),
            ("KitStarRating", "stars; unearned drain saturation", "-"),
            ("KitSpinner", "ring / dots / bar; runs while paused", "-"),
            ("KitTooltip", "hint with a tail naming its owner", "-"),
            ("KitInputHint", "[E] Action, with chord support", "-"),
            ("KitRadarChart", "the folder's only comparison widget", "-"),
            ("KitOrnament", "crown/wings/laurel/trophy; inert", "-"),
            ("KitPanelHanger", "chain/rope/nail/tape/roll/vine", "-"),
            ("KitBookSpread", "two pages with a shaded spine", "-"),
            ("KitSpinWheel", "prize wheel; caller picks the prize", "-"),
        };

        private static string ReadAll(string path)
        {
            using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            return f?.GetAsText() ?? "";
        }

        private static int Occurrences(string s, string needle)
        {
            int n = 0, i = 0;
            while ((i = s.IndexOf(needle, i, System.StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }

        private static int CountConverted(string s, string widget)
            => Occurrences(s, $"kit/{widget}.cs") > 0
                ? Occurrences(s, $"ExtResource(\"kit_{widget.Replace("Kit", "").ToLowerInvariant()}\")")
                : 0;

        private static string BumpLoadSteps(string text, int by)
        {
            const string tag = "load_steps=";
            int i = text.IndexOf(tag, System.StringComparison.Ordinal);
            if (i < 0) return text;
            int j = i + tag.Length, k = j;
            while (k < text.Length && char.IsDigit(text[k])) k++;
            if (k == j || !int.TryParse(text[j..k], out int cur)) return text;
            return text[..j] + (cur + by) + text[k..];
        }

        private static string Str(JsonObject? a, string k) => a?[k]?.GetValue<string>() ?? "";

        private static bool Bool(JsonObject? a, string k, bool fallback)
            => a?[k] is JsonValue v && v.TryGetValue(out bool b) ? b : fallback;

        private static JsonObject Err(string msg) => new() { ["error"] = msg };
    }
}

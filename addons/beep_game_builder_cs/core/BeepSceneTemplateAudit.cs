using Godot;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Beep.GameBuilder
{
    /// <summary>
    /// Read-only validation for addon scene templates. The generator copies templates verbatim,
    /// so template problems become project problems unless they are reported before stamping.
    /// </summary>
    public static class BeepSceneTemplateAudit
    {
        public const string TemplateRoot = "res://addons/beep_game_builder_cs/templates/scenes";

        private static readonly HashSet<string> GameFacingRawUiTypes = new()
        {
            "Button", "Label", "PanelContainer", "PopupMenu", "ColorRect",
            "CheckButton", "OptionButton", "ItemList", "Tree",
        };

        public sealed class Issue
        {
            public string Scene { get; init; } = "";
            public int Line { get; init; }
            public string Kind { get; init; } = "";
            public string Detail { get; init; } = "";
        }

        public sealed class Result
        {
            public int SceneCount;
            public readonly List<Issue> Issues = new();
            public bool Ok => Issues.Count == 0;
        }

        public static Result AuditAll(string root = TemplateRoot)
        {
            var result = new Result();
            Collect(root, result);
            return result;
        }

        public static List<string> Describe(Result result)
        {
            var lines = new List<string>();
            if (result.Ok)
            {
                lines.Add($"Scene template audit passed ({result.SceneCount} scene templates).");
                return lines;
            }

            lines.Add($"Scene template audit found {result.Issues.Count} issue(s) in {result.SceneCount} scene templates.");
            foreach (var issue in result.Issues)
                lines.Add($"WARN {issue.Scene}:{issue.Line}: {issue.Kind}: {issue.Detail}");
            return lines;
        }

        private static void Collect(string dir, Result result)
        {
            using var d = DirAccess.Open(dir);
            if (d == null) return;

            d.ListDirBegin();
            for (string f = d.GetNext(); f != ""; f = d.GetNext())
            {
                if (d.CurrentIsDir())
                {
                    if (!f.StartsWith(".")) Collect($"{dir}/{f}", result);
                    continue;
                }

                if (!f.EndsWith(".tscn")) continue;
                AuditScene($"{dir}/{f}", result);
            }
            d.ListDirEnd();
        }

        private static void AuditScene(string scenePath, Result result)
        {
            result.SceneCount++;
            string text = ReadAll(scenePath);
            if (string.IsNullOrEmpty(text))
            {
                result.Issues.Add(new Issue
                {
                    Scene = scenePath,
                    Line = 1,
                    Kind = "unreadable",
                    Detail = "template could not be read",
                });
                return;
            }

            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            AuditResourcePaths(scenePath, lines, result);
            AuditResourceIds(scenePath, lines, result);
            AuditNodeTree(scenePath, lines, result);
            AuditNodePaths(scenePath, lines, result);
            AuditRawGameFacingUi(scenePath, lines, result);
        }

        private static void AuditResourcePaths(string scenePath, string[] lines, Result result)
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                Match match = Regex.Match(lines[i], "path=\"(res://[^\"]+)\"");
                if (!match.Success) continue;

                string path = match.Groups[1].Value;
                if (!seen.Add(path)) continue;
                if (FileAccess.FileExists(path)) continue;

                result.Issues.Add(new Issue
                {
                    Scene = scenePath,
                    Line = i + 1,
                    Kind = "missing-resource",
                    Detail = path,
                });
            }
        }

        private static void AuditRawGameFacingUi(string scenePath, string[] lines, Result result)
        {
            int nodeLine = -1;
            string nodeName = "";
            string nodeType = "";
            bool hasScript = false;

            void Flush()
            {
                if (nodeLine < 0) return;
                if (!GameFacingRawUiTypes.Contains(nodeType) || hasScript) return;
                result.Issues.Add(new Issue
                {
                    Scene = scenePath,
                    Line = nodeLine + 1,
                    Kind = "raw-ui-node",
                    Detail = $"{nodeType} '{nodeName}' has no kit/component script",
                });
            }

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                Match node = Regex.Match(line, "^\\[node name=\"([^\"]+)\" type=\"([^\"]+)\"");
                if (node.Success)
                {
                    Flush();
                    nodeLine = i;
                    nodeName = node.Groups[1].Value;
                    nodeType = node.Groups[2].Value;
                    hasScript = false;
                    continue;
                }

                if (nodeLine >= 0 && line.StartsWith("script = ExtResource"))
                    hasScript = true;
            }

            Flush();
        }

        private static void AuditResourceIds(string scenePath, string[] lines, Result result)
        {
            var ext = new Dictionary<string, int>();
            var sub = new Dictionary<string, int>();

            for (int i = 0; i < lines.Length; i++)
            {
                Match extDecl = Regex.Match(lines[i], "^\\[ext_resource .*id=\"([^\"]+)\"");
                if (extDecl.Success)
                {
                    AddDeclaredResource(scenePath, result, ext, extDecl.Groups[1].Value, i + 1, "ext-resource");
                    continue;
                }

                Match subDecl = Regex.Match(lines[i], "^\\[sub_resource .*id=\"([^\"]+)\"");
                if (subDecl.Success)
                    AddDeclaredResource(scenePath, result, sub, subDecl.Groups[1].Value, i + 1, "sub-resource");
            }

            for (int i = 0; i < lines.Length; i++)
            {
                foreach (Match use in Regex.Matches(lines[i], "ExtResource\\(\"([^\"]+)\"\\)"))
                {
                    string id = use.Groups[1].Value;
                    if (!ext.ContainsKey(id))
                        result.Issues.Add(new Issue
                        {
                            Scene = scenePath,
                            Line = i + 1,
                            Kind = "undeclared-ext-resource",
                            Detail = id,
                        });
                }

                foreach (Match use in Regex.Matches(lines[i], "SubResource\\(\"([^\"]+)\"\\)"))
                {
                    string id = use.Groups[1].Value;
                    if (!sub.ContainsKey(id))
                        result.Issues.Add(new Issue
                        {
                            Scene = scenePath,
                            Line = i + 1,
                            Kind = "undeclared-sub-resource",
                            Detail = id,
                        });
                }
            }
        }

        private static void AddDeclaredResource(
            string scenePath,
            Result result,
            Dictionary<string, int> declared,
            string id,
            int line,
            string kind)
        {
            if (declared.TryAdd(id, line)) return;
            result.Issues.Add(new Issue
            {
                Scene = scenePath,
                Line = line,
                Kind = $"duplicate-{kind}",
                Detail = $"{id} was already declared at line {declared[id]}",
            });
        }

        private static void AuditNodeTree(string scenePath, string[] lines, Result result)
        {
            var paths = new HashSet<string>();
            var siblings = new HashSet<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                Match node = Regex.Match(line, "^\\[node name=\"([^\"]+)\"(?: type=\"([^\"]+)\")?(?: parent=\"([^\"]*)\")?.*\\]$");
                if (!node.Success)
                {
                    if (line.StartsWith("[node "))
                    {
                        result.Issues.Add(new Issue
                        {
                            Scene = scenePath,
                            Line = i + 1,
                            Kind = "malformed-node-header",
                            Detail = line,
                        });
                    }
                    continue;
                }

                string name = node.Groups[1].Value;
                string parent = node.Groups[3].Success ? node.Groups[3].Value : "__ROOT__";
                string fullPath;
                string siblingKey;

                if (parent == "__ROOT__")
                {
                    fullPath = ".";
                    siblingKey = "__ROOT__/" + name;
                }
                else
                {
                    if (!paths.Contains(parent))
                    {
                        result.Issues.Add(new Issue
                        {
                            Scene = scenePath,
                            Line = i + 1,
                            Kind = "missing-parent",
                            Detail = $"{name} parent='{parent}'",
                        });
                    }

                    fullPath = parent == "." ? name : $"{parent}/{name}";
                    siblingKey = $"{parent}/{name}";
                }

                if (!siblings.Add(siblingKey))
                {
                    result.Issues.Add(new Issue
                    {
                        Scene = scenePath,
                        Line = i + 1,
                        Kind = "duplicate-sibling",
                        Detail = siblingKey,
                    });
                }

                paths.Add(fullPath);
            }
        }

        private sealed class SceneNode
        {
            public string Name = "";
            public string Parent = "";
            public string Path = "";
            public string ScriptPath = "";
        }

        private static void AuditNodePaths(string scenePath, string[] lines, Result result)
        {
            var extResources = new Dictionary<string, string>();
            var nodes = new Dictionary<string, SceneNode>();
            var orderedNodes = new List<SceneNode>();
            var pathProperties = new List<(SceneNode Node, int Line, string Property, string Path)>();
            SceneNode? current = null;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                Match ext = Regex.Match(line, "^\\[ext_resource .*path=\"([^\"]+)\".*id=\"([^\"]+)\"");
                if (ext.Success)
                {
                    extResources[ext.Groups[2].Value] = ext.Groups[1].Value;
                    continue;
                }

                Match node = Regex.Match(line, "^\\[node name=\"([^\"]+)\"(?: type=\"([^\"]+)\")?(?: parent=\"([^\"]*)\")?.*\\]$");
                if (node.Success)
                {
                    string name = node.Groups[1].Value;
                    string parent = node.Groups[3].Success ? node.Groups[3].Value : "__ROOT__";
                    string path = parent == "__ROOT__" ? "." : parent == "." ? name : $"{parent}/{name}";
                    current = new SceneNode { Name = name, Parent = parent, Path = path };
                    nodes[path] = current;
                    orderedNodes.Add(current);
                    continue;
                }

                if (current == null) continue;

                Match script = Regex.Match(line, "^script\\s*=\\s*ExtResource\\(\"([^\"]+)\"\\)");
                if (script.Success)
                {
                    string id = script.Groups[1].Value;
                    if (extResources.TryGetValue(id, out string? scriptPath))
                        current.ScriptPath = scriptPath;
                    continue;
                }

                Match nodePath = Regex.Match(line, "^([A-Za-z0-9_]+)\\s*=\\s*NodePath\\(\"([^\"]*)\"\\)");
                if (nodePath.Success)
                    pathProperties.Add((current, i + 1, nodePath.Groups[1].Value, nodePath.Groups[2].Value));
            }

            foreach (var item in pathProperties)
            {
                if (string.IsNullOrWhiteSpace(item.Path)) continue;
                if (item.Path.StartsWith("/")) continue;

                string basePath = ResolutionBase(item.Node);
                if (PathExists(nodes, basePath, item.Path)) continue;

                result.Issues.Add(new Issue
                {
                    Scene = scenePath,
                    Line = item.Line,
                    Kind = "bad-node-path",
                    Detail = $"{item.Node.Path}.{item.Property} = NodePath(\"{item.Path}\") does not resolve from '{basePath}'",
                });
            }
        }

        private static string ResolutionBase(SceneNode node)
        {
            string script = node.ScriptPath.Replace("\\", "/");
            bool resolvesFromParent =
                script.EndsWith("/GameInfoBinder.cs")
                || script.EndsWith("/HudComponent.cs")
                || Regex.IsMatch(script, "/hud/[^/]+HudComponent\\.cs$");

            if (!resolvesFromParent) return node.Path;
            return node.Parent == "__ROOT__" ? "." : node.Parent;
        }

        private static bool PathExists(Dictionary<string, SceneNode> nodes, string basePath, string relativePath)
        {
            if (relativePath == ".") return nodes.ContainsKey(basePath);

            var parts = new List<string>();
            if (!string.IsNullOrEmpty(basePath) && basePath != ".")
                parts.AddRange(basePath.Split('/'));

            foreach (string segment in relativePath.Split('/'))
            {
                if (string.IsNullOrEmpty(segment) || segment == ".") continue;
                if (segment == "..")
                {
                    if (parts.Count > 0) parts.RemoveAt(parts.Count - 1);
                    continue;
                }
                parts.Add(segment);
            }

            string resolved = parts.Count == 0 ? "." : string.Join("/", parts);
            return nodes.ContainsKey(resolved);
        }

        private static string ReadAll(string path)
        {
            using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            return f == null ? "" : f.GetAsText();
        }
    }
}

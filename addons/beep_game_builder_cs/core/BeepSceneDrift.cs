using Godot;
using System.Collections.Generic;

namespace Beep.GameBuilder
{
    /// <summary>
    /// Does a generated project's `scenes/` still match the templates it was stamped from?
    ///
    /// WHY THIS IS A SEPARATE, UI-FREE CLASS
    /// ------------------------------------
    /// The generator COPIES templates into `scenes/`, so updating the addon does not reach an
    /// already-generated project: a fix to a template stays invisible until the developer
    /// re-generates with overwrite on. The dock had a way to force that refresh
    /// ("Overwrite existing scenes") and no way to find out whether it was needed — the
    /// destructive option existed and the diagnostic one did not.
    ///
    /// It lives here rather than in the dock because <see cref="BeepGameBuilderDock"/> builds an
    /// <c>EditorResourcePicker</c> in its `_Ready`, which is editor-only: instantiating the dock
    /// outside the editor SEGFAULTS, so anything embedded in it cannot be tested headlessly. The
    /// comparison is plain logic and now runs anywhere; the dock only formats the result.
    ///
    /// Read-only by construction — there is no code path here that writes or deletes.
    /// </summary>
    public static class BeepSceneDrift
    {
        public const string TemplateRoot = "res://addons/beep_game_builder_cs/templates/scenes";
        public const string GeneratedRoot = "res://scenes";

        public sealed class Result
        {
            /// <summary>False when there is no generated project to compare against.</summary>
            public bool HasGeneratedProject;
            /// <summary>Scenes identical to their template.</summary>
            public int UpToDate;
            /// <summary>Scenes present in both but differing — these are behind.</summary>
            public readonly List<string> Drifted = new();
            /// <summary>Scenes with no template: the developer's own screens. Never touched by a
            /// refresh, so they are reported separately rather than counted as drift.</summary>
            public int OwnScreens;
            /// <summary>Duplicate basenames, which make the pairing ambiguous.</summary>
            public readonly List<string> Ambiguous = new();
        }

        public static Result Compare(string templateRoot = TemplateRoot,
                                     string generatedRoot = GeneratedRoot)
        {
            var r = new Result();
            if (!DirAccess.DirExistsAbsolute(generatedRoot)) return r;
            r.HasGeneratedProject = true;

            var templates = new Dictionary<string, string>();
            Collect(templateRoot, templates, r);
            var mine = new Dictionary<string, string>();
            Collect(generatedRoot, mine, r);

            var names = new List<string>(mine.Keys);
            names.Sort();
            foreach (string name in names)
            {
                if (!templates.TryGetValue(name, out string? tPath)) { r.OwnScreens++; continue; }
                string a = ReadAll(tPath), b = ReadAll(mine[name]);
                // An unreadable file is not "identical" -- counting it as up to date would be the
                // quiet false negative this whole class exists to remove.
                if (a.Length == 0 || b.Length == 0) { r.Drifted.Add(name + " (unreadable)"); continue; }
                if (a == b) r.UpToDate++;
                else r.Drifted.Add(name);
            }
            return r;
        }

        /// <summary>Pairs by filename, which is how the generator lays scenes out
        /// (`scenes/ui/&lt;genre&gt;/x.tscn` from `templates/scenes/&lt;genre&gt;/x.tscn`).</summary>
        private static void Collect(string dir, Dictionary<string, string> into, Result r)
        {
            using var d = DirAccess.Open(dir);
            if (d == null) return;
            d.ListDirBegin();
            for (string f = d.GetNext(); f != ""; f = d.GetNext())
            {
                if (d.CurrentIsDir())
                {
                    if (!f.StartsWith(".")) Collect($"{dir}/{f}", into, r);
                }
                else if (f.EndsWith(".tscn") && !into.TryAdd(f, $"{dir}/{f}"))
                {
                    // First wins, and say so: silently comparing against whichever the walk
                    // reached first would make the verdict depend on directory order.
                    if (!r.Ambiguous.Contains(f)) r.Ambiguous.Add(f);
                }
            }
            d.ListDirEnd();
        }

        private static string ReadAll(string path)
        {
            using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            return f == null ? "" : f.GetAsText();
        }

        /// <summary>The result as lines for the dock's log (or any other sink).</summary>
        public static List<string> Describe(Result r)
        {
            var lines = new List<string>();
            if (!r.HasGeneratedProject)
            {
                lines.Add($"No {GeneratedRoot}/ — nothing generated yet, so nothing can be behind.");
                return lines;
            }
            foreach (string f in r.Ambiguous)
                lines.Add($"  ? {f} — two scenes share this name; the pairing is ambiguous.");
            foreach (string f in r.Drifted)
                lines.Add($"  ▲ {f} — differs from its template");

            lines.Add(r.Drifted.Count == 0
                ? $"✔ {r.UpToDate} scene(s) match their templates. {r.OwnScreens} of yours have "
                  + "no template (your own screens — a refresh never touches them)."
                : $"▲ {r.Drifted.Count} scene(s) behind the templates, {r.UpToDate} up to date. "
                  + "Re-Create with 'Overwrite existing scenes' ticked to pull them forward — that "
                  + "DISCARDS your edits to the listed files, so copy anything you want first.");
            return lines;
        }
    }
}

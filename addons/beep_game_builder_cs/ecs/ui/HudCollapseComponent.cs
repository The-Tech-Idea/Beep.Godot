using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Adds collapse handles to screen-edge HUD blocks under the parent Godot.Control.
    /// Attach once under HUD/Root. Direct HUD children are treated as widgets; nested content
    /// is left alone unless IncludeNestedPanels is explicitly enabled.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class HudCollapseComponent : UIComponent
    {
        [Export] public bool AutoAttachPanels { get; set; } = false;
        [Export] public bool IncludeNestedPanels { get; set; } = false;
        [Export] public bool IncludeSelfDrawingWidgets { get; set; } = true;
        [Export] public Godot.Collections.Array<string> ExcludedNames { get; set; } = new()
        {
            "Theme",
            "GenreHud",
            "GameInfoBinder",
            "HudCollapse",
            "RpgHudCollapse",
            "CrosshairLayer"
        };

        private readonly HashSet<string> _excluded = new(StringComparer.OrdinalIgnoreCase);

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            if (!AutoAttachPanels) return;
            CallDeferred(nameof(Setup));
        }

        private void Setup()
        {
            _excluded.Clear();
            foreach (string name in ExcludedNames)
                if (!string.IsNullOrWhiteSpace(name))
                    _excluded.Add(name);

            Node? root = GetParent();
            if (root is not Godot.Control && root is not CanvasLayer)
            {
                GD.PushWarning($"[{Name}] HudCollapseComponent must be a child of a HUD root Godot.Control or CanvasLayer.");
                return;
            }

            foreach (Node child in root.GetChildren())
                Visit(child, directChild: true);
        }

        private void Visit(Node node, bool directChild)
        {
            if (node == this || _excluded.Contains(node.Name.ToString())) return;
            if (node is Godot.Control control && ShouldAttach(control, directChild))
                EnsureCollapse(control);

            if (!IncludeNestedPanels) return;
            foreach (Node child in node.GetChildren())
                Visit(child, directChild: false);
        }

        private bool ShouldAttach(Godot.Control control, bool directChild)
        {
            if (!control.Visible) return false;
            if (control.GetNodeOrNull<CollapsiblePanelComponent>("Collapse") != null) return false;
            if (control is Button or Label or TextureRect or ColorRect) return false;
            if (!directChild && control.GetChildCount() == 0 && !IncludeSelfDrawingWidgets) return false;

            string name = control.Name.ToString();
            if (name.EndsWith("Frame", StringComparison.OrdinalIgnoreCase)) return false;
            if (directChild && IncludeSelfDrawingWidgets) return true;
            if (name.EndsWith("Panel", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.EndsWith("Box", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.EndsWith("Dock", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.EndsWith("Bar", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.Contains("Minimap", StringComparison.OrdinalIgnoreCase)) return true;
            if (directChild && control.GetChildCount() > 0) return true;
            return false;
        }

        private static void EnsureCollapse(Godot.Control control)
        {
            var collapse = new CollapsiblePanelComponent
            {
                Name = "Collapse",
                Title = Pretty(control.Name.ToString()),
                ParticipatesInSave = true,
                SaveKey = control.GetPath().ToString().Replace("/", "."),
                GenerateControlsWhenPathsEmpty = true,
            };
            control.AddChild(collapse);
        }

        private static string Pretty(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "HUD";
            return raw.Replace("HUD", "HUD ").Replace("Hud", "HUD ");
        }
    }
}

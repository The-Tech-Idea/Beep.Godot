using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// The single owner of the scene's ambient tint.
    ///
    /// Godot allows only ONE CanvasModulate per viewport, but several systems want to tint
    /// the world — the day/night cycle, the weather system, potentially seasons. They used
    /// to each find-or-create their own CanvasModulate and fight over it. This component owns
    /// the one CanvasModulate; everyone else contributes a colour through it and it composes
    /// them (multiplicatively, so a storm at midnight reads darker than a storm at noon).
    ///
    /// Contributors call <see cref="SetContribution"/> with a stable key; each key is one
    /// multiplicative layer. Clearing a key (null) removes that layer. The final tint is
    /// BaseColor times every contribution.
    ///
    /// Attach to the world root (a Node2D). If a CanvasModulate already exists in the scene
    /// it is adopted; otherwise one is created.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class AmbientController : WorldComponent
    {
        /// <summary>Baseline tint before any contribution. White = no change.</summary>
        [Export] public Color BaseColor { get; set; } = new(1, 1, 1, 1);

        /// <summary>How fast the visible tint eases toward the composed target (per second).
        /// 0 snaps instantly; higher is snappier. Smooths dawn/dusk and weather changes.</summary>
        [Export(PropertyHint.Range, "0,20,0.1")] public float EaseSpeed { get; set; } = 3.0f;

        private CanvasModulate? _modulate;
        private readonly Dictionary<string, Color> _contributions = new();
        private Color _target = new(1, 1, 1, 1);

        public override void _Ready()
        {
            base._Ready();
            // Runtime only: this adopts/creates a CanvasModulate node. In the editor that
            // would just add a node to the open scene.
            if (Engine.IsEditorHint()) return;
            CallDeferred(nameof(EnsureModulate));
        }

        private void EnsureModulate()
        {
            if (GetParent() is not Node parent) return;

            // Adopt any CanvasModulate already in the scene (the genre scenes ship one), so
            // we don't create a second — Godot only honours one per viewport anyway.
            _modulate = EntityComponent.FindComponent<CanvasModulate>(GetTree()?.Root, true);
            if (_modulate == null)
            {
                _modulate = new CanvasModulate { Name = "Ambient", Color = BaseColor };
                parent.AddChild(_modulate);
                if (parent.IsInsideTree()) _modulate.Owner = parent.Owner;
            }
            _target = Compose();
            _modulate.Color = _target;
        }

        /// <summary>Set (or replace) a named tint layer. Pass null to remove it. Recomposes
        /// the target immediately; the visible colour eases toward it in _Process.</summary>
        public void SetContribution(string key, Color? tint)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (tint is { } c) _contributions[key] = c;
            else _contributions.Remove(key);
            _target = Compose();
        }

        private Color Compose()
        {
            Color c = BaseColor;
            foreach (var contribution in _contributions.Values)
            {
                c.R *= contribution.R;
                c.G *= contribution.G;
                c.B *= contribution.B;
            }
            c.A = 1f;
            return c;
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint() || _modulate == null) return;

            if (EaseSpeed <= 0f)
            {
                _modulate.Color = _target;
                return;
            }
            float t = Mathf.Clamp((float)delta * EaseSpeed, 0f, 1f);
            _modulate.Color = _modulate.Color.Lerp(_target, t);
        }

        /// <summary>Find the AmbientController driving <paramref name="node"/>'s tree, so
        /// contributors don't need a hard reference. Null (with a one-line warning) if the
        /// scene has none — the caller's other effects still work, it just won't tint.</summary>
        public static AmbientController? ForTree(Node node)
        {
            var found = EntityComponent.FindComponent<AmbientController>(node.GetTree()?.Root, true);
            if (found == null)
                GD.PushWarning($"[{node.Name}] No AmbientController in the scene — ambient tint disabled. " +
                               "Add one to the world root.");
            return found;
        }
    }
}

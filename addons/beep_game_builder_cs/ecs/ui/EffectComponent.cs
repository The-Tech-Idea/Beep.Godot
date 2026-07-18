using System.Collections.Generic;
using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Base class for UI effect components (ripple, pulse, shake, slide, …).
    /// Adds OPTIONAL cascade: set <see cref="ApplyToChildren"/> = true and the
    /// effect applies to ALL descendant Controls (Buttons only if
    /// <see cref="ButtonsOnly"/>), so one component under a container affects
    /// every child. Default behaviour (ApplyToChildren = false) targets only the
    /// parent — unchanged from the original single-target effects.
    /// </summary>
    [Tool]
    [GlobalClass]
    public abstract partial class EffectComponent : UIComponent
    {
        /// <summary>If true, the effect applies to all descendant UI nodes of the
        /// parent, not just the parent itself. Drop the component under a container
        /// and every button/label inside gets the effect.</summary>
        [Export] public bool ApplyToChildren { get; set; } = false;

        /// <summary>When ApplyToChildren is true, only affect Buttons (skip Labels,
        /// Panels, etc.). Default true — usually you only want effects on clickable things.</summary>
        [Export] public bool ButtonsOnly { get; set; } = true;

        /// <summary>The set of Controls this effect currently targets. Populated in
        /// _Ready via <see cref="ResolveTargets"/>. Subclasses iterate this list.</summary>
        protected readonly List<Godot.Control> Targets = new();

        public override void _Ready()
        {
            base._Ready();
            // Defer so the parent's full subtree is in the tree before we scan it.
            CallDeferred(nameof(ResolveTargets));
        }

        /// <summary>Populate <see cref="Targets"/>: the parent alone, or the parent
        /// plus all descendant Controls (Buttons only if ButtonsOnly).</summary>
        protected void ResolveTargets()
        {
            Targets.Clear();
            if (GetParent() is not Godot.Control parent)
            {
                if (!Engine.IsEditorHint())
                    GD.PushWarning($"[{Name}] {GetType().Name} needs a Control parent to affect; got '{GetParent()?.GetType().Name ?? "null"}'. Its Shake()/SlideIn()/etc. will do nothing until reparented under a Control.");
                return;
            }

            AddTarget(parent);
            if (ApplyToChildren)
                CollectControls(parent);
        }

        private void CollectControls(Node root)
        {
            foreach (var child in root.GetChildren())
            {
                if (child is Godot.Control c)
                {
                    if (!ButtonsOnly || c is Button)
                        AddTarget(c);
                    CollectControls(child);
                }
            }
        }

        private void AddTarget(Godot.Control c) => Targets.Add(c);

        /// <summary>True if this effect should operate on the given control.</summary>
        protected bool TargetsNode(Godot.Control c) => Targets.Contains(c);
    }
}

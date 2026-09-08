using Godot;
using System.Collections.Generic;
using Beep.ECS.UI.Kit;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Binds a row of <see cref="CooldownComponent"/>s to a <see cref="KitSlotGrid"/>, so an
    /// ability bar shows what is ready and what is still winding down.
    ///
    /// This closes a real gap rather than adding a widget for its own sake. The addon has had
    /// <c>CooldownComponent</c> — with <c>Progress</c>, <c>IsReady</c> and a per-tick
    /// <c>CooldownProgress</c> signal — since early on, and exactly one thing consumed it
    /// (<c>AttackComponent</c>, to decide whether an attack may fire). Nothing anywhere displayed
    /// a cooldown, and no widget could: the slot grid already called itself "an inventory / hotbar
    /// / recipe slot grid" and had no notion of one.
    ///
    /// It binds, it does not build. The grid is authored in the scene, and this resolves it by
    /// path or by name, in keeping with every other component in this folder: the kit draws, the
    /// scene composes, and a component wires the two together.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class AbilityBarComponent : UIComponent
    {
        /// <summary>The authored <see cref="KitSlotGrid"/> that draws the bar. Left empty, the
        /// component looks for a child or sibling named "AbilityBar", then any slot grid under the
        /// parent.</summary>
        [Export] public NodePath SlotGridPath { get; set; } = new("");

        /// <summary>The abilities, in bar order. Each entry is a path to a
        /// <see cref="CooldownComponent"/>; slot 0 shows the first, and so on.</summary>
        [Export] public Godot.Collections.Array<NodePath> AbilityPaths { get; set; } = new();

        /// <summary>The key or button that fires each slot, in the same order. Shorter than the
        /// ability list is fine — the remaining slots simply show no glyph.</summary>
        [Export] public string[] Hotkeys { get; set; } = System.Array.Empty<string>();

        /// <summary>Emitted when an ability finishes cooling down, with its slot index. Lets a HUD
        /// flash the slot or play a sound without subscribing to each cooldown itself.</summary>
        [Signal] public delegate void AbilityReadyEventHandler(int slot);

        /// <summary>One bound ability and the exact delegates it was subscribed with.
        ///
        /// The handlers are kept rather than rebuilt at unsubscribe time: `event -= someLambda`
        /// only removes a delegate equal to the one added, and a freshly written lambda never is,
        /// so rebuilding them would silently remove nothing and leave the bar subscribed to a
        /// component it no longer draws.</summary>
        private sealed class BoundAbility
        {
            public required CooldownComponent Ability;
            public required CooldownComponent.CooldownProgressEventHandler OnProgress;
            public required CooldownComponent.CooldownReadyEventHandler OnReady;
        }

        private KitSlotGrid? _grid;
        private readonly List<BoundAbility> _abilities = new();
        private bool _bound;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;

            _grid = ResolveGrid();
            if (_grid == null)
            {
                GD.PushWarning($"[{Name}] found no KitSlotGrid to drive, so no ability cooldowns "
                             + "will be shown. Set SlotGridPath or name one 'AbilityBar'.");
                return;
            }

            BindAbilities();
            ApplyHotkeys();
            // Paint the starting state rather than waiting for the first tick: an ability that is
            // already cooling down when the bar appears would otherwise read as ready.
            RefreshAll();
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            foreach (BoundAbility bound in _abilities)
            {
                if (!GodotObject.IsInstanceValid(bound.Ability)) continue;
                bound.Ability.CooldownProgress -= bound.OnProgress;
                bound.Ability.CooldownReady -= bound.OnReady;
            }
            _abilities.Clear();
            _bound = false;
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (ResolveGrid() == null)
                return new[] { "Point SlotGridPath at a KitSlotGrid, or name one 'AbilityBar'." };
            if (AbilityPaths.Count == 0)
                return new[] { "Add at least one CooldownComponent path, or this bar shows nothing." };
            return System.Array.Empty<string>();
        }

        /// <summary>Repaint every slot from its ability's current state. Call after changing the
        /// bound abilities at runtime.</summary>
        public void RefreshAll()
        {
            if (_grid == null) return;
            for (int slot = 0; slot < _abilities.Count; slot++)
                Apply(slot);
        }

        private void BindAbilities()
        {
            if (_bound) return;

            foreach (NodePath path in AbilityPaths)
            {
                var ability = GetNodeOrNull<CooldownComponent>(path);
                if (ability == null)
                {
                    GD.PushWarning($"[{Name}] ability path '{path}' does not resolve to a "
                                 + "CooldownComponent; its slot will stay blank.");
                    continue;
                }

                int slot = _abilities.Count;
                CooldownComponent.CooldownProgressEventHandler onProgress = _ => Apply(slot);
                CooldownComponent.CooldownReadyEventHandler onReady = () => OnReady(slot);
                _abilities.Add(new BoundAbility
                {
                    Ability = ability,
                    OnProgress = onProgress,
                    OnReady = onReady,
                });
                ability.CooldownProgress += onProgress;
                ability.CooldownReady += onReady;
            }

            _bound = true;
        }

        private void OnReady(int slot)
        {
            Apply(slot);
            EmitSignal(SignalName.AbilityReady, slot);
        }

        /// <summary>
        /// Push one ability's state onto its slot.
        ///
        /// The slot stores what REMAINS, while CooldownComponent reports how far it has come, so
        /// the two are complements. Getting that backwards would draw a full wedge on a ready
        /// ability, which is why it is converted in exactly one place.
        /// </summary>
        private void Apply(int slot)
        {
            if (_grid == null || slot < 0 || slot >= _abilities.Count) return;

            CooldownComponent ability = _abilities[slot].Ability;
            if (!GodotObject.IsInstanceValid(ability)) return;

            _grid.SetSlotCooldown(slot, ability.IsReady ? 0f : 1f - ability.Progress);
        }

        private void ApplyHotkeys()
        {
            if (_grid == null || Hotkeys.Length == 0) return;
            _grid.SetSlotHotkeys(Hotkeys);
        }

        private KitSlotGrid? ResolveGrid()
        {
            if (!SlotGridPath.IsEmpty && GetNodeOrNull<KitSlotGrid>(SlotGridPath) is { } byPath)
                return byPath;

            Node? parent = GetParent();
            if (parent?.FindChild("AbilityBar", true, false) is KitSlotGrid named)
                return named;
            if (parent is KitSlotGrid parentGrid)
                return parentGrid;

            if (parent != null)
                foreach (Node child in parent.GetChildren())
                    if (child is KitSlotGrid found)
                        return found;

            return null;
        }
    }
}

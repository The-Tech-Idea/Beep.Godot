using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Active buff/debuff icon display. Listens to sibling StatusEffectComponent
    /// and shows each active effect as a small icon with a duration progress ring.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class BuffBarComponent : UIComponent
    {
        [Export] public int MaxSlots { get; set; } = 8;
        [Export] public Vector2 IconSize { get; set; } = new(32, 32);
        [Export] public Color BuffColor { get; set; } = new(0.3f, 0.8f, 0.3f, 1f);
        [Export] public Color DebuffColor { get; set; } = new(0.8f, 0.2f, 0.2f, 1f);

        private HBoxContainer? _container;
        private readonly Dictionary<string, ProgressRingComponent> _icons = new();
        // Resolved once in Setup — walking the sibling list every _Process frame was pure waste.
        private StatusEffectComponent? _status;

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(Setup));
        }

        private void Setup()
        {
            if (Engine.IsEditorHint()) return;
            _container = new HBoxContainer
            {
                Name = "BuffBar"
            };
            _container.AddThemeConstantOverride("separation", 4);

            if (GetParent() is Node parent)
            {
                parent.AddChild(_container);
                if (parent.IsInsideTree())
                    _container.Owner = parent.Owner;
            }

            _status = GetSiblingComponent<StatusEffectComponent>();
            if (_status != null)
            {
                _status.EffectApplied += OnEffectApplied;
                _status.EffectExpired += OnEffectExpired;
                _status.EffectTicked += OnEffectTicked;
            }
            else
            {
                GD.PushWarning($"[{Name}] BuffBarComponent found no sibling StatusEffectComponent — it will display nothing. Add one alongside it.");
            }
        }

        public override void _Process(double delta)
        {
            // Update progress rings from active effects.
            if (_status == null || !GodotObject.IsInstanceValid(_status)) return;
            foreach (var effect in _status.ActiveEffects)
            {
                if (_icons.TryGetValue(effect.Id, out var ring))
                {
                    ring.MaxValue = 1f;
                    ring.Value = effect.TotalDuration > 0
                        ? 1f - (effect.Duration / effect.TotalDuration) : 1f;
                }
            }
        }

        private void OnEffectApplied(string effectId, int stackCount)
        {
            if (_container == null || _icons.ContainsKey(effectId)) return;
            if (_icons.Count >= MaxSlots) return;

            bool isBuff = true;
            if (_status != null)
            {
                var effect = _status.ActiveEffects.Find(e => e.Id == effectId);
                if (effect != null) isBuff = effect.IsBuff;
            }

            var ring = new ProgressRingComponent
            {
                Name = $"Buff_{effectId}",
                CustomMinimumSize = IconSize,
                RingColor = isBuff ? BuffColor : DebuffColor
            };
            _container.AddChild(ring);
            _icons[effectId] = ring;
        }

        private void OnEffectExpired(string effectId)
        {
            if (_icons.TryGetValue(effectId, out var ring))
            {
                _icons.Remove(effectId);
                ring.QueueFree();
            }
        }

        private void OnEffectTicked(string effectId, float remaining) { /* optional tick visual */ }

        public override void _ExitTree()
        {
            // Drop the sibling subscriptions so the freed StatusEffectComponent doesn't
            // fire into a disposed buff bar (and this bar can be freed independently).
            if (_status != null && GodotObject.IsInstanceValid(_status))
            {
                _status.EffectApplied -= OnEffectApplied;
                _status.EffectExpired -= OnEffectExpired;
                _status.EffectTicked -= OnEffectTicked;
            }
            _status = null;
        }
    }
}

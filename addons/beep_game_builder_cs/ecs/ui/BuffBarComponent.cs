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

            var status = GetSiblingComponent<StatusEffectComponent>();
            if (status != null)
            {
                status.EffectApplied += OnEffectApplied;
                status.EffectExpired += OnEffectExpired;
                status.EffectTicked += OnEffectTicked;
            }
        }

        public override void _Process(double delta)
        {
            // Update progress rings from active effects.
            var status = GetSiblingComponent<StatusEffectComponent>();
            if (status == null) return;
            foreach (var effect in status.ActiveEffects)
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

            var status = GetSiblingComponent<StatusEffectComponent>();
            bool isBuff = true;
            if (status != null)
            {
                var effect = status.ActiveEffects.Find(e => e.Id == effectId);
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
    }
}

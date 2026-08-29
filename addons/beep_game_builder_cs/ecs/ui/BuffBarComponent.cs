using Godot;
using System.Collections.Generic;
using Beep.ECS.UI.Kit;

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
        [Export] public NodePath ContainerPath { get; set; } = new("");
        [Export] public bool BuildInEditor { get; set; } = true;
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = false;

        private HBoxContainer? _container;
        private bool _createdContainer;
        private readonly Dictionary<string, ProgressRingComponent> _icons = new();
        // Resolved once in Setup — walking the sibling list every _Process frame was pure waste.
        private StatusEffectComponent? _status;

        public override void _Ready()
        {
            base._Ready();
            SetProcess(false);
            if (!Engine.IsEditorHint() || BuildInEditor)
                CallDeferred(nameof(Setup));
            UpdateConfigurationWarnings();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (!GenerateControlsWhenPathsEmpty && FindBuffBar() == null)
                return new[] { "Add an authored HBoxContainer named BuffBar, set ContainerPath, or enable GenerateControlsWhenPathsEmpty." };
            return System.Array.Empty<string>();
        }

        private void Setup()
        {
            if (!BindExistingContainer())
            {
                if (!GenerateControlsWhenPathsEmpty)
                    return;

                BuildGeneratedContainer();
            }

            if (_container == null)
                return;

            StyleContainer();
            if (Engine.IsEditorHint())
                return;

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
            UpdateProcessing();
        }

        private void BuildGeneratedContainer()
        {
            if (GetParent() is not Node parent)
                return;

            _createdContainer = true;
            _container = new HBoxContainer
            {
                Name = "BuffBar"
            };
            StyleContainer();

            parent.AddChild(_container);
            SetEditedOwner(_container);
        }

        private bool BindExistingContainer()
        {
            _createdContainer = false;
            _container = FindBuffBar();
            return _container != null;
        }

        public bool UsesSceneControls()
            => FindBuffBar() != null;

        private HBoxContainer? FindBuffBar()
        {
            if (!ContainerPath.IsEmpty && GetNodeOrNull<HBoxContainer>(ContainerPath) is { } pathContainer)
                return pathContainer;

            if (FindChild("BuffBar", recursive: true, owned: false) is HBoxContainer childContainer)
                return childContainer;

            return GetParent()?.FindChild("BuffBar", recursive: true, owned: false) as HBoxContainer;
        }

        private void StyleContainer()
        {
            if (_container != null)
                KitChrome.SetConstantOverrideIfChanged(_container, "separation", 4);
        }

        public override void _Process(double delta)
        {
            // Update progress rings from active effects.
            if (_status == null || !GodotObject.IsInstanceValid(_status))
            {
                UpdateProcessing();
                return;
            }
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
                Accent = isBuff ? UiSurface.Role.Success : UiSurface.Role.Danger
            };
            _container.AddChild(ring);
            SetEditedOwner(ring);
            _icons[effectId] = ring;
            UpdateProcessing();
        }

        private void OnEffectExpired(string effectId)
        {
            if (_icons.TryGetValue(effectId, out var ring))
            {
                _icons.Remove(effectId);
                ring.QueueFree();
            }
            UpdateProcessing();
        }

        private void OnEffectTicked(string effectId, float remaining) { /* optional tick visual */ }

        private void UpdateProcessing()
            => SetProcess(!Engine.IsEditorHint()
                          && IsActive
                          && _status != null
                          && GodotObject.IsInstanceValid(_status)
                          && _icons.Count > 0);

        public override void _ExitTree()
        {
            base._ExitTree();
            // Drop the sibling subscriptions so the freed StatusEffectComponent doesn't
            // fire into a disposed buff bar (and this bar can be freed independently).
            if (_status != null && GodotObject.IsInstanceValid(_status))
            {
                _status.EffectApplied -= OnEffectApplied;
                _status.EffectExpired -= OnEffectExpired;
                _status.EffectTicked -= OnEffectTicked;
            }
            _status = null;
            if (_createdContainer && _container != null && GodotObject.IsInstanceValid(_container)) _container.QueueFree();
            _container = null;
        }

        private void SetEditedOwner(Node node)
        {
            if (!Engine.IsEditorHint())
                return;

            node.Owner = GetTree()?.EditedSceneRoot;
        }
    }
}

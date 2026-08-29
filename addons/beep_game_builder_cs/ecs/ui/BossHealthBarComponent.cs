using Godot;
using Beep.ECS.UI.Kit;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Segmented boss health bar at the top of the screen with multi-phase colors:
    /// phase-based color transitions driven by a sibling HealthComponent.
    /// (No slide animation — the old SlideDuration export was never wired to anything.)
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class BossHealthBarComponent : UIComponent
    {
        [Export] public int PhaseCount { get; set; } = 3;
        // Palette-derived, not a literal. A colour baked into a component is a palette
        // pinned where no skin can reach it; these follow theme -> palette like every
        // other control. Computed, so a skin change is picked up with no invalidation.
        public Color BarColor => UiSurface.Semantic(this, UiSurface.Role.Danger);
        /// <summary>The name shown above the bar. Settable at runtime — updates the label live.</summary>
        [Export] public string BossName
        {
            get => _bossName;
            set { _bossName = value; SetNameText(value); }
        }
        private string _bossName = "BOSS";
        [Export] public NodePath NameLabelPath { get; set; } = new("");
        [Export] public NodePath BarPath { get; set; } = new("");
        [Export] public bool BuildInEditor { get; set; } = true;
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = false;

        [Signal] public delegate void PhaseChangedEventHandler(int phase);

        private KitMeter? _bar;
        private Godot.Control? _nameLabel;
        private int _currentPhase;
        private Control? _generatedRoot;
        private HealthComponent? _health;

        public override void _Ready()
        {
            base._Ready();
            if (!Engine.IsEditorHint() || BuildInEditor)
                CallDeferred(nameof(Setup));
            UpdateConfigurationWarnings();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (!GenerateControlsWhenPathsEmpty && FindBar() == null)
                return new[] { "Set BarPath, add a scene-authored KitMeter named BossBar, or enable GenerateControlsWhenPathsEmpty." };
            return System.Array.Empty<string>();
        }

        private void Setup()
        {
            if (!BindExistingControls())
            {
                if (!GenerateControlsWhenPathsEmpty)
                    return;

                BuildGeneratedControls();
            }

            StyleControls();
            if (Engine.IsEditorHint())
            {
                SetNameText(_bossName);
                if (_bar != null)
                {
                    _bar.Value = 1f;
                    _bar.Readout = "100 / 100";
                    _bar.Visible = true;
                }
                return;
            }

            _health = GetSiblingComponent<HealthComponent>();
            if (_health != null && _bar != null)
            {
                _health.HealthChanged += OnHealthChanged;
                _bar.Value = _health.MaxHealth <= 0f ? 0f : _health.CurrentHealth / _health.MaxHealth;
                _bar.Readout = $"{Mathf.RoundToInt(_health.CurrentHealth)} / {Mathf.RoundToInt(_health.MaxHealth)}";
                _bar.Visible = true;
            }
            else if (_bar != null)
            {
                _bar.Visible = false;
                GD.PushWarning($"[{Name}] BossHealthBarComponent found no sibling HealthComponent; bind a HealthComponent beside it or keep the authored bar hidden.");
            }
        }

        private void BuildGeneratedControls()
        {
            if (_generatedRoot != null && GodotObject.IsInstanceValid(_generatedRoot))
                _generatedRoot.QueueFree();
            _generatedRoot = null;

            if (GetParent() is not Node parent) return;
            int fs = UiSurface.FontSize(this);
            _bar = new KitMeter
            {
                Name = "BossBar",
                CustomMinimumSize = new Vector2(fs * 28f, fs * 1.7f),
                SizeFlagsHorizontal = Godot.Control.SizeFlags.ExpandFill,
                Fill = UiSurface.Role.Danger,
                EndCaps = true,
                Visible = false,
                MouseFilter = Godot.Control.MouseFilterEnum.Ignore
            };
            _nameLabel = new KitHudText
            {
                Name = "BossName",
                Text = _bossName,
                Role = UiSurface.TextRole.Subtitle,
                MouseFilter = Godot.Control.MouseFilterEnum.Ignore
            };

            var root = new VBoxContainer { Name = "BossHealthBar", MouseFilter = Godot.Control.MouseFilterEnum.Ignore };
            root.SetAnchorsPreset(Godot.Control.LayoutPreset.TopWide);
            KitChrome.SetConstantOverrideIfChanged(root, "separation", 4);
            root.OffsetLeft = fs * 4f;
            root.OffsetRight = -fs * 4f;
            root.AddChild(_nameLabel);
            root.AddChild(_bar);

            parent.AddChild(root);
            _generatedRoot = root;
            SetEditedOwner(root);
            SetEditedOwner(_nameLabel);
            SetEditedOwner(_bar);
        }

        private bool BindExistingControls()
        {
            if (!UsesSceneControls())
                return false;

            KitMeter? bar = FindBar();
            if (bar == null)
                return false;

            Godot.Control? label = FindNameLabel();
            if (_generatedRoot != null && GodotObject.IsInstanceValid(_generatedRoot))
            {
                _generatedRoot.QueueFree();
                _generatedRoot = null;
            }

            _bar = bar;
            _nameLabel = label;
            return true;
        }

        public bool UsesSceneControls()
            => !NameLabelPath.IsEmpty || !BarPath.IsEmpty || FindNameLabel() != null || FindBar() != null;

        private Godot.Control? FindNameLabel()
        {
            if (!NameLabelPath.IsEmpty && GetNodeOrNull<Godot.Control>(NameLabelPath) is { } pathLabel)
                return pathLabel;

            if (FindChild("BossName", recursive: true, owned: false) is Godot.Control childLabel)
                return childLabel;

            return GetParent()?.FindChild("BossName", recursive: true, owned: false) as Godot.Control;
        }

        private KitMeter? FindBar()
        {
            if (!BarPath.IsEmpty && GetNodeOrNull<KitMeter>(BarPath) is { } pathBar)
                return pathBar;

            if (FindChild("BossBar", recursive: true, owned: false) is KitMeter childBar)
                return childBar;

            return GetParent()?.FindChild("BossBar", recursive: true, owned: false) as KitMeter;
        }

        private void StyleControls()
        {
            int fs = UiSurface.FontSize(this);
            if (_nameLabel != null)
            {
                _nameLabel.MouseFilter = Godot.Control.MouseFilterEnum.Ignore;
                if (_nameLabel is KitHudText hud)
                {
                    hud.Text = _bossName;
                    hud.Role = UiSurface.TextRole.Subtitle;
                }
                else if (_nameLabel is Label label)
                {
                    label.Text = _bossName;
                    label.HorizontalAlignment = HorizontalAlignment.Center;
                    label.VerticalAlignment = VerticalAlignment.Center;
                    label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                    label.ClipText = true;
                    KitChrome.SetFontSizeOverrideIfChanged(label, "font_size", fs);
                }
            }

            if (_bar == null)
                return;

            _bar.CustomMinimumSize = new Vector2(fs * 28f, fs * 1.7f);
            _bar.SizeFlagsHorizontal = Godot.Control.SizeFlags.ExpandFill;
            _bar.Fill = UiSurface.Role.Danger;
            _bar.EndCaps = true;
            _bar.MouseFilter = Godot.Control.MouseFilterEnum.Ignore;
        }

        private void SetNameText(string text)
        {
            if (_nameLabel is KitHudText hud) hud.Text = text;
            else if (_nameLabel is Label label) label.Text = text;
        }

        private void OnHealthChanged(float current, float max)
        {
            if (_bar == null || !IsActive) return;
            _bar.Value = max <= 0f ? 0f : current / max;
            _bar.Readout = $"{Mathf.RoundToInt(current)} / {Mathf.RoundToInt(max)}";

            if (max <= 0f || PhaseCount <= 0) return;   // guard 0/0 → NaN on a degenerate config

            // Phase transition: divide health into equal segments.
            int phase = Mathf.CeilToInt((current / max) * PhaseCount);
            if (phase != _currentPhase)
            {
                _currentPhase = phase;
                float phasePct = (float)phase / PhaseCount;
                _bar.Fill = phasePct <= 0.34f ? UiSurface.Role.Danger
                    : phasePct <= 0.67f ? UiSurface.Role.Warning
                    : UiSurface.Role.Success;
                _bar.Tier = Mathf.Max(1, phase);
                EmitSignal(SignalName.PhaseChanged, phase);
            }
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (_health != null && GodotObject.IsInstanceValid(_health))
                _health.HealthChanged -= OnHealthChanged;
            if (_generatedRoot != null && GodotObject.IsInstanceValid(_generatedRoot))
                _generatedRoot.QueueFree();
        }

        private void SetEditedOwner(Node node)
        {
            if (!Engine.IsEditorHint())
                return;

            node.Owner = GetTree()?.EditedSceneRoot;
        }
    }
}

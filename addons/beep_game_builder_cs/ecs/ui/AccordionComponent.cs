using Godot;
using Beep.ECS.UI.Kit;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Accordion / collapsible section. Attach to a VBoxContainer with a Button header + content.
    /// Blind — works for settings panels, FAQ sections, collapsible menus.
    /// First child = header (Button), rest = content (collapses).
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class AccordionComponent : UIComponent
    {
        [Export] public bool StartExpanded { get; set; } = false;
        [Export] public float AnimationDuration { get; set; } = 0.3f;
        [Export] public string ExpandedIcon { get; set; } = "▼";
        [Export] public string CollapsedIcon { get; set; } = "▶";
        [Export] public NodePath HeaderPath { get; set; } = new("");
        [Export] public NodePath ContentRootPath { get; set; } = new("");
        [Export] public bool BuildInEditor { get; set; } = true;
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = false;

        [Signal] public delegate void ExpandedEventHandler();
        [Signal] public delegate void CollapsedEventHandler();

        private Container? _container;
        private Button? _header;
        private bool _isExpanded;
        private bool _createdHeader;
        private string _headerText = "";
        private readonly System.Collections.Generic.List<Godot.Control> _contentControls = new();
        private readonly System.Collections.Generic.List<Tween> _activeTweens = new();

        public override void _Ready()
        {
            base._Ready();
            if (!Engine.IsEditorHint() || BuildInEditor)
                CallDeferred(nameof(Setup));
            UpdateConfigurationWarnings();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (!GenerateControlsWhenPathsEmpty && FindHeader() == null)
                return new[] { "Add an authored Button/KitPushButton named AccordionHeader, set HeaderPath, make the first sibling a Button, or enable GenerateControlsWhenPathsEmpty." };
            return System.Array.Empty<string>();
        }

        private void Setup()
        {
            _container = GetParent() as Container;
            if (_container == null)
            {
                if (GenerateControlsWhenPathsEmpty)
                    GD.PushWarning($"[{Name}] AccordionComponent needs a Container parent to lay out sections; got '{GetParent()?.GetType().Name ?? "null"}'. Parent it to a VBoxContainer.");
                return;
            }

            if (!BindExistingHeader())
            {
                if (!GenerateControlsWhenPathsEmpty)
                    return;

                BuildGeneratedHeader();
            }

            if (_header == null)
                return;

            StyleHeader();
            _header.Pressed += Toggle;
            ResolveContentControls();
            if (!StartExpanded) SetExpanded(false, true);
            else
            {
                _isExpanded = true;
                UpdateHeaderText();
            }
        }

        public void Toggle()
        {
            if (!IsActive) return;
            SetExpanded(!_isExpanded);
        }

        public void SetExpanded(bool expand, bool instant = false)
        {
            if (_container == null) return;
            _isExpanded = expand;
            UpdateHeaderText();

            foreach (var t in _activeTweens)
                t?.Kill();
            _activeTweens.Clear();

            foreach (var ctrl in _contentControls)
            {
                if (instant)
                {
                    ctrl.Visible = expand;
                    ctrl.Modulate = new Color(1, 1, 1, expand ? 1 : 0);
                }
                else
                {
                    ctrl.Visible = true;
                    // Animate the offset_transform layer, not raw scale — the content lives inside a
                    // VBox/Container that re-sorts every layout pass and would overwrite a scale tween
                    // (the CLAUDE.md container-transform rule). Neutral is Vector2.One, collapsed is (1,0).
                    ctrl.OffsetTransformEnabled = true;
                    var tween = ctrl.CreateTween().SetParallel(true);
                    _activeTweens.Add(tween);

                    if (expand)
                    {
                        ctrl.Modulate = new Color(1, 1, 1, 0);
                        ctrl.OffsetTransformScale = new Vector2(1, 0);
                        tween.TweenProperty(ctrl, "modulate:a", 1f, AnimationDuration);
                        tween.TweenProperty(ctrl, "offset_transform_scale", Vector2.One, AnimationDuration)
                            .SetEase(Tween.EaseType.Out);
                    }
                    else
                    {
                        tween.TweenProperty(ctrl, "modulate:a", 0f, AnimationDuration * 0.5f);
                        tween.TweenProperty(ctrl, "offset_transform_scale", new Vector2(1, 0), AnimationDuration)
                            .SetEase(Tween.EaseType.In);
                        tween.Finished += () => OnCollapseFinished(ctrl);
                    }
                }
            }

            EmitSignal(expand ? SignalName.Expanded : SignalName.Collapsed);
        }

        private void OnCollapseFinished(Godot.Control ctrl) => ctrl.Visible = false;

        private bool BindExistingHeader()
        {
            _createdHeader = false;
            Button? header = FindHeader();

            if (header == null)
                return false;

            _header = header;
            _headerText = HeaderBaseText(_header);
            return true;
        }

        private Button? FindHeader()
        {
            if (!HeaderPath.IsEmpty && GetNodeOrNull<Button>(HeaderPath) is { } pathHeader)
                return pathHeader;

            if (FindChild("AccordionHeader", recursive: true, owned: false) is Button childHeader)
                return childHeader;

            if (GetParent()?.FindChild("AccordionHeader", recursive: true, owned: false) is Button parentHeader)
                return parentHeader;

            return FindFirstHeaderCandidate();
        }

        private Button? FindFirstHeaderCandidate()
        {
            if (GetParent() is not Container container)
                return null;

            foreach (Node child in container.GetChildren())
            {
                if (child == this) continue;
                if (child is Button button) return button;
                break;
            }

            return null;
        }

        private void BuildGeneratedHeader()
        {
            if (_container == null) return;
            _createdHeader = true;
            _headerText = _container.Name.ToString();
            _header = new KitPushButton
            {
                Name = "AccordionHeader",
                Text = _headerText,
            };
            StyleHeader();
            _container.AddChild(_header);
            _container.MoveChild(_header, 0);
            SetEditedOwner(_header);
        }

        public bool UsesSceneControls()
            => FindHeader() != null;

        private void StyleHeader()
        {
            if (_header == null) return;
            if (_header is KitPushButton kit)
            {
                kit.Accent = UiSurface.Role.Info;
            }
            _header.FocusMode = Godot.Control.FocusModeEnum.All;
            _header.SizeFlagsHorizontal = Godot.Control.SizeFlags.ExpandFill;
            _header.CustomMinimumSize = new Vector2(0, UiSurface.FontSize(this) * 2.4f);
        }

        private void ResolveContentControls()
        {
            _contentControls.Clear();
            if (!ContentRootPath.IsEmpty && GetNodeOrNull<Godot.Control>(ContentRootPath) is { } contentRoot)
            {
                _contentControls.Add(contentRoot);
                return;
            }

            if (_container == null) return;
            bool pastHeader = false;
            foreach (Node child in _container.GetChildren())
            {
                if (child == this) continue;
                if (child == _header) { pastHeader = true; continue; }
                if (!pastHeader) continue;
                if (child is Godot.Control ctrl) _contentControls.Add(ctrl);
            }
        }

        private void UpdateHeaderText()
        {
            if (_header == null) return;
            string icon = _isExpanded ? ExpandedIcon : CollapsedIcon;
            _header.Text = $"{icon} {_headerText}";
        }

        private static string HeaderBaseText(Button button)
            => string.IsNullOrWhiteSpace(button.Text)
                ? button.Name.ToString()
                : button.Text.TrimStart('▶', '▼', ' ').Trim();

        public override void _ExitTree()
        {
            base._ExitTree();
            foreach (var t in _activeTweens)
                t?.Kill();
            _activeTweens.Clear();

            if (_header != null && GodotObject.IsInstanceValid(_header))
                _header.Pressed -= Toggle;
            if (_createdHeader && _header != null && GodotObject.IsInstanceValid(_header))
                _header.QueueFree();
        }

        private void SetEditedOwner(Node node)
        {
            if (!Engine.IsEditorHint())
                return;

            node.Owner = GetTree()?.EditedSceneRoot;
        }
    }
}

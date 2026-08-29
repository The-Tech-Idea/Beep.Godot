using Godot;
using Beep.ECS.UI.Kit;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Search bar component. Attach to a Container to create a search input with icon and clear.
    /// Blind — works for any list filtering, table search, item lookup.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class SearchBarComponent : UIComponent
    {
        [Export] public string Placeholder { get; set; } = "Search...";
        [Export] public float SearchDelay { get; set; } = 0.3f;
        [Export] public NodePath InputPath { get; set; } = new("");
        [Export] public NodePath ClearButtonPath { get; set; } = new("");
        [Export] public NodePath IconButtonPath { get; set; } = new("");
        [Export] public bool BuildInEditor { get; set; } = true;
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = false;

        [Signal] public delegate void SearchChangedEventHandler(string query);
        [Signal] public delegate void SearchSubmittedEventHandler(string query);

        private Container? _container;
        private LineEdit? _input;
        private Button? _clearBtn;
        private Button? _iconBtn;
        private Control? _generatedRoot;
        private float _debounceTimer;
        private bool _debouncePending;

        public override void _Ready()
        {
            base._Ready();
            SetProcess(false);
            if (!Engine.IsEditorHint() || BuildInEditor)
                CallDeferred(nameof(RebuildSearch));
            UpdateConfigurationWarnings();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (!GenerateControlsWhenPathsEmpty && (FindInput() == null || FindClearButton() == null))
                return new[] { "Add authored LineEdit/Input and ClearButton children, set paths, or enable GenerateControlsWhenPathsEmpty." };
            return System.Array.Empty<string>();
        }

        public void RebuildSearch()
        {
            DisconnectControls();

            if (!BindExistingControls())
            {
                if (!GenerateControlsWhenPathsEmpty)
                    return;

                BuildGeneratedSearch();
            }

            ConfigureControls();
        }

        private void BuildGeneratedSearch()
        {
            if (_generatedRoot != null && GodotObject.IsInstanceValid(_generatedRoot))
                _generatedRoot.QueueFree();

            _generatedRoot = null;
            _container = GetParent() as Container;
            if (_container == null)
            {
                GD.PushWarning($"[{Name}] SearchBarComponent needs a Container parent to generate the search field; got '{GetParent()?.GetType().Name ?? "null"}'. Parent it to a VBox/HBoxContainer or bind authored controls by path.");
                return;
            }

            int fs = UiSurface.FontSize(this);
            float h = Mathf.Max(32f, fs * 2.25f);
            var hbox = new HBoxContainer { Name = "SearchBar" };
            KitChrome.SetConstantOverrideIfChanged(hbox, "separation", 0);
            _generatedRoot = hbox;

            _iconBtn = new KitIconButton
            {
                Name = "SearchIcon",
                Glyph = "Search",
                Disabled = true,
                MouseFilter = Godot.Control.MouseFilterEnum.Ignore
            };
            _iconBtn.CustomMinimumSize = new Vector2(h, h);

            _input = new LineEdit { Name = "Input", PlaceholderText = Placeholder, SizeFlagsHorizontal = Godot.Control.SizeFlags.ExpandFill };

            _clearBtn = new KitIconButton { Name = "ClearButton", Glyph = "X", Flat = true, Visible = false, CustomMinimumSize = new Vector2(h, h) };

            hbox.AddChild(_iconBtn);
            hbox.AddChild(_input);
            hbox.AddChild(_clearBtn);
            _container.AddChild(hbox);
            SetEditedOwner(hbox);
            SetEditedOwner(_iconBtn);
            SetEditedOwner(_input);
            SetEditedOwner(_clearBtn);
        }

        private bool BindExistingControls()
        {
            LineEdit? input = FindInput();
            Button? clear = FindClearButton();
            Button? icon = FindIconButton();

            if (input == null || clear == null)
                return false;

            if (_generatedRoot != null && GodotObject.IsInstanceValid(_generatedRoot))
                _generatedRoot.QueueFree();

            _generatedRoot = null;
            _input = input;
            _clearBtn = clear;
            _iconBtn = icon;
            return true;
        }

        public bool UsesSceneControls()
            => FindInput() != null || FindClearButton() != null || FindIconButton() != null;

        private LineEdit? FindInput()
        {
            if (!InputPath.IsEmpty && GetNodeOrNull<LineEdit>(InputPath) is { } pathInput)
                return pathInput;

            if (FindChild("Input", recursive: true, owned: false) is LineEdit childInput)
                return childInput;

            return GetParent()?.FindChild("Input", recursive: true, owned: false) as LineEdit;
        }

        private Button? FindClearButton()
        {
            if (!ClearButtonPath.IsEmpty && GetNodeOrNull<Button>(ClearButtonPath) is { } pathClear)
                return pathClear;

            if (FindChild("ClearButton", recursive: true, owned: false) is Button childClear)
                return childClear;

            return GetParent()?.FindChild("ClearButton", recursive: true, owned: false) as Button;
        }

        private Button? FindIconButton()
        {
            if (!IconButtonPath.IsEmpty && GetNodeOrNull<Button>(IconButtonPath) is { } pathIcon)
                return pathIcon;

            if (FindChild("SearchIcon", recursive: true, owned: false) is Button childIcon)
                return childIcon;

            return GetParent()?.FindChild("SearchIcon", recursive: true, owned: false) as Button;
        }

        private void ConfigureControls()
        {
            if (_input == null || _clearBtn == null)
                return;

            int fs = UiSurface.FontSize(this);
            float h = Mathf.Max(32f, fs * 2.25f);

            if (_iconBtn != null)
            {
                _iconBtn.Disabled = true;
                _iconBtn.MouseFilter = Godot.Control.MouseFilterEnum.Ignore;
                _iconBtn.CustomMinimumSize = new Vector2(h, h);
                if (_iconBtn is KitIconButton icon)
                    icon.Glyph = "Search";
            }

            _input.PlaceholderText = Placeholder;
            _input.SizeFlagsHorizontal = Godot.Control.SizeFlags.ExpandFill;
            _input.CustomMinimumSize = new Vector2(0, h);
            KitChrome.SetFontSizeOverrideIfChanged(_input, "font_size", UiSurface.FontSize(this, UiSurface.TextRole.Caption));
            _input.TextChanged += OnTextChanged;
            _input.TextSubmitted += OnTextSubmitted;

            _clearBtn.Visible = !string.IsNullOrEmpty(_input.Text);
            _clearBtn.CustomMinimumSize = new Vector2(h, h);
            KitChrome.SetFontSizeOverrideIfChanged(_clearBtn, "font_size", UiSurface.FontSize(this, UiSurface.TextRole.Caption));
            if (_clearBtn is KitIconButton clear)
            {
                clear.Glyph = "X";
                clear.Flat = true;
            }
            _clearBtn.Pressed += OnClearPressed;

            // Style
            Color surface = UiSurface.Of(this);
            var sb = new StyleBoxFlat
            {
                BgColor = surface.Darkened(0.12f),
                BorderColor = UiSurface.Semantic(this, UiSurface.Role.Accent) with { A = 0.62f }
            };
            sb.SetCornerRadiusAll(Mathf.RoundToInt(h * 0.5f));
            sb.SetBorderWidthAll(Mathf.Max(1, Mathf.RoundToInt(fs * 0.08f)));
            sb.ContentMarginLeft = fs * 0.6f;
            sb.ContentMarginRight = fs * 0.6f;
            KitChrome.SetStyleboxOverrideIfChanged(_input, "normal", sb);
            KitChrome.SetStyleboxOverrideIfChanged(_input, "focus", sb);
        }

        private void OnTextChanged(string text)
        {
            _clearBtn!.Visible = !string.IsNullOrEmpty(text);
            _debounceTimer = 0;
            _debouncePending = true;  // arm a single emit once typing settles
            SetProcess(true);
        }

        private void OnTextSubmitted(string query) => EmitSignal(SignalName.SearchSubmitted, query);

        private void OnClearPressed()
        {
            if (_input != null) _input.Text = "";
            if (_clearBtn != null) _clearBtn.Visible = false;
            EmitSignal(SignalName.SearchChanged, "");
        }

        public override void _Process(double delta)
        {
            // Emit ONCE after the text settles, then wait for the next change. The old version
            // kept firing SearchChanged every SearchDelay for as long as the field was non-empty —
            // a repeater, not a debouncer.
            if (_input == null || !IsActive || !_debouncePending) return;
            _debounceTimer += (float)delta;
            if (_debounceTimer >= SearchDelay)
            {
                _debounceTimer = 0;
                _debouncePending = false;
                EmitSignal(SignalName.SearchChanged, _input.Text);
                SetProcess(false);
            }
        }

        public string Text => _input?.Text ?? "";
        public void Clear()
        {
            if (_input != null)
            {
                _input.Text = "";
                _clearBtn!.Visible = false;
            }
            _debouncePending = false;
            SetProcess(false);
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            DisconnectControls();
        }

        private void DisconnectControls()
        {
            if (_input != null)
            {
                _input.TextChanged -= OnTextChanged;
                _input.TextSubmitted -= OnTextSubmitted;
            }
            if (_clearBtn != null)
                _clearBtn.Pressed -= OnClearPressed;
        }

        private void SetEditedOwner(Node node)
        {
            if (!Engine.IsEditorHint())
                return;

            node.Owner = GetTree()?.EditedSceneRoot;
        }
    }
}

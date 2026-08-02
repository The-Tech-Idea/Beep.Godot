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

        [Signal] public delegate void SearchChangedEventHandler(string query);
        [Signal] public delegate void SearchSubmittedEventHandler(string query);

        private Container? _container;
        private LineEdit? _input;
        private Button? _clearBtn;
        private float _debounceTimer;
        private bool _debouncePending;

        public override void _Ready()
        {
            base._Ready();
            _container = GetParent() as Container;
            if (_container == null)
            {
                GD.PushWarning($"[{Name}] SearchBarComponent needs a Container parent to build the search field; got '{GetParent()?.GetType().Name ?? "null"}'. Parent it to a VBox/HBoxContainer.");
                return;
            }
            BuildSearch();
        }

        private void BuildSearch()
        {
            if (Engine.IsEditorHint()) return;
            int fs = UiSurface.FontSize(this);
            float h = Mathf.Max(32f, fs * 2.25f);
            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 0);

            var icon = new KitIconButton
            {
                Glyph = "Search",
                Disabled = true,
                MouseFilter = Godot.Control.MouseFilterEnum.Ignore
            };
            icon.CustomMinimumSize = new Vector2(h, h);

            _input = new LineEdit { PlaceholderText = Placeholder, SizeFlagsHorizontal = Godot.Control.SizeFlags.ExpandFill };
            _input.CustomMinimumSize = new Vector2(0, h);
            _input.AddThemeFontSizeOverride("font_size", UiSurface.FontSize(this, UiSurface.TextRole.Caption));
            _input.TextChanged += OnTextChanged;
            _input.TextSubmitted += OnTextSubmitted;

            _clearBtn = new KitIconButton { Glyph = "X", Flat = true, Visible = false, CustomMinimumSize = new Vector2(h, h) };
            _clearBtn.AddThemeFontSizeOverride("font_size", UiSurface.FontSize(this, UiSurface.TextRole.Caption));
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
            _input.AddThemeStyleboxOverride("normal", sb);
            _input.AddThemeStyleboxOverride("focus", sb);

            hbox.AddChild(icon);
            hbox.AddChild(_input);
            hbox.AddChild(_clearBtn);
            _container?.AddChild(hbox);
        }

        private void OnTextChanged(string text)
        {
            _clearBtn!.Visible = !string.IsNullOrEmpty(text);
            _debounceTimer = 0;
            _debouncePending = true;  // arm a single emit once typing settles
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
            }
        }

        public string Text => _input?.Text ?? "";
        public void Clear() { if (_input != null) { _input.Text = ""; _clearBtn!.Visible = false; } }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (_input != null)
            {
                _input.TextChanged -= OnTextChanged;
                _input.TextSubmitted -= OnTextSubmitted;
            }
            if (_clearBtn != null)
                _clearBtn.Pressed -= OnClearPressed;
        }
    }
}

using Godot;

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

        public override void _Ready()
        {
            base._Ready();
            _container = GetParent() as Container;
            if (_container == null) return;
            BuildSearch();
        }

        private void BuildSearch()
        {
            if (Engine.IsEditorHint()) return;
            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 0);

            var icon = new Label { Text = "🔍", VerticalAlignment = VerticalAlignment.Center };
            icon.AddThemeFontSizeOverride("font_size", 14);
            icon.CustomMinimumSize = new Vector2(32, 36);

            _input = new LineEdit { PlaceholderText = Placeholder, SizeFlagsHorizontal = Godot.Control.SizeFlags.ExpandFill };
            _input.CustomMinimumSize = new Vector2(0, 36);
            _input.TextChanged += OnTextChanged;
            _input.TextSubmitted += OnTextSubmitted;

            _clearBtn = new Button { Text = "×", Flat = true, Visible = false, CustomMinimumSize = new Vector2(28, 36) };
            _clearBtn.Pressed += OnClearPressed;

            // Style
            var sb = new StyleBoxFlat { BgColor = new Color(0.15f, 0.15f, 0.2f, 1f) };
            sb.SetCornerRadiusAll(18);
            sb.ContentMarginLeft = 8;
            sb.ContentMarginRight = 8;
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
            if (_input == null || !IsActive) return;
            if (!string.IsNullOrEmpty(_input.Text))
            {
                _debounceTimer += (float)delta;
                if (_debounceTimer >= SearchDelay)
                {
                    _debounceTimer = 0;
                    EmitSignal(SignalName.SearchChanged, _input.Text);
                }
            }
        }

        public string Text => _input?.Text ?? "";
        public void Clear() { if (_input != null) { _input.Text = ""; _clearBtn!.Visible = false; } }

        public override void _ExitTree()
        {
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

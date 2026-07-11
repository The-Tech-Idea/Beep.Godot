using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Typewriter text reveal. Attach to any RichTextLabel or Label.
    /// Blind — works for dialog boxes, tutorials, flavor text, credits.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TypewriterComponent : UIComponent
    {
        [Export] public float CharsPerSecond { get; set; } = 40f;
        [Export] public bool AutoPlay { get; set; } = false;
        [Export] public string FullText { get; set; } = "";
        [Export] public bool ShowCursor { get; set; } = true;
        [Export] public string CursorChar { get; set; } = "▌";

        [Signal] public delegate void CharacterRevealedEventHandler(int count, int total);
        [Signal] public delegate void TextCompleteEventHandler();
        [Signal] public delegate void SkippedEventHandler();

        private RichTextLabel? _label;
        private float _revealedCount;
        private bool _isPlaying;
        private bool _completed;
        private float _cursorTimer;

        public override void _Ready()
        {
            base._Ready();
            _label = GetParent() as RichTextLabel;
            if (AutoPlay && !string.IsNullOrEmpty(FullText)) Play(FullText);
        }

        public void Play(string text)
        {
            if (_label == null || !IsActive) return;
            FullText = text;
            _revealedCount = 0;
            _isPlaying = true;
            _completed = false;
            _label.Text = "";
        }

        public void Skip()
        {
            if (_label == null) return;
            _label.Text = FullText;
            _isPlaying = false;
            _completed = true;
            EmitSignal(SignalName.Skipped);
            EmitSignal(SignalName.TextComplete);
        }

        public override void _Process(double delta)
        {
            if (_label == null || !_isPlaying || !IsActive) return;

            _revealedCount += CharsPerSecond * (float)delta;
            int visible = Mathf.Min((int)_revealedCount, FullText.Length);
            string display = FullText[..visible];

            _cursorTimer += (float)delta * 2f;
            if (ShowCursor && !_completed)
                display += Mathf.Sin(_cursorTimer) > 0 ? CursorChar : " ";

            _label.Text = display;
            EmitSignal(SignalName.CharacterRevealed, visible, FullText.Length);

            if (visible >= FullText.Length)
            {
                _isPlaying = false;
                _completed = true;
                _label.Text = FullText;
                EmitSignal(SignalName.TextComplete);
            }
        }
    }
}

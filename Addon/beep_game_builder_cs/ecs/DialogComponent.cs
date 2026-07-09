using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Dialog component. Blind — attach to any NPC, signpost, terminal, or readable object.
    /// Holds dialog lines and emits signals when interacted with.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class DialogComponent : EntityComponent
    {
        [Export(PropertyHint.MultilineText)]
        public string[] Lines { get; set; } = new[] { "Hello!" };
        [Export] public float TextSpeed { get; set; } = 0.05f;
        [Export] public bool OneShot { get; set; } = true;
        [Export] public string SpeakerName { get; set; } = "";

        [Signal] public delegate void DialogStartedEventHandler(string speaker, string[] lines);
        [Signal] public delegate void LineDisplayedEventHandler(string line, int index);
        [Signal] public delegate void DialogFinishedEventHandler();
        [Signal] public delegate void InteractedEventHandler();

        private bool _hasPlayed;

        public void Interact()
        {
            if (!IsActive) return;
            if (OneShot && _hasPlayed) return;
            _hasPlayed = true;
            EmitSignal(SignalName.Interacted);
            EmitSignal(SignalName.DialogStarted, SpeakerName, Lines);
        }

        public void Reset() => _hasPlayed = false;
    }
}

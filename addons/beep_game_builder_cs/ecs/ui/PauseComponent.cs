using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Pause overlay controller. Attach as a child of the pause-menu Control
    /// (which should be a CanvasLayer or Control on its own layer). On the
    /// <c>pause</c> input action it toggles <c>GetTree().Paused</c> and
    /// shows/hides the parent. The parent's <c>ProcessMode</c> must be
    /// <c>WhenPaused</c> so it stays interactive while gameplay is frozen.
    ///
    /// Pair with a <see cref="MenuComponent"/> (action "resume"/"restart"/"menu")
    /// connected to a <c>NavigationComponent</c> for the full pause loop.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class PauseComponent : UIComponent
    {
        /// <summary>Input action that toggles pause (default "pause" = Escape).</summary>
        [Export] public string ToggleAction { get; set; } = "pause";

        /// <summary>If true, the component captures the toggle input itself. Set false to drive Pause()/Resume() manually.</summary>
        [Export] public bool CaptureInput { get; set; } = true;

        /// <summary>When pausing, also pause the audio? (Default false — menus usually keep their own sounds.)</summary>
        [Export] public bool PauseAudio { get; set; } = false;

        [Signal] public delegate void PausedEventHandler();
        [Signal] public delegate void ResumedEventHandler();

        private Node? _overlay;

        public override void _Ready()
        {
            base._Ready();
            // Accept any Node parent — both Control and CanvasLayer roots work.
            _overlay = GetParent();
            // The overlay must run while the tree is paused.
            if (_overlay != null)
            {
                _overlay.ProcessMode = Node.ProcessModeEnum.WhenPaused;
                SetOverlayVisible(false);
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!CaptureInput || !IsActive || _overlay == null) return;
            if (@event.IsActionPressed(ToggleAction))
            {
                Toggle();
                GetViewport().SetInputAsHandled();
            }
        }

        public void Toggle()
        {
            if (GetTree().Paused) Resume();
            else Pause();
        }

        public void Pause()
        {
            if (!IsActive || _overlay == null || GetTree().Paused) return;
            GetTree().Paused = true;
            SetOverlayVisible(true);
            if (PauseAudio) SetAudioPaused(true);
            EmitSignal(SignalName.Paused);
        }

        public void Resume()
        {
            if (!IsActive || _overlay == null || !GetTree().Paused) return;
            SetOverlayVisible(false);
            GetTree().Paused = false;
            if (PauseAudio) SetAudioPaused(false);
            EmitSignal(SignalName.Resumed);
        }

        /// <summary>Toggle visibility on the overlay — works for both CanvasItem
        /// (Control/Node2D) and CanvasLayer roots.</summary>
        private void SetOverlayVisible(bool visible)
        {
            if (_overlay == null) return;
            if (_overlay is CanvasItem ci)
                ci.Visible = visible;
            else if (_overlay is CanvasLayer cl)
                cl.Visible = visible;
        }

        private void SetAudioPaused(bool paused)
        {
            int bus = AudioServer.GetBusIndex("Master");
            if (bus >= 0)
                AudioServer.SetBusMute(bus, paused);
        }
    }
}

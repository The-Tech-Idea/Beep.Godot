using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Optional self-contained pause controller for a CUSTOM pause overlay. The framework's default
    /// pause needs none of this: <see cref="GameFlowComponent"/> shows the main menu over the frozen
    /// game and toggles it. Use this only when you build your own pause overlay scene and want it to
    /// pause/toggle itself.
    ///
    /// Attach it as a child of your overlay's root (a CanvasLayer or Control on its own layer). On the
    /// <c>pause</c> input action it toggles <c>GetTree().Paused</c> and shows/hides the parent, and sets
    /// the parent's <c>ProcessMode</c> to <c>Always</c> so its buttons stay clickable while gameplay is
    /// frozen (WhenPaused did not reliably deliver GUI input, so the overlay rendered but froze).
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
            // Runtime only: this mutates the PARENT (ProcessMode + hides it). Without the
            // guard, opening the overlay scene in the editor flips the parent's ProcessMode
            // and hides it in the viewport.
            if (Engine.IsEditorHint()) return;

            // Accept any Node parent — both Control and CanvasLayer roots work.
            _overlay = GetParent();
            // The overlay must stay interactive while the tree is paused. Always (not WhenPaused): a
            // WhenPaused Control did not reliably receive mouse/GUI input here, so the pause buttons
            // froze with the game. Always keeps them clickable and matches the sub-overlays that work.
            if (_overlay != null)
            {
                _overlay.ProcessMode = Node.ProcessModeEnum.Always;
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

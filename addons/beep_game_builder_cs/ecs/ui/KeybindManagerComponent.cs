using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Node host for the static <c>BeepKeybindManager</c>. Place in a scene (or as
    /// an autoload) to route input into the keybind manager, which then fires the
    /// registered Action callbacks. Without a host, you must call
    /// BeepKeybindManager.ProcessInput(e) from your own _Input.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KeybindManagerComponent : UIComponent
    {
        [Export] public bool CaptureInput { get; set; } = true;

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!IsActive || !CaptureInput || Engine.IsEditorHint()) return;
            Beep.GameBuilder.BeepKeybindManager.ProcessInput(@event);
        }
    }
}

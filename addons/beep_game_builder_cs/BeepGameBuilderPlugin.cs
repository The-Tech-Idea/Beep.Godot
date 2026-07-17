using Godot;

namespace Beep.GameBuilder;

[Tool]
public partial class BeepGameBuilderPlugin : EditorPlugin
{
    private Godot.Control? _dock;

    public override void _EnterTree()
    {
        _dock = new BeepGameBuilderDock { EditorPlugin = this };
        AddControlToDock(DockSlot.RightUl, _dock);

        // Expose Beep's own tools to an AI agent. This only registers handlers in a
        // static registry — it is a no-op unless the separate `godot_mcp` addon is
        // also enabled, so this addon never depends on the bridge being present.
        BeepMcpCommands.Register();

        GD.Print("[Beep Game Builder] Plugin enabled.");
    }

    public override void _ExitTree()
    {
        BeepMcpCommands.Unregister();

        if (_dock is not null)
        {
            RemoveControlFromDocks(_dock);
            _dock.QueueFree();
            _dock = null;
        }

        GD.Print("[Beep Game Builder] Plugin disabled.");
    }
}

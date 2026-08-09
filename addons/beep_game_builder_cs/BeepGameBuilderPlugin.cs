using Godot;

namespace Beep.GameBuilder;

[Tool]
public partial class BeepGameBuilderPlugin : EditorPlugin
{
    private EditorDock? _dock;

    public override void _EnterTree()
    {
        var dockContent = new BeepGameBuilderDock { EditorPlugin = this };
        _dock = new EditorDock
        {
            Name = "BeepGameBuilderDock",
            Title = "Beep Game Builder",
            DefaultSlot = EditorDock.DockSlot.RightUl,
            AvailableLayouts = EditorDock.DockLayout.Vertical | EditorDock.DockLayout.Floating
        };
        _dock.AddChild(dockContent);
        AddDock(_dock);

        // Expose Beep's own tools to an AI agent. This only registers handlers in a
        // static registry — it is a no-op unless the separate `godot_mcp` addon is
        // also enabled, so this addon never depends on the bridge being present.
        BeepMcpCommands.Register();
        // The Game UI Kit's own surface. Registered separately so the kit can be added to or
        // removed from the bridge without touching the main command layer.
        BeepMcpKitCommands.Register();

        GD.Print("[Beep Game Builder] Plugin enabled.");
    }

    public override void _ExitTree()
    {
        BeepMcpCommands.Unregister();
        BeepMcpKitCommands.Unregister();

        if (_dock is not null)
        {
            RemoveDock(_dock);
            _dock.QueueFree();
            _dock = null;
        }

        GD.Print("[Beep Game Builder] Plugin disabled.");
    }
}

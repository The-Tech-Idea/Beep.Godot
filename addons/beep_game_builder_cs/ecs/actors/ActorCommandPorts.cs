using Godot;

namespace Beep.ECS;

/// <summary>Optional authored command executor, including scripts not written in C#.</summary>
public interface IActorCommandHandler
{
    bool CanExecuteActorCommand(Godot.Collections.Dictionary command);
    bool StartActorCommand(Godot.Collections.Dictionary command);
    bool IsActorCommandComplete();
    void CancelActorCommand();
}

internal static class ActorCommandPorts
{
    public static bool Supports(Node node) => node is IActorCommandHandler ||
        node.HasMethod("CanExecuteActorCommand") && node.HasMethod("StartActorCommand") &&
        node.HasMethod("IsActorCommandComplete") && node.HasMethod("CancelActorCommand");
    public static bool CanExecute(Node node, Godot.Collections.Dictionary command)
        => node is IActorCommandHandler handler ? handler.CanExecuteActorCommand(command) : node.Call("CanExecuteActorCommand", command).AsBool();
    public static bool Start(Node node, Godot.Collections.Dictionary command)
        => node is IActorCommandHandler handler ? handler.StartActorCommand(command) : node.Call("StartActorCommand", command).AsBool();
    public static bool Complete(Node node)
        => node is IActorCommandHandler handler ? handler.IsActorCommandComplete() : node.Call("IsActorCommandComplete").AsBool();
    public static void Cancel(Node node)
    {
        if (node is IActorCommandHandler handler) handler.CancelActorCommand(); else node.Call("CancelActorCommand");
    }
}

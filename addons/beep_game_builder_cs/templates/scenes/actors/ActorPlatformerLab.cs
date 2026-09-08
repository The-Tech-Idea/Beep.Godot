using Godot;

namespace Beep.GameBuilder;

/// <summary>Standalone example bootstrapping; actor behavior lives in reusable components.</summary>
public partial class ActorPlatformerLab : Node2D
{
    public override void _EnterTree() => BeepInputMapGenerator.SetupDefaultInput();
}

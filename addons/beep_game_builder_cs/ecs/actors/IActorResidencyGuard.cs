namespace Beep.ECS;

/// <summary>An authored actor component can veto ambient suspension while its activity needs simulation.</summary>
public interface IActorResidencyGuard
{
    bool CanSuspendActor();
}

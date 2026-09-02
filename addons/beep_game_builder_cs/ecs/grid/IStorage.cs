namespace Beep.ECS
{
    /// <summary>
    /// A stationary hold - a tank, a silo, a warehouse, a pipeline buffer:
    /// both ports and nothing else. It cannot be asked to move, which is
    /// exactly why it is not an ITransporter. GridStorageComponent is the
    /// shipped implementation.
    /// </summary>
    public interface IStorage : ILoadPort, IUnloadPort
    {
    }
}

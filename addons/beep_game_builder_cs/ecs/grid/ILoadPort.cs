namespace Beep.ECS
{
    /// <summary>
    /// The RECEIVING port: anything material can be pushed INTO. One of the
    /// two atomic connectors the whole logistics layer is built from -
    /// IExtractor, IStorage and ITransporter all implement ILoadPort and
    /// IUnloadPort, which is what lets a developer connect anything to
    /// anything: extractor to pipeline, pipeline to tank, tank to truck.
    ///
    /// Managers read this shape by NAME (duck typing) as well, so a GDScript
    /// node with the same members participates without implementing anything
    /// - GDScript cannot implement a C# interface. The interface is the
    /// contract written down; duck typing is how it is read.
    /// </summary>
    public interface ILoadPort
    {
        /// <summary>Total units this port's hold takes, across everything in it.</summary>
        int Capacity { get; }

        /// <summary>
        /// Units currently held. Free space is Capacity - CurrentLoad, which
        /// is what a planner (a pump deciding whether to push, a manager
        /// choosing a receiver) actually asks.
        /// </summary>
        int CurrentLoad { get; }

        /// <summary>Whether this port takes the given resource at all.</summary>
        bool CanAccept(string resourceId);

        /// <summary>
        /// Pushes material in and returns how much was actually accepted -
        /// capacity may cut it short, and the remainder stays the giver's.
        /// </summary>
        int Load(string resourceId, int amount);
    }
}

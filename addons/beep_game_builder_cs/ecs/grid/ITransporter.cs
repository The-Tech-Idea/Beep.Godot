using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Both ports plus MOBILITY: a truck, a boat, a train, a drone. Because a
    /// transporter is also a load and an unload port, it hands cargo to any
    /// other port - truck to tank, boat to wharf, segment to segment.
    /// GridHaulerComponent is the shipped implementation, and
    /// GridTransportManagerComponent is the registry that dispatches hauls to
    /// whichever registered transporter accepts.
    /// </summary>
    public interface ITransporter : ILoadPort, IUnloadPort
    {
        /// <summary>Whether the transporter is mid-haul and must not be asked.</summary>
        bool IsBusy { get; }

        /// <summary>
        /// Effective throughput, in units per second - a pipeline flows
        /// differently than a truck, a truck than a mule. The transport
        /// manager offers a haul to the FASTEST accepting transporter first,
        /// so authoring this is how a fleet gets a pecking order.
        /// </summary>
        float TransportRate { get; }

        /// <summary>
        /// Asks the transporter to move a load from a cell to wherever it
        /// delivers. True means it took the whole job; false leaves the
        /// manager to try the next one.
        /// </summary>
        bool RequestHaul(Vector2I fromCell, string resourceId, int amount);
    }
}

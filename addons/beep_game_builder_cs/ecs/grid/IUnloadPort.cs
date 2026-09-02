namespace Beep.ECS
{
    /// <summary>
    /// The GIVING port: anything material can be drawn OUT of. The second of
    /// the two atomic connectors - see ILoadPort. A hand-off is one Unload
    /// into one Load, and GridTransportManagerComponent.Transfer performs it
    /// safely (remainder back to the giver, cargo never duplicated or lost).
    /// </summary>
    public interface IUnloadPort
    {
        /// <summary>Units of the given resource currently held.</summary>
        int Stored(string resourceId);

        /// <summary>Releases material and returns how much actually came out.</summary>
        int Unload(string resourceId, int amount);
    }
}

namespace Beep.ECS
{
    /// <summary>
    /// Something that produces material out of the world - and, because it is
    /// both ports too, something the rest of the chain connects to directly:
    /// its output buffer is an unload port a pipeline or hauler draws from
    /// (and a load port, for the rare thing injected back down a well).
    /// GridExtractorComponent is the shipped implementation;
    /// GridExtractionManagerComponent is the registry of everything
    /// currently extracting.
    /// </summary>
    public interface IExtractor : ILoadPort, IUnloadPort
    {
        /// <summary>The resource currently being worked, or empty.</summary>
        string ActiveResourceId { get; }

        /// <summary>Whether the extractor is currently drawing a deposit down.</summary>
        bool IsExtracting { get; }
    }
}

namespace Beep.ECS
{
    /// <summary>
    /// Something that claims and works ONE job at a time from
    /// GridJobQueueComponent - a settler, a crane, a robot, a drone; any
    /// agent, not just "a person with a shovel". GridWorkerComponent is the
    /// shipped implementation. Deliberately minimal, matching ITransporter/
    /// IExtractor's own economy of surface: a system that needs to know
    /// "who is executing this job, and are they actually working it right
    /// now" - the construction-effect family in this same directory, in
    /// particular - can be written against this contract instead of the
    /// concrete GridWorkerComponent type.
    ///
    /// A GDScript worker cannot implement a C# interface (the same
    /// limitation the port contracts document). A system that must also
    /// recognize a duck-typed GDScript worker reads this shape by name
    /// instead - see GridWorkerPorts.
    /// </summary>
    public interface IWorker
    {
        /// <summary>The id this worker claims/completes jobs under.</summary>
        string WorkerId { get; }

        /// <summary>
        /// Whether the worker is actively executing a job's work timer right
        /// now - not idle, and not still travelling to the job.
        /// </summary>
        bool IsWorking { get; }

        /// <summary>The job currently held, or empty.</summary>
        string CurrentJobId { get; }
    }
}

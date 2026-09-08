# IWorker

C# interface for something that claims and works ONE job at a time from `GridJobQueueComponent` — a settler, a crane, a robot, a drone; any agent, not just "a person with a shovel." `GridWorkerComponent` is the shipped implementation. Deliberately minimal, matching `ITransporter`/`IExtractor`'s own economy of surface: a system that needs to know "who is executing this job, and are they actually working it right now" — the construction-in-progress effect family (`GridWorkerBuildActivityEffectComponent` and siblings) in particular — can be written against this contract instead of the concrete `GridWorkerComponent` type.

Unlike the port contracts, `IWorker` is not itself a dispatch mechanism — there is no worker registry, and nothing pushes work at an `IWorker`. A worker claims jobs by calling `GridJobQueueComponent.ClaimJob`/`ClaimNextJob` on itself (a pull model), the same as it always did; `IWorker` only formalizes the shape other systems can read once a worker exists.

## Public API
- `string WorkerId { get; }` — the id this worker claims/completes jobs under.
- `bool IsWorking { get; }` — whether the worker is actively executing a job's work timer right now, not idle and not still travelling to it.
- `string CurrentJobId { get; }` — the job currently held, or empty.

## Dependencies
No dependencies of its own. `GridWorkerComponent : GameplayComponent, IWorker` is the shipped implementation — `WorkerId`/`CurrentJobId` already existed as public properties and satisfy the interface as-is; `IsWorking` is a new one-line computed property (`State == WorkerState.Working`).

## Notes
- GDScript cannot implement a C# interface, so a system that must also recognize a duck-typed GDScript worker reads this shape by name instead — see `GridWorkerPorts`.
- A custom worker implementation is expected to also call `GridJobQueueComponent.ReportProgress` while working, the same way calling `ClaimJob`/`CompleteJob` is already part of participating in the job queue at all — `IWorker` itself does not declare this (a C# interface cannot require a specific caller relationship to a *different* object), but the construction-effect family depends on it for progress to be visible.

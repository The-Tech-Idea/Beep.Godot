# DuckTypedNodeRegistry

The shared machinery behind a registry of duck-typed nodes: one node announces itself to ONE manager instead of every consumer crawling the scene tree for it, and the manager never decides what the node's job *means* — it only asks whether the node answers the shape by name, so a GDScript registrant participates exactly like a shipped C# one.

Abstract, and deliberately **not** `[GlobalClass]`: it is never instantiated on its own, so the editor's create-node dialog still offers only the concrete managers. Two classes derive from it today — `GridTransportManagerComponent` and `GridExtractionManagerComponent`.

## Why it exists

The four registry mechanics were byte-identical in both managers (the list, the duplicate guard, the count-with-prune, and the reverse-loop prune). That is one capability living in two files, and the next change to it — a signal-timing fix, a valid-node check, a remainder rule — would have had to land in both and would drift the moment it landed in only one. Only two things genuinely differed, and only one of them was essential: the contract test (method-based for transporters, property-based for extractors), and the incidental `void`-vs-`bool` return of `Register`. The contract test stays per-manager as an override; the return type unified to the more useful `bool`.

## Public API
- `bool Register(Node node)` — `false` for null or a freed instance; `true` for a duplicate, which is a no-op that still reports success (the caller asked for the node to be in the registry and it is); otherwise runs `AnswersContract`, and on failure pushes a `GD.PushWarning` naming the manager, the node and the missing contract, returning `false`. A genuine add calls `OnRegistered` and returns `true`.
- `void Unregister(Node node)` — removes and calls `OnUnregistered`, but only if the node is still a valid instance; a node that was already freed is dropped silently by the next read instead, which is not an unregistration anybody is owed a signal for.
- `int Count { get; }` — `Prune()` first, then the count. Each manager keeps a domain-named forwarder (`TransporterCount`, `ExtractorCount`) because HUDs and the probes read those names.
- `protected IReadOnlyList<Node> Registered { get; }` — the pruned view that every subclass domain method iterates (`RequestHaul`, `Extractors()`, `ActiveCountFor`, `EstimatedRatePerTurn`). Not a copy: a caller that mutates the registry while iterating is still an error, exactly as before.
- `protected void Prune()` — the one reverse loop. Lazy, run on every read rather than on a timer or from the tracked node's own `_ExitTree`, so a manager nobody queries carries inert stale entries instead of paying for bookkeeping nobody asked for.

## Subclass contract
- `protected abstract bool AnswersContract(Node node)` — the by-name shape question. **This is the whole of what the two managers do not share**, so it is the override, not a copy: `GridTransportManagerComponent` asks by method (`HasMethod` for `RequestHaul`/`CanAccept`/`Load`/`Unload`), `GridExtractionManagerComponent` asks by property (`Get("IsExtracting")`/`Get("ActiveResourceId")`, where a missing property reads back as a `Nil` Variant — the same question asked one level down).
- `protected abstract string ContractSummary { get; }` — the contract's own words, fed into the single refusal warning so each manager still says what it actually asked for ("transporter contract (CanAccept, Load, Unload, RequestHaul)" vs "extractor contract (IsExtracting, ActiveResourceId)").
- `protected virtual void OnRegistered(Node node)` / `OnUnregistered(Node node)` — HOOKs: each manager emits its own typed signal (`TransporterRegistered`/`Unregistered`, `ExtractorRegistered`/`Unregistered`). The base does not know what the subclass's signals are called.

## Notes
- Null and freed registrants are refused rather than tracked: `Register` checks `GodotObject.IsInstanceValid`, so a stale entry can only arise from a node that was valid at registration and later vanished *without* unregistering.
- **A freed node inside the tree is not that case.** Its `_ExitTree` calls `Unregister` first, so the entry never goes stale and the prune is never what removed it — a test that frees a rig and asserts the count dropped is proving `Unregister` works, not `Prune`. The prune's real case is a registrant with no `_exit_tree` cleanup (or one registered before it was ever added to the tree); `tests/grid_terrain_subsurface_probe.gd` asserts that case explicitly, and it is the only red the shared `Prune` has.
- Behaviour is preserved exactly from the two managers it replaced, including the warning text — same sentence, same manager and node names — so nothing that greps for a refusal message changed. This is an extract-only refactor: no indexed/spatial lookup, no signal-timing change.
- Pinned by `tests/addon_contract_scan.ps1` (DUP-16): the base must own these members including the refusal warning, both managers must declare `: DuckTypedNodeRegistry` and answer their own contract, and neither may re-grow a `private void Prune(` or its own `List<Node> _transporters`/`_extractors`.

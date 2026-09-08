# GridDispatchTaskDefinition

`GridDispatchTaskDefinition` is a pure-data `Resource` describing one dispatchable task for `GridDispatchBoardComponent`: which button triggers it, where the vehicle goes, what becomes visible/hidden/recoloured on arrival, and what the wallet gains or spends. It carries no logic of its own — every field is read and interpreted entirely by `GridDispatchBoardComponent`.

The class doc comment names the motivation directly: "This is the shape every task in a settlers-style loop shares — clear the brush, hoe the plots, water them, plant, harvest, lay a road. They differ only in where the vehicle goes, what becomes visible or hidden, and what the wallet gains." It replaces a demo controller where "all eight [tasks] as a switch over button names with literal screen coordinates in the cases, so adding a ninth meant editing C# and nobody could see the set at once" — as a `Resource` array (`GridDispatchBoardComponent.Tasks`), the whole task set is visible and editable in the inspector without touching code.

## Public API
- `string Action` — matches the **name** of the button that requests this task.
- `string Label` — shown while the task runs, and again with "complete" appended after.
- `NodePath VehiclePath` / `Vector2 Target` — which vehicle moves, and where it tweens to (resolved relative to the owning `GridDispatchBoardComponent`, not to this resource).
- `Array<NodePath> Show` / `Array<NodePath> Hide` — nodes toggled visible/invisible on arrival.
- `NodePath RecolourTarget` / `Color Recolour` — optional single-node recolour on arrival; inert unless `RecolourTarget` is set.
- `string RewardResourceId` / `int RewardAmount` — wallet delta on arrival; a negative amount spends rather than earns (e.g. road stone), and an empty id pays nothing.

## Dependencies

Consumed exclusively by `GridDispatchBoardComponent` (this batch), which reads every field to drive its tween/apply/finish sequence. Nothing in this batch constructs or mutates instances at runtime beyond what the Godot inspector does through `[Export]`.

## Notes

- No validation of its own — e.g. a negative `RewardAmount` is a valid, intentional "this task costs a resource" per the class doc comment, not a value that needs guarding against.
- Being a `Resource` rather than a `Node`, it has no `[Tool]` attribute (unlike every `Node`-derived class in this batch); it's authored as a `.tres` and assigned into `GridDispatchBoardComponent.Tasks`.

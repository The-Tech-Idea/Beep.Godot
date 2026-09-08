# GridPanelComponent

Abstract `[Tool] [GlobalClass]` `Control` base for the grid HUD panels. It owns the structural bootstrap every one of them used to carry its own byte-for-byte copy of: the editor-owner stamp a generated node needs to survive a scene save, the node-name sanitiser, and the three-tier authored-control lookup.

The shared contract it encodes is the one every grid HUD panel follows: bind the scene's own authored controls when they exist, and only generate a default layout when the author has explicitly opted in. Left at its default, a panel with no authored controls draws nothing rather than inventing a layout over a scene someone is still building.

It takes the same shape `EntityComponent` already uses in this addon — an abstract partial Godot class carrying `[Export]`s that concrete subclasses inherit — so exported properties still appear in the inspector on the derived node and existing scene overrides keep resolving by name.

## Public API
- `[Export] bool BuildInEditor` (default `true`) — whether the panel also builds itself while the editor is open, not only at runtime.
- `[Export] bool GenerateControlsWhenPathsEmpty` (default `false`) — opt-in to the generated default layout.
- `protected void SetEditedOwner(Node node)` — stamps the edited scene root as owner, in the editor only. Without it a generated layout disappears the moment the scene is saved and reloaded.
- `protected T? FindControl<T>(NodePath path, params string[] names) where T : Node` — the authored control for a slot: the exported path first, then each candidate name as a child of this panel, then each candidate name under the panel's parent. Several names search by TIER, not name by name — every candidate is tried as a child before any is tried under the parent, which is the order the resource bar has always used for its authored row and then its generated one.
- `protected static string SafeName(string value, string fallback)` — a node name from an arbitrary id. Strips `Path.GetInvalidFileNameChars()` plus space, `/`, `\` and `:` explicitly, because that char set is platform-dependent (no backslash on Unix) and a row key can be a whole node path.

## Dependencies
`Godot.Control`, `Engine.IsEditorHint`, `SceneTree.EditedSceneRoot`, `Node.FindChild`. Subclassed by [GridListPanelComponent](GridListPanelComponent.md) and directly by `GridResourceBarComponent` and `GridBuildToolbarComponent`.

## Notes
- The parent tier of `FindControl` is what lets a panel sit BESIDE the controls it drives instead of having to own them — `tests/addon_contract_scan.ps1` pins that behaviour, formerly per panel file and now here.
- `FindControl` allocates a `params` array per call. It is called from bind/rebuild paths and configuration warnings, never per refresh tick, so this is not on a hot path.
- Abstract and `[GlobalClass]`, matching `EntityComponent`; it appears in the editor's node list like any global class, and like `EntityComponent` is not meant to be instantiated directly.

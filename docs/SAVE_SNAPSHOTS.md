# Save Snapshot Ownership

GameStateManager captures a fresh GameStateData on every Save, SaveAutosave or
SyncAllSaveables call. It publishes the new snapshot only after capture succeeds.
Reacquire GetCurrentState after capture; do not retain a reference as mutable live state.

## Component Records

Implement ISaveable and join the saveables group. Write the complete current record
in Save, including any GameData, Features or World entries the component owns.
Entries that no current participant writes disappear from the next snapshot.
Do not depend on the previous snapshot to supply missing fields.

Discovery uses CurrentScene, or the explicit SaveRootPath, plus GameApp for session
and progression. Nodes below a queued-for-deletion ancestor are excluded. Headless
hosts without CurrentScene default to the tree root; set SaveRootPath to narrow it.
Previews inside the selected subtree must opt out of save participation.

## Explicit Choices

Use SetGameData and GetGameData for choices such as a selected recipe or booster.
These values live in CustomData, serialized under custom_data, and survive snapshot
rebuilding. Component-written GameData is a separate namespace. Existing mixed-bag
saves are not migrated into CustomData. NewGame clears both namespaces.

## Failure And Limits

Capture failure returns false from Save/SaveAutosave and leaves the existing file
untouched. Nested captures and captures interrupted by session changes are rejected.
SyncAllSaveables has no return value; rejected captures emit a warning when caused
by an exception, such as an invalid explicit scope.

Stable entity reconstruction and duplicate-key validation are not implemented by
fresh snapshots. Authors must still use distinct component keys. Snapshot capture
does not undo arbitrary gameplay side effects inside a custom Save implementation;
Save implementations should only read live state and write into the supplied snapshot.

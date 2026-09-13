# Legacy terrain art

This repository-root folder is outside `addons/` and is not copied by the addon
installer. It is version-controlled. It deliberately has no `.gdignore`: legacy
Godot resources must still be able to import their textures after relocation.

No original asset has been moved merely by creating this folder. `manifest.json`
records verified copies and reference migrations, not proposed destinations.
Unresolved approval or dynamic usage means retain the original until reviewed.

Migration sequence: explicit batch review, hash snapshot, copy with relative
relationships preserved, update references and import paths, load affected scenes,
verify retained files, then request removal approval for the superseded locations.
Never keep old resource-path strings in a supposedly migrated working prefab.

Production resources may not depend on this folder. If a legacy image is reused,
make a documented production-owned copy and validate its calibration separately.

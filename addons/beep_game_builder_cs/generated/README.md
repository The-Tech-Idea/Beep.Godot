# Terrain asset library

The root's old folders are not all approved or production-ready. They remain in
place until reference-aware migration is reviewed; their presence is not a claim
that they should ship forever.

- `production/`: approved, validated cartoon and pixel packs only.
- `dev/`: pack specifications, staging candidates and ignored experiments.
- `test/`: maintained fixtures and ignored disposable `output/` folders.
- Repository-root `legacy_art/`: approved older art, outside the addon.
- Repository-root `.art_quarantine/`: ignored recoverable copies, not permanent deletion.

Production must never reference dev, test or legacy. Runtime copies of approved
source art belong inside the production pack and carry provenance. Green-backed
masters are retained; runtime sprites require validated alpha extraction.

From the repository root, using Node.js:

```text
node tools/terrain-library/cli.mjs init
node tools/terrain-library/cli.mjs audit
node tools/terrain-library/cli.mjs validate
node --test tests/terrain_library.test.mjs
```

The audit writes inventory, exact-duplicate review groups and a searchable visual
catalog into `test/library/output/`. Literal and possible dynamic references are
reported separately. These are review aids, never proof that an asset is unused.

No production pack exists until visual acceptance and the required Godot checks
are recorded. An empty successful dependency check is not a completed library.

See `plans/TERRAIN_LIBRARY_REBUILD.md` and `tools/terrain-library/specification.json`.

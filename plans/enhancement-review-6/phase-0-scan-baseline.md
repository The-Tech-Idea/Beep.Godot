# Phase 0: Scan Baseline

## Why

The previous documentation and plan set had drifted. This phase establishes a fresh baseline from the addon source itself, excluding old docs/plans from review input.

## Work

- Read every non-documentation text source/config/template file under the three addon roots.
- Separate C# and GDScript source from templates/catalog data, while still validating all text files.
- Record measured counts instead of legacy component claims.
- Attempt build verification and record the actual blocker.

## Gotchas

- `beep_game_builder_cs` has 862 scanned files, but 504 are JSON/template/config files. Treat those as data contracts, not C# components.
- `[GlobalClass]` count is a source count, not proof of editor registration until the C# build passes.
- `dotnet build .\Beep.Godot.csproj` currently fails before addon runtime checks because generated `obj` files are included.

## Verify

- Re-run addon inventory and confirm 909 scanned files.
- Re-run template/catalog reference checks and expect zero missing refs.
- Re-run build after Phase 1 and expect the duplicate assembly attribute errors to disappear.

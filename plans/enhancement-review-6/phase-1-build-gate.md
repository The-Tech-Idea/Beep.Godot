# Phase 1: Build Gate

## Why

`Beep.Godot.csproj:1` uses default Godot SDK compile inclusion. The build currently includes generated C# files under `tests/Beep.Godot.Tests/obj` and `.godot/mono/temp/obj`, producing duplicate assembly attributes.

Status: fixed.

## Work

- Added explicit default item excludes for generated directories:
  - `tests/**/obj/**`
  - `tests/**/bin/**`
  - `.godot/**`
- Kept addon source inclusion automatic so copied addon usage remains simple.
- Re-ran `dotnet build .\Beep.Godot.csproj`.

## Gotchas

- Do not disable all default compile items unless every addon source file is explicitly included.
- `.godot` is editor-generated and should not be compiled by the project.
- Test project files should be built through their own project when present, not swept into the root addon assembly.

## Verify

- `dotnet build .\Beep.Godot.csproj` completes with 0 errors.
- The follow-up warning cleanup pass now leaves the clean build at 0 warnings and 0 errors.

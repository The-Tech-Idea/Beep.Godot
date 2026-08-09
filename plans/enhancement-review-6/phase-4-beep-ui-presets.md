# Phase 4: beep_ui Preset Source Of Truth

## Why

Before this phase, `addons/beep_ui/theme/beep_theme.gd:130` was the actual preset registry while `addons/beep_ui/theme/theme_applier.gd:14` duplicated the same values in an exported enum hint.

Status: fixed.

## Work

- Replaced the hardcoded exported enum hint with a normal `String` property.
- Implemented `_get_property_list()` on `BeepThemeApplier` to populate the enum hint from `BeepPreset.preset_names()`.
- Kept the existing `set_preset()` validation and warning behavior.
- Kept preset files unchanged.

## Gotchas

- Godot editor property hints need a comma-separated enum hint string.
- `BeepPreset` must be loadable in tool mode before `_get_property_list()` runs.
- Existing scenes storing `preset = "Modern"` must continue to deserialize.

## Verify

- `tests/addon_contract_scan.ps1` verifies `theme_applier.gd` has no hardcoded exported enum hint, defines `_get_property_list()`, and reads `BeepPreset.preset_names()`.
- Manual editor verification is still recommended: open inspector for `BeepThemeApplier`; enum choices should match `BeepPreset.preset_names()`.
- Existing scenes with preset strings should still deserialize because the stored property remains named `preset`.

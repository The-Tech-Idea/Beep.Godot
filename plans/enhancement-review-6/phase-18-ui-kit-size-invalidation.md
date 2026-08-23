# Phase 18 - UI Kit Size Invalidation

## Status

Fixed.

## Finding

Several custom UI-kit controls had `_GetMinimumSize()` implementations or one-time `CustomMinimumSize` setup, but their exported layout properties only called `QueueRedraw()`. In Godot containers, changing those properties in the inspector or at runtime can leave the parent layout using stale minimum-size data until another unrelated size refresh happens.

Affected controls:

- `KitPager.ShowJump`
- `KitSlotGrid.Columns`
- `KitSlotGrid.Rows`
- `KitStarRating.Total`
- `KitTree.Columns`
- `KitTree.Tiers`
- `KitLevelPath.PerRow`
- `KitItemCard.Layout`

## Fix

- Added `UpdateMinimumSize()` to size-affecting exported setters.
- Added `_GetMinimumSize()` to `KitTree` and `KitLevelPath`, matching their existing theme/font-based startup sizing.
- Changed `KitItemCard.Layout` to force its custom minimum size from the new layout so row-to-tile transitions can shrink instead of keeping the previous larger row size.
- Renamed `KitStarRating`'s private helper to avoid shadowing Godot's inherited `UpdateMinimumSize()` method.

## Verification

- `tests/addon_contract_scan.ps1` now rejects missing `UpdateMinimumSize()` calls on size-affecting exported properties.
- `tests/addon_contract_scan.ps1` now rejects grid/path controls that fall back to one-time `CustomMinimumSize` without `_GetMinimumSize()`.

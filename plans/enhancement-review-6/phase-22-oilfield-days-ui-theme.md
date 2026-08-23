# Phase 22 - Oilfield Days UI Theme

## Status

Fixed.

## Mockups Reviewed

- `C:\Users\f_ald\source\repos\The-Tech-Idea\Beep.OilandGas.Sim\src\OGSim.Game\assets\Oilfield Days_mockups\gameplay1.png`
- `C:\Users\f_ald\source\repos\The-Tech-Idea\Beep.OilandGas.Sim\src\OGSim.Game\assets\Oilfield Days_mockups\setupmenu.png`
- `C:\Users\f_ald\source\repos\The-Tech-Idea\Beep.OilandGas.Sim\src\OGSim.Game\assets\Oilfield Days_mockups\gameplay_2_DispatchBoard.png`
- `C:\Users\f_ald\source\repos\The-Tech-Idea\Beep.OilandGas.Sim\referenceart\Mockup\Oilfield Days\VehicleEquipment Garage mockup.jpg`
- `C:\Users\f_ald\source\repos\The-Tech-Idea\Beep.OilandGas.Sim\referenceart\Mockup\Oilfield Days\Field Lease ConstructionPlacement mockup.jpg`

## Implemented

- Added `citybuilder/oilfield_days` as a selectable skin theme.
- Matched the gameplay HUD direction: nearly black blue-green panels, thin layered steel borders, amber selected tabs/headings, green focus/selection outlines, and blue/green meter fills.
- Tuned kit geometry for compact industrial controls: square/low-radius panels, condensed uppercase labels, tighter padding, hard shadows, structural frame behavior, stronger rim contrast, and corner studs.
- Added theme-authorable kit knobs for `frame_mode`, `studs`, `rim_brightness`, `height_ratio`, `pad_ratio`, `rim`, `bevel`, `gloss`, `sparkle`, `well_shade`, and `hairline_px`.
- Added contract coverage so the Oilfield Days theme remains listed and the new kit JSON fields remain supported.

## Remaining Follow-Up

- The mockup setup/detail screens use richer parchment panels, wood title plates, metal corner brackets, and visible bolts. The new theme captures their proportions and frame behavior procedurally, but a closer match requires either dedicated 9-patch art slots for those menu surfaces or a future renderer pass for corner bracket ornaments.

## Verification

- Run `powershell -ExecutionPolicy Bypass -File .\tests\run_addon_checks.ps1`.

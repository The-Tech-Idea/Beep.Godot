# Phase 3 — FX-shell honesty + docs

## The finding (POLISH)

`beep_ui/widgets/widget_factory.gd` has two tiers of "starter" builder, and only one is honest:
- `_scaffold` (`:346`) and `_system` (`:364`) render disclaimer text — *"Starter scaffold — themed
  shell, extend with your logic/sprites."* / *"System module (non-visual)… implement as an autoload."*
- `_overlay` (`:315`), `_caption` (`:222`), `_stat` (`:208`) render a static rect / plain Label / a
  static `"0"` behind **evocative, animated-sounding catalog names** — `glitch_effect`, `scanlines`,
  `chromatic_aberration`, `dissolve_effect`, `typewriter_label`, `marquee`, `animated_number` — and say
  **nothing** about being inert. A developer dragging in `typewriter_label` sees a static label and no
  hint that the animation is theirs to wire.

This is the repo's own "silence is indistinguishable from breakage" rule. It's borderline by-design
(the addon ships no shader art — the developer's canvas), so it's POLISH, not a bug — but note `beep_ui`
*does* ship `ui_effect.gd` with Glitch/Typewriter/Shimmer types these shells could name-drop.

## The fix

Give `_overlay`/`_caption`/`_stat` the same honesty as `_scaffold`: a short disclaimer line (or a
tooltip) naming what the developer must add — e.g. *"static shell — add a shader / `BeepUIEffect`
(Glitch/Typewriter) to animate."* No behavior change; just make the silent seam speak, per CLAUDE.md.

## Verify
- No C# touched → `dotnet build` still 0 errors, `validate_scenes.sh` still PASS.
- Editor: drop `typewriter_label` / `glitch_effect` from the widget panel → the placeholder now states
  it's a shell to extend.

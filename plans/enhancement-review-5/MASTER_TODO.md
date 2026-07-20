# Master TODO Tracker — Enhancement Review Round 5 (convergence)

> Fifth sweep. Verifies round 4 held and reads the last corners no round had deeply covered: the 74
> scene **templates** + their 34 driver scripts, the 22 `beep_ui` presets, and every `widget_factory`
> builder. Per-phase detail beside this file.
>
> **This round is where the review converges.** Three parallel passes turned up **one regression**
> (in a round-4 fix, now corrected), **one MEDIUM framework bug** (the i18n pipeline is inert), and
> **one POLISH honesty gap** (FX-named widget shells don't disclaim). Everything else — 25 of 26
> round-4 fixes, all 22 presets, the 114-entry widget catalog, and 5 of 6 scene-template defect
> classes — verified **clean**.

---

## How this review was produced

| Pass | Coverage | Result |
|---|---|---|
| Round-4 regression check | all 26 round-4 edits + GDScript scan | 25 HELD; **1 regression** (`RemoveAutoload` empty-sentinel) |
| Scene templates + driver scripts | 74 `.tscn` + 34 `ecs/scenes/*.cs` | 5/6 classes clean; **1 MEDIUM** (i18n inert) |
| beep_ui presets + widget builders | 22 `preset_*.gd` + all `widget_factory` builders | presets + catalog clean; **1 POLISH** (FX shells silent) |

The scene templates' export-name hygiene, component parenting, NodePath resolution, script refs,
silent-failure guards, and gameplay-verb wiring are all **clean** — the framework's structural surface
has now been verified from four angles.

---

## Progress

- [x] **Phase 0 — Review** — the 3-pass sweep above.
- [x] **[Phase 1 — round-4 regression fix](phase-1-regression.md)** — **done, build 0 errors.**
      `BeepProjectDefaults.RemoveAutoload` cleared the key with `Set(key, "")`, but `HasSetting` still
      reported the empty entry as present, so `EnsureAutoload` would later refuse to re-register an
      autoload a subsequent genre needs. Switched to `ProjectSettings.Clear(key)`.
- [x] **[Phase 2 — i18n pipeline made live](phase-2-i18n.md)** — **done, build 0 errors, validator PASS.**
      `translations.csv` rekeyed on the exact English chrome strings the shipped scenes render (en echoes
      the source, es/ja translate) — Godot auto-translation now fires with zero scene/code edits;
      `LocalizationComponent` doc rewritten to teach the source-string convention. Chrome only; dynamic
      strings left to the developer via `TrF`.
- [x] **[Phase 3 — FX-shell honesty + docs](phase-3-polish.md)** — **done (GDScript-only; C# gates
      unaffected & green).** `_overlay`/`_caption`/`_stat` now carry a `_SHELL_HINT` tooltip, and
      `_overlay` a visible bottom note, so an FX-named static shell no longer reads as "working but
      broken" — matching `_scaffold`/`_system`'s honesty.

## Verification gates (every phase)

1. `dotnet build` → 0 errors.
2. `cd addons/beep_game_builder_cs/templates/scenes && ./validate_scenes.sh` → PASS.
3. i18n is a runtime behavior — verified by reading + the editor language-switch pass (no `dotnet` gate).

## Decisions

| # | Decision | Phase |
|---|---|---|
| A | i18n fix: rekey CSV on English source strings (auto-translate, no scene edits) vs. convert 352 scene literals to symbolic keys. **Chose source-string rekey** — idiomatic Godot 4, non-invasive, and the shipped menu chrome translates immediately. | 2 |

## Cross-references

- **enhancement-review-1…4** — the ecs/, core/, dock, and GDScript work. Round 5 is the convergence pass:
  the templates + presets + widget builders those didn't deeply read, plus a regression re-check.
- After round 5, the open review surface is exhausted — further rounds would re-scan clean ground.

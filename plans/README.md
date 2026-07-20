# plans/

Each initiative owns a folder, a tracker, and one doc per phase/stage. **There is no single
repo-wide tracker** — a "MASTER_TODO.md" is always scoped to the initiative it sits in.

| Initiative | Tracker | State |
|---|---|---|
| **Textured-UI feature (bring-your-own-art)** — every genre theme is texture-ready (nine-patch slots + default margins + graceful procedural fallback), plus a `CursorComponent` and an opt-in import helper; the addon ships **no** art, the developer supplies their own | `ui-asset-integration/MASTER_TODO.md` + 5 phase docs + `import_kenney.py`/`stamp_textures.py` | **feature complete** — all 50 themes slotted, CursorComponent shipped, README documents BYO flow; build 0 errors, validator PASS. No art shipped. |
| **Enhancement review round 5** — convergence sweep; reads the last uncovered corners (74 scene templates + 34 driver scripts, 22 presets, every widget_factory builder) and re-verifies round 4. Turned up 1 regression (a round-4 fix), 1 MEDIUM (i18n pipeline inert), 1 POLISH (FX shells silent); everything else clean | `enhancement-review-5/MASTER_TODO.md` + 3 phase docs | **all 3 phases implemented** (build 0 errors, validator PASS); i18n now translates the shipped chrome, `RemoveAutoload` regression fixed. Review surface exhausted — further rounds re-scan clean ground |
| **Enhancement review round 4** — fourth sweep, deliberately targeting the ground rounds 1–3 never touched: `core/` generators + editor dock + utility/data classes, the MCP bridge, and the whole `beep_ui` GDScript addon; verifies round 3 held (all 8 did) and skin JSON is 100% clean, then fixes the ~15 real bugs + gaps that untouched code was hiding, in 5 phases | `enhancement-review-4/MASTER_TODO.md` + 5 phase docs | **all 5 phases implemented** (build 0 errors, validator PASS); ~26 findings fixed (incl. FormBuilder crash, untypeable debug console, world-entity positions lost on save, real-time genre inheriting the turn axis, Typewriter looping forever) |
| **Enhancement review round 3** — third full sweep; verifies round 2 held (all did, zero regressions) and picks up 3 real pre-existing bugs (incl. precipitation never rendering) + two consistency gaps round 2 applied unevenly, in 4 phases | `enhancement-review-3/MASTER_TODO.md` + 4 phase docs | **all 4 phases implemented** (build 0 errors, validator PASS); ~20 findings fixed |
| **Enhancement review round 2** — second full one-by-one sweep after round 1 landed; verifies the fixes held and picks up the long tail (1 crash, 2 teardown throws, 1 regression, a leftover-child-leak class) in 7 phases | `enhancement-review-2/MASTER_TODO.md` + 7 phase docs | **all 7 phases implemented** (build 0 errors, validator PASS); 4 decisions resolved; ~40 findings fixed |
| **Enhancement review 2026-07 (round 1)** — full one-by-one sweep of 213 components/scripts, 54 scenes, 239 signals; fixes in 8 phases (lifecycle → dead API → behavior → silent-failure → session-state → atmosphere → transforms → UX) | `enhancement-review/MASTER_TODO.md` + 8 phase docs | all 8 phases implemented (build 0 errors, validator PASS); dead API deleted per revised decision |
| **Genre-based scene templates** — the menu→game→pause→game-over loop, per-genre scenes, the generator | `MASTER_TODO.md` + `genre-templates/` | largely complete; see the tracker's Progress list |
| **Entity & item model** — `GameItem` tree, equipment, damage typing, archetype composition | `entity-model/MASTER_TODO.md` + `entity-model/` | planned, not started |
| ↳ **Component disposition** — per-component verdict + fix for all ~146 components | `entity-model/components/` (8 docs) | planned, not started |
| ↳ **Per-genre item trees** — what each genre's `GameItem` tree is, or why it wants none | `entity-model/items/` (11 docs) | planned, not started |
| Component audit, round 2 | `component-audit-round2.md` | complete |
| Component-first refactor | `component-first-refactor.md` | historical |

## Conventions

- **Tracker** — goal, decisions, a `[ ]`/`[x]` progress list linking to the phase docs, and
  the verification that gates every phase. Kept current as phases land.
- **Phase doc** — why, the work, the gotchas, and how to verify. One per phase.
- **Cite `file:line`.** A claim about the code without a citation is a guess, and this repo
  has been burned by confident guesses — see `CLAUDE.md` § *Testing* for the receipts.
- **Record what a finding invalidated**, not just the conclusion. The reasoning is the part
  that stops the next person redoing the work.
</content>

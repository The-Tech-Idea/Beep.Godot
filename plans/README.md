# plans/

Each initiative owns a folder, a tracker, and one doc per phase/stage. **There is no single
repo-wide tracker** — a "MASTER_TODO.md" is always scoped to the initiative it sits in.

| Initiative | Tracker | State |
|---|---|---|
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

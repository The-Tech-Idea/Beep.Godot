# `ui5.png` — mega casual GUI sheet

**1200 × 3579** · asset sheet · **multi-material casual** — the largest file in the folder
**Relevance:** every genre.

**Scope note, stated plainly:** this sheet contains several hundred elements across ~12
material families. I documented its **organising principle and widget families** and did
not scan every instance — at this size a per-instance pass would need its own session. Where
a number appears below it is either measured here or carried from a sibling sheet, and
which is which is marked. Nothing is guessed.

---

## The organising principle — and it is the most important thing on the sheet

**The same dialog is drawn ~10 times in ~10 different materials.** Identical layout, identical
control positions, identical proportions; only the surface changes:

| # | material | distinguishing feature |
|---|---|---|
| 1 | **wood plank** | horizontal boards, visible grain, nail heads |
| 2 | **parchment scroll** | rolled ends top and bottom |
| 3 | **stone / grey** | carved blocks, gear ornaments at the corners |
| 4 | **stone + vines** | the same stone with foliage growing over it |
| 5 | **bone / skull** | bone-framed, skull ornament |
| 6 | **book spread** | open book, two pages, ribbon bookmark |
| 7 | **cardboard + tape** | torn card with masking tape across the corners |
| 8 | **chained metal** | hung on chains from the top edge |
| 9 | **signpost** | planks mounted on posts |
| 10 | **fabric banner** | hanging cloth with a torn lower edge |

This is the **direct visual proof of the project's `genre (skin) → theme → palette`
model**, executed by an artist: geometry is invariant, material is the variable. It is also
the strongest argument that the kit's `KitMaterial` must be a genuinely separate axis from
`KitGeometry` — because here one geometry carries ten materials with no layout change at
all.

**The user's original instruction — "for each genre (skin) geometry and texture should be
different, not just changing colors" — is satisfied by this sheet in reverse:** it shows how
far *material alone* can go. The kit needs both axes, and this sheet is the acceptance test
for the material axis.

## Widget family 1 — `PanelHanger` (confirms catalogue §F, with the full set visible)

Every panel on the sheet attaches to the screen by a **hanger crossing its top edge**:

| hanger | anatomy |
|---|---|
| **ChainHang** | two chains rising from the top edge |
| **RopeHang** | rope or cord, often knotted |
| **NailPin** | a nail or screw head at each top corner |
| **TapeCorner** | masking-tape strips across the corners at an angle |
| **ScrollRoll** | parchment rolled at top and bottom |
| **VineFrame** | leaves and vines growing around the frame |
| **PostMount** | the panel sits on visible posts |

All are `KitAttach` instances with `Overhang > 0.5` — the primitive already exists; only the
shapes are new. `ui8.md`'s measured collapsible handle (33px, straddling the leading edge)
gives the anchor geometry.

## Widget family 2 — `ButtonRow` colour sets

Buttons ship in **five-colour sets** (blue / green / yellow / red / orange) with identical
geometry — role-coloured rather than one accent. Matches `UiSurface.Role` exactly, and
matches `gameui4`/`gameui5`'s measured finding that the palette lands on **one** element.

## Widget family 3 — dialog types, each present in most materials

`Settings` · `Game Over` · `You Won` · `Level Complete` · `Level Selection` · `Shop` ·
`Pause` · `Confirm (No/Yes)` · `Tooltip` · `Loading`.

**Ten dialog types × ten materials** is the sheet's matrix. For the kit that means the
dialog set should be defined once as layouts, with material applied at render time.

## Widget family 4 — controls seen across materials

| widget | notes |
|---|---|
| **OnOffSwitch** | two-segment plate, one lit — confirms `gameui1`'s **42px** measurement |
| **Slider** | track + knob, knob shape varies by material |
| **StarRating** | 1–3 stars, gold earned / grey unearned |
| **LevelNodeGrid** | numbered circles, **locked variant with a padlock**, connected paths |
| **SpinWheel** | segmented circular wheel with a pointer |
| **RewardSlotRow** | a row of empty boxes filled on claim |
| **MedalRosette** | circular medal with **ribbon tails below** |
| **SegmentedBar** | progress as discrete chunks — confirms the folder-wide default |
| **LoadingIndicator** | text plus animated dots, or a chain motif |
| **ItemCardWithFooter** | art + name + **`SELECT` footer** — the welded action footer, measured at **0.10 × card** in `store.md` |
| **ConfirmPair** | green ✓ / red ✕, matching `citybuilder5`'s measured 45px pair |
| **CalloutButton** | `PLAY Now!` in several treatments — the highest-emphasis control on the sheet |

## Widget family 5 — `BookSpread`

An open book with two pages, a ribbon bookmark, and per-page content. Appears three times in
different materials. Confirms `survaivleandrpg`'s two-page spread and its finding that a
spread needs **multi-select tabs**.

---

## What this sheet settles

1. **Geometry and material are genuinely separable** — one artist drew ten materials over
   one geometry. The kit's two-axis model is correct and this is the proof.
2. **The hanger set is complete**: chain, rope, nail, tape, scroll-roll, vine, post.
3. **Five-colour role sets** are the norm for buttons.
4. **A dialog library is ten layouts, not a hundred designs.**

## Actions

- [ ] Use this sheet as the **acceptance test for `KitMaterial`**: one dialog layout must
      render convincingly in wood, parchment, stone, vine-stone, bone, book, taped card,
      chained metal, signpost and fabric **without touching the layout**.
- [ ] Add the seven **hangers** as `KitAttach` shapes.
- [ ] Define the **ten dialog layouts** once, material-agnostic.
- [ ] **Follow-up needed:** a per-instance measured pass of this sheet, focusing on how each
      material changes frame thickness and rim ratio — the two discriminators in
      `INDEX.md`. That is a session's work on its own and is not done.

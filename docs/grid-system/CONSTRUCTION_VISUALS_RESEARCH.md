# How real games show a building being built — research and design (2026-09-04)

**Why this exists.** The construction-in-progress effect family (`GridBuildStageVisualComponent`, `GridBuildProgressBarComponent`, `GridWorkerBuildActivityEffectComponent`, `construction_stage.gd`) shows a slab, a frame, walls and a roof rising in with a height-slice, a bar, and a dust puff at the worker. The owner's verdict: "still not like a real game's building construction." This document is the research that answers *what real games actually do*, what our version lacks, and the corrected design — before any more art is made.

**Method.** Five web-research passes, one per game family (modern 3D city builders; classic isometric builders; RTS; colony/base sims; 2D top-down and farm games — 45 games with evidence), plus a techniques survey, a synthesis, and an adversarial critique that also read this addon's own source. 96 sources, listed at the end; the strongest are open-source or decompiled engines where the behaviour is *code*, not a review's impression: The Settlers II (Return to the Roots), The Settlers III (JSettlers), Caesar III (Julius), RimWorld (decompiled 1.6), Oxygen Not Included, Stardew Valley, Command & Conquer (EA source), OpenRA, openage, Satisfactory (modding headers), SimCity 4 (gzcom-dll), OpenTTD NewGRF specs. Claims the critique could not verify are marked.

---

## 1. What makes a site READ as construction (not "a building slowly appearing")

These are the cues the praised sequences share and the criticised ones lack. Our current version has the third and none of the others.

1. **Things that exist only during the build and are removed at the end.** A building that merely gets more complete reads as loading. Cities: Skylines 1's whole effect is a scaffold box rising, then *tearing down*; Settlers II swaps signboard → foundation stone → nothing; Prison Architect strips the foundation overlay; SimCity 4 turns construction sand back to grass; AoE IV's scaffolding and ghost crew vanish on completion.
2. **The ground is disturbed beyond the footprint.** RimWorld's underfield is 1.15× the footprint; Manor Lords leaves a dirt patch; SimCity 4 switches the lot texture to construction sand; Banished draws a brown rectangle. A slab that fits the footprint exactly reads as a floor, not a site.
3. **Skeleton before skin, with the ground visible through it.** Settlers II ships a see-through skeleton sprite for every building; Manor Lords shows the timber frame before infill; AoE II DE draws skeleton and partial walls inside scaffolding. *(We have this.)*
4. **Assembly is piecemeal and uneven, not one clean slice.** Beams pop in one at a time (Manor Lords); artisans lay one tile at a time (Zeus); Settlers III adds a sawtooth to its reveal edge so the top reads as jagged masonry rather than a flat cut. A single flat line rising through a whole face is the tell-tale of "appearing".
5. **Motion is driven by workers, and stops when they leave.** Every praised sequence (Manor Lords, Foundation, Banished, Settlers) gates progress on a worker at the site; the criticised one (Cities: Skylines II — "cranes wiggle around, then the building teleports into being") runs on a timer with no crew.
6. **The worker stands at an authored spot, facing the work, with the tool effect at the contact point on the structure.** Settlers III bricklayers have fixed positions and facing; ONI moves the sparks to the multitool's target; RimWorld plays the effecter at the touched cells. A dust puff at the worker's feet reads as "standing near something".
7. **Something on site is taller than the building.** Scaffold poles project above the wall line; Cities: Skylines' crane is "a helpful indicator of building height". If the silhouette never changes shape, only fills in, it is a fade.
8. **Material is visibly delivered and visibly consumed.** Piles at fixed offsets grow as carriers arrive and shrink as the builder uses them (Settlers II/III, Banished); Timberborn's percentage *is* the deposited quantity.
9. **Raw material looks different from finished material.** The frame is lighter, rawer timber than the finished planks; the unpainted shell precedes paint (Tropico, Manor Lords). Same colour = texture reveal, not finishing.
10. **Vehicles come and go.** Ox carts drag logs (Manor Lords), supply trucks arrive (Prison Architect), sledges haul stone (Pharaoh).
11. **Sound is per hit, synchronised to the tool** — hammer every 4th frame (Settlers II), cue at 70% of a 1 s action (Settlers III), tags at 0.38/0.67/1.0 of the loop (AoE III) — not a completion fanfare.
12. **The site is a gameplay state, not only art**: impassable footprint, refund tied to progress, extra damage while unfinished (AoE IV +50%).

## 2. Universal patterns across 45 games

- **The placement ghost is the finished art recoloured**, never a separate blueprint drawing (Caesar III green mask; AoE II DE semi-transparent foundation; OpenRA 0.65 alpha; RimWorld pale-blue edge-detect; Satisfactory hologram).
- **Committing leaves a ground marker immediately**, before any structure: dirt patch + dotted outline (Manor Lords), signboard then foundation stone (Settlers II), construction-mark posts (Settlers III), corner brackets on a 1.15× underfield (RimWorld), a post-frame that grows 0→16 px over 1 s (Stardew), construction sand (SimCity 4), a beam skeleton (Frostpunk).
- **Ground preparation is its own visible phase** in every simulated-site game (Settlers II's planer, Settlers III diggers, Banished/Tropico labourers, Manor Lords flattening).
- **Materials arrive physically, pile at fixed offsets, and shrink** (Settlers II plank stack 5 px left / stone 8 px right of the door; Settlers III authored stack offsets; Timberborn 10 % per delivery).
- **Skeleton first, skin second** (Settlers II Rohbau; Settlers III BUILD then FINAL image; Manor Lords frame then infill; Anno 1404 phases *Scaffolding → Masonry → Windows*).
- **Two engine strategies for the in-between**: discrete stages (OpenTTD exactly 3; AoE II frames at 0/25/50/75/100 %; SC1 two special states) or a continuous bottom-up reveal (Settlers II/III clip; WC3 one 60 s birth animation stretched to build time; Satisfactory slice plane). The best 2D sites combine both — stage swaps whose newest part rises.
- **The worker is the animation.** Builders at authored positions facing inward, looping a hammer action with a per-hit sound; free-walking between bouts (Settlers II); dust + sparks at the contact point (RimWorld, ONI).
- **A stall is visible** (Frostpunk 0/10 ring; ONI "no worker"; Timberborn stuck at a percentage; Settlers waiting-for-material state).
- **Completion is a hard swap + sound + teardown beat, never a fade** (Settlers II deletes the site and creates the building in one tick; CS1 tears the scaffold down; RimWorld/ONI completion sounds).
- **Progress is read off the site first, a panel second.** Bars over the site are rare in 2D (They Are Billions); Settlers, Banished, CS1, Anno 1800 show none; RTS use the HP bar rising from 1 HP.
- **Scaffolding is the strongest signifier in the 3D/modern family, always sized to the target** — and it is *absent* from the 2D colony sims (RimWorld, ONI, Prison Architect), which is exactly why those sites read as "appearing".
- **Era decides the props**: cranes for modern/industrial (CS2, SimCity 4, C&C), timber scaffolds/ladders/carts/mortar for medieval (Manor Lords, Foundation), welding sparks for steel (SC2 Terran, CoH, RimWorld ConstructMetal).

**Genre differences worth knowing.** 3D builders split into timer-driven reveals with no crew (CS1, CS2, Anno 1800) and logistics-driven sites where nothing advances until agents deliver and stand there (Manor Lords, Foundation, Frostpunk, Timberborn) — only the latter are praised. Classic isometric builders are either instant plop with a sound (Caesar, Pharaoh, Zeus, most Anno) or fully simulated sites (Settlers, Banished, Tropico, monuments) that all use the same four beats: marker, ground prep, piles, worker-gated growth. Farm games (Stardew) use an NPC-attended multi-day static frame with a hard swap at the day boundary. Crafting builders (Forager, Terraria, Core Keeper) place instantly with a poof.

## 3. The strongest precedent for OUR approach

AoE I/II: every building of a footprint class shares the *same* construction-site sprites (dirt lot, timber piles, scaffold poles); the building-specific art appears only at the end. A modular, footprint-sized site built from generic parts is the industry pattern for 2D, not a compromise. Settlers II's two-pass rise (skeleton sprite rises, then the finished sprite rises over it) is the template for a modular skeleton/skin split.

## 4. What our current version gets wrong — including code-level defects the critique found

Art-level:
- One flat reveal line per part, all parts of a stage rising together; no per-part stagger, no row quantisation — the "appearing" tell.
- No ground disturbance beyond the slab; the slab reads as a floor.
- Nothing taller than the building; the silhouette only fills in.
- No scaffold, no piles, no marks, no teardown — nothing exists only during the build.
- `wood_beam.png` and `wood_planks.png` are the same tan: the wall stage reads as a texture reveal over the frame, not a skin on a skeleton.
- The dust puff is at the worker, not at the contact point on the wall.
- The roof and slab are tiled in screen space while the right wall is tiled along its skew — inconsistent; a row-quantised roof sweep would produce screen-horizontal bands.
- Height-slice steps land mid-brick: brick.png has 4 courses per repeat (16 px at 64 px cells) and the brick recipe rises in 5 steps of 19.2 px.

Engine-level (these produce the "fade-in" look before any art decision is made):
- `GridBuildSiteComponent.UnderConstructionModulate` defaults to a translucent yellow and every stage is parented under the placed node, so the whole modular site renders as a ghost. The demo overrides it to white by hand; a real project will not.
- `HidePlacedUntilBuilt` defaults false: the finished `Scene` is visible at 0.72 alpha for the entire build, so "the final silhouette is not shown until the end" is violated by defaults.
- Two z domains: the placed node's z is its anchor (back-left) cell y and stage parts are relative 0..10 on top; units use absolute z = their own y. A worker sent to the anchor cell sorts *behind* the front wall and beams as they rise.
- The job cell is the anchor cell *inside* the footprint; `GridObjectComponent` blocks it and `GridNavigationComponent.AllowBlockedGoal` defaults false, so `MoveToCell` fails with `no_path` and the job is released. The demo works only because it calls `RegisterPlacedBuild` directly with no `GridObjectComponent`.
- The awaiting-materials state draws nothing: `GridBuildStageVisualComponent` instantiates stages on `BuildSiteCreated`, which fires after the last delivery. Banished's "piles then frame", Timberborn's deliveries and Settlers' stacks all happen in the state we do not draw.
- Materials are unloaded in one shot at `StartBuildJob`; there is no delivery-driven progress to show. Piles must be drawn while pending from `storage.Stored()` and faked from progress while working.
- `Costs` is wallet currency; the physical stock is `RequiredMaterials`. Piles must come from the latter.
- The job queue is single-claim: multi-worker sites do not exist; AoE's `3t/(n+2)` speed-up is a queue redesign, not art.
- The shipped worker is a truck sprite with no frames and no facing: hammer loops and hit-frame sound sync have nothing to bind to.
- `Stage`/`StageCount`/`MaterialKind` are baked into 21 `.tscn` files; every added phase is a renumber of all 21. A "phase that persists while stalled" is a *state*, not a progress band, and the visual component only receives progress.

## 5. Revised design (grounded; each item names the game that does it)

1. **Fix the owners before the art.** `GridBuildStageVisualComponent` owns the under-construction look when `ConstructionStages` is non-empty: stages live *outside* the placed node's modulate chain (a sibling positioned from `placed.GlobalPosition`, as the bar already does) and the finished art is hidden until `BuildSiteCompleted`. `UnderConstructionModulate` then applies only to builds with no stages — one owner of the look. The site visual is instantiated on `BuildSiteAwaitingMaterials`, not only on `BuildSiteCreated`. Stage selection becomes **state-driven** — `pending_materials` (staked plot + piles filling from `storage.Stored()`), `queued_no_worker` (same + stalled meter), `working` (progress-band stages), `complete` (hard swap) — read from the site's existing `grid_build_site_state` meta, extended to four values. Piles come from `RequiredMaterials`.
2. **The job owns the stand cell** (AoE, RimWorld, Settlers): `StartBuildJob` computes an approach cell on the front edge (+1 in y from the anchor column, then the next boundary along the front per re-claim) and `GridWorkerComponent` paths there. This is the only thing that fixes `no_path` under `AllowBlockedGoal=false`. The stage exposes `ContactPoint` (the current reveal line on the nearest face, stage-local) for effects; it does not own stand positions.
3. **One z domain for the site.** Everything drawn on the site is a child of the stage node with relative z; the stage node's own z is computed from the *front* edge (`y_front`), so a unit on the front apron sorts in front and one a row behind sorts behind. Workers never stand on the structure (Settlers II's builder never leaves the ground). The crane (steel/concrete families only, `WallHeightCells ≥ 2`) sits back-right at relative z 12.
4. **Two-pass rise, row-quantised** (Settlers II/III): pass 1 reveals the frame group (posts, base/top/cross beams, append order, 1.5× stagger), pass 2 the skin group (front wall, right wall, roof) over the finished frame. The shader gains `row_px` and quantises the line to it in each face's *own* texture axis (pass the face's v from `vertex()`, not `VERTEX.y`), so the skewed wall and the roof quantise along their courses. Each swatch declares `rows_per_swatch` (brick 4, planks 4, shingles/tile N, steel panel 1 → whole panels pop); the brick step count becomes `H / row_px` so every step lands on a mortar line. Left-to-right progression comes from geometry: wall quads split per footprint column with shared absolute UVs, staggered. Keep a 4–8 px raw/wet cut band below the line (Ronja); drop the sawtooth and dither ideas — on tiled swatches they cut mid-course.
5. **Fix the swatches, not the mechanism.** `wood_beam.png` visibly rawer (lighter, yellower, no plank seams) than `wood_planks.png`; a grey mortar tub for brick; retile the roof and slab in their own skewed space so courses follow the face like the right wall's already do.
6. **Cut the dressing to what is cited, as flat geometry.** Phase-0 look = dirt apron (footprint + 0.5 cell, a new `dirt` swatch at z −1 — RimWorld 1.15×, SimCity 4 sand, Manor Lords) + four corner marks that *grow* over the first second (Stardew 0→16 px, Settlers III `drawByProgress`) + a ground shadow quad. Sign only in the pending/stalled state (Settlers II shows its Baustellenschild only in planning). Scaffold on the front and right faces only, as vertex-coloured Polygon2D poles/ledgers/braces (no swatch — 10 px poles smear a tiled texture), taller than H, present from frame to roof, removed by running its reveal 1→0 at completion (CS1, Factorio). Piles: 2–4 stacked thin quads of the family swatch at hashed apron cells. **No** fence, formwork, spoil heap, tarp, tyre tracks or completion truck — none has a cited game (the critique traced each).
7. **Completion is a hard swap with a teardown beat.** No cross-fade. At 1.0: scaffold sinks (~1 s), apron and piles freed, finished art shown, completion sound + one larger dust burst from the component, and only then the finished scene's "alive" emitters (smoke, lights) start — `IConstructionVisual.BuildProgress = 1` on the finished scene is the switch.
8. **Effects and sound with the assets that exist.** `GridWorkerBuildActivityEffectComponent` keeps its cadence (`BurstIntervalSeconds` *is* the hammer cadence; default 0.6 s, per-hit clip chosen by the site's `MaterialKind`), but spawns under the stage at its `ContactPoint`, so it sorts with the wall. Burst scene by material: dust (brick/concrete), sawdust tint (wood), additive sparks (steel). No hit-frame sync until a humanoid builder scene exists; until then the truck is the worker: parks on the approach cell, flips to face the site, shows its hidden `Cargo` polygon on the way in and hides it on the way out (Settlers carriers, Banished citizens with logs).
9. **Progress UI.** Keep the KitMeter overlay outside the site subtree; show it while selected/hovered or stalled; continuous value (there is no delivery-driven progress to quantise); a **stalled** look driven by state (pending materials / queued with no claimant — Frostpunk, ONI, Banished). No worker counts.
10. **Stage files.** Stop baking `Stage`/`StageCount`/`MaterialKind` into 21 `.tscn` files: one scene per family with `Stage` assigned by the component at instantiation (it already names them `Stage_n`), or one scene reading `MaterialKind` from the delivered `RequiredMaterials` (RimWorld's stuff colour). Adding a phase becomes a code change in `construction_stage.gd`.
11. **Deferred**: multi-worker speed-up (queue redesign); non-rectangular footprints (`Footprint` is a `Vector2I`, no data source); sci-fi dissolve family (no consumer); tower crane on non-steel families; every Manor Lords Aug-2026 detail (the only source returned 403 — unverified).

## 5a. Implementation status (2026-09-04)

Implemented, all guarded by `tests/grid_worker_build_effects_probe.gd` and shown live by `templates/scenes/construction_demo.gd`:

- **Owners** (design 1): `GridBuildSiteComponent` hides a staged build's placed node until completion; `GridBuildStageVisualComponent` draws the site under its own root outside the placed node's modulate, from `BuildSiteAwaitingMaterials` on, state-driven (`pending` / `queued` / `working` / `complete`), piles from `RequiredMaterials` and the site's `GridStorageComponent`, `WorkPulse` only while progress advances, teardown window after the hard swap.
- **The job owns the stand cell** (2): `GridJobQueueComponent.SetJobApproachCell` / `GetJobApproachCell`, set by `GridBuildSiteComponent.ApproachCellFor`, pathed to by `GridWorkerComponent` with a fallback to the job cell.
- **One z domain** (3): the site root's z is the footprint's front edge, absolute, like units.
- **Two-pass, row-quantised rise with jittered column stagger** (4): `construction_reveal.gdshader` clips along each face's own texture axis (`v_axis`/`v_scale`), quantised to `row_v`, with a `cut_v`/`cut_color` band; `construction_stage.gd` sequences slab → frame (posts then beams, staggered) → walls (front columns left to right with deterministic jitter, then the right wall; brick has no frame, courses rise on mortar lines) → roof (rows from the eave). Steel's cut band is weld-orange.
- **Swatches** (5): raw `wood_beam` visibly paler/yellower than `wood_planks`; a `dirt` swatch for the apron; roof and slab tiled in their own skewed space.
- **Dressing** (6): dirt apron (footprint + 0.5 cell), corner marks growing over the first second (a procedural cyan blueprint plan for the steel/industrial family instead — the modern-era counterpart), a site sign only while pending/queued, material piles at hashed apron corners sized by delivered stock, a ground shadow that grows with the built height, scaffold poles/ledgers/braces on the front and right faces from the frame on. No fence, formwork, tarp, tyre tracks or completion truck.
- **Completion** (7): hard swap by the site component; the scene sinks its scaffold, hides piles and fades the apron over `ScaffoldTeardownSeconds`.
- **Effects and sound** (8): the scene spawns dust (wood/brick) or additive weld sparks (steel) at the contact point of the part being worked on each `WorkPulse`, with a Kenney `hit_00x` / `hit_metal_00x` clip pitched by material. Nothing assumes what the worker is.
- **Progress UI** (9): `GridBuildProgressBarComponent.StalledModulate` while the job is queued with no claimant.
- **Stage files** (10): 21 per-stage `.tscn` replaced by one scene per family.

Deferred, unchanged: multi-worker speed-up, non-rectangular footprints, sci-fi dissolve family, cranes on non-steel families, the Manor Lords Aug-2026 details, hover/selection-gated bars (no selection system to gate on here).

Considered from a later "construction VFX layers" suggestion and **not** adopted, because §1 and §4 argue against them: a dissolve/dither/noise reveal or blueprint overlay run over the whole finished sprite (that is the fade-in the whole design replaces; blueprints are for the *planned* state and the industrial family has one on the ground), a glowing scan line on wood/brick (kept only as steel's weld-orange cut band), a shared noise texture (the jitter is deterministic per column, no texture), and the supplied spark/dust/scan-line images (grey, not transparent, backgrounds; the addon already ships alpha `spark_01`/`dirt_02`).

## 6. Reference games to study first

- **The Settlers II** (RttR: `noBuildingSite.cpp`, `nofBuilder.cpp`, `nofPlaner.cpp`, `glSmartBitmap::drawPercent`) — the complete 2D template: sign → foundation stone → plank/stone stacks at fixed offsets → rising skeleton → rising finished sprite; builder free-walking with per-hit hammer sounds; planer levelling the perimeter.
- **The Settlers III** (JSettlers: `MapObjectDrawer.drawWithConstructionMask`, `BricklayerStrategy`, `big_livinghouse.xml`) — authored mark and stack offsets, fixed bricklayer positions with facing, BUILD then FINAL image, sawtooth reveal edge.
- **Manor Lords** — the modern gold standard for a worker-driven site: dirt patch + dotted outline, flattening, ox-dragged logs, frame then infill popping in phases, mortar mixing, builds deliberately lengthened so there is time to watch.
- **Age of Empires IV / II DE** — perimeter scaffolding sized to the footprint, structure rising inside, real villagers outside the footprint; generic per-size sites in AoE I/II.
- **Cities: Skylines 1 vs 2** — the scaffold box that rises then tears down, and (CS2's crane-only lot) what it looks like when the teardown is missing.
- **Banished**, **Foundation**, **Frostpunk**, **Timberborn** — piles → frame → partial → finished, all gated on hauling; the clearest stall UIs.
- **RimWorld** (`Frame.cs`, `JobDriver_ConstructFinishFrame`, `Effecters_Construction.xml`) — 1.15× underfield, corner brackets, pawn facing the frame, dust/sparks effecters by material, completion sound threshold.
- **Stardew Valley** (`Building.cs drawInConstruction`) — corner/edge post-frame growing 0→16 px over 1000 ms; Robin hammering at the site.

## 7. Sources

Primary (code):
- https://raw.githubusercontent.com/Return-To-The-Roots/s25client/master/libs/s25main/buildings/noBuildingSite.cpp
- https://raw.githubusercontent.com/Return-To-The-Roots/s25client/master/libs/s25main/figures/nofBuilder.cpp
- https://raw.githubusercontent.com/Return-To-The-Roots/s25client/master/libs/s25main/figures/nofPlaner.cpp
- https://raw.githubusercontent.com/Return-To-The-Roots/s25client/master/libs/s25main/ogl/glSmartBitmap.cpp
- https://raw.githubusercontent.com/Return-To-The-Roots/s25client/master/libs/s25main/ingameWindows/iwBuildingSite.cpp
- https://raw.githubusercontent.com/jsettlers/settlers-remake/master/jsettlers.graphics/src/main/java/jsettlers/graphics/map/draw/MapObjectDrawer.java
- https://raw.githubusercontent.com/jsettlers/settlers-remake/master/jsettlers.logic/src/main/java/jsettlers/logic/buildings/Building.java
- https://raw.githubusercontent.com/jsettlers/settlers-remake/master/jsettlers.logic/src/main/java/jsettlers/logic/movable/strategies/BricklayerStrategy.java
- https://raw.githubusercontent.com/jsettlers/settlers-remake/master/jsettlers.common/src/main/resources/jsettlers/common/buildings/big_livinghouse.xml
- https://raw.githubusercontent.com/bvschaik/julius/master/src/widget/city_building_ghost.c
- https://raw.githubusercontent.com/bvschaik/julius/master/src/building/construction.c
- https://github.com/32bitx64bit/RW-Decompiled-1.6/blob/master/RimWorld/Frame.cs
- https://github.com/32bitx64bit/RW-Decompiled-1.6/blob/master/RimWorld/JobDriver_ConstructFinishFrame.cs
- https://github.com/32bitx64bit/RW-Decompiled-1.6/blob/master/RimWorld/ThingDefGenerator_Buildings.cs
- https://github.com/spacedrabbit/RimworldPocketGuide/blob/master/RimworldPocketGuide/Core/Defs/EffecterDefs/Effecters_Construction.xml
- https://github.com/undancer/oni-data/blob/master/Managed/main/Constructable.cs
- https://github.com/undancer/oni-data/blob/master/Managed/main/MultitoolController.cs
- https://github.com/undancer/oni-data/blob/master/Managed/main/BuildingDef.cs
- https://raw.githubusercontent.com/veywrn/StardewValley/master/StardewValley/Buildings/Building.cs
- https://raw.githubusercontent.com/electronicarts/CnC_Tiberian_Dawn/master/BUILDING.CPP
- https://raw.githubusercontent.com/OpenRA/OpenRA/bleed/OpenRA.Mods.Common/Traits/Render/WithMakeAnimation.cs
- https://raw.githubusercontent.com/SFTtech/openage/master/openage/convert/processor/conversion/aoc/ability_subprocessor.py
- https://github.com/satisfactorymodding/SatisfactoryModLoader/blob/master/Source/FactoryGame/Public/FGBuildEffectActor.h
- https://raw.githubusercontent.com/nsgomez/gzcom-dll/master/gzcom-dll/include/cISC4ConstructionOccupant.h
- https://github.com/benjaminfoo/GoingMedievalModLauncher/blob/master/GoingMedievalModLauncher/src/MonoScripts/BarrelBuildableView.cs
- https://newgrf-specs.tt-wiki.net/wiki/VariationalAction2/Houses
- https://mods.factorio.com/mod/space-platform-entity-build-animation-lib

Developer blogs, wikis, patch notes, forums:
- https://shiningrocksoftware.com/2012-08-27-creating-artwork/ (Banished)
- https://factorio.com/blog/post/fff-380 , https://factorio.com/blog/post/fff-382 , https://wiki.factorio.com/Ghost
- https://wiki.polymorph.games/foundation/Buildings , https://www.polymorph.games/foundation/news/category/release-notes/
- https://timberborn.wiki.gg/wiki/Construction , https://timberborn.wiki.gg/wiki/Builders
- https://frostpunk.fandom.com/wiki/Buildings
- https://anno1404.fandom.com/wiki/Imperial_cathedral , https://anno1404.fandom.com/wiki/Sultan's_mosque
- https://banished.fandom.com/wiki/Buildings_and_Construction
- https://prisonarchitect.paradoxwikis.com/Foundation , https://prisonarchitect.paradoxwikis.com/Workman
- https://ageofempires.fandom.com/wiki/Building_(Age_of_Empires_II) , https://ageofempires.fandom.com/wiki/Building_(Age_of_Empires_IV)
- https://forums.ageofempires.com/t/settlement-under-construction-looking-like-a-pre-de-foundation/284336
- https://forums.ageofempires.com/t/pikeman-build-animations/270409
- https://forums.ageofempires.com/t/age4-as-for-builders-and-buildings-under-construction/127254
- https://www.hiveworkshop.com/threads/model-of-under-construction-buildings.292242/
- https://www.hiveworkshop.com/threads/adding-birth-build-animations-to-buildings.312113/
- https://www.hiveworkshop.com/threads/changing-birth-animation.125191/
- https://liquipedia.net/starcraft2/Buildings , https://staredit-network.fandom.com/wiki/IceCC_Animations , https://classic.battle.net/scc/protoss/units/probe.shtml
- https://modenc.renegadeprojects.com/BuildupTime
- https://cs2.paradoxwikis.com/Patch_1.0.X , https://cs2.paradoxwikis.com/Patch_1.1.X
- https://www.gamewatcher.com/news/cities-skylines-2-footage-finished-spawn-in-mechanics-buildings-
- https://www.pcgamesn.com/cities-skylines/cities-skylines-review
- https://steamcommunity.com/sharedfiles/filedetails/?id=759152265 (CS1 construction-site props)
- https://community.simtropolis.com/files/file/29436-construction-tower-crane-override/ , https://simcity.fandom.com/wiki/Growable_building
- https://www.pcgamesn.com/manor-lords/advanced-structures , https://fakehistoryhunter.net/2025/11/23/game-review-manor-lords-2024/
- https://nichegamer.com/manor-lords-august-2026-update/ (**returned 403 — unverified**)
- https://steamcommunity.com/app/1363080/discussions/0/4358998116397481606/ , https://steamcommunity.com/app/1363080/allnews/ (Manor Lords)
- https://steamcommunity.com/app/949230/discussions/0/4031347296563794921/ and sibling CS2 threads: 3815166994158791078, 3937895062999704718, 3811782223871002215, 6367585249990137186, 4339860355400521022
- https://steamcommunity.com/app/255710/discussions/0/1648792158819993246/ , https://steamcommunity.com/app/255710/discussions/0/1733210552685567216/ (CS1)
- https://steamcommunity.com/app/323190/discussions/0/1735464254637459731/ , 1696045708638964267 , 2145343824304115457 (Frostpunk)
- https://steamcommunity.com/app/916440/discussions/0/1742232339933523144/ , 3198115500345770248 (Foundation)
- https://steamcommunity.com/app/242920/discussions/0/2119355556480848244 (Banished)
- https://steamcommunity.com/app/24780/discussions/0/3735204542169485503/ (SimCity 4)
- https://steamcommunity.com/app/644930/discussions/0/1642041886372602655/?ctp=3 (They Are Billions)
- https://impressionsgames.fandom.com/wiki/Stepped_Pyramid , https://pharaoh.heavengames.com/faqs/monuments/ , https://caesar3.heavengames.com/cgi-bin/caeforumscgi/display.cgi?action=st&fn=12&tn=332
- https://archive.org/download/manual_Tropico/ , https://tropico.fandom.com/wiki/Construction_Office_(Tropico_3)
- https://riseofindustry.fandom.com/wiki/Creating_new_Buildings
- https://dwarffortresswiki.org/index.php/Construction
- https://www.gamespew.com/2023/02/how-to-build-buildings-in-company-of-heroes-3/
- https://www.gamingnexus.com/Article/5173/Prison-Architect
- https://www.galaxus.fr/en/page/foundation-is-a-brilliantly-chaotic-and-creative-building-game-36583
- https://satisfactory.wiki.gg/wiki/Build_Gun
- https://www.youtube.com/watch?v=C8aisve52aI (WC3 birth animations)

Technique references:
- https://www.ronja-tutorials.com/post/021-plane-clipping/ (clip plane with cut-face colour)
- https://docs.godotengine.org/en/stable/tutorials/shaders/shader_reference/canvas_item_shader.html

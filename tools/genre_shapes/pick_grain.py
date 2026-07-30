#!/usr/bin/env python3
"""Choose each genre's grain pattern BY MEASUREMENT, from the CC0 Kenney Pattern Pack.

The kit's missing third axis is material (see measure_material.py). The patterns that supply
it must ship, so they come from Kenney (CC0 1.0 — commercial use, no attribution required,
no template/redistribution restriction). Example_Art is licensed Vecteezy reference and is
used for MEASUREMENT ONLY; none of its pixels ship.

HOW A PATTERN IS CHOSEN
-----------------------
The patterns are pure black/white, so their raw `hf` is maximal and matching it directly
against a photographic reference tile is meaningless. The two properties split cleanly:

    pattern choice  ->  sets `dir` (grain direction) and the spatial character
    amplitude       ->  scales `hf` to the target

So a genre's reference material fixes the TARGET (`dir` from the material, `hf` from it too),
the pattern is picked as the nearest `dir` match within a plausible family, and the amplitude
is then solved so the composited plate lands on the reference `hf`. That makes both numbers
derived rather than dialled in by eye.

Targets come from Example_Art/uitexturs.png, scale-normalised (measure_material.py):

    stone         hf 0.0055  dir 0.11      leather        hf 0.1037  dir 0.06
    brushed-metal hf 0.0218  dir 0.02      wood-plank     hf 0.1224  dir 0.67
    rubber-dots   hf 0.0344  dir 0.01      denim          hf 0.1253  dir 0.05
    graph-paper   hf 0.0345  dir 0.00      diamond-plate  hf 0.3703  dir 0.01
    glossy-leaf   hf 0.0358  dir 0.23

USAGE
    python pick_grain.py --score          # score every pattern
    python pick_grain.py --pick           # genre -> pattern + amplitude
    python pick_grain.py --install        # copy the chosen patterns into the addon
"""
import os
import shutil
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from measure_material import hf_energy, directionality

PACK = (r"H:\GameDev\GFX\GameAssets\Kenney Game Assets All-in-1 3.6.0"
        r"\2D assets\Pattern Pack\PNG\Default")
REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
DEST = os.path.join(REPO, "addons", "beep_game_builder_cs", "textures", "grain")

# genre -> the reference material its art is actually MADE of, per the art pass:
# rpgui is wood and survival's store cards are hide/parchment; citybuilder/strategy are
# stone; shooter/racing are metal (plate and brushed); the casual family
# (platformer/puzzle/cardgame) is a flat printed surface; topdown is the glossy pixel era.
#
# The numeric targets are MEASURED off Example_Art/uitexturs.png at run time rather than
# transcribed — a transcribed constant silently rots the moment the metric changes, and this
# metric has already changed once (scale normalisation).
GENRE_MATERIAL = {
    "rpg": "wood-plank",       "survival": "leather",
    "citybuilder": "stone",    "strategy": "stone",
    "shooter": "diamond-plate", "racing": "brushed-metal",
    "platformer": "rubber-dots", "puzzle": "graph-paper",
    "cardgame": "denim",       "topdown": "glossy-leaf",
}

SHEET = os.path.join(REPO, "Example_Art", "uitexturs.png")


def reference_targets():
    """(hf, dir, coarse) per material, measured from the reference sheet itself."""
    from measure_material import NINE, grid
    if not os.path.isfile(SHEET):
        raise SystemExit(f"reference sheet not found: {SHEET}")
    out = {}
    for name, tile in zip(NINE, grid(SHEET, 3, 3)):
        out[name] = (hf_energy(tile), directionality(tile), coarseness(tile))
    return out


def coarseness(g):
    """Characteristic feature SIZE: mean run length of same-side pixels, as a fraction of
    the crop. Fine dot field -> small; big brick or blotch -> large.

    A third axis is required. `hf` and `dir` cannot tell a fine dot grid from a coarse brick
    — both are isotropic mid-energy — and six of the ten reference materials have dir < 0.1,
    so ranking on those two alone handed five genres near-identical patterns. That would have
    rebuilt "every genre looks the same" inside the very layer meant to cure it.

    The first attempt used hf(64px)/hf(256px) and returned a saturated 2.00 for all 171
    patterns — a dead axis that still produced a full, plausible-looking assignment table.
    Run length is measured directly on the thresholded image, so it cannot saturate that way.
    """
    a = _normalise_for_runs(g)
    b = a > a.mean()
    runs = []
    for axis in (0, 1):
        arr = b if axis == 0 else b.T
        # Transitions per line -> mean run length = line length / (transitions + 1).
        trans = np.abs(np.diff(arr.astype(np.int8), axis=1)).sum(axis=1)
        runs.append((arr.shape[1] / (trans + 1.0)).mean())
    return float(np.mean(runs) / a.shape[0])


def _normalise_for_runs(g, n=256):
    return np.asarray(Image.fromarray(g.astype(np.uint8)).resize((n, n), Image.LANCZOS))


def score_all():
    """Score every pattern. REFUSES rather than returning an empty list: the first run
    pointed at the pack root instead of PNG/Default, scored 0 patterns, and every genre
    printed 'NO CANDIDATE' — which reads like a selection problem, not a path problem."""
    if not os.path.isdir(PACK):
        raise SystemExit(f"pattern pack not found: {PACK}")
    out = []
    for name in sorted(os.listdir(PACK)):
        if not name.lower().endswith(".png") or name == "Preview.png":
            continue
        p = os.path.join(PACK, name)
        a = np.asarray(Image.open(p).convert("L"))
        # Coverage: the fraction of dark pixels. A pattern at 0.5 is a bold check; one at
        # 0.05 is a sparse dot field. It matters as much as dir for how a face reads.
        cov = float((a < 128).mean())
        out.append((name, hf_energy(a), directionality(a), cov, coarseness(a)))
    if not out:
        raise SystemExit(f"no .png found in {PACK}")
    return out


def composite_hf(pattern_path, amp):
    """hf of a mid-grey plate with the pattern applied at `amp`, exactly as KitGrain does:
    a luminance mask modulating the plate multiplicatively about 1.0."""
    m = np.asarray(Image.open(pattern_path).convert("L")).astype(np.float64) / 255.0
    plate = np.full(m.shape, 140.0)
    return hf_energy(np.clip(plate * (1.0 - amp * (1.0 - m)), 0, 255).astype(np.uint8))


def solve_amp(pattern_path, target_hf):
    """Smallest amplitude whose composited hf reaches the target. Bisection: composited hf is
    monotonic in amp, so this is exact to the tolerance rather than a guess."""
    lo, hi = 0.0, 1.0
    if composite_hf(pattern_path, hi) < target_hf:
        return hi, composite_hf(pattern_path, hi), False
    for _ in range(24):
        mid = (lo + hi) / 2
        if composite_hf(pattern_path, mid) < target_hf:
            lo = mid
        else:
            hi = mid
    return hi, composite_hf(pattern_path, hi), True


# A grain is a GRAIN. Above this the pattern stops modulating the plate and becomes the
# plate, which is a different (and worse) widget -- the mask's own shapes start reading as
# the button's geometry. The reference targets are full-strength photographic materials, so
# most are not reachable under this cap; that is intended. What must hold is that the ten
# genres come out DISTINGUISHABLE, which is the actual complaint being fixed.
MAX_AMP = 0.30

# Genres that may legitimately share a pattern, because they share a material.
SHARE_OK = ({"citybuilder", "strategy"},)


def _may_share(a, b):
    return any({a, b} <= grp for grp in SHARE_OK)


def pick(verbose=True):
    scored = score_all()
    refs = reference_targets()
    # A pattern covering >55% of the face stops being a grain; below 3% it is invisible.
    pool = [s for s in scored if 0.03 <= s[3] <= 0.55]
    chosen, taken = {}, {}

    if verbose:
        print(f"{'genre':<13}{'material':<15}{'pattern':<16}{'dir':>7}{'coarse':>8}"
              f"{'tile':>6}{'amp':>7}{'hf':>9}")
    for genre, mat in GENRE_MATERIAL.items():
        t_hf, t_dir, t_coarse = refs[mat]
        # Rank on DIRECTION and coverage only. Feature scale is deliberately NOT ranked on,
        # because tiling fixes it exactly (see `tile` below) while direction cannot be fixed
        # by any transform -- a horizontal grain stays horizontal however often it repeats.
        # Kenney's patterns bottom out at coarse 0.077 while diamond-plate/leather/denim need
        # 0.032-0.043, so without tiling those three could never be matched at all.
        ranked = sorted(pool, key=lambda s: (abs(s[2] - t_dir) * 1.4
                                             + abs(s[3] - 0.22) * 0.3))
        # UNIQUE per genre unless the pair explicitly shares a material.
        name = next((s[0] for s in ranked
                     if s[0] not in taken or _may_share(taken[s[0]], genre)), None)
        if name is None:
            print(f"{genre:<13}{mat:<15}{'NO CANDIDATE':<16}")
            continue
        taken.setdefault(name, genre)
        row = next(s for s in pool if s[0] == name)

        # TILE REPEATS. Coarseness is linear in feature size (verified: a checker of cell C
        # scores exactly C/256), so repeating the pattern k times across the plate divides its
        # coarseness by k. Solving for the reference material's own coarseness makes the tile
        # count derived rather than eyeballed.
        tile = max(1, min(8, round(row[4] / max(t_coarse, 1e-6))))
        eff_coarse = row[4] / tile

        amp, got, reached = solve_amp(os.path.join(PACK, name), t_hf)
        if amp > MAX_AMP:
            amp = MAX_AMP
            got = composite_hf(os.path.join(PACK, name), amp)
            reached = False
        chosen[genre] = (name, round(amp, 3), mat, got, row[2], eff_coarse, tile)
        if verbose:
            flag = "" if reached else f"  (amp capped {MAX_AMP}; ref hf {t_hf:.4f})"
            print(f"{genre:<13}{mat:<15}{name:<16}{row[2]:>7.2f}{eff_coarse:>8.3f}"
                  f"{tile:>6}{amp:>7.3f}{got:>9.4f}{flag}")

    chosen = separate(chosen, pool, refs, verbose)
    if verbose:
        distinctness(chosen)
    return chosen


# Two genres closer than this on (hf, dir, coarse) are not telling themselves apart by
# material. Mirrors measure_material.py's PAIR_MIN, which grades the RENDERED plates -- this
# is the design-time constraint, that is the check on the result.
SEP_MIN = 0.055


def separate(chosen, pool, refs, verbose=True):
    """Repair assignments until no two genres are indistinguishable by material.

    Uniqueness of the FILE is not enough, and assuming it was is what shipped puzzle and
    racing as near-identical plates: their reference materials (graph-paper, brushed-metal)
    both have dir ~= 0, so ranking on direction handed both a large isotropic blob at one
    tile. Different files, same look -- which is precisely the complaint, reproduced inside
    the fix for it.

    So the constraint is on the RESULT: repeatedly take the worst pair and move the genre
    with the weaker material fit to its next candidate.
    """
    # Patterns each genre has already been given. Without this the repair CYCLES: shooter
    # moved 42 -> 59 -> 42 -> 59 forever, because "next candidate that is free and not the
    # current one" oscillates between two once both are better than everything else. Advancing
    # monotonically through the candidate list is what makes the loop terminate.
    tried = {g: {chosen[g][0]} for g in chosen}

    for _ in range(60):
        worst, wd = None, None
        keys = list(chosen)
        for i, a in enumerate(keys):
            for b in keys[i + 1:]:
                if _may_share(a, b):
                    continue
                d = _dist(chosen[a], chosen[b])
                if wd is None or d < wd:
                    worst, wd = (a, b), d
        if worst is None or wd >= SEP_MIN:
            break

        # Move whichever of the pair is further from its own reference material -- the one
        # whose current pattern was the weaker match has least to lose.
        a, b = worst
        move = max(worst, key=lambda g: abs(chosen[g][3] - refs[GENRE_MATERIAL[g]][0]))
        taken = {v[0] for k, v in chosen.items() if k != move}
        t_hf, t_dir, t_coarse = refs[GENRE_MATERIAL[move]]
        ranked = sorted(pool, key=lambda s: (abs(s[2] - t_dir) * 1.4 + abs(s[3] - 0.22) * 0.3))
        cur = chosen[move][0]
        nxt = next((s for s in ranked
                    if s[0] not in taken and s[0] not in tried[move]), None)
        if nxt is None:
            # This genre has exhausted its candidates. Try the OTHER half of the pair before
            # giving up, so one saturated genre does not block the whole repair.
            other = b if move == a else a
            nxt = next((s for s in sorted(
                pool, key=lambda s: (abs(s[2] - refs[GENRE_MATERIAL[other]][1]) * 1.4
                                     + abs(s[3] - 0.22) * 0.3))
                if s[0] not in {v[0] for k, v in chosen.items() if k != other}
                and s[0] not in tried[other]), None)
            if nxt is None:
                if verbose:
                    print(f"  separate: {a} vs {b} at {wd:.4f} — both exhausted, LEAVING AS IS")
                break
            move, t_hf, t_dir, t_coarse = other, *refs[GENRE_MATERIAL[other]]
            cur = chosen[move][0]
        tried[move].add(nxt[0])
        tile = max(1, min(8, round(nxt[4] / max(t_coarse, 1e-6))))
        amp, got, _ = solve_amp(os.path.join(PACK, nxt[0]), t_hf)
        amp = min(amp, MAX_AMP)
        got = composite_hf(os.path.join(PACK, nxt[0]), amp)
        if verbose:
            print(f"  separate: {a} vs {b} at {wd:.4f} < {SEP_MIN} -> "
                  f"{move} moves {cur} -> {nxt[0]}")
        chosen[move] = (nxt[0], round(amp, 3), chosen[move][2], got, nxt[2], nxt[4] / tile, tile)
    return chosen


def _dist(x, y):
    return (((x[3] - y[3]) * 8) ** 2 + (x[4] - y[4]) ** 2 + (x[5] - y[5]) ** 2) ** 0.5


def distinctness(chosen):
    """The real gate: every genre pair must be separable on (hf, dir).

    Matching each reference material exactly is nice-to-have; being TELLABLE APART is the
    requirement. This is the material-axis equivalent of verify_greyscale.py's outline
    column, and it is what gets reported rather than the PASS line.
    """
    print(f"\n{'closest pair per genre':<34}{'dist':>8}")
    keys = list(chosen)
    worst = None
    for i, a in enumerate(keys):
        best = None
        for j, b in enumerate(keys):
            if i == j:
                continue
            _, _, _, hf_a, d_a, c_a, _ = chosen[a]
            _, _, _, hf_b, d_b, c_b, _ = chosen[b]
            # hf spans ~0.005-0.10 while dir and coarse span 0-1, so hf is scaled up to a
            # comparable range before the three axes are combined.
            dist = (((hf_a - hf_b) * 8) ** 2
                    + (d_a - d_b) ** 2
                    + (c_a - c_b) ** 2) ** 0.5
            if best is None or dist < best[1]:
                best = (b, dist)
        shared = chosen[a][0] == chosen[best[0]][0]
        tag = "  (shares pattern, by design)" if shared and _may_share(a, best[0]) else ""
        print(f"{a + ' vs ' + best[0]:<34}{best[1]:>8.3f}{tag}")
        if not shared and (worst is None or best[1] < worst[1]):
            worst = (f"{a} vs {best[0]}", best[1])
    if worst:
        print(f"\nclosest non-sharing pair: {worst[0]} at {worst[1]:.3f}")


def to_alpha_mask(src, dst):
    """Bake the pattern into an ALPHA MASK: RGB stays white, alpha = 1 - luminance.

    This is what lets the grain carry no colour of its own. Drawn with a modulate colour,
    the result darkens the plate only where the pattern was black and leaves it untouched
    where the pattern was white — so the same file reskins with every palette.

    The naive alternative (ship the black/white PNG and draw it modulated) does NOT work:
    standard alpha blending with an opaque texture and a black modulate paints flat black
    across the whole rect, ignoring the pattern entirely.
    """
    im = Image.open(src).convert("L")
    a = np.asarray(im)
    rgba = np.zeros((a.shape[0], a.shape[1], 4), np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = 255 - a
    Image.fromarray(rgba, "RGBA").save(dst)


def install(chosen):
    os.makedirs(DEST, exist_ok=True)
    seen = {}
    for genre, entry in sorted(chosen.items()):
        name, amp, mat = entry[0], entry[1], entry[2]
        if name in seen:
            print(f"  {genre:<13} -> {seen[name]} (shared)")
            continue
        dst_name = f"grain_{os.path.splitext(name)[0]}.png"
        to_alpha_mask(os.path.join(PACK, name), os.path.join(DEST, dst_name))
        seen[name] = dst_name
        print(f"  {genre:<13} -> {dst_name}")
    # Emit the genre table as C# so the shipped constants ARE the measured ones. Hand-copying
    # them is how a "measured" number quietly becomes a stale number.
    rows = []
    for genre in sorted(chosen):
        name, amp, mat, got, d, coarse, tile = chosen[genre]
        rows.append(f'            ["{genre}"] = new("{os.path.splitext(name)[0]}", '
                    f'{amp:.3f}f, {tile}, "{mat}"),')
    gen = os.path.join(REPO, "addons", "beep_game_builder_cs", "ecs", "ui", "kit",
                       "KitGrainTable.cs")
    with open(gen, "w", encoding="utf-8") as f:
        f.write(
            "// <auto-generated>\n"
            "//     GENERATED by tools/genre_shapes/pick_grain.py -- do not edit by hand.\n"
            "//     Regenerate with:  python tools/genre_shapes/pick_grain.py --install\n"
            "//\n"
            "//     Each genre's pattern is chosen by MEASUREMENT against the material its\n"
            "//     reference art is made of (Example_Art/uitexturs.png), on three\n"
            "//     colour- and scale-invariant axes: hf (detail energy), dir (grain\n"
            "//     direction) and coarseness (feature size). Direction drives the choice\n"
            "//     because tiling cannot fix it; tile count is then solved so the feature\n"
            "//     size matches the reference material, and amplitude so the detail energy\n"
            "//     does. Patterns are CC0 (Kenney Pattern Pack); see textures/grain/LICENSE.txt.\n"
            "// </auto-generated>\n"
            "using System.Collections.Generic;\n\n"
            "namespace Beep.ECS.UI.Kit\n{\n"
            "    /// <summary>One genre's grain: which mask, how strongly, how many repeats.</summary>\n"
            "    public readonly record struct KitGrainDef(string Pattern, float Amount,\n"
            "                                              int Tiles, string Material);\n\n"
            "    /// <summary>The measured genre -> grain assignment.</summary>\n"
            "    public static class KitGrainTable\n    {\n"
            "        public static readonly IReadOnlyDictionary<string, KitGrainDef> ByGenre =\n"
            "            new Dictionary<string, KitGrainDef>\n        {\n"
            + "\n".join(rows) + "\n        };\n    }\n}\n")
    print(f"  KitGrainTable.cs written -> {gen}")

    lic = os.path.join(DEST, "LICENSE.txt")
    with open(lic, "w", encoding="utf-8") as f:
        f.write(
            "Grain patterns\n"
            "==============\n\n"
            "Source: Kenney Pattern Pack (https://kenney.nl/assets/pattern-pack)\n"
            "License: CC0 1.0 Universal (public domain dedication)\n\n"
            "Free for personal, educational and commercial use. Written permission is not\n"
            "required; crediting Kenney is voluntary and appreciated.\n\n"
            "These are used as LUMINANCE MASKS by KitGrain -- modulated into the active\n"
            "palette's plate colour rather than drawn as art -- so they carry no colour of\n"
            "their own and reskin with the theme.\n\n"
            "Selected by measurement, not by eye: see tools/genre_shapes/pick_grain.py.\n\n"
            "NOTE: Example_Art/ is licensed Vecteezy reference used for MEASUREMENT ONLY.\n"
            "None of its pixels ship in this addon.\n")
    print(f"  LICENSE.txt written -> {lic}")


if __name__ == "__main__":
    mode = sys.argv[1] if len(sys.argv) > 1 else "--pick"
    if mode == "--score":
        for name, hf, d, cov in sorted(score_all(), key=lambda s: -s[1])[:40]:
            print(f"{name:<20}{hf:>9.4f}{d:>8.2f}{cov:>8.2f}")
    elif mode == "--install":
        install(pick())
    else:
        pick()

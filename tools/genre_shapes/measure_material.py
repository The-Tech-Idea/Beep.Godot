#!/usr/bin/env python3
"""The MATERIAL axis — the third axis, and the one the kit does not have.

WHY THIS EXISTS
---------------
`verify_greyscale.py` measures outline and structure. Both are about SHAPE. But
`Example_Art/uitexturs.png` draws nine tiles that share ONE silhouette — same rounded
square, same corner radius, same drop shadow — and they are unmistakably nine different
things: leather, rubber, glossy leaf, brushed metal, diamond plate, stone, wood plank,
denim, graph paper. `ui5.png` does the same with ten dialog materials at one geometry.

Whatever separates them is neither outline nor hue, so it is measurable in greyscale,
exactly like the existing axes. That measurement is this file, and it is what turns
"add materials" from a description into a gate with a number.

THE METRIC
----------
  hf   mean |laplacian| inside the plate, DIVIDED BY the plate's own mean tone.
       The division is the whole trick: doubling a tile's brightness doubles both the
       detail amplitude and the mean, so the ratio is colour-invariant — the same
       property that lets the outline/structure axes gate a theme-free render.
  dir  ||dx| - |dy|| / (|dx| + |dy|). Planks run one way; most materials do not.

Two axes are required, not one. rubber-dots (0.0572), graph-paper (0.0573) and
glossy-leaf (0.0603) sit within 0.006 of each other on `hf` alone.

REFERENCE VALUES, measured off Example_Art/uitexturs.png:

    stone          0.0086 / 0.11        leather        0.1770 / 0.06
    brushed-metal  0.0368 / 0.03        wood-plank     0.2107 / 0.69
    rubber-dots    0.0572 / 0.01        denim          0.2211 / 0.04
    graph-paper    0.0573 / 0.00        diamond-plate  0.6462 / 0.01
    glossy-leaf    0.0603 / 0.23

    spread 75x.   The kit currently scores a flat fill: no grain, no gradient, no
    pattern anywhere in the 32 widgets (KitLayer.cs:119 -- "Deliberately NO Shade layer").

READ THIS BEFORE TRUSTING A NUMBER OUT OF THIS FILE
---------------------------------------------------
Two earlier versions of this measurement produced clean, confident, WRONG answers:

  1. A fixed fractional crop sampled empty BACKGROUND -- content in gs_rpg.png lives at
     cols 505-645, the crop took x[115:368]. It reported exactly 0.0000 for all ten
     genres and looked like a devastating finding. It measured nothing.
  2. A crop that did find the plate was dominated by the button's LABEL. At 130x45 the
     inset core is ~73x25 and mostly glyph, scoring the kit at 0.59-1.03 -- above
     diamond plate.

So: `--proof` LOCATES the plate rather than assuming it, and REFUSES rather than
reporting when the plate is too small or too uniform to hold a material. It still cannot
subtract a glyph, which is why gating the kit needs a LABEL-FREE proof render first;
until that exists this file measures references honestly and refuses proofs loudly.

Run `--selftest` first. A gate you have only seen pass is not evidence.

USAGE
    python measure_material.py --grid <sheet.png> [rows cols]   # a reference sheet
    python measure_material.py --proof "tmp/kitproof/gs_*.png"  # rendered kit buttons
    python measure_material.py --selftest
"""
import glob
import os
import sys

import numpy as np
from PIL import Image

# A plate whose face is this uniform cannot be carrying a material, and any hf measured
# in it is noise. Below this, --proof reports FLAT rather than a number.
FLAT_HF = 0.004
# Under this many pixels there is not enough face left to distinguish a material from
# compression noise, whatever the number says.
MIN_CORE_PX = 256

NINE = ["leather", "rubber-dots", "glossy-leaf", "brushed-metal", "diamond-plate",
        "stone", "wood-plank", "denim", "graph-paper"]


# Every crop is resampled to this before measuring. See SCALE below -- without it the
# metric silently compares nothing.
NORM_PX = 256


def _normalise(g):
    """Resample a crop to a fixed size so hf is comparable across sources.

    SCALE: the laplacian is a PER-PIXEL difference, so it shrinks as resolution rises --
    neighbouring pixels of the same feature get more similar. Measured directly: one wood
    plank region scored hf 0.0248 on the 1920px JPG and 0.0144 on the 4898px EPS render of
    the SAME artwork, and downsampling the render back to the JPG's size returned 0.0254.
    The metric was consistent at matched scale and meaningless across scales.

    That mattered in practice: the reference tiles (~486px crops) and a rendered kit plate
    (~73x25 crop) are nowhere near the same scale, so comparing a kit number against the
    reference table would have been an apples-to-oranges gate that still printed a
    confident-looking pass or fail.
    """
    if g.shape[0] < 3 or g.shape[1] < 3:
        return None
    return np.asarray(Image.fromarray(g.astype(np.uint8)).resize(
        (NORM_PX, NORM_PX), Image.LANCZOS))


def hf_energy(g):
    """Tone-normalised, scale-normalised high-frequency energy.

    Colour-invariant (divided by the crop's own mean tone) AND scale-invariant (measured at
    a fixed NORM_PX). Both normalisations are required for the number to be a gate rather
    than a coincidence of how big the source happened to be.
    """
    a = _normalise(g)
    if a is None:
        return 0.0
    a = a.astype(np.float64)
    lap = (4 * a[1:-1, 1:-1]
           - a[:-2, 1:-1] - a[2:, 1:-1] - a[1:-1, :-2] - a[1:-1, 2:])
    return float(np.abs(lap).mean() / max(a.mean(), 1.0))


def directionality(g):
    """Grain anisotropy: 0 isotropic, 1 fully one-directional. Scale-normalised for the
    same reason as hf_energy, and additionally because a non-square crop would otherwise
    bias dx against dy purely through aspect ratio."""
    a = _normalise(g)
    if a is None:
        return 0.0
    a = a.astype(np.float64)
    dx = np.abs(np.diff(a, axis=1)).mean() if a.shape[1] > 1 else 0.0
    dy = np.abs(np.diff(a, axis=0)).mean() if a.shape[0] > 1 else 0.0
    t = dx + dy
    return 0.0 if t < 1e-6 else float(abs(dx - dy) / t)


def content_box(a, thresh=200):
    """Bounding box of everything that is not flat background. Found, never assumed."""
    dx = np.abs(np.diff(a.astype(float), axis=1)).sum(axis=0)
    dy = np.abs(np.diff(a.astype(float), axis=0)).sum(axis=1)
    cols, rows = np.nonzero(dx > thresh)[0], np.nonzero(dy > thresh)[0]
    if cols.size == 0 or rows.size == 0:
        return None
    return int(cols.min()), int(rows.min()), int(cols.max()), int(rows.max())


def grid(path, rows, cols, inset=0.22):
    """Tiles of a reference sheet, cropped well inside each plate.

    The inset excludes rim and shadow deliberately: crossing an edge would score the
    SILHOUETTE, which is the axis verify_greyscale.py already owns.
    """
    im = Image.open(path).convert("L")
    W, H = im.size
    tw, th = W / cols, H / rows
    for r in range(rows):
        for c in range(cols):
            yield np.asarray(im.crop((
                int(c * tw + tw * inset), int(r * th + th * inset),
                int(c * tw + tw * (1 - inset)), int(r * th + th * (1 - inset)))))


def report_grid(path, rows, cols):
    names = NINE if rows * cols == 9 else [f"t{i}" for i in range(rows * cols)]
    print(f"{'tile':<16}{'tone':>7}{'hf':>9}{'dir':>8}")
    vals = []
    for name, t in zip(names, grid(path, rows, cols)):
        e = hf_energy(t)
        vals.append(e)
        print(f"{name:<16}{t.mean():>7.0f}{e:>9.4f}{directionality(t):>8.2f}")
    if vals:
        print(f"\nspread: min {min(vals):.4f}  max {max(vals):.4f}  "
              f"ratio {max(vals) / max(min(vals), 1e-6):.1f}x")


def report_proof(pattern):
    print(f"{'genre':<14}{'plate':>10}{'tone':>7}{'hf':>9}{'dir':>8}  note")
    seen = False
    graded = []
    for p in sorted(glob.glob(pattern)):
        seen = True
        a = np.asarray(Image.open(p).convert("L"))
        base = os.path.basename(p)
        name = base.removeprefix("gs_").removeprefix("gm_").removesuffix(".png")
        box = content_box(a)
        if box is None:
            print(f"{name:<14}{'-':>10}{'-':>7}{'-':>9}{'-':>8}  REFUSED: no content found")
            continue
        x0, y0, x1, y1 = box
        iw, ih = x1 - x0, y1 - y0
        core = a[y0 + int(ih * .22):y1 - int(ih * .22),
                 x0 + int(iw * .22):x1 - int(iw * .22)]
        if core.size < MIN_CORE_PX:
            print(f"{name:<14}{f'{iw}x{ih}':>10}{'-':>7}{'-':>9}{'-':>8}  "
                  f"REFUSED: plate too small ({core.size}px)")
            continue
        e, d = hf_energy(core), directionality(core)
        # gm_*.png are the LABEL-FREE plates (KitProofProbe's second pass) and are the only
        # honest input for this axis. gs_*.png carry a "PLAY" glyph that dominates the crop
        # and once scored flat-filled plates ABOVE diamond plate, so they are reported but
        # never graded.
        labelled = not base.startswith("gm_")
        note = ("FLAT — no material" if e < FLAT_HF
                else "includes glyph — NOT gradeable" if labelled else "")
        print(f"{name:<14}{f'{iw}x{ih}':>10}{core.mean():>7.0f}{e:>9.4f}{d:>8.2f}  {note}")
        if not labelled:
            graded.append((name, e, d))
    if not seen:
        print(f"REFUSED: no files matched {pattern!r}")
        return 1
    if not graded:
        print("\nNOT GATED: no gm_*.png (label-free) renders. The material axis cannot be "
              "graded off labelled plates — run tools/genre_shapes/kit_proof.tscn.")
        return 0
    return gate(graded)


# A plate must carry THIS much more detail than a flat fill to count as having a material.
# Set from the reference sheet's own floor: stone, the subtlest of the nine tiles, measures
# 0.0055, so anything below half of that is indistinguishable from no material at all.
MATERIAL_MIN = 0.0028
# Two genres closer than this on (hf, dir) are not telling themselves apart by material.
PAIR_MIN = 0.010


def gate(graded):
    """The material axis as a GATE, not a description.

    Two requirements, reported separately because they fail for different reasons:
      1. every genre must actually HAVE a material (vs a flat fill)
      2. no two genres may be indistinguishable BY that material
    """
    print(f"\n{'genre':<14}{'hf':>9}  material present (>= " f"{MATERIAL_MIN:.4f})")
    flat = [n for n, e, _ in graded if e < MATERIAL_MIN]
    for n, e, _ in graded:
        print(f"{n:<14}{e:>9.4f}  {'FLAT' if e < MATERIAL_MIN else 'ok'}")

    print(f"\n{'closest pair':<30}{'dist':>8}")
    worst = None
    for i, (a, ea, da) in enumerate(graded):
        best = None
        for j, (b, eb, db) in enumerate(graded):
            if i == j:
                continue
            dist = (((ea - eb) * 8) ** 2 + (da - db) ** 2) ** 0.5
            if best is None or dist < best[1]:
                best = (b, dist)
        print(f"{a + ' vs ' + best[0]:<30}{best[1]:>8.4f}")
        if worst is None or best[1] < worst[1]:
            worst = (f"{a} vs {best[0]}", best[1])

    ok = True
    if flat:
        ok = False
        print(f"\nFAIL: {len(flat)} genre(s) render FLAT — no material: {', '.join(flat)}")
    if worst and worst[1] < PAIR_MIN:
        ok = False
        print(f"FAIL: closest pair {worst[0]} at {worst[1]:.4f} < {PAIR_MIN}")
    elif worst:
        print(f"\nclosest pair: {worst[0]} at {worst[1]:.4f} (bar {PAIR_MIN})")
    print("\nMATERIAL", "PASS" if ok else "FAIL")
    return 0 if ok else 1


def selftest():
    """Synthesise known-flat and known-textured plates and require the metric to say so.

    Also proves the colour-invariance claim rather than asserting it: the same grain at
    half brightness must land within 5% of itself.
    """
    rng = np.random.default_rng(7)
    h = w = 128
    ok = True

    flat = np.full((h, w), 120, np.uint8)
    e = hf_energy(flat)
    good = e < FLAT_HF
    ok &= good
    print(f"[{'ok ' if good else 'FAIL'}] flat plate            hf={e:.4f} (want < {FLAT_HF})")

    planks = np.full((h, w), 120, np.float64)
    planks[:, ::8] = 70                       # vertical seams only
    e, d = hf_energy(planks.astype(np.uint8)), directionality(planks.astype(np.uint8))
    good = e > 0.02 and d > 0.5
    ok &= good
    print(f"[{'ok ' if good else 'FAIL'}] vertical planks       hf={e:.4f} dir={d:.2f} "
          f"(want hf > 0.02, dir > 0.5)")

    grain = np.clip(120 + rng.normal(0, 18, (h, w)), 0, 255)
    e_full = hf_energy(grain.astype(np.uint8))
    d_iso = directionality(grain.astype(np.uint8))
    good = e_full > 0.02 and d_iso < 0.2
    ok &= good
    print(f"[{'ok ' if good else 'FAIL'}] isotropic grain       hf={e_full:.4f} dir={d_iso:.2f} "
          f"(want hf > 0.02, dir < 0.2)")

    e_half = hf_energy((grain * 0.5).astype(np.uint8))
    drift = abs(e_half - e_full) / e_full
    good = drift < 0.05
    ok &= good
    print(f"[{'ok ' if good else 'FAIL'}] same grain at 50% lum hf={e_half:.4f} "
          f"drift={drift * 100:.1f}% (want < 5% — this is the colour-invariance claim)")

    # SCALE INVARIANCE. This is the check whose absence made the first reference table
    # incomparable with anything: the same planks at 4x resolution must score the same.
    big = np.asarray(Image.fromarray(planks.astype(np.uint8)).resize(
        (w * 4, h * 4), Image.LANCZOS))
    e_big, e_sm = hf_energy(big), hf_energy(planks.astype(np.uint8))
    sdrift = abs(e_big - e_sm) / max(e_sm, 1e-9)
    good = sdrift < 0.10
    ok &= good
    print(f"[{'ok ' if good else 'FAIL'}] same planks at 4x res  hf={e_big:.4f} vs {e_sm:.4f} "
          f"drift={sdrift * 100:.1f}% (want < 10% — the scale-invariance claim)")

    d_big, d_sm = directionality(big), directionality(planks.astype(np.uint8))
    good = abs(d_big - d_sm) < 0.10
    ok &= good
    print(f"[{'ok ' if good else 'FAIL'}] dir at 4x res          {d_big:.2f} vs {d_sm:.2f}")

    print("\nSELFTEST", "PASS" if ok else "FAIL")
    return 0 if ok else 1


if __name__ == "__main__":
    if len(sys.argv) < 2 or sys.argv[1] == "--selftest":
        sys.exit(selftest())
    if sys.argv[1] == "--grid":
        r, c = (int(sys.argv[3]), int(sys.argv[4])) if len(sys.argv) > 4 else (3, 3)
        report_grid(sys.argv[2], r, c)
        sys.exit(0)
    if sys.argv[1] == "--proof":
        sys.exit(report_proof(sys.argv[2]))
    print(__doc__)
    sys.exit(2)

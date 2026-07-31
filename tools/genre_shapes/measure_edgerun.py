#!/usr/bin/env python3
"""Assert a declared EDGE RUN is drawn, and drawn in SEVERAL PIECES.

The sci-fi frame (art pass files 14, 43) is a run list per edge: the stroke changes weight,
BREAKS and restarts, and no two corners match. Two failures are silent:

  * the run never reaches the renderer -- the widget draws an ordinary border, which in a
    screenshot reads as "the frame is subtle";
  * the run draws as ONE unbroken piece -- a border with decoration, i.e. the thing the
    edge-run model exists to replace.

HOW IT MEASURES, AND WHY NOT THE OBVIOUS WAY
--------------------------------------------
The first version scanned a fixed row just inside the widget and counted marked stretches. That
broke the moment the run began following a SHEARED silhouette: a sheared frame is diagonal, so it
stops crossing the scan line and a perfectly good run measured as "not broken" -- both declared
genres regressed to 1,1,1,1 the instant the renderer got MORE correct.

So it differences a render with the run against one without (KitEdge.Enabled), leaving only the
frame, and counts connected components. Immune to shear, silhouette and shadow alike, and it
counts the frame's pieces directly: an unbroken border is one piece, a run with gaps is several.

    python measure_edgerun.py --proof "../../tmp/shadow/pol_*.png" --expect shooter,racing
"""
import glob
import os
import sys

import numpy as np
from PIL import Image


def components(mask):
    """Connected components in a boolean mask, 4-neighbour, iterative flood fill."""
    seen = np.zeros_like(mask, dtype=bool)
    h, w = mask.shape
    n = 0
    for sy, sx in zip(*np.nonzero(mask)):
        if seen[sy, sx]:
            continue
        n += 1
        stack = [(int(sy), int(sx))]
        seen[sy, sx] = True
        while stack:
            y, x = stack.pop()
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                ny, nx = y + dy, x + dx
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    stack.append((ny, nx))
    return n


def strip(name):
    for pre in ("pol_", "noe_", "nos_", "sh_", "gs_", "gm_"):
        name = name.removeprefix(pre)
    return name.removesuffix(".png")


def analyse(path):
    off = os.path.join(os.path.dirname(path),
                       os.path.basename(path).replace("pol_", "noe_", 1))
    if not os.path.isfile(off):
        raise SystemExit(f"REFUSED: {path} has no run-off pair at {off}. "
                         "Render tools/genre_shapes/shadow_probe.tscn.")
    on = np.asarray(Image.open(path).convert("L")).astype(int)
    no = np.asarray(Image.open(off).convert("L")).astype(int)
    if on.shape != no.shape:
        return None
    diff = np.abs(on - no) > 8
    if diff.sum() < 12:
        return dict(pixels=0, pieces=0)
    return dict(pixels=int(diff.sum()), pieces=components(diff))


def main(pattern, expect):
    files = sorted(glob.glob(pattern))
    if not files:
        print(f"REFUSED: no files matched {pattern!r}")
        return 1
    print(f"{'genre':<13}{'frame px':>10}{'pieces':>8}   verdict")
    bad = 0
    for p in files:
        name = strip(os.path.basename(p))
        r = analyse(p)
        if r is None:
            print(f"{name:<13}{'-':>10}{'-':>8}   unreadable")
            continue

        want = name in expect
        if want:
            ok = r["pixels"] > 0 and r["pieces"] > 1
            why = "" if ok else (" <-- NOTHING DRAWN" if r["pixels"] == 0 else " <-- ONE PIECE")
        else:
            # Negative control. Because this differences run-on against run-off, a discontinuous
            # SILHOUETTE (Spiked, Torn) cancels out -- the confusion between a broken shape and a
            # broken stroke that the scan-based version suffered cannot arise here.
            ok = r["pixels"] == 0
            why = "" if ok else " <-- unexpected frame"
        if not ok:
            bad += 1
        print(f"{name:<13}{r['pixels']:>10}{r['pieces']:>8}   "
              f"{'run' if want else 'plain':<6}{why}")

    print(f"\nEDGERUN {'PASS' if bad == 0 else f'FAIL ({bad})'}")
    return 0 if bad == 0 else 1


if __name__ == "__main__":
    argv = sys.argv[1:]
    exp = set()
    if "--expect" in argv:
        i = argv.index("--expect")
        exp = set(argv[i + 1].split(","))
        del argv[i:i + 2]
    if argv and argv[0] == "--proof":
        sys.exit(main(argv[1], exp))
    print(__doc__)
    sys.exit(2)

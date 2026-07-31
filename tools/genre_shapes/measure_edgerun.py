#!/usr/bin/env python3
"""Assert a declared EDGE RUN is actually drawn, and is asymmetric.

The sci-fi frame (art pass files 14, 43) is a run list per edge: the stroke changes weight,
BREAKS and restarts, and no two corners match. Two things can go wrong and both are silent:

  * the run never reaches the renderer -- the widget draws an ordinary unbroken border, which
    from a screenshot looks like "the frame is subtle";
  * the run is drawn symmetrically -- which is a border with decoration, i.e. the thing the
    edge-run model exists to replace.

So this counts BREAKS along each edge of a rendered widget and requires the declared genres to
show more than one run per edge, and to differ between opposite edges.

    python measure_edgerun.py --proof "../../tmp/shadow/nos_*.png" --expect shooter,racing
"""
import glob
import os
import sys

import numpy as np
from PIL import Image


def runs_along(line, bg, tol=14):
    """Number of contiguous marked stretches in a 1-D scan."""
    marked = np.abs(line.astype(int) - bg) > tol
    if marked.size == 0:
        return 0
    return int(np.count_nonzero(marked[1:] & ~marked[:-1]) + (1 if marked[0] else 0))


def analyse(path):
    a = np.asarray(Image.open(path).convert("L")).astype(int)
    bg = int(np.median(a[:4]))
    m = np.abs(a - bg) > 18
    ys, xs = np.nonzero(m)
    if ys.size < 64:
        return None
    y0, y1, x0, x1 = ys.min(), ys.max(), xs.min(), xs.max()

    # Scan just INSIDE each edge, where the run is stroked. One row in from the boundary so the
    # widget's own anti-aliased outline is not counted as a run.
    top = runs_along(a[y0 + 1, x0 + 2:x1 - 1], bg)
    bottom = runs_along(a[y1 - 1, x0 + 2:x1 - 1], bg)
    left = runs_along(a[y0 + 2:y1 - 1, x0 + 1], bg)
    right = runs_along(a[y0 + 2:y1 - 1, x1 - 1], bg)
    return dict(top=top, right=right, bottom=bottom, left=left)


def main(pattern, expect):
    files = sorted(glob.glob(pattern))
    if not files:
        print(f"REFUSED: no files matched {pattern!r}")
        return 1
    print(f"{'genre':<13}{'top':>5}{'right':>7}{'bottom':>8}{'left':>6}   verdict")
    bad = 0
    for p in files:
        name = os.path.basename(p)
        for pre in ("nos_", "sh_", "gs_", "gm_"):
            name = name.removeprefix(pre)
        name = name.removesuffix(".png")
        r = analyse(p)
        if r is None:
            print(f"{name:<13}{'-':>5}{'-':>7}{'-':>8}{'-':>6}   no widget")
            continue

        want_run = name in expect
        broken = max(r.values()) > 1
        asymmetric = (r["top"] != r["bottom"]) or (r["left"] != r["right"])
        if want_run:
            ok = broken and asymmetric
            why = "" if ok else (" <-- NOT BROKEN" if not broken else " <-- SYMMETRIC")
        else:
            # A genre with no declared run must NOT show one: this is the negative control.
            ok = not broken
            why = "" if ok else " <-- unexpected run"
        if not ok:
            bad += 1
        print(f"{name:<13}{r['top']:>5}{r['right']:>7}{r['bottom']:>8}{r['left']:>6}   "
              f"{'run' if want_run else 'plain':<6}{why}")

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

"""Gate for KitRegister.Pixel: is the corner a STAIRCASE or an ARC?

The sweep proves a theme selects the register. This proves the register reaches the renderer.

METRIC -- edge mobility. Walk the rows of the top-left corner region and record, for each row, the
first column where the image stops being background. An ARC moves that column on nearly every row;
a STAIRCASE holds it for `pixel_size` rows and then jumps. So:

    mobility = distinct edge columns / rows sampled

An arc approaches 1.0. A staircase quantised to N screen px approaches 1/N. The measure is
scale-free (a ratio), colour-free (it works off "differs from the background corner pixel", not off
any particular hue), and it cannot be satisfied by simply drawing the plate darker.

SELFTEST builds a synthetic arc and a synthetic staircase and asserts the metric separates them,
because a metric that has only ever been run on real renders has never been shown to be able to
fail.
"""
import sys, pathlib
import numpy as np
from PIL import Image

OUT = pathlib.Path("tmp/pixelproof")
# Set from the SYNTHETICS, not fitted to the renders: a true arc measures 0.70 (it goes
# near-vertical at its base, so consecutive rows share a column -- it is not 1.0 and expecting
# that was wrong), a 3-step staircase measures 0.03. 0.40 sits between them with room for the
# anti-aliased first and last row of a real render.
THRESH = 0.40


def mobility(a, box=90):
    """Edge mobility over the top-left corner of the widget in image `a` (2-D luminance)."""
    bg = a[2, 2]                                     # a corner pixel is always background
    mask = np.abs(a.astype(int) - int(bg)) > 12
    ys, xs = np.nonzero(mask)
    if len(ys) < 100:
        return None, "no widget found"
    y0, x0 = ys.min(), xs.min()

    cols = []
    for y in range(y0, min(y0 + box, a.shape[0])):
        row = np.nonzero(mask[y])[0]
        if len(row) == 0:
            continue
        # Only the rows where the corner is still being constructed: once the edge reaches the
        # widget's left side it is a straight vertical run and every construction looks the same.
        if row[0] <= x0 + 1:
            break
        cols.append(int(row[0]))
    if len(cols) < 8:
        return None, f"corner too small to measure ({len(cols)} rows)"
    return len(set(cols)) / len(cols), f"{len(set(cols))} distinct cols / {len(cols)} rows"


def levels(a):
    """Distinct luminance values inside the widget. The register's ANTI-ALIASING promise.

    A pixel surface is a handful of flat colours; an arc-cornered, gloss-banded one is a smear.
    This is the metric that separated the two pixel themes when mobility could not: topdown/classic
    rendered 3 levels while platformer/pixel8bit rendered 67, both claiming the same register --
    and the second one was wrong.
    """
    bg = a[2, 2]
    m = np.abs(a.astype(int) - int(bg)) > 12
    ys, xs = np.nonzero(m)
    if len(ys) < 100:
        return None
    inner = a[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    return int(len(np.unique(inner)))


def selftest():
    ok = True
    h = 120
    # Top-left corner arc: the edge starts at x=r on the first row and reaches x=0 at y=r.
    # (Written the other way round first, which put the edge at x=0 on row 0 -- the walk then
    # terminated on its own first row and reported "corner too small". The metric was fine; the
    # fixture was upside down. Worth the note: a gate that fails on its own selftest is doing its
    # job, and the first guess about why is not automatically right.)
    r = 90.0
    arc = np.full((200, 200), 107, np.uint8)
    for y in range(200):
        x = int(round(r - (r * r - (r - y) ** 2) ** 0.5)) if y < r else 0
        arc[y, x:] = 30
    m, why = mobility(arc)
    good = m is not None and m > 0.60
    print(f"[{'ok ' if good else 'FAIL'}] synthetic arc        mobility={m} ({why}) want > 0.60")
    ok &= good

    # staircase: 3 steps of 30px, quantised
    st = np.full((200, 200), 107, np.uint8)
    for y in range(200):
        step = min(y // 30, 3)
        x = (3 - step) * 30 if y < 120 else 0
        st[y, x:] = 30
    m, why = mobility(st)
    good = m is not None and m < 0.30
    print(f"[{'ok ' if good else 'FAIL'}] synthetic staircase  mobility={m} ({why}) want < 0.30")
    ok &= good

    # levels: a flat two-tone plate against a smoothly shaded one
    flat = np.full((200, 200), 107, np.uint8)
    flat[40:160, 40:160] = 30
    flat[44:156, 44:156] = 70
    grad = np.full((200, 200), 107, np.uint8)
    for y in range(40, 160):
        grad[y, 40:160] = 30 + (y - 40)
    for name, img, bound, flatter in (("flat plate", flat, 12, True),
                                      ("shaded plate", grad, 20, False)):
        n = levels(img)
        good = n is not None and ((n <= bound) if flatter else (n >= bound))
        print(f"[{'ok ' if good else 'FAIL'}] synthetic {name:<12} levels={n} "
              f"want {'<=' if flatter else '>='} {bound}")
        ok &= good

    print("\nSELFTEST " + ("PASS" if ok else "FAIL"))
    return ok


def main():
    if "--selftest" in sys.argv:
        sys.exit(0 if selftest() else 1)

    if not OUT.exists():
        print(f"PIXEL FAIL: {OUT} missing -- run tools/genre_shapes/pixel_probe.tscn first")
        sys.exit(2)

    bad = 0

    # ── ANTI-ALIASING: applies to every pixel theme, whatever its base silhouette ──
    for f in sorted(OUT.glob("*.png")):
        n = levels(np.asarray(Image.open(f).convert("L")))
        px = f.stem.startswith("px_")
        good = n is not None and ((n <= 12) if px else (n >= 20))
        print(f"[{'ok ' if good else 'FAIL'}] {f.stem:<16} levels={str(n):<5}"
              f"want {'<= 12 (flat)' if px else '>= 20 (shaded)'}")
        if not good:
            bad += 1

    # ── CORNER CONSTRUCTION: only where the genre's base shape is actually round ──
    #
    # topdown is deliberately NOT tested here. Its base silhouette is KitShape.Stepped for EVERY
    # theme (KitMaterial.ShapeForGenre), so both members of the pair are staircases and the
    # comparison cannot separate the register from the genre. The first version of this gate
    # measured it anyway and reported rr_topdown as a failed "arc" -- the CONTROL was invalid, not
    # the code. A pair that cannot distinguish the thing under test does not belong in a gate.
    pair = {}
    for tag in ("px_platformer", "rr_platformer"):
        p = OUT / f"{tag}.png"
        if not p.exists():
            continue
        m, why = mobility(np.asarray(Image.open(p).convert("L")))
        pair[tag] = m
        want = "staircase" if tag.startswith("px_") else "arc"
        good = m is not None and ((m < THRESH) if want == "staircase" else (m >= THRESH))
        shown = "None" if m is None else f"{m:.2f}"
        print(f"[{'ok ' if good else 'FAIL'}] {tag:<16} mobility={shown} ({why}) want {want}")
        if not good:
            bad += 1
    if len(pair) == 2 and all(v is not None for v in pair.values()):
        gap = pair["rr_platformer"] - pair["px_platformer"]
        good = gap > 0.30
        print(f"[{'ok ' if good else 'FAIL'}] platformer       separation={gap:.2f} (want > 0.30)")
        if not good:
            bad += 1

    print("\nPIXEL " + ("PASS" if bad == 0 else f"FAIL ({bad})"))
    sys.exit(0 if bad == 0 else 1)


main()

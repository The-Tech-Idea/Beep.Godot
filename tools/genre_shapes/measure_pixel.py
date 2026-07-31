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
    # RESOLUTION REQUIREMENT, not a threshold on the answer. A 3-step staircase needs at least a
    # few rows to be visible at all; 8 was arbitrary and, once corners became UNIT-based (a
    # constant ~12px instead of a proportion of the widget), it rejected a corner the metric can
    # read perfectly well.
    if len(cols) < 5:
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
    seen_levels = {}

    # ── ANTI-ALIASING: applies to every pixel theme, whatever its base silhouette ──
    # ONLY this gate's own renders. The directory is shared with the gloss proof (gl_*), and
    # globbing "*.png" graded those too -- a Linear-gloss plate is deliberately flat, so it failed
    # an anti-aliasing check it was never the subject of.
    for f in sorted(list(OUT.glob("px_*.png")) + list(OUT.glob("sh_*.png"))):
        n = levels(np.asarray(Image.open(f).convert("L")))
        px = f.stem.startswith("px_")
        seen_levels[f.stem] = n
        good = n is not None and (n <= 12 if px else n > 12)
        print(f"[{'ok ' if good else 'FAIL'}] {f.stem:<16} levels={str(n):<5}"
              f"want {'<= 12 (flat)' if px else '> 12 (shaded)'}")
        if not good:
            bad += 1

    # The claim is a SEPARATION, not an absolute count. Stated as `>= 20` it was really a
    # statement about how much banding the renderer happened to produce -- and the shape-layer
    # restructure legitimately reduced that (clipped shade bands and a polyline bevel make fewer
    # distinct levels than stacked rects and per-edge lines), dropping the controls to 18-19. The
    # thing being tested is that a pixel theme is FLAT RELATIVE to a non-pixel one, which is what
    # this asserts and what survives a change in how the non-pixel path draws.
    for genre in ("platformer", "topdown"):
        px_n, sh_n = seen_levels.get(f"px_{genre}"), seen_levels.get(f"sh_{genre}")
        if px_n is None or sh_n is None:
            continue
        good = sh_n >= px_n * 3
        print(f"[{'ok ' if good else 'FAIL'}] {genre:<16} flatness  px={px_n} vs sh={sh_n} "
              f"(want sh >= 3x px)")
        if not good:
            bad += 1

    # ── CORNER CONSTRUCTION: only where the genre's base shape is actually round ──
    #
    # topdown is deliberately NOT tested here. Its base silhouette is KitShape.Stepped for EVERY
    # theme (KitMaterial.ShapeForGenre), so both members of the pair are staircases and the
    # comparison cannot separate the register from the genre. The first version of this gate
    # measured it anyway and reported rr_topdown as a failed "arc" -- the CONTROL was invalid, not
    # the code. A pair that cannot distinguish the thing under test does not belong in a gate.
    # The arc control is platformer/MODERN, not /cartoon. cartoon carries wobble 0.008, and a
    # wavering edge never settles onto the widget's leftmost column -- the walk then runs its full
    # window and measures the WOBBLE (0.19, indistinguishable from a staircase) instead of the
    # corner. A control has to hold everything constant except the thing under test.
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

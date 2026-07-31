"""Gate for KitGloss: is the upper-face highlight Linear, HardBand or CurvedGlass?

Declaring the enum proves nothing -- the sweep already shows all three are selected by some theme.
This measures the RENDER, on the two properties the three constructions actually differ by.

For each column of the plate, find the row of the strongest DOWNWARD luminance step in the upper
part of the face: that is the highlight's lower boundary. Then:

  strength  = how sharp the darkest downward step is inside the BAND WINDOW (0.18-0.34 of the
              plate). A band has a hard edge there by construction; the soft inset sheen has its
              boundary lower AND at about a twentieth of the alpha, so it barely steps at all.
              Measuring "depth of the strongest step anywhere" instead found the BEVEL for the
              Linear theme -- at 0.16 x Gloss x 0.35 the Casual sheen is ~5% alpha, fainter than
              the bevel highlight above it. The metric was reading a real edge, just not this one.

  curvature = (centre boundary - mean of the boundaries at 12% and 88%) / plate height
              CurvedGlass is convex by design (0.55 * 0.26 = 0.14); HardBand is a straight line, 0

Both are fractions of the plate, so neither depends on the render resolution, and both are read off
a luminance step rather than any particular colour.

SELFTEST synthesises the three boundaries and asserts the metrics separate them, because a metric
that has only ever been run on real renders has never been shown to be able to fail.
"""
import sys, pathlib
import numpy as np
from PIL import Image

OUT = pathlib.Path("tmp/pixelproof")


def boundary(a):
    """(depth, curvature, plate height) for the highlight's lower edge, or None."""
    bg = a[2, 2]
    mask = np.abs(a.astype(int) - int(bg)) > 12
    ys, xs = np.nonzero(mask)
    if len(ys) < 500:
        return None
    y0, y1, x0, x1 = ys.min(), ys.max(), xs.min(), xs.max()
    h, w = y1 - y0 + 1, x1 - x0 + 1
    if h < 40 or w < 40:
        return None

    # Search the face between 8% and 60% of the height. Below 8% is the rim and the bevel's own
    # highlight; past 60% is the face shade, which is a gradient rather than a boundary.
    lo, hi = y0 + int(h * 0.08), y0 + int(h * 0.60)
    face = a[lo:hi, x0:x1 + 1].astype(int)
    if face.shape[0] < 6:
        return None
    step = face[:-1] - face[1:]                     # positive = gets darker going down

    def edge_at(frac):
        # Average a few columns: a single column can land on a stud, a glyph or a corner.
        c = int(w * frac)
        band = step[:, max(0, c - 3):c + 4]
        if band.size == 0:
            return None, 0.0
        prof = band.mean(axis=1)
        return lo + int(np.argmax(prof)), float(prof.max())

    # The BAND WINDOW. It has to span the whole range a banded gloss can occupy: HardBand sits
    # flat at 0.26, but CurvedGlass sweeps from 0.26 at the centre up to 0.26 x 0.45 = 0.117 at the
    # ends. A window of 0.18-0.34 clipped those ends and halved the measured curvature (0.07
    # against a true 0.14) -- the curve was fine, the window was too narrow to see it.
    #
    # The upper bound still excludes the soft sheen's boundary at 0.44, and the lower bound still
    # excludes the bevel highlight at ~0.08, which is what the window is for.
    wlo, whi = int(h * 0.11) - int(h * 0.08), int(h * 0.34) - int(h * 0.08)
    win = step[max(0, wlo):max(1, whi)]

    def in_window(frac):
        c = int(w * frac)
        b = win[:, max(0, c - 3):c + 4]
        if b.size == 0:
            return None, 0.0
        prof = b.mean(axis=1)
        return lo + max(0, wlo) + int(np.argmax(prof)), float(prof.max())

    mid, strength = in_window(0.50)
    if mid is None:
        return None
    ends = [e for e, _ in (in_window(0.12), in_window(0.88)) if e is not None]
    if not ends:
        return None
    depth = (mid - y0) / h
    curvature = (mid - sum(ends) / len(ends)) / h
    return depth, curvature, h, strength


def selftest():
    ok = True

    def synth(kind):
        a = np.full((300, 400), 107, np.uint8)
        a[50:250, 60:340] = 120                     # the plate
        for x in range(60, 340):
            t = (x - 60) / 279
            if kind == "hard":
                yb = 50 + int(200 * 0.26)
            elif kind == "curved":
                yb = 50 + int(200 * 0.26 * (0.45 + 0.55 * np.sin(np.pi * t)))
            else:                                   # inset soft sheen, lower edge at 0.44
                if t < 0.07 or t > 0.93:
                    continue
                yb = 50 + int(200 * 0.44)
            a[50:yb, x] = 190                       # the highlight
        return a

    for kind, want_depth, want_curve in (("hard", 0.26, 0.0), ("curved", 0.26, 0.14)):
        r = boundary(synth(kind))
        if r is None:
            print(f"[FAIL] synthetic {kind:<7} no boundary found")
            ok = False
            continue
        d, c, _, _ = r
        good = abs(d - want_depth) < 0.06 and abs(c - want_curve) < 0.06
        print(f"[{'ok ' if good else 'FAIL'}] synthetic {kind:<7} depth={d:.2f} (want {want_depth}) "
              f"curvature={c:.2f} (want {want_curve})")
        ok &= good

    print("\nSELFTEST " + ("PASS" if ok else "FAIL"))
    return ok


def main():
    if "--selftest" in sys.argv:
        sys.exit(0 if selftest() else 1)
    if not OUT.exists():
        print(f"GLOSS FAIL: {OUT} missing -- run tools/genre_shapes/pixel_probe.tscn first")
        sys.exit(2)

    bad = 0
    got = {}
    for tag in ("gl_linear", "gl_hard", "gl_curved"):
        p = OUT / f"{tag}.png"
        if not p.exists():
            print(f"[FAIL] {tag:<12} render missing")
            bad += 1
            continue
        r = boundary(np.asarray(Image.open(p).convert("L")))
        if r is None:
            print(f"[FAIL] {tag:<12} no highlight boundary found")
            bad += 1
            continue
        got[tag] = r
        print(f"[   ] {tag:<12} depth={r[0]:.2f} curvature={r[1]:+.2f} "
              f"strength={r[3]:.1f} (plate {r[2]}px)")

    if len(got) == 3:
        # BANDED sits high on the face; the soft inset sheen sits far lower. Asserted as a
        # SEPARATION rather than absolute values, so the check survives a change to either
        # construction's exact proportions.
        hs, ls = got["gl_hard"][3], got["gl_linear"][3]
        good = hs > ls * 2.0 and hs > 6.0
        print(f"[{'ok ' if good else 'FAIL'}] band vs sheen  strength={hs:.1f} vs {ls:.1f} "
              f"(want band > 2x sheen and > 6)")
        bad += 0 if good else 1

        # CURVED must actually bow. A straight band is 0; anything that reads as glass is well
        # clear of it.
        cc, hc = got["gl_curved"][1], got["gl_hard"][1]
        good = cc - hc > 0.04
        print(f"[{'ok ' if good else 'FAIL'}] curved vs hard curvature={cc:+.2f} vs {hc:+.2f} "
              f"(want > +0.04 apart)")
        bad += 0 if good else 1

    print("\nGLOSS " + ("PASS" if bad == 0 else f"FAIL ({bad})"))
    sys.exit(0 if bad == 0 else 1)


main()

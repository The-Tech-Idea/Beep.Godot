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

    # Search the face from 2% of the height. It was 8%, which existed to skip the rim and the
    # bevel highlight -- correct when those were fractions of the widget, wrong now that they are
    # UNIT multiples and sit within about 2px of the top. On a 262px plate an 8% floor starts at
    # row 16 and a unit-sized band's ENDS are at row 9, so the search began below the thing it was
    # looking for and reported the curve backwards. Dropping it to 2% then let the RIM dominate --
    # a much stronger step than the band -- and all three constructions collapsed onto it. The
    # floor has to sit BETWEEN the edge stack (~5px, unit-based) and the band's shallow end, hence
    # an absolute 9px minimum rather than a pure fraction.
    lo, hi = y0 + max(9, int(h * 0.035)), y0 + int(h * 0.60)
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

    # The BAND WINDOW, in fractions of the plate.
    #
    # It has moved TWICE, both times because the thing it is looking for moved. It began at
    # 0.18-0.34 when the band was `plate height * 0.26`; widening it to 0.11-0.34 was needed
    # because CurvedGlass sweeps UP toward its ends and the narrow window clipped them, halving
    # the measured curvature. Then the band became UNIT-based (~1.6 units, about 26px), so on a
    # 262px plate it sits at 0.04-0.10 -- entirely ABOVE a window whose floor was 0.11, and the
    # metric measured whatever else it found and reported the curve backwards.
    #
    # 0.02-0.20 covers a unit-sized band on a large plate. The floor still clears the carved edge
    # stack, which is now within about 2px of the top.
    wlo, whi = 0, int(h * 0.20) - max(9, int(h * 0.035))
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
    # 5% / 95%, not 12% / 88%. A curved band's bow is a sine over the FULL width, so sampling
    # near the middle measures almost none of it -- between 35% and 65% the boundary moves 1.5px.
    # The ends have to be genuinely near the edges, which is why the host must be a flat-topped
    # shape rather than a stadium whose "ends" are inside the round cap.
    ends = [e for e, _ in (in_window(0.05), in_window(0.95)) if e is not None]
    if not ends:
        return None
    depth = (mid - y0) / h
    curvature = (mid - sum(ends) / len(ends)) / h
    return depth, curvature, h, strength


def selftest():
    ok = True

    def synth(kind):
        # Geometry matched to what the renderer now produces: a UNIT-sized band, about a tenth of
        # a large plate, not the old quarter.
        band = 0.10
        a = np.full((300, 400), 107, np.uint8)
        a[50:250, 60:340] = 120                     # the plate
        for x in range(60, 340):
            t = (x - 60) / 279
            if kind == "hard":
                yb = 50 + int(200 * band)
            elif kind == "curved":
                yb = 50 + int(200 * band * (0.62 + 0.38 * np.sin(np.pi * t)))
            else:                                   # the soft inset sheen: lower AND far fainter
                if t < 0.07 or t > 0.93:
                    continue
                yb = 50 + int(200 * band * 1.3)
            a[50:yb, x] = 190 if kind != "linear" else 132
        return a

    for kind, want_depth, want_curve in (("hard", 0.10, 0.0), ("curved", 0.10, 0.038)):
        r = boundary(synth(kind))
        if r is None:
            print(f"[FAIL] synthetic {kind:<7} no boundary found")
            ok = False
            continue
        d, c, _, _ = r
        good = abs(d - want_depth) < 0.04 and abs(c - want_curve) < 0.03
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
        good = cc - hc > 0.02
        print(f"[{'ok ' if good else 'FAIL'}] curved vs hard curvature={cc:+.2f} vs {hc:+.2f} "
              f"(want > +0.02 apart)")
        bad += 0 if good else 1

    print("\nGLOSS " + ("PASS" if bad == 0 else f"FAIL ({bad})"))
    sys.exit(0 if bad == 0 else 1)


main()

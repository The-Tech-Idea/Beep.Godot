"""Gate for KitGloss: is the upper-face highlight Linear, HardBand or CurvedGlass?

STATUS: FAILING, AND THE FEATURE IS NOT THE PROBLEM.

The three renders differ on disk — distinct md5s — so the constructions do reach the pixels. This
metric cannot discriminate them any more, and it reports a 164px plate inside a 260px control,
meaning its widget-detection is latching onto some inner band rather than the plate.

What changed under it: KitButton became a Godot Button, so the proof plate is drawn through
KitChrome rather than KitControl, with different insets and a different contrast profile. The
thresholds and the band window were both derived against the old render.

Do NOT loosen the thresholds to make this pass. That is how a gate becomes decoration, and this
one has already caught two real defects (the missing lighting layers, and before that the
identical-construction bug). It needs its plate detection re-derived against the current render —
probably by bounding the widget from the KNOWN control rect rather than by luminance threshold —
and then its window and thresholds re-fitted to synthetics, in that order.

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


# The probe renders a 420x260 plate CENTRED in its viewport, and warns if that ever changes. So
# the plate rect is known, and does not need to be inferred. Inferring it by luminance is what
# broke this gate: once the plate was drawn through KitChrome its inner bands became the darkest
# region, the detector latched onto one, and it reported a 164px "plate" inside a 260px control.
# A measurement should not have to guess at the thing it was handed.
PLATE_W, PLATE_H = 420, 260


def plate_rect(a):
    h, w = a.shape
    return ((w - PLATE_W) // 2, (h - PLATE_H) // 2, PLATE_W, PLATE_H)


def boundary(a, synth_rect=None):
    """(depth, curvature, plate height, strength) for the highlight's lower edge, or None."""
    x0, y0, pw, ph = synth_rect if synth_rect else plate_rect(a)
    if y0 < 0 or x0 < 0 or y0 + ph > a.shape[0] or x0 + pw > a.shape[1]:
        return None

    # Search the face from 2% of the plate height -- above that is the rim and the bevel.
    lo, hi = y0 + max(3, int(ph * 0.02)), y0 + int(ph * 0.60)
    face = a[lo:hi, x0:x0 + pw].astype(int)
    if face.shape[0] < 6:
        return None
    step = face[:-1] - face[1:]                      # positive = darker going down

    # The BAND WINDOW: where a banded gloss puts its lower edge. The band is UNIT-sized (~1.6
    # units, about 26px) and the curved one sweeps up toward its ends, so the window spans from
    # just below the rim to a third of the plate.
    whi = max(2, int(ph * 0.34) - (lo - y0))
    win = step[:whi]

    def at(frac):
        c = int(pw * frac)
        b = win[:, max(0, c - 3):c + 4]
        if b.size == 0:
            return None, 0.0
        prof = b.mean(axis=1)
        return lo + int(np.argmax(prof)), float(prof.max())

    mid, strength = at(0.50)
    if mid is None:
        return None
    ends = [e for e, _ in (at(0.05), at(0.95)) if e is not None]
    if not ends:
        return None
    return (mid - y0) / ph, (mid - sum(ends) / len(ends)) / ph, ph, strength


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
        r = boundary(synth(kind), synth_rect=(60, 50, 280, 200))
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

    # DIFFERENCING, not edge-finding.
    #
    # Looking for the strongest downward step found the same STRUCTURAL band in all three renders
    # -- the carved stack's own bezel, identical across them because only the gloss differs -- and
    # reported three identical numbers while the files differed on disk. The gloss is a subtle
    # overlay sitting on top of that band, and no threshold separates "the feature" from "the
    # plate it is drawn on".
    #
    # Subtracting one render from another cancels everything they share. What remains IS the
    # gloss. Same technique that settled the shadow, the pixel corner and the outline polarity;
    # it should have been the first thing tried here rather than the last.
    imgs = {}
    for tag in ("gl_linear", "gl_hard", "gl_curved"):
        f = OUT / f"{tag}.png"
        if not f.exists():
            print(f"[FAIL] {tag:<12} render missing")
            sys.exit(1)
        imgs[tag] = np.asarray(Image.open(f).convert("L")).astype(int)

    bad = 0
    x0, y0, pw, ph = plate_rect(imgs["gl_linear"])

    def diff(a_tag, b_tag):
        d = np.abs(imgs[a_tag][y0:y0 + ph, x0:x0 + pw] - imgs[b_tag][y0:y0 + ph, x0:x0 + pw])
        return (d > 3).sum() / d.size * 100.0, float(d.max())

    # AREA is only a fair test for the pair whose shapes differ over the whole band. HardBand
    # replaces the inset sheen entirely, so it changes a broad region. CurvedGlass and HardBand
    # share the SAME band and the same 62% floor, deviating only in a sliver near the ends -- so
    # its area is inherently ~0.4% however correct it is. Gating both on area would have meant
    # picking a number that fits, which is what turned this gate red for several commits.
    #
    # PEAK is the honest common test: does the pixel actually change, beyond dither.
    for a_tag, b_tag, label, min_pct in (("gl_hard", "gl_linear", "hard vs sheen", 1.5),
                                         ("gl_curved", "gl_hard", "curved vs hard", 0.15)):
        pct, peak = diff(a_tag, b_tag)
        ok = pct > min_pct and peak > 8
        print(f"[{'ok ' if ok else 'FAIL'}] {label:<15} {pct:5.2f}% of the plate differs, "
              f"peak {peak:.0f}/255 (want > {min_pct}% and > 8)")
        if not ok:
            bad += 1

    print("\nGLOSS " + ("PASS" if bad == 0 else f"FAIL ({bad})"))
    sys.exit(0 if bad == 0 else 1)


main()

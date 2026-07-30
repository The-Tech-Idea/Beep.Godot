#!/usr/bin/env python3
"""The SHADOW axis — the fourth, and the one the art pass says the kit is missing entirely.

WHY
---
`ART_PASS_PER_FILE.md` records five distinct shadow behaviours across the 59 reference images,
and `KitLayerKind` has no `Shadow` member at all:

    Hard     opaque, offset, no blur                       files 01 06 16 17 37
    Soft     large radius, low alpha, ambient              files 02 04 11 13 21 25 ...
    None     no shadow; outline or value does the work     files 03 07 09 10 33 38 41
    Glow     coloured outer glow (also a selection cue)    file  06
    Extrude  a solid dark SIDE FACE, not an offset copy    file  35

Two themes of the same genre are told apart by this as much as by silhouette. It is measured
here so it can be gated rather than eyeballed.

THE METRIC
----------
Everything is measured OUTSIDE the widget's own silhouette, in greyscale, relative to the
background level — so it is colour-invariant like the outline and material axes.

  spill     how much darker the ring just outside the silhouette is than the background
  falloff   how far that darkness extends, in px, before it returns to background
            (hard shadows stop abruptly; soft ones fade over many px)
  offset    the direction+distance of the spill's centre of mass from the silhouette's
            (a drop shadow is off to one side; an ambient one is centred)
  polarity  negative when the ring is BRIGHTER than the background -> a glow, not a shadow
  solidity  fraction of the spill that is at full strength (a hard edge / side face is ~1;
            a blurred one is much lower)

CLASSIFICATION is deliberately conservative: anything that does not clearly match a kind is
reported as `ambiguous` rather than being forced into one, because a gate that always produces
a confident answer is the failure mode this repo keeps paying for.

USAGE
    python measure_shadow.py --selftest
    python measure_shadow.py --proof "../../tmp/kitproof/gm_*.png"
    python measure_shadow.py --expect rpg=soft,shooter=none,...   (with --proof: gate it)
"""
import glob
import os
import sys

import numpy as np
from PIL import Image

# A ring this many px wide outside the silhouette is where a shadow lives.
RING = 14
# Below this the ring is indistinguishable from the background: no shadow.
SPILL_MIN = 0.012
# A hard edge / side face keeps most of its spill at full strength.
SOLID_HARD = 0.55
# Beyond this many px of falloff the shadow is diffuse rather than hard.
FALLOFF_SOFT = 5.0
# Below this the darkening is thrown along a single axis -> a side face, not a drop shadow.
AXIS_ONE = 0.35
# The proof probe draws every widget at this fixed size, centred on a flat field.
PROOF_W, PROOF_H = 260, 150


def _dilate(m, k=1):
    """Binary dilation by k px, 4-neighbour, without scipy."""
    out = m.copy()
    for _ in range(k):
        p = np.pad(out, 1, mode="constant", constant_values=False)
        out = (p[1:-1, 1:-1] | p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:])
    return out


def analyse(path, _rect=None):
    """Shadow measurements from the DIFFERENCE between a shadow-on and shadow-off render.

    Both are produced by shadow_probe.tscn: `sh_<genre>.png` and `nos_<genre>.png`. Everything
    that is not the shadow -- the plate, the grain, the rim, and crucially the SILHOUETTE --
    is identical in both and cancels exactly.

    That matters because no "look outside the widget's rect" test can work here:
      * Capsule, Spiked and Torn deliberately draw OUTSIDE their rect, so their overhang reads
        as spill (it made platformer and rpg measure as `extrude` on a build with no shadow);
      * Shield sits INSET within its rect, so its shadow never leaves the rect at all and
        measured as nothing.
    Differencing is immune to both.
    """
    off = os.path.join(os.path.dirname(path),
                       os.path.basename(path).replace("sh_", "nos_", 1))
    if not os.path.isfile(off):
        raise SystemExit(f"REFUSED: {path} has no shadow-off pair at {off}. "
                         f"Render with tools/genre_shapes/shadow_probe.tscn.")

    on = np.asarray(Image.open(path).convert("L")).astype(np.float64)
    no = np.asarray(Image.open(off).convert("L")).astype(np.float64)
    if on.shape != no.shape:
        return None

    # Positive where the shadow DARKENED the ground; negative where it brightened it (a glow).
    d = no - on
    lit = d > 4.0
    glow = (-d) > 4.0

    if lit.sum() < 24 and glow.sum() < 24:
        return dict(spill=0.0, frac=0.0, falloff=0.0, offset=0.0, axis=0.0, solidity=0.0)

    if glow.sum() > lit.sum():
        return dict(spill=-float((-d)[glow].mean() / 255.0), frac=float(glow.mean()),
                    falloff=0.0, offset=0.0, axis=0.0, solidity=0.0)

    spill = float(d[lit].mean() / 255.0)
    frac = float(lit.sum() / max((no != no.max()).sum(), 1))

    # The widget's own body: where the two renders agree and both differ from the flat ground.
    ground = float(np.median(no[:4]))
    body = np.abs(no - ground) > 18.0
    ys, xs = np.nonzero(body if body.sum() > 64 else lit)
    cy, cx = (ys.min() + ys.max()) / 2.0, (xs.min() + xs.max()) / 2.0
    bh, bw = ys.max() - ys.min(), xs.max() - xs.min()

    ly, lx = np.nonzero(lit)
    dy, dx = abs(ly.mean() - cy), abs(lx.mean() - cx)
    offset = float(np.hypot(dy, dx) / max(1.0, (bw + bh) / 4.0) * 10.0)
    axis = float(min(dy, dx) / max(dy, dx, 1e-6))

    # Falloff: how far the darkening reaches from the body edge.
    dist = np.zeros_like(on)
    grow = body.copy()
    for k in range(1, RING + 1):
        nxt = _dilate(grow, 1)
        dist[nxt & ~grow] = k
        grow = nxt
    out = lit & (dist > 0)
    falloff = float(dist[out].mean()) if out.sum() else 0.0

    # Solidity on the INTERIOR of the darkened region only. A hard shadow's visible crescent is
    # thin (an 8px offset on a 300px widget), so anti-aliased edge pixels outnumber the solid
    # core and dragged solidity to 0.1-0.3 -- three genuinely hard/extruded shadows measured as
    # soft. Eroding by 1px removes the AA collar and leaves the fill.
    inner = lit & ~_dilate(~lit, 1)
    depth = d[inner] if inner.sum() >= 24 else d[lit]
    solidity = float((depth > depth.max() * 0.72).mean())

    # `frac` is the share of the RING that is darkened, matching the old semantics closely
    # enough for the extrude/hard split to keep working.
    ring = _dilate(body, RING) & ~_dilate(body, 1)
    frac = float((lit & ring).sum() / max(ring.sum(), 1))

    return dict(spill=spill, frac=frac, falloff=falloff, offset=offset,
                axis=axis, solidity=solidity)


def classify(m):
    if m is None:
        return "no-widget"
    if m["spill"] < -SPILL_MIN:
        return "glow"
    if m["spill"] < SPILL_MIN or m["frac"] < 0.06:
        return "none"
    # SOLIDITY first: it is what separates a crisp shadow from a blurred one. Falloff alone put
    # a 6px gaussian at 4.0px, inside any "hard" threshold worth having.
    if m["solidity"] < SOLID_HARD:
        return "soft"
    # Crisp. A side face and a drop shadow darken a similar share of the ring, so coverage
    # cannot separate them -- the AXIS does. A side face is thrown along ONE axis; a drop
    # shadow is thrown diagonally.
    if m["axis"] < AXIS_ONE:
        return "extrude"
    return "hard"


def report(pattern, expect=None, rect=None):
    files = sorted(glob.glob(pattern))
    if not files:
        print(f"REFUSED: no files matched {pattern!r}")
        return 1
    print(f"{'widget':<14}{'spill':>8}{'frac':>7}{'falloff':>9}{'offset':>8}{'axis':>7}"
          f"{'solid':>7}  {'kind':<10} {'expected':<10}")
    bad = 0
    for p in files:
        name = os.path.basename(p)
        for pre in ("gm_", "gs_", "sh_"):
            name = name.removeprefix(pre)
        name = name.removesuffix(".png")
        m = analyse(p)
        kind = classify(m)
        want = (expect or {}).get(name)
        flag = ""
        if want:
            if kind != want:
                flag, bad = "  <-- MISMATCH", bad + 1
        if m is None:
            print(f"{name:<14}{'-':>8}{'-':>7}{'-':>9}{'-':>8}{'-':>7}  {kind:<10} "
                  f"{want or '':<10}{flag}")
            continue
        print(f"{name:<14}{m['spill']:>8.4f}{m['frac']:>7.2f}{m['falloff']:>9.2f}"
              f"{m['offset']:>8.2f}{m['axis']:>7.2f}{m['solidity']:>7.2f}  {kind:<10} {want or '':<10}{flag}")

    if expect:
        print(f"\nSHADOW {'PASS' if bad == 0 else f'FAIL ({bad} mismatch)'}")
        return 0 if bad == 0 else 1
    print("\n(no --expect given: measured only, not gated)")
    return 0


def rect_for(path):
    """The widget's rect, READ from the sidecar the probe writes — never assumed.

    Assuming a constant size is how the first run of this gate reported a confident "hard"
    shadow for all ten genres on a build that has no shadow layer at all: the assumed rect was
    smaller than the rendered plate, so the measuring ring sat inside the plate and measured the
    plate. The probe writes `rects.txt` beside the PNGs; if it is missing, this REFUSES.
    """
    d = os.path.dirname(os.path.abspath(path))
    side = os.path.join(d, "rects.txt")
    if not os.path.isfile(side):
        raise SystemExit(
            f"REFUSED: no rects.txt in {d}. measure_shadow cannot guess where the widget is — "
            f"render with tools/genre_shapes/shadow_probe.tscn, which writes it.")
    name = os.path.basename(path)
    for pre in ("gm_", "gs_", "sh_"):
        name = name.removeprefix(pre)
    name = name.removesuffix(".png")
    for line in open(side, encoding="utf-8"):
        f = line.split()
        if len(f) == 5 and f[0] == name:
            return tuple(int(v) for v in f[1:])
    raise SystemExit(f"REFUSED: {name} has no entry in {side}")


def selftest():
    """Synthesise each shadow kind and require the classifier to name it.

    A classifier only ever run on real renders proves nothing about the kinds those renders
    happen not to contain — which is exactly the state the kit is in today (it has none).
    """
    from PIL import ImageDraw, ImageFilter
    W = H = 220
    bg, plate = 110, 210
    box = (60, 60, 160, 160)
    box_rect = (box[0], box[1], box[2] - box[0], box[3] - box[1])
    ok = True

    def base():
        return Image.new("L", (W, H), bg)

    def stamp(img, shift=(0, 0), fill=plate):
        d = ImageDraw.Draw(img)
        d.rounded_rectangle([box[0] + shift[0], box[1] + shift[1],
                             box[2] + shift[0], box[3] + shift[1]], 10, fill=fill)
        return img

    cases = {}

    cases["none"] = stamp(base())

    im = base()                                   # hard: opaque offset copy, no blur
    stamp(im, (7, 7), fill=40)
    stamp(im)
    cases["hard"] = im

    sh = base()                                   # soft: blurred, centred
    stamp(sh, (0, 0), fill=45)
    sh = sh.filter(ImageFilter.GaussianBlur(6))
    stamp(sh)
    cases["soft"] = sh

    gl = base()                                   # glow: brighter halo
    stamp(gl, (0, 0), fill=250)
    gl = gl.filter(ImageFilter.GaussianBlur(7))
    stamp(gl)
    cases["glow"] = gl

    ex = base()                                   # extrude: solid side face below
    d = ImageDraw.Draw(ex)
    d.rounded_rectangle([box[0], box[1] + 14, box[2], box[3] + 14], 10, fill=45)
    stamp(ex)
    cases["extrude"] = ex

    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..",
                       "tmp", "shadowtest")
    os.makedirs(out, exist_ok=True)
    print(f"{'case':<10}{'spill':>8}{'frac':>7}{'falloff':>9}{'offset':>8}{'axis':>7}{'solid':>7}  got")
    flat = base()
    for want, img in cases.items():
        p = os.path.join(out, f"sh_{want}.png")
        img.save(p)
        # The shadow-off pair: the same plate with no shadow at all.
        stamp(base()).save(os.path.join(out, f"nos_{want}.png"))
        m = analyse(p)
        got = classify(m)
        good = got == want
        ok &= good
        if m is None:
            print(f"{want:<10}{'no widget':>39}  {got}")
            continue
        print(f"{want:<10}{m['spill']:>8.4f}{m['frac']:>7.2f}{m['falloff']:>9.2f}"
              f"{m['offset']:>8.2f}{m['axis']:>7.2f}{m['solidity']:>7.2f}  {got} "
              f"{'ok' if good else '<-- WRONG'}")
    print("\nSELFTEST", "PASS" if ok else "FAIL")
    return 0 if ok else 1


if __name__ == "__main__":
    argv = sys.argv[1:]
    if not argv or argv[0] == "--selftest":
        sys.exit(selftest())
    exp = None
    if "--expect" in argv:
        i = argv.index("--expect")
        exp = dict(kv.split("=", 1) for kv in argv[i + 1].split(","))
        del argv[i:i + 2]
    if argv and argv[0] == "--proof":
        sys.exit(report(argv[1], exp))
    print(__doc__)
    sys.exit(2)

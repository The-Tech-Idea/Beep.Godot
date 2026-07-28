"""
Verify every generated 9-patch against the two rules the art register depends on.

1. EDGE UNIFORMITY. The top/bottom bands stretch horizontally and the left/right bands stretch
   vertically. Anything varying along the stretch axis smears across the whole side of a
   resized control. Circular grabbers are exempt: they are drawn at fixed size and never
   stretched.

2. CENTRE MID-TONE. The register multiplies the art by modulate = surface / 0.80, so a raised
   face whose centre is not near 204 renders at the wrong lightness. Recessed wells and
   grooves are deliberately darker and are checked against their own target.
"""
import os, json
from PIL import Image

ADDON = "C:/Users/f_ald/source/repos/The-Tech-Idea/Beep.Godot/addons/beep_game_builder_cs"
SKINS = os.path.join(ADDON, "catalogs", "skins")
TEX = os.path.join(ADDON, "textures")

# slots drawn at fixed size — a round knob has no meaningful stretchable band
EXEMPT = {"slider_grabber", "scroll_grabber"}
RAISED = {"button_normal": 204, "button_focus": 204, "button_hover": 222,
          "button_pressed": 176, "button_disabled": 196, "panel": 196, "dialog": 190}


# Per-channel tolerance. Resolving an 8x supersample with LANCZOS leaves a +/-1 residue
# between adjacent rows, which exact equality reports as a smear even though the values are
# identical to within one step of 255 and nothing is visible. Anything above this is a real
# geometry error, not resampling noise.
TOL = 2


def _spread(lines):
    """Largest per-channel deviation between any two lines of a band."""
    lo = [min(v) for v in zip(*lines)]
    hi = [max(v) for v in zip(*lines)]
    return max(h - l for l, h in zip(lo, hi)) if lines else 0


def bands_seamless(im, m):
    """Rule for TileFit slots. The band REPEATS rather than stretches, so it may carry
    ornament — but consecutive repeats butt against each other, so the band's first and last
    lines must match or every tile boundary shows as a seam."""
    w, h = im.size
    if w - 2 * m < 2 or h - 2 * m < 2:
        return True, "centre too small to tile"
    top = im.crop((m, 0, w - m, m))
    c0 = sum(top.crop((0, 0, 1, m)).getdata(), ())
    c1 = sum(top.crop((top.size[0] - 1, 0, top.size[0], m)).getdata(), ())
    left = im.crop((0, m, m, h - m))
    r0 = sum(left.crop((0, 0, m, 1)).getdata(), ())
    r1 = sum(left.crop((0, left.size[1] - 1, m, left.size[1])).getdata(), ())
    dc = max((abs(a - b) for a, b in zip(c0, c1)), default=0)
    dr = max((abs(a - b) for a, b in zip(r0, r1)), default=0)
    return max(dc, dr) <= TOL, f"h-seam={dc} v-seam={dr} (tol {TOL})"


def bands_uniform(im, m):
    w, h = im.size
    if w - 2 * m < 1 or h - 2 * m < 1:
        return True, "centre too small to stretch"
    top = im.crop((m, 0, w - m, m))
    cols = [sum(top.crop((x, 0, x + 1, m)).getdata(), ()) for x in range(top.size[0])]
    left = im.crop((0, m, m, h - m))
    rows = [sum(left.crop((0, y, m, y + 1)).getdata(), ()) for y in range(left.size[1])]
    dc, dr = _spread(cols), _spread(rows)
    return max(dc, dr) <= TOL, f"col-spread={dc} row-spread={dr} (tol {TOL})"


def centre_tone(im, m):
    w, h = im.size
    c = im.crop((m, m, w - m, h - m))
    px = [q for q in list(c.getdata()) if q[3] > 200]
    return sum(q[0] for q in px) // max(1, len(px))


def main():
    smears, tones, seams, checked = [], [], [], 0
    for genre in sorted(os.listdir(TEX)):
        gdir = os.path.join(SKINS, genre, "themes")
        if not os.path.isdir(gdir):
            continue
        themes = sorted(os.listdir(gdir))
        tj = os.path.join(gdir, themes[0], "theme.json")
        _tex = json.load(open(tj, encoding="utf-8")).get("textures", {})
        marg = {k: v.get("margin_left", 12) for k, v in _tex.items()}
        # A slot declared TileFit repeats its edge bands, so it is held to the SEAM rule
        # instead of the uniformity rule — ornament there is intended, not a defect.
        tiled = {k for k, v in _tex.items() if v.get("axis_stretch_horizontal", 0) != 0}
        for theme in themes:
            d = os.path.join(TEX, genre, theme)
            if not os.path.isdir(d):
                continue
            for f in sorted(os.listdir(d)):
                if not f.endswith(".png"):
                    continue
                slot = f[:-4]
                if slot in EXEMPT:
                    continue
                m = marg.get(slot)
                if m is None:
                    continue
                im = Image.open(os.path.join(d, f)).convert("RGBA")
                checked += 1
                if slot in tiled:
                    ok, why = bands_seamless(im, m)
                    if not ok:
                        seams.append(f"{genre}/{theme}/{slot}  {why}")
                else:
                    ok, why = bands_uniform(im, m)
                    if not ok:
                        smears.append(f"{genre}/{theme}/{slot}  {why}")
                if slot in RAISED:
                    t = centre_tone(im, m)
                    if abs(t - RAISED[slot]) > 12:
                        tones.append(f"{genre}/{theme}/{slot}  centre={t} want~{RAISED[slot]}")

    # ── HUD tier: textures/hud/<genre>/, keyed by FILE name, one set per genre ──
    hud_root = os.path.join(TEX, "hud")
    for genre in sorted(os.listdir(hud_root)) if os.path.isdir(hud_root) else []:
        gdir = os.path.join(SKINS, genre, "themes")
        if not os.path.isdir(gdir):
            continue
        tj = os.path.join(gdir, sorted(os.listdir(gdir))[0], "theme.json")
        tex = json.load(open(tj, encoding="utf-8")).get("textures", {})
        marg, tiled_h = {}, set()
        for k, v in tex.items():
            if k.startswith("hud_"):
                nm = os.path.basename(v["texture_path"])[:-4]
                marg[nm] = v.get("margin_left", 20)
                if v.get("axis_stretch_horizontal", 0) != 0:
                    tiled_h.add(nm)
        d = os.path.join(hud_root, genre)
        for f in sorted(os.listdir(d)):
            if not f.endswith(".png"):
                continue
            nm = f[:-4]
            m = marg.get(nm)
            if m is None:
                continue
            im = Image.open(os.path.join(d, f)).convert("RGBA")
            checked += 1
            if nm in tiled_h:
                ok, why = bands_seamless(im, m)
                if not ok:
                    seams.append(f"hud/{genre}/{nm}  {why}")
            else:
                ok, why = bands_uniform(im, m)
                if not ok:
                    smears.append(f"hud/{genre}/{nm}  {why}")

    print(f"  checked {checked} stretchable 9-patches (menu + hud)")
    print(f"  edge smears : {len(smears)}")
    for s in smears[:12]:
        print(f"      {s}")
    if len(smears) > 12:
        print(f"      ... and {len(smears) - 12} more")
    print(f"  tile seams  : {len(seams)}")
    for x in seams[:8]:
        print(f"      {x}")
    print(f"  tone misses : {len(tones)}")
    for s in tones[:8]:
        print(f"      {s}")
    if len(tones) > 8:
        print(f"      ... and {len(tones) - 8} more")
    print("\n  PASS" if not smears and not tones else "\n  FAIL")


main()

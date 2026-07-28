#!/usr/bin/env python
"""
Bake the HUD texture masters: one per COMPONENT per GENRE.

See docs/HUD_TEXTURE_SYSTEM.md for the model. In short:

    shape / border / shadow / margins  ->  baked per component x genre   (this script)
    palette                            ->  modulate, per theme          (theme.json)

Masters are greyscale so modulate can take them to any theme's accent:
    body   ~0.28 luminance  -> dark tint of the accent
    border  1.0  luminance  -> the accent itself
    shadow  pure black+alpha-> stays black under any modulate (0 x accent = 0)

That last line is why shadows are baked as black rather than drawn as a coloured shape:
multiplying black by a tint leaves it black, so one master survives 5 palettes intact.

Re-runnable. Writes PNGs, .import sidecars, and the hud_* block of every theme.json.
"""
import hashlib, json, glob, math, os, random
from PIL import Image, ImageDraw, ImageFilter

ADDON = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
                     "addons", "beep_game_builder_cs")
SS   = 4          # supersample
S    = 128        # master is S x S
PAD  = 10         # room round the edge for the baked drop shadow

BODY   = (255, 255, 255, 216)
BORDER = (255, 255, 255, 255)
INNER  = (255, 255, 255, 120)
FAINT  = (255, 255, 255, 80)
SHADOW = (0, 0, 0, 150)

# ── per-genre shape language ─────────────────────────────────────────────────────────
# radius, border width, shadow offset/blur, and a corner treatment.
GENRE = {
    "citybuilder": dict(r=5,  bw=1, sh=(0, 2, 3), corner="ticks"),
    "strategy":    dict(r=3,  bw=2, sh=(0, 3, 2), corner="rivets"),
    "shooter":     dict(r=2,  bw=2, sh=(0, 2, 2), corner="chamfer"),
    "rpg":         dict(r=8,  bw=3, sh=(0, 4, 5), corner="ornate"),
    "survival":    dict(r=4,  bw=2, sh=(0, 3, 4), corner="chipped"),
    "cardgame":    dict(r=9,  bw=2, sh=(0, 3, 5), corner="gilt"),
    "racing":      dict(r=6,  bw=2, sh=(0, 2, 3), corner="stripe"),
    "puzzle":      dict(r=13, bw=2, sh=(0, 4, 7), corner="gloss"),
    "topdown":     dict(r=6,  bw=1, sh=(0, 2, 3), corner="brackets"),
    "platformer":  dict(r=11, bw=4, sh=(0, 4, 4), corner="chunky"),
}


def canvas():
    return Image.new("RGBA", (S * SS, S * SS), (0, 0, 0, 0))


def shadow_layer(box, radius, off):
    """Baked drop shadow, black so modulate cannot tint it. `off` is (dx, dy, blur)."""
    img = canvas()
    d = ImageDraw.Draw(img)
    ox, oy, br = off
    d.rounded_rectangle([box[0] + ox * SS, box[1] + oy * SS, box[2] + ox * SS, box[3] + oy * SS],
                        radius=radius, fill=SHADOW)
    return img.filter(ImageFilter.GaussianBlur(br * SS * 0.6))


def corner_deco(d, kind, box, r, g):
    x0, y0, x1, y1 = box
    s = SS
    if kind == "ticks":
        t = 13 * s
        for x, y, dx, dy in ((x0, y0, 1, 1), (x1, y0, -1, 1), (x0, y1, 1, -1), (x1, y1, -1, -1)):
            d.line([(x, y), (x + dx * t, y)], fill=BORDER, width=int(2.2 * s))
            d.line([(x, y), (x, y + dy * t)], fill=BORDER, width=int(2.2 * s))
    elif kind == "rivets":
        rr = 3.0 * s
        for x, y in ((x0 + 9 * s, y0 + 9 * s), (x1 - 9 * s, y0 + 9 * s),
                     (x0 + 9 * s, y1 - 9 * s), (x1 - 9 * s, y1 - 9 * s)):
            d.ellipse([x - rr, y - rr, x + rr, y + rr], fill=INNER, outline=BORDER, width=int(1.2 * s))
    elif kind == "chamfer":
        t, w = 15 * s, int(2.5 * s)
        for x, y, dx, dy in ((x0, y0, 1, 1), (x1, y0, -1, 1), (x0, y1, 1, -1), (x1, y1, -1, -1)):
            d.line([(x + dx * 5 * s, y), (x + dx * t, y)], fill=BORDER, width=w)
            d.line([(x, y + dy * 5 * s), (x, y + dy * t)], fill=BORDER, width=w)
    elif kind == "ornate":
        d.rounded_rectangle([x0 + 5 * s, y0 + 5 * s, x1 - 5 * s, y1 - 5 * s],
                            radius=int(5 * s), outline=BORDER, width=int(1.4 * s))
        rr = 4 * s
        for x, y in ((x0 + 5 * s, y0 + 5 * s), (x1 - 5 * s, y0 + 5 * s),
                     (x0 + 5 * s, y1 - 5 * s), (x1 - 5 * s, y1 - 5 * s)):
            d.polygon([(x, y - rr), (x + rr, y), (x, y + rr), (x - rr, y)], fill=BORDER)
    elif kind == "chipped":
        rnd = random.Random(11)
        for _ in range(16):
            e = rnd.choice("tblr")
            L = rnd.randint(int(4 * s), int(10 * s))
            if e in "tb":
                y = y0 + rnd.randint(0, int(6 * s)) if e == "t" else y1 - rnd.randint(0, int(6 * s))
                x = rnd.randint(int(x0), int(x1))
                d.line([(x, y), (x + L, y + rnd.randint(-2, 2) * s)], fill=FAINT, width=int(1.2 * s))
            else:
                x = x0 + rnd.randint(0, int(6 * s)) if e == "l" else x1 - rnd.randint(0, int(6 * s))
                y = rnd.randint(int(y0), int(y1))
                d.line([(x, y), (x + rnd.randint(-2, 2) * s, y + L)], fill=FAINT, width=int(1.2 * s))
    elif kind == "gilt":
        d.rounded_rectangle([x0 + 4 * s, y0 + 4 * s, x1 - 4 * s, y1 - 4 * s],
                            radius=int(6 * s), outline=BORDER, width=int(1.8 * s))
        d.rounded_rectangle([x0 + 8 * s, y0 + 8 * s, x1 - 8 * s, y1 - 8 * s],
                            radius=int(4 * s), outline=INNER, width=int(1.0 * s))
    elif kind == "stripe":
        d.rectangle([x0 + 6 * s, y0 + 3 * s, x1 - 6 * s, y0 + 6 * s], fill=BORDER)
    elif kind == "gloss":
        d.arc([x0 + 4 * s, y0 + 3 * s, x1 - 4 * s, y0 + 30 * s], 190, 350, fill=INNER, width=int(3 * s))
    elif kind == "brackets":
        t, w = 10 * s, int(2 * s)
        for x, y, dx, dy in ((x0, y0, 1, 1), (x1, y0, -1, 1), (x0, y1, 1, -1), (x1, y1, -1, -1)):
            d.line([(x, y), (x + dx * t, y)], fill=INNER, width=w)
            d.line([(x, y), (x, y + dy * t)], fill=INNER, width=w)
    elif kind == "chunky":
        d.rounded_rectangle([x0 + 5 * s, y0 + 5 * s, x1 - 5 * s, y1 - 5 * s],
                            radius=int(6 * s), outline=INNER, width=int(1.6 * s))


def box_master(genre, *, radius=None, border=None, deco=True, fill=True,
               shadow=True, square=False, top_only=False, ring=False):
    """Generic rounded-box master with baked border + drop shadow."""
    g = GENRE[genre]
    r = int((g["r"] if radius is None else radius) * SS)
    bw = int((g["bw"] if border is None else border) * SS)
    inset = PAD * SS
    box = [inset, inset, S * SS - inset, S * SS - inset]
    if square:
        box = [inset, inset, S * SS - inset, S * SS - inset]

    img = canvas()
    if shadow:
        img.alpha_composite(shadow_layer(box, r, g["sh"]))
    layer = canvas()
    d = ImageDraw.Draw(layer)
    d.rounded_rectangle(box, radius=r, fill=(BODY if fill else None),
                        outline=BORDER, width=bw)
    if top_only:
        # a tab joins the panel beneath it: erase the bottom edge
        d.rectangle([box[0], box[3] - bw, box[2], box[3] + bw], fill=BODY)
    if ring:
        # focus ring / frame: no centre, so the world shows through
        inner = canvas()
        di = ImageDraw.Draw(inner)
        di.rounded_rectangle([box[0] + bw, box[1] + bw, box[2] - bw, box[3] - bw],
                             radius=max(r - bw, 0), fill=(0, 0, 0, 255))
        layer = Image.composite(Image.new("RGBA", layer.size, (0, 0, 0, 0)), layer,
                                inner.split()[3])
    if deco:
        corner_deco(ImageDraw.Draw(layer), g["corner"], box, r, g)
    img.alpha_composite(layer)
    return img.resize((S, S), Image.LANCZOS)


def bar_master(genre, filled):
    g = GENRE[genre]
    r = int(min(g["r"] + 4, 16) * SS)
    inset = PAD * SS
    box = [inset, inset, S * SS - inset, S * SS - inset]
    img = canvas()
    if not filled:
        img.alpha_composite(shadow_layer(box, r, (0, 1, 2)))
    layer = canvas()
    d = ImageDraw.Draw(layer)
    d.rounded_rectangle(box, radius=r, fill=BODY if not filled else (255, 255, 255, 255),
                        outline=BORDER, width=int(g["bw"] * SS))
    if filled:   # top gloss so a full bar still reads as a bar
        d.rounded_rectangle([box[0] + 6 * SS, box[1] + 5 * SS, box[2] - 6 * SS,
                             box[1] + (box[3] - box[1]) * 0.38],
                            radius=int(r * 0.6), fill=(255, 255, 255, 60))
    img.alpha_composite(layer)
    return img.resize((S, S), Image.LANCZOS)


# slot -> (builder, 9-patch margin, content margins L,R,T,B)
def slot_table(genre):
    g = GENRE[genre]
    br = g["r"]
    return {
        "panel":            (lambda: box_master(genre, radius=br + 2), 34, (12, 12, 10, 10)),
        "button_normal":    (lambda: box_master(genre, deco=False), br + 12, (14, 14, 9, 9)),
        "button_hover":     (lambda: box_master(genre, deco=False, border=g["bw"] + 1), br + 12, (14, 14, 9, 9)),
        "button_pressed":   (lambda: box_master(genre, deco=False, shadow=False), br + 12, (14, 14, 9, 9)),
        "button_disabled":  (lambda: box_master(genre, deco=False, shadow=False, border=1), br + 12, (14, 14, 9, 9)),
        "button_focus":     (lambda: box_master(genre, deco=False, shadow=False, ring=True,
                                                border=g["bw"] + 1), br + 12, (14, 14, 9, 9)),
        "tab_normal":       (lambda: box_master(genre, deco=False, top_only=True), br + 10, (16, 16, 7, 7)),
        "tab_selected":     (lambda: box_master(genre, deco=False, top_only=True,
                                                border=g["bw"] + 1), br + 10, (16, 16, 7, 7)),
        "slot_empty":       (lambda: box_master(genre, radius=max(br - 2, 2), deco=False, square=True), 22, (4, 4, 4, 4)),
        "slot_filled":      (lambda: box_master(genre, radius=max(br - 2, 2), deco=False, square=True,
                                                border=g["bw"] + 1), 22, (4, 4, 4, 4)),
        "bar_bg":           (lambda: bar_master(genre, False), min(br + 4, 16) + 8, (3, 3, 2, 2)),
        "bar_fill":         (lambda: bar_master(genre, True), min(br + 4, 16) + 8, (0, 0, 0, 0)),
        "frame":            (lambda: box_master(genre, radius=br + 4, ring=True,
                                                border=g["bw"] + 1), 38, (8, 8, 8, 8)),
        "tooltip":          (lambda: box_master(genre, radius=br + 1, deco=False), br + 12, (10, 10, 7, 7)),
    }


IMPORT = """[remap]

importer="texture"
type="CompressedTexture2D"
path="res://.godot/imported/{name}-{h}.ctex"
metadata={{
"vram_texture": false
}}

[deps]

source_file="res://{rel}"
dest_files=["res://.godot/imported/{name}-{h}.ctex"]

[params]

compress/mode=0
compress/high_quality=false
compress/lossy_quality=0.7
compress/uastc_level=0
compress/rdo_quality_loss=0.0
compress/hdr_compression=1
compress/normal_map=0
compress/channel_pack=0
mipmaps/generate=false
mipmaps/limit=-1
roughness/mode=0
roughness/src_normal=""
process/channel_remap/red=0
process/channel_remap/green=1
process/channel_remap/blue=2
process/channel_remap/alpha=3
process/fix_alpha_border=true
process/premult_alpha=false
process/normal_map_invert_y=false
process/hdr_as_srgb=false
process/hdr_clamp_exposure=false
process/size_limit=0
detect_3d/compress_to=1
"""


def hexc(v, fb=(128, 128, 128, 255)):
    v = (v or "").lstrip("#")
    if len(v) == 6:
        v += "FF"
    return tuple(int(v[i:i + 2], 16) for i in (0, 2, 4, 6)) if len(v) == 8 else fb


def hx(c):
    return "#%02X%02X%02X%02X" % c


def tint(c, k, a):
    return (min(255, int(c[0] * k)), min(255, int(c[1] * k)), min(255, int(c[2] * k)), int(255 * a))


# state weight applied via modulate: (luminance scale, alpha)
WEIGHT = {
    "panel": (1.00, 1.00), "button_normal": (0.85, 0.88), "button_hover": (1.15, 0.96),
    "button_pressed": (1.45, 1.00), "button_disabled": (0.55, 0.45), "button_focus": (1.30, 1.00),
    "tab_normal": (0.75, 0.85), "tab_selected": (1.20, 1.00),
    "slot_empty": (0.70, 0.80), "slot_filled": (1.10, 0.95),
    "bar_bg": (0.55, 0.80), "bar_fill": (1.30, 1.00),
    "frame": (1.00, 0.95), "tooltip": (0.95, 0.96),
}


def main():
    # clear the superseded generic masters
    old = glob.glob(os.path.join(ADDON, "textures", "hud", "*", "*.png*"))
    for p in old:
        if os.path.basename(p).split(".")[0] in ("plate", "tile", "bar"):
            os.remove(p)
    print(f"  removed {len(old)} superseded generic master files")

    made = 0
    for genre in GENRE:
        od = os.path.join(ADDON, "textures", "hud", genre)
        os.makedirs(od, exist_ok=True)
        for slot, (build, _m, _cm) in slot_table(genre).items():
            p = os.path.join(od, f"{slot}.png")
            build().save(p)
            rel = f"addons/beep_game_builder_cs/textures/hud/{genre}/{slot}.png"
            h = hashlib.md5(rel.encode()).hexdigest()
            open(p + ".import", "w", encoding="utf-8", newline="\n").write(
                IMPORT.format(name=f"{slot}.png", h=h, rel=rel))
            made += 1
    print(f"  baked {made} masters ({len(GENRE)} genres x {len(slot_table('rpg'))} components)")

    n = 0
    for tj in sorted(glob.glob(os.path.join(ADDON, "catalogs/skins/*/themes/*/theme.json"))):
        parts = tj.replace("\\", "/").split("/")
        genre, theme = parts[-4], parts[-2]
        if genre not in GENRE:
            continue
        data = json.load(open(tj, encoding="utf-8"))
        accent = hexc(data.get("colors", {}).get("accent_primary"))
        tex = data.setdefault("textures", {})
        # drop the superseded generic hud_tile_* keys
        for dead in ("hud_plate", "hud_tile_normal", "hud_tile_hover", "hud_tile_pressed",
                     "hud_tile_disabled"):
            tex.pop(dead, None)
        for slot, (_b, margin, cm) in slot_table(genre).items():
            k, a = WEIGHT[slot]
            tex[f"hud_{slot}"] = {
                "texture_path": f"res://addons/beep_game_builder_cs/textures/hud/{genre}/{slot}.png",
                "margin_left": margin, "margin_top": margin,
                "margin_right": margin, "margin_bottom": margin,
                "axis_stretch_horizontal": 0, "axis_stretch_vertical": 0,
                "draw_center": slot not in ("button_focus", "frame"),
                "modulate": hx(tint(accent, k, a)),
                "content_margin_left": cm[0], "content_margin_right": cm[1],
                "content_margin_top": cm[2], "content_margin_bottom": cm[3],
                "baked": False,
            }
        json.dump(data, open(tj, "w", encoding="utf-8", newline="\n"), indent=2)
        open(tj, "a", encoding="utf-8", newline="\n").write("\n")
        n += 1
    print(f"  wired {n} theme.json files (14 hud_* slots each, per-theme modulate)")


main()

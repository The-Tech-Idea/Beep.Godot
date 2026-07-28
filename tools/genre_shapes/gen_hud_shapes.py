"""
Give the HUD tier the same per-genre silhouette the menu tier already has.

gen_all_genres.py covers the 13 MENU slots. The 14 HUD slots are a separate set living at
textures/hud/<genre>/ — one set per genre, shared by that genre's five themes, because HUD
shape belongs to the genre and the themes recolour it through modulate.

Two problems this fixes:

1. The shipped HUD art was the same generic rounded rectangle for all ten genres, so the
   place a player actually looks at for hours carried no genre identity at all.

2. Its mid-tone sat at 233-255 rather than 204. The register tints a HUD plate with
   modulate = plate_colour / 0.80, so art that bright renders roughly 15-25% lighter than the
   plate colour asked for — HUD chrome was washed out in every skin.

Primitives are imported from gen_all_genres rather than copied, so a fix to the bevel or the
9-patch discipline lands on both tiers at once.
"""
import os, json, hashlib
from PIL import Image, ImageDraw

import gen_all_genres as G

ADDON = G.ADDON
SKINS = G.SKINS
HUD = os.path.join(G.TEX, "hud")
SS = G.SS


def tab(w, h, m, style, marks, selected):
    """A tab is welded to the panel beneath it: rounded on top, square at the bottom, and no
    bottom border. Drawn here rather than reusing the plate so the join is real geometry
    instead of a plate with its bottom edge hidden."""
    img = G.canvas(w, h)
    d = ImageDraw.Draw(img)
    s = SS
    r = max(2, min(m - 3, 14)) * s
    face = G.FACE if selected else 176
    d.rounded_rectangle([0, 0, w * s - 1, h * s + r], radius=r,
                        fill=G.g(face), outline=G.g(G.EDGE), width=int(2.0 * s))
    G.bevel(img, w, h, s, 3.0, False)
    if marks:
        G.corner_marks(d, w, h, s, m, marks)
    return G.done(img, w, h)


def slot(w, h, m, style, filled):
    """Inventory slot: a recessed square well, raised slightly when it holds something."""
    return G.build(style, w, h, m, face=G.FACE if filled else G.WELL, sunken=not filled)


def make(genre, slot_name, size, m, style, marks, gloss, edge):
    w, h = size
    e = edge if slot_name in ("button_normal", "button_hover", "button_pressed",
                              "button_focus", "panel", "frame", "tooltip") else None
    if slot_name.startswith("bar_"):
        return G.capsule(w, h, m, fill=slot_name.endswith("fill"))
    if slot_name.startswith("tab_"):
        return tab(w, h, m, style, marks, slot_name.endswith("selected"))
    if slot_name.startswith("slot_"):
        return slot(w, h, m, style, slot_name.endswith("filled"))
    if slot_name == "frame":
        # The frame is chrome around live gameplay, so its centre is knocked out — a filled
        # centre would paint over the world it is supposed to frame.
        img = G.build(style, w, h, m, face=190, marks=marks, edge=e)
        d = ImageDraw.Draw(img)
        d.rectangle([m, m, w - m - 1, h - m - 1], fill=(0, 0, 0, 0))
        return img
    if slot_name == "panel":
        return G.build(style, w, h, m, face=196, marks=marks, edge=e)
    if slot_name == "tooltip":
        return G.build(style, w, h, m, face=210, marks=marks, edge=e)
    face = {"button_normal": G.FACE, "button_hover": 222, "button_pressed": 176,
            "button_disabled": 196, "button_focus": G.FACE}[slot_name]
    return G.build(style, w, h, m,
                   face=face,
                   sunken=(slot_name == "button_pressed"),
                   ring=255 if slot_name == "button_focus" else 0,
                   gloss=gloss and slot_name != "button_pressed",
                   marks=marks if slot_name != "button_disabled" else None,
                   edge=e)


def hud_margins(genre):
    """Declared HUD margins, keyed by FILE name rather than slot id."""
    themes = sorted(os.listdir(os.path.join(SKINS, genre, "themes")))
    tj = os.path.join(SKINS, genre, "themes", themes[0], "theme.json")
    tex = json.load(open(tj, encoding="utf-8")).get("textures", {})
    out = {}
    for k, v in tex.items():
        if k.startswith("hud_"):
            out[os.path.basename(v["texture_path"])[:-4]] = v.get("margin_left", 20)
    return out, themes


def set_hud_axis(genre, themes, names, mode):
    """Declare TileFit on the ornamented HUD slots across the genre's themes."""
    for theme in themes:
        tj = os.path.join(SKINS, genre, "themes", theme, "theme.json")
        d = json.load(open(tj, encoding="utf-8"))
        tex = d.get("textures", {})
        hit = 0
        for k, v in tex.items():
            if k.startswith("hud_") and os.path.basename(v["texture_path"])[:-4] in names:
                v["axis_stretch_horizontal"] = mode
                v["axis_stretch_vertical"] = mode
                hit += 1
        if hit:
            with open(tj, "w", encoding="utf-8", newline=G.LF) as fh:
                json.dump(d, fh, indent=2, ensure_ascii=False)
                fh.write(G.LF)


EDGE_NAMES = {"button_normal", "button_hover", "button_pressed", "button_focus",
              "panel", "frame", "tooltip"}


def main():
    made = 0
    for genre, (style, marks, gloss, edge) in G.GENRES.items():
        d = os.path.join(HUD, genre)
        if not os.path.isdir(d):
            print(f"  no hud dir for {genre}")
            continue
        marg, themes = hud_margins(genre)
        for f in sorted(os.listdir(d)):
            if not f.endswith(".png"):
                continue
            name = f[:-4]
            size = Image.open(os.path.join(d, f)).size
            m = marg.get(name, max(6, min(size) // 5))
            img = make(genre, name, size, m, style, marks, gloss, edge)
            p = os.path.join(d, f)
            img.save(p)
            rel = f"addons/beep_game_builder_cs/textures/hud/{genre}/{f}"
            with open(p + ".import", "w", encoding="utf-8", newline=G.LF) as fh:
                fh.write(G.IMPORT.format(name=f, h=hashlib.md5(rel.encode()).hexdigest(),
                                         rel=rel))
            made += 1
        if edge:
            set_hud_axis(genre, themes, EDGE_NAMES, G.TILE_FIT)
        print(f"  {genre:12s} {style:8s} edge={str(edge):8s} -> hud slots redrawn")
    print(f"\n  wrote {made} HUD files")


main()

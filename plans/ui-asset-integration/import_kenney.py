#!/usr/bin/env python
"""Import curated Kenney UI textures into the addon per the Phase 3 mapping.

Copies + renames the chosen source PNGs into
    addons/beep_game_builder_cs/textures/<genre>/<theme>/{button_normal,button_hover,button_pressed,panel}.png
and cursor PNGs into textures/cursors/. Re-runnable: edit MAP and re-run.

Source: H:\\GameDev\\GFX\\GameAssets\\Kenney Game Assets All-in-1 3.6.0\\UI assets  (CC0)
"""
import os, shutil, sys

SRC = r"H:\GameDev\GFX\GameAssets\Kenney Game Assets All-in-1 3.6.0\UI assets"
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DEST = os.path.join(REPO, "addons", "beep_game_builder_cs", "textures")

# (genre, theme) -> descriptor string. See resolve() for grammar.
MAP = {
 ("cardgame","arcane"):"fantasy:002:grey", ("cardgame","casino"):"uipack:Red:gloss",
 ("cardgame","paper"):"adventure:grey", ("cardgame","royal"):"fantasy:015:brown",
 ("cardgame","velvet"):"fantasy:008:red",
 ("citybuilder","blueprint"):"uipack:Blue:line", ("citybuilder","eco"):"uipack:Green:flat",
 ("citybuilder","future"):"scifi:blue", ("citybuilder","industrial"):"adventure:grey",
 ("citybuilder","urban"):"uipack:Grey:gloss",
 ("platformer","cartoon"):"uipack:Green:gloss", ("platformer","modern"):"uipack:Grey:flat",
 ("platformer","nature"):"adventure:brown", ("platformer","pixel8bit"):"pixel",
 ("platformer","retro80s"):"pixel",
 ("puzzle","candy"):"uipack:Red:gloss", ("puzzle","cartoon"):"uipack:Green:gloss",
 ("puzzle","japan"):"adventure:red", ("puzzle","modern"):"uipack:Grey:flat",
 ("puzzle","sea"):"uipack:Blue:gloss",
 ("racing","arcade"):"scifi", ("racing","carbon"):"scifi",
 ("racing","motorsport"):"uipack:Red:line", ("racing","neon"):"scifi:blue",
 ("racing","street"):"uipack:Grey:flat",
 ("rpg","arcane"):"fantasy:002:grey", ("rpg","darkfantasy"):"fantasy:010:grey",
 ("rpg","fantasy"):"adventure:brown", ("rpg","parchment"):"adventure:grey",
 ("rpg","royal"):"fantasy:015:brown",
 ("shooter","cyberpunk"):"scifi:blue", ("shooter","military"):"adventure:grey",
 ("shooter","scifi"):"scifi:blue", ("shooter","space"):"scifi", ("shooter","toxic"):"scifi",
 ("strategy","blueprint"):"uipack:Blue:line", ("strategy","command"):"scifi",
 ("strategy","military"):"adventure:grey", ("strategy","royal"):"fantasy:015:brown",
 ("strategy","scifi"):"scifi:blue",
 ("survival","apocalypse"):"adventure:grey", ("survival","desert"):"adventure:brown",
 ("survival","frozen"):"uipack:Blue:flat", ("survival","industrial"):"adventure:grey",
 ("survival","wilderness"):"adventure:brown",
 ("topdown","classic"):"uipack:Grey:flat", ("topdown","fantasy"):"adventure:brown",
 ("topdown","japan"):"adventure:red", ("topdown","military"):"adventure:grey",
 ("topdown","nature"):"adventure:brown",
}

def first_existing(*cands):
    for c in cands:
        if c and os.path.isfile(c): return c
    return None

def resolve(desc):
    """Return dict slot->absolute source path (existing only)."""
    p = desc.split(":")
    kind = p[0]
    def A(root, rel): return os.path.join(SRC, root, "PNG", rel)
    if kind == "uipack":
        color, style = p[1], p[2]
        root = "UI Pack"
        normal = A(root, f"{color}\\Default\\button_rectangle_depth_{style}.png")
        pressed = A(root, f"{color}\\Default\\button_rectangle_{style}.png")
        panel = first_existing(A(root, f"{color}\\Default\\button_square_depth_flat.png"),
                               A(root, f"Grey\\Default\\button_square_depth_flat.png"))
        return {"button_normal":normal,"button_hover":normal,"button_pressed":pressed,"panel":panel}
    if kind == "adventure":
        tone = p[1]; root="UI Pack - Adventure"
        btn = A(root, f"Default\\button_{tone}.png")
        panel = first_existing(A(root, f"Default\\panel_{tone}.png"), A(root, "Default\\panel_brown.png"))
        return {"button_normal":btn,"button_hover":btn,"button_pressed":btn,"panel":panel}
    if kind == "scifi":
        blue = len(p)>1 and p[1]=="blue"; root="UI Pack - Sci-fi"
        metal = first_existing(A(root, "metalPanel_blue.png") if blue else A(root,"metalPanel.png"), A(root,"metalPanel.png"))
        panel = first_existing(A(root, "glassPanel.png"), A(root, "Extra\\Default\\panel_glass.png"))
        return {"button_normal":metal,"button_hover":metal,"button_pressed":metal,"panel":panel}
    if kind == "fantasy":
        num, tone = p[1], p[2]
        panel = A("Fantasy UI Borders", f"Default\\Border\\panel-border-{num}.png")
        btn = A("UI Pack - Adventure", f"Default\\button_{tone}.png")
        return {"button_normal":btn,"button_hover":btn,"button_pressed":btn,"panel":panel}
    if kind == "pixel":
        # Pixel Adventure has no PNG/ folder; tiles are under Tiles/Large tiles/Thick outline/.
        # STARTING PICK: one bordered box tile for both button and panel — the user picks the
        # exact tiles in-editor (91 tiles available). tile_0000 is a framed box.
        proot = os.path.join(SRC, "UI Pack - Pixel Adventure", "Tiles", "Large tiles", "Thick outline")
        btn = os.path.join(proot, "tile_0000.png")
        panel = os.path.join(proot, "tile_0000.png")
        return {"button_normal":btn,"button_hover":btn,"button_pressed":btn,"panel":panel}
    raise ValueError("bad desc "+desc)

def main():
    copied=missing=0
    misses=[]
    for (genre,theme),desc in MAP.items():
        slots = resolve(desc)
        outdir = os.path.join(DEST, genre, theme)
        os.makedirs(outdir, exist_ok=True)
        for slot,src in slots.items():
            dst = os.path.join(outdir, slot+".png")
            if src and os.path.isfile(src):
                shutil.copyfile(src, dst); copied+=1
            else:
                missing+=1; misses.append(f"{genre}/{theme}/{slot}  <- {desc}")
    # cursors
    cdir = os.path.join(DEST, "cursors"); os.makedirs(cdir, exist_ok=True)
    for name,rel in [("pointer","Cursor Pack\\PNG\\Basic\\Default"),("pointer_pixel","Cursor Pixel Pack\\Tiles")]:
        pass  # cursor selection handled in Phase 5 / CursorComponent; folder created here.
    print(f"copied={copied} missing={missing}")
    for m in misses: print("  MISS:", m)

if __name__=="__main__":
    main()

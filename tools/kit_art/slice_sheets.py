"""
Slice 9-patch widget art out of the reference sheets in Example_Art/ and write it in the layout
addons/.../ecs/ui/kit/KitArt.cs resolves:

    <dest>/<genre>/<widget>_<slot>.png     + a sibling <...>.png.margins

This is PLAN.md phase E's other half. The kit widgets already draw procedurally; KitArt lets any
slot be replaced by a real 9-patch, which is the ONLY way to reach the painted register the plan
records as "not reachable procedurally".

LICENSING — READ THIS BEFORE POINTING IT AT A SHEET
---------------------------------------------------
Example_Art/ is REFERENCE material. The project's own audit records that gameui2, gameui3 and
gameui7 are watermarked comps (Dreamstime, Game Art Partners, Envato) and are "style reference
only - not shippable art", with the standing rule that "shipped art stays CC0 Kenney or authored".

So this tool:
  * refuses the sheets flagged as comps unless --i-have-a-licence is passed;
  * defaults --dest to a path OUTSIDE the addon, because the addon must ship no third-party
    pixels (the same resolution docs/HUD_TEXTURE_SYSTEM.md reached for the Kenney HUD art);
  * writes a PROVENANCE.txt beside the output naming the sheet every file came from, so a
    project can always answer "where did this pixel come from".

USAGE
    python slice_sheets.py --list                       # show sheets and any recorded regions
    python slice_sheets.py --sheet rpgui --dest ../../../my-game/ui_art/kit --genre rpg \
        --slot button:base:120,340,300,86:14,14,14,14
    #                     ^widget ^slot  ^x,y,w,h        ^9-patch margins l,t,r,b

Each --slot is one crop. Margins are required: a 9-patch with guessed margins slices its own
corner artwork, which is the most visible way textured chrome goes wrong.
"""
import argparse
import os
import sys

try:
    from PIL import Image
except ImportError:
    sys.exit("PIL is required: pip install pillow")

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.normpath(os.path.join(HERE, "..", "..", "Example_Art"))

# Recorded by the licensing audit in plans/MASTER_TODO.md. Not exhaustive proof of the rest being
# clean -- it is the set we KNOW is unusable, so the tool can refuse it rather than rely on memory.
COMPS = {"gameui2", "gameui3", "gameui7"}


def sheets():
    if not os.path.isdir(ART):
        return []
    return sorted(f[:-4] for f in os.listdir(ART) if f.lower().endswith(".png"))


def parse_slot(spec):
    """widget:slot:x,y,w,h:l,t,r,b"""
    parts = spec.split(":")
    if len(parts) != 4:
        raise argparse.ArgumentTypeError(
            f"--slot must be widget:slot:x,y,w,h:l,t,r,b (got {spec!r})")
    widget, slot, rect, margins = parts
    try:
        x, y, w, h = (int(v) for v in rect.split(","))
        l, t, r, b = (int(v) for v in margins.split(","))
    except ValueError:
        raise argparse.ArgumentTypeError(f"--slot numbers malformed in {spec!r}")
    if w <= 0 or h <= 0:
        raise argparse.ArgumentTypeError(f"--slot has a non-positive size in {spec!r}")
    if l + r >= w or t + b >= h:
        raise argparse.ArgumentTypeError(
            f"--slot margins {l},{t},{r},{b} leave no centre in a {w}x{h} crop ({spec!r}); "
            "a 9-patch needs a stretchable middle")
    return widget, slot, (x, y, w, h), (l, t, r, b)


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--list", action="store_true", help="list available sheets and exit")
    ap.add_argument("--sheet", help="sheet name without .png, e.g. rpgui")
    ap.add_argument("--genre", default="_common",
                    help="genre folder to write into; _common dresses every genre")
    ap.add_argument("--dest", help="output root, e.g. <your-game>/ui_art/kit")
    ap.add_argument("--slot", action="append", type=parse_slot, default=[],
                    help="widget:slot:x,y,w,h:l,t,r,b (repeatable)")
    ap.add_argument("--i-have-a-licence", action="store_true",
                    help="acknowledge you have rights to a sheet flagged as a stock comp")
    args = ap.parse_args()

    if args.list or not args.sheet:
        print(f"Example_Art: {ART}")
        for s in sheets():
            print(f"  {s:<22}{'  [COMP - not shippable]' if s in COMPS else ''}")
        print("\nPoint --dest at YOUR GAME's folder, never at addons/ — the addon ships no "
              "third-party art.")
        return 0

    if args.sheet in COMPS and not args.i_have_a_licence:
        sys.exit(f"'{args.sheet}' is recorded as a watermarked stock comp (style reference only, "
                 "not shippable). Re-run with --i-have-a-licence if you have separately licensed "
                 "it.")

    if not args.dest:
        sys.exit("--dest is required. Point it at your game's own folder, e.g. "
                 "../../../my-game/ui_art/kit")
    if os.path.normpath(os.path.abspath(args.dest)).replace("\\", "/").find("/addons/") != -1:
        sys.exit("--dest points inside addons/. The addon must ship no third-party pixels; "
                 "slice into your own project and set beep/ui/kit_art_root to it.")
    if not args.slot:
        sys.exit("no --slot given, so there is nothing to cut.")

    src = os.path.join(ART, args.sheet + ".png")
    if not os.path.isfile(src):
        sys.exit(f"no such sheet: {src}")
    sheet = Image.open(src).convert("RGBA")

    out_dir = os.path.join(args.dest, args.genre)
    os.makedirs(out_dir, exist_ok=True)
    written = []

    for widget, slot, (x, y, w, h), margins in args.slot:
        if x < 0 or y < 0 or x + w > sheet.width or y + h > sheet.height:
            sys.exit(f"{widget}_{slot}: crop {x},{y},{w},{h} falls outside "
                     f"{args.sheet} ({sheet.width}x{sheet.height})")
        crop = sheet.crop((x, y, x + w, y + h))
        name = f"{widget}_{slot}.png"
        crop.save(os.path.join(out_dir, name))
        with open(os.path.join(out_dir, name + ".margins"), "w", encoding="utf-8") as f:
            f.write(" ".join(str(v) for v in margins))
        written.append((name, f"{args.sheet}.png @ {x},{y} {w}x{h}", margins))
        print(f"  wrote {args.genre}/{name}  ({w}x{h}, margins {margins})")

    # Provenance, always -- so a project can answer "where did this pixel come from".
    prov = os.path.join(args.dest, "PROVENANCE.txt")
    with open(prov, "a", encoding="utf-8") as f:
        for name, origin, margins in written:
            f.write(f"{args.genre}/{name}\t{origin}\tmargins={margins}\n")

    print(f"\n{len(written)} slot(s) -> {out_dir}")
    print(f"provenance appended to {prov}")
    print("\nNow set the kit art root in your project so KitArt finds it:")
    print('  ProjectSettings -> beep/ui/kit_art_root = "res://ui_art/kit"')
    return 0


if __name__ == "__main__":
    sys.exit(main())

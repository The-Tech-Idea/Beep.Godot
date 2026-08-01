"""Every Control in a scene must say where it is.

A Control with no layout properties sits at (0,0) at its minimum size and moves the moment the
viewport changes. Godot gives it no default and logs nothing, so it looks correct at the resolution
it was authored in and collides at every other one.

A Control is legitimately placed in ONE of two ways, and must have one:

  * a CONTAINER parent positions it   -- VBoxContainer, MarginContainer, CenterContainer, ...
  * it positions ITSELF               -- anchors + offsets

Container children are exempt, and that exemption is the whole reason this file was rewritten.
`layout_mode` is an EDITOR hint, not a runtime requirement: a Label inside a VBoxContainer is laid
out whether or not it carries one. The first version flagged them and reported 552 "defects",
nearly all of them fine. A gate that over-reports by an order of magnitude is worse than no gate --
it trains you to ignore it.

Non-Controls are exempt too. A component (`TemperatureComponent`, `ThemePresetComponent`) is a
plain Node with nothing to lay out; mistaking one for a widget sent an earlier pass chasing a
non-bug.

Run:  python tools/check_control_layout.py [roots...]
"""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
PARENTS = ROOT / "tools" / "godot_class_parents.txt"

# Any one of these means the node has said something about where it goes.
PLACED = ("layout_mode", "anchor", "offset_", "size_flags", "custom_minimum_size",
          "position", "grow_")

parents = {}
for line in PARENTS.read_text(encoding="utf-8").splitlines():
    if "\t" in line:
        k, v = line.split("\t", 1)
        parents[k] = v


def descends(t, ancestor):
    seen = set()
    while t and t not in seen:
        seen.add(t)
        if t == ancestor:
            return True
        t = parents.get(t)
    return False


NODE = re.compile(r'\[node name="([^"]+)"(?:\s+type="([^"]+)")?(?:\s+parent="([^"]+)")?')


def scan(scene):
    text = scene.read_text(encoding="utf-8", errors="ignore")
    blocks = re.split(r"(?m)^(?=\[node )", text)
    if len(blocks) < 2:
        return 0, []

    # name -> declared type, so a child can ask what its parent IS.
    types = {}
    for b in blocks[1:]:
        m = NODE.match(b)
        if m and m.group(2):
            types[m.group(1)] = m.group(2)
    root_match = NODE.match(blocks[1])
    root_type = root_match.group(2) if root_match else None

    problems, checked = [], 0
    for b in blocks[1:]:
        m = NODE.match(b)
        if not m or not m.group(2) or not m.group(3):
            continue                                   # instanced sub-scene, or the root itself
        name, ntype, parent = m.group(1), m.group(2), m.group(3)
        if not descends(ntype, "Control"):
            continue                                   # a component, not a widget

        pname = parent.split("/")[-1]
        ptype = root_type if pname == "." else types.get(pname)
        if ptype and descends(ptype, "Container"):
            continue                                   # its container places it

        checked += 1
        body = b.split("\n", 1)[1] if "\n" in b else ""
        if any(re.search(rf"(?m)^{p}", body) for p in PLACED):
            continue
        problems.append(f"  {name} ({ntype}) under '{parent}' ({ptype or '?'}) — NO layout "
                        f"properties; sits at (0,0) and moves with the viewport")
    return checked, problems


def main():
    roots = sys.argv[1:] or ["addons", "examples"]
    total = bad = scenes = 0
    for r in roots:
        for scene in sorted((ROOT / r).rglob("*.tscn")):
            scenes += 1
            checked, problems = scan(scene)
            total += checked
            if problems:
                print(f"FAIL {scene.relative_to(ROOT)}")
                for p in problems:
                    print(p)
                bad += len(problems)

    print(f"\ncontrol layout: {total} free-standing Controls in {scenes} scenes, "
          + ("all placed" if bad == 0 else f"{bad} UNPLACED"))
    print("PASS" if bad == 0 else "FAIL")
    sys.exit(0 if bad == 0 else 1)


main()

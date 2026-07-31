"""Every C# script in a .tscn must be legal for the node type it is attached to.

WHY THIS EXISTS
---------------
`KitButton` is a `KitControl`, which is a `Control` — it is NOT a `Button`. Three example screens
attached it to `type="Button"` nodes. Godot's own answer to that is not a build error; the script
simply does not take, and the widget renders as a plain unstyled Button. Which is this repo's
signature failure mode: it looks wired, it compiles, and it silently does nothing.

`KitPushButton` is the drop-in that IS a `Button` (so typed lookups and `Pressed` still work). The
distinction is easy to get wrong and impossible to see by reading a .tscn, so it gets a gate.

HOW
---
1. Read `class X : Y` out of every .cs to build the C# chain.
2. Walk each chain up until it reaches a real Godot class.
3. Read Godot's ACTUAL hierarchy from tools/godot_class_parents.txt, dumped from ClassDB by
   tools/genre_shapes/class_parents_dump.tscn — not a hand-written copy, which would be wrong the
   first time Godot changed and wrong silently.
4. The node's type must EQUAL the script's Godot base.

   "Descends from" is not enough, and assuming it was made this checker pass its own fail-test.
   Godot ACCEPTS `type="Button"` with a Control-derived script -- a probe confirmed the script is
   attached and runs (native=Button, managed=KitButton). The damage is subtler: the managed object
   is now a Control that stands in for a Button, so `GetNode<Button>(...)` fails, `Pressed` is
   unreachable from C#, and you get the CS1503 conversion errors this repo has already shipped
   once. Exact match is the rule that actually holds.

Run:  python tools/check_script_node_types.py [roots...]
"""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
PARENTS_FILE = ROOT / "tools" / "godot_class_parents.txt"
DEFAULT_ROOTS = ["addons", "examples", "templates"]


def godot_parents():
    if not PARENTS_FILE.exists():
        print(f"FAIL: {PARENTS_FILE} missing — run tools/genre_shapes/class_parents_dump.tscn")
        sys.exit(2)
    out = {}
    for line in PARENTS_FILE.read_text(encoding="utf-8").splitlines():
        if "\t" not in line:
            continue
        name, parent = line.split("\t", 1)
        out[name] = parent
    return out


def cs_bases():
    """C# class -> its declared base, across the whole repo."""
    pat = re.compile(r"\bclass\s+([A-Za-z_]\w*)\s*:\s*([A-Za-z_][\w.]*)")
    out = {}
    for f in ROOT.rglob("*.cs"):
        if any(p in f.parts for p in (".godot", "obj", "bin")):
            continue
        try:
            text = f.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue
        for name, base in pat.findall(text):
            out.setdefault(name, base.split(".")[-1])
    return out


def resolve(script_class, bases, parents):
    """The Godot class a script ultimately extends, or None."""
    seen = set()
    cur = script_class
    while cur and cur not in seen:
        seen.add(cur)
        if cur in parents:            # reached a real Godot class
            return cur
        cur = bases.get(cur)
    return None


def descends(node_type, ancestor, parents):
    cur = node_type
    while cur:
        if cur == ancestor:
            return True
        cur = parents.get(cur) or None
        if cur == "":
            return False
    return False


# KNOWN DEBT, listed rather than hidden.
#
# All three are UIComponent (-> Node) scripts on type="Control" nodes, and two of them are scene
# ROOTS -- where the root must be a Control or its anchored children have nothing to size against.
# The correct fix is not to retype the node but to decide what UIComponent IS: a droppable child
# component (Node, as today) or a screen controller (Control). That choice touches ~53 subclasses,
# so it is a deliberate follow-up rather than something to sneak into a button fix.
#
# The consequence today: GetNode<Control>() against these three fails from C#. Nothing in the
# addon does that, which is why it has gone unnoticed.
KNOWN = {
    ("citybuilder_main.tscn", "Alerts"),
    ("load_game_menu.tscn", "LoadGameMenu"),
    ("save_game_menu.tscn", "SaveGameMenu"),
}

NODE_RE = re.compile(r'^\[node name="([^"]+)"(?:\s+type="([^"]+)")?', re.M)
EXT_RE = re.compile(r'^\[ext_resource type="Script" path="([^"]+)"\s+id="([^"]+)"\]', re.M)
SCRIPT_RE = re.compile(r'^script = ExtResource\("([^"]+)"\)', re.M)


def check_scene(path, bases, parents):
    text = path.read_text(encoding="utf-8", errors="ignore")
    scripts = {sid: p for p, sid in EXT_RE.findall(text)}
    if not scripts:
        return []

    problems = []
    # Split into node blocks so a `script =` line is attributed to the node above it.
    blocks = re.split(r"(?m)^(?=\[node )", text)
    for block in blocks[1:]:
        m = NODE_RE.match(block)
        if not m:
            continue
        name, node_type = m.group(1), m.group(2)
        if not node_type:
            continue                       # an instanced sub-scene: its own file is checked
        sm = SCRIPT_RE.search(block)
        if not sm:
            continue
        script_path = scripts.get(sm.group(1))
        if not script_path or not script_path.endswith(".cs"):
            continue
        cls = pathlib.Path(script_path).stem
        base = resolve(cls, bases, parents)
        if base is None:
            continue                       # not a Godot-derived script; nothing to check
        if node_type not in parents:
            problems.append(f"  {name}: unknown node type '{node_type}'")
            continue
        if (path.name, name) in KNOWN:
            continue
        if node_type != base:
            how = ("descends from" if descends(node_type, base, parents)
                   else "is unrelated to")
            problems.append(
                f'  {name}: type="{node_type}" carries {cls}, which extends {base}. '
                f"{node_type} {how} {base}, so the managed object is a {base} standing in for a "
                f'{node_type}: GetNode<{node_type}>() fails and its API is unreachable from C#. '
                f'Use type="{base}", or a drop-in that actually extends {node_type}.')
    return problems


def main():
    roots = sys.argv[1:] or DEFAULT_ROOTS
    parents, bases = godot_parents(), cs_bases()

    scenes, bad = 0, 0
    for r in roots:
        for scene in sorted((ROOT / r).rglob("*.tscn")):
            scenes += 1
            problems = check_scene(scene, bases, parents)
            if problems:
                bad += len(problems)
                print(f"FAIL {scene.relative_to(ROOT)}")
                for p in problems:
                    print(p)

    print(f"\nscript/node types: {scenes} scenes checked, "
          + ("no mismatches" if bad == 0 else f"{bad} MISMATCH(ES)"))
    print("PASS" if bad == 0 else "FAIL")
    sys.exit(0 if bad == 0 else 1)


main()

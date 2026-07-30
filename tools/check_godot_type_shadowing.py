#!/usr/bin/env python3
"""Fail if the addon uses a bare Godot type name that a USER PROJECT is likely to shadow.

WHY
---
Godot generates `Control.cs` at the project root whenever you attach a script to a Control node
without naming the class:

    public partial class Control : Godot.Control { }

That lands in the GLOBAL namespace, so inside `namespace Beep.ECS.UI.Kit` a bare `Control` binds
to the *user's* class, not Godot's. Two things then happen, and the second is much worse:

  1. Some sites fail to COMPILE -- "cannot convert from 'KitCheckButton' to 'Control'". Loud.
  2. Others compile fine and bind to the wrong type at RUNTIME. `GetParent() as Control` returns
     null for every real Godot Control, `Find<Control>("X")` matches nothing, `node is Control`
     is false. The component does nothing and says nothing.

(2) is this repo's dominant defect class, and a user project triggers it just by using the editor
normally. ~60 addon files already write `Godot.Control` for exactly this reason; the rule was
convention, never enforced, so a new file broke it.

The same applies to any Godot class a user might name a script after. `Node`, `Timer`, `Label`,
`Button`, `Camera2D` and friends are all plausible root-level script names.

USAGE
    python tools/check_godot_type_shadowing.py          # report, exit 1 on any hit
"""
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "addons")

# Godot types a user project realistically defines a same-named script for. Deliberately not the
# whole ClassDB: a name like `CanvasItem` is never a user script, and flagging it would be noise
# that gets the check switched off.
# Scope: `Control` only, for now.
#
# Godot generates a root-level script named after the node type, so `Node.cs`, `Label.cs` and
# `Button.cs` are all possible too -- a wider sweep flags 515 sites across the addon. That is a
# real exposure and it is recorded in the plan, but turning it on before those sites are fixed
# would mean shipping a check that always fails, which is how a check gets switched off.
#
# `Control` is the one with a DEMONSTRATED failure (two CS1503s in a real user project, plus
# silent runtime misbinding), so it is enforced now and the rest follows behind the fixes.
SHADOWABLE = ["Control"]

# Code positions where a bare name is a TYPE reference. Strings and comments are stripped first.
def patterns(t):
    return [
        re.compile(r'(?<![.\w])' + t + r'\s*\??\s+[A-Za-z_]\w*\s*[;=,){]'),  # decl / param
        re.compile(r'(?<![.\w])as\s+' + t + r'(?![\w])'),
        re.compile(r'(?<![.\w])is\s+' + t + r'(?![\w])'),
        re.compile(r'<\s*' + t + r'\s*[>,]'),
        re.compile(r'(?<![.\w])new\s+' + t + r'\s*[({]'),
        re.compile(r'\(\s*' + t + r'\s*\)'),                                  # cast
    ]


def strip_noise(src):
    """Remove comments and string literals so warning text does not produce false hits.

    Replaced with same-length blanks so reported line numbers stay correct.
    """
    out = list(src)
    i, n = 0, len(src)
    while i < n:
        c = src[i]
        if c == '/' and i + 1 < n and src[i + 1] == '/':
            while i < n and src[i] != '\n':
                out[i] = ' '
                i += 1
        elif c == '/' and i + 1 < n and src[i + 1] == '*':
            while i < n and not (src[i] == '*' and i + 1 < n and src[i + 1] == '/'):
                if src[i] != '\n':
                    out[i] = ' '
                i += 1
            i += 2
        elif c == '"':
            # Handles "..." and $"..."; verbatim @"..." is close enough for this purpose.
            out[i] = ' '
            i += 1
            while i < n and src[i] != '"':
                if src[i] == '\\':
                    out[i] = ' '
                    i += 1
                if i < n and src[i] != '\n':
                    out[i] = ' '
                i += 1
            if i < n:
                out[i] = ' '
            i += 1
        else:
            i += 1
    return "".join(out)


def main():
    hits = []
    for dirpath, _, files in os.walk(ROOT):
        for f in files:
            if not f.endswith(".cs"):
                continue
            p = os.path.join(dirpath, f)
            src = strip_noise(open(p, encoding="utf-8", errors="replace").read())
            for ln, line in enumerate(src.split("\n"), 1):
                if "Godot." in line and not re.search(r'(?<!Godot\.)\bControl\b', line):
                    pass
                for t in SHADOWABLE:
                    # A fully-qualified use is exactly what we want; blank it before matching.
                    probe = line.replace(f"Godot.{t}", " " * (len(t) + 6))
                    for pat in patterns(t):
                        if pat.search(probe):
                            hits.append((os.path.relpath(p, ROOT), ln, t, line.strip()[:88]))
                            break

    if not hits:
        print("  ok — no bare shadowable Godot type names in the addon")
        return 0

    print(f"  {len(hits)} bare Godot type name(s) a user project can shadow:\n")
    for path, ln, t, text in hits:
        print(f"    {path}:{ln}  bare '{t}'  ->  write 'Godot.{t}'")
        print(f"        {text}")
    print("\n  Godot generates e.g. `public partial class Control : Godot.Control` at the")
    print("  project root when a script is attached to a Control node. A bare name then binds")
    print("  to THAT class: some sites fail to compile, others silently bind wrong at runtime.")
    return 1


if __name__ == "__main__":
    sys.exit(main())

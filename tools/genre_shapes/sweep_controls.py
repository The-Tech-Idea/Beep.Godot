#!/usr/bin/env python3
"""Attach the kit's drop-in scripts to the stock Godot controls still left in the templates.

WHY A SCRIPT AND NOT HAND EDITS
-------------------------------
The last sweep of this kind touched 108 buttons across 35 files and shipped two bugs that
compiled clean and passed the scene validator. A script states exactly what it changed, can be
re-run after a template changes, and refuses rather than half-applying.

WHAT IT DOES
------------
Adds `script = ExtResource(...)` to nodes of a given Godot type. It does NOT change the node's
type: every drop-in derives from the type it replaces (KitOptionButton : OptionButton, ...), so
the scene tree, every `Find<T>` and every signal binding stay exactly as they were. That is the
whole reason the drop-ins are shaped this way.

    python sweep_controls.py --dry     # report only (default)
    python sweep_controls.py --apply
"""
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                    "..", "..", "addons", "beep_game_builder_cs",
                                    "templates", "scenes"))
KIT = "res://addons/beep_game_builder_cs/ecs/ui/kit"

# godot type -> (drop-in script, ext_resource id)
MAP = {
    "HSlider":      (f"{KIT}/KitSliderBar.cs",     "k_slider"),
    "OptionButton": (f"{KIT}/KitOptionButton.cs",  "k_opt"),
    "CheckButton":  (f"{KIT}/KitCheckButton.cs",   "k_check"),
    "TabContainer": (f"{KIT}/KitTabPanel.cs",      "k_tabs"),
}

NODE_RE = re.compile(r'^\[node name="([^"]+)" type="([^"]+)"(.*)\]\s*$', re.M)


def sweep(path, apply):
    src = open(path, encoding="utf-8").read()
    changed, notes = src, []

    for gtype, (script, rid) in MAP.items():
        # Which nodes of this type lack a script?
        hits = []
        for m in NODE_RE.finditer(changed):
            if m.group(2) != gtype:
                continue
            # The node's property block runs to the next [ or EOF.
            nxt = changed.find("\n[", m.end())
            block = changed[m.end():nxt if nxt != -1 else len(changed)]
            if "script = ExtResource" in block:
                continue                      # already carries a script; never stack two
            hits.append((m, block))
        if not hits:
            continue

        if f'path="{script}"' not in changed:
            # Insert the ext_resource after the last existing one, keeping the header tidy.
            last = None
            for m in re.finditer(r'^\[ext_resource .*\]\s*$', changed, re.M):
                last = m
            line = f'[ext_resource type="Script" path="{script}" id="{rid}"]'
            if last:
                changed = changed[:last.end()] + "\n" + line + changed[last.end():]
            else:
                hdr = re.search(r'^\[gd_scene[^\]]*\]\s*$', changed, re.M)
                changed = changed[:hdr.end()] + "\n\n" + line + changed[hdr.end():]
            # load_steps counts ext+sub resources; a wrong value makes Godot log a load error.
            changed = re.sub(
                r'(\[gd_scene load_steps=)(\d+)',
                lambda mm: mm.group(1) + str(int(mm.group(2)) + 1), changed, count=1)

        # Re-find after the header edit shifted offsets.
        for name, _ in [(m.group(1), b) for m, b in hits]:
            pat = re.compile(r'(^\[node name="' + re.escape(name) + r'" type="'
                             + gtype + r'"[^\]]*\]\s*\n)', re.M)
            m2 = pat.search(changed)
            if not m2:
                notes.append(f"    ! {name} ({gtype}) vanished after header edit — SKIPPED")
                continue
            changed = changed[:m2.end()] + f'script = ExtResource("{rid}")\n' + changed[m2.end():]
            notes.append(f"    {name:<24} {gtype:<14} -> {os.path.basename(script)}")

    if notes and apply:
        open(path, "w", encoding="utf-8", newline="\n").write(changed)
    return notes


if __name__ == "__main__":
    apply = "--apply" in sys.argv
    total = 0
    for dirpath, _, files in os.walk(ROOT):
        for f in sorted(files):
            if not f.endswith(".tscn"):
                continue
            p = os.path.join(dirpath, f)
            notes = sweep(p, apply)
            if notes:
                print(f"  {os.path.relpath(p, ROOT)}")
                print("\n".join(notes))
                total += len(notes)
    print(f"\n{total} control(s) {'swept' if apply else 'would be swept (dry run)'}")

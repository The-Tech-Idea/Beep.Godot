#!/usr/bin/env bash
# Validate every .tscn template for the faults that have actually shipped here.
#
# Godot's text-scene parser is quiet about most of these: it drops what it can't
# resolve and loads the scene anyway, so a broken template looks fine until a
# button does nothing at runtime. Run this after editing any template.
#
#   ./validate_scenes.sh          # from templates/scenes/
#
# Exit code 1 if anything is wrong.
#
# Each check exists because it caught a real bug:
#   1 undeclared ExtResource  — duplicate GameStateManager nodes referenced 10_state,
#                               declared only in platformer_main.tscn -> parse error.
#   2 undeclared SubResource  — save/load menus referenced LabelSettings_title and
#                               StyleBoxFlat_slot_bg with no [sub_resource] blocks.
#   3 parent path resolution  — save_game_menu's Slot0-4 claimed
#                               PanelContainer/VBox/SlotsVBox (real node was under
#                               SlotsScroll), so the slots vanished and the component
#                               crashed indexing an empty container.
#                               NOTE: check every segment, not just the first — an
#                               earlier version of this check only tested the first
#                               and passed this file.
#   4 duplicate siblings      — two GameStateManager nodes under the same parent.
#   5 malformed node headers  — `[node name="X" type="Y"` left unclosed with
#                               `script = ...]` swallowed into the tag: Godot silently
#                               drops the script, so _Ready never runs and every
#                               button is dead.
set -uo pipefail
cd "$(dirname "$0")"
fail=0

echo "--- undeclared ExtResource ---"; found=0
for f in $(find . -name "*.tscn" | sort); do
  decl=$(grep -oE '^\[ext_resource .*id="[^"]+"' "$f" | sed -E 's/.*id="([^"]+)"/\1/' | sort -u)
  for r in $(grep -oE 'ExtResource\("[^"]+"\)' "$f" | sed -E 's/ExtResource\("([^"]+)"\)/\1/' | sort -u); do
    printf '%s\n' "$decl" | grep -qx -- "$r" || { echo "  $f -> ExtResource(\"$r\")"; found=1; fail=1; }
  done
done; [ $found -eq 0 ] && echo "  ok"

echo "--- undeclared SubResource ---"; found=0
for f in $(find . -name "*.tscn" | sort); do
  decl=$(grep -oE '^\[sub_resource .*id="[^"]+"' "$f" | sed -E 's/.*id="([^"]+)"/\1/' | sort -u)
  for r in $(grep -oE 'SubResource\("[^"]+"\)' "$f" | sed -E 's/SubResource\("([^"]+)"\)/\1/' | sort -u); do
    printf '%s\n' "$decl" | grep -qx -- "$r" || { echo "  $f -> SubResource(\"$r\")"; found=1; fail=1; }
  done
done; [ $found -eq 0 ] && echo "  ok"

echo "--- parent paths (every segment) ---"; found=0
for f in $(find . -name "*.tscn" | sort); do
  out=$(awk -v F="$f" '
    /^\[node / {
      name=""; parent="__ROOT__"
      if (match($0, /name="[^"]+"/))   name=substr($0,RSTART+6,RLENGTH-7)
      if (match($0, /parent="[^"]*"/)) parent=substr($0,RSTART+8,RLENGTH-9)
      if (parent=="__ROOT__") { paths["."]=1; next }
      if (!(parent in paths)) print "  " F " -> node \"" name "\" parent=\"" parent "\" does not exist"
      paths[(parent=="." ? name : parent "/" name)]=1
    }' "$f")
  [ -n "$out" ] && { echo "$out"; found=1; fail=1; }
done; [ $found -eq 0 ] && echo "  ok"

echo "--- duplicate sibling node names ---"; found=0
for f in $(find . -name "*.tscn" | sort); do
  dup=$(grep -oE '^\[node name="[^"]+"( type="[^"]+")? parent="[^"]*"' "$f" | sort | uniq -d)
  [ -n "$dup" ] && { echo "  $f"; echo "$dup" | sed 's/^/      /'; found=1; fail=1; }
done; [ $found -eq 0 ] && echo "  ok"

echo "--- malformed node headers ---"; found=0
for f in $(find . -name "*.tscn" | sort); do
  grep -qE '^\[node name="[^"]*" type="[^"]*"$' "$f" && { echo "  $f (unclosed [node ...] header)"; found=1; fail=1; }
done; [ $found -eq 0 ] && echo "  ok"

echo "--- script files referenced actually exist ---"; found=0
for f in $(find . -name "*.tscn" | sort); do
  for p in $(grep -oE 'path="res://addons/beep_game_builder_cs/[^"]+\.cs"' "$f" | sed -E 's|path="res://addons/beep_game_builder_cs/([^"]+)"|\1|'); do
    [ -f "../../$p" ] || { echo "  $f -> missing $p"; found=1; fail=1; }
  done
done; [ $found -eq 0 ] && echo "  ok"

# Every component here derives from EntityComponent -> Node, so its C# type can only
# ever represent a plain Node. Attaching one to a typed node (CharacterBody2D,
# ParallaxLayer, ...) means the script silently fails to drive it — the genre templates
# put the player controller straight onto the CharacterBody2D, so ResolveBody2D()
# returned null and the player could not move. Put the script on a child Node instead.
echo "--- scripts attached to a typed node (must be Node/Control/CanvasLayer/Node2D) ---"; found=0
for f in $(find . -name "*.tscn" | sort); do
  out=$(awk -v F="$f" '
    /^\[ext_resource type="Script"/ {
      id=""; p=""
      if (match($0,/id="[^"]+"/))   id=substr($0,RSTART+4,RLENGTH-5)
      if (match($0,/path="[^"]+"/)) p=substr($0,RSTART+6,RLENGTH-7)
      script[id]=p; next }
    /^\[node / { curtype=""; curname=""
      if (match($0,/type="[^"]+"/)) curtype=substr($0,RSTART+6,RLENGTH-7)
      if (match($0,/name="[^"]+"/)) curname=substr($0,RSTART+6,RLENGTH-7)
      next }
    /^script = ExtResource\(/ {
      id=""
      if (match($0,/ExtResource\("[^"]+"\)/)) id=substr($0,RSTART+13,RLENGTH-15)
      if (!(id in script)) next
      if (curtype=="Node" || curtype=="Control" || curtype=="CanvasLayer" || curtype=="Node2D") next
      print "  " F " -> node \"" curname "\" is " curtype " with script " script[id]
    }' "$f")
  [ -n "$out" ] && { echo "$out"; found=1; fail=1; }
done; [ $found -eq 0 ] && echo "  ok"

echo "--- PackedScene ext_resources actually exist ---"; found=0
for f in $(find . -name "*.tscn" | sort); do
  for p in $(grep -oE 'type="PackedScene" path="res://addons/beep_game_builder_cs/[^"]+\.tscn"' "$f" | sed -E 's|.*path="res://addons/beep_game_builder_cs/([^"]+)".*|\1|'); do
    [ -f "../../$p" ] || { echo "  $f -> missing PackedScene $p"; found=1; fail=1; }
  done
done; [ $found -eq 0 ] && echo "  ok"

# Atmosphere (weather/day-night/fog/etc.) belongs to world-genre gameplay only, and is now
# shared via atmosphere.tscn. No other scene should reference ecs/atmosphere/ scripts
# directly — a menu or board-genre main doing so is the placement bug this guards against.
echo "--- atmosphere scripts only in atmosphere.tscn ---"; found=0
for f in $(find . -name "*.tscn" | sort); do
  [ "$(basename "$f")" = "atmosphere.tscn" ] && continue
  grep -qE 'path="res://addons/beep_game_builder_cs/ecs/atmosphere/' "$f" \
    && { echo "  $f -> references ecs/atmosphere/ directly (should instance atmosphere.tscn)"; found=1; fail=1; }
done; [ $found -eq 0 ] && echo "  ok"

[ $fail -eq 0 ] && echo "PASS: all scenes valid" || echo "FAIL: see above"
exit $fail

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
    /^\[node / { innode=1; curtype=""; curname=""
      if (match($0,/type="[^"]+"/)) curtype=substr($0,RSTART+6,RLENGTH-7)
      if (match($0,/name="[^"]+"/)) curname=substr($0,RSTART+6,RLENGTH-7)
      next }
    # Any other section header (sub_resource, gd_scene, ...) leaves node context. A
    # [sub_resource type="Resource"] legitimately carries a `script =` line (a scripted
    # custom Resource like GameItem) — that is not a node, so the node-script rule must skip it.
    /^\[/ { innode=0; next }
    /^script = ExtResource\(/ {
      if (!innode) next
      id=""
      if (match($0,/ExtResource\("[^"]+"\)/)) id=substr($0,RSTART+13,RLENGTH-15)
      if (!(id in script)) next
      if (curtype=="Node" || curtype=="Control" || curtype=="CanvasLayer" || curtype=="Node2D") next
      print "  " F " -> node \"" curname "\" is " curtype " with script " script[id]
    }' "$f")
  # A script on a NON-generic node type is fine when its class actually DERIVES from that type —
  # e.g. KitPanelContainer : PanelContainer on a PanelContainer node. The allowed-list above
  # predates the kit, when every script was Node/Control-based; without this filter the rule
  # rejects the one arrangement that makes a kit container a safe drop-in for a bare
  # PanelContainer (keeping the container's layout, so no child collapses).
  if [ -n "$out" ]; then
    real=""
    while IFS= read -r line; do
      [ -z "$line" ] && continue
      ntype=$(printf '%s' "$line" | sed -n 's/.* is \([A-Za-z0-9_]*\) with script .*/\1/p')
      spath=$(printf '%s' "$line" | sed -n 's|.* with script res://\(.*\)$|\1|p')
      cs="../../../../$spath"
      if [ -n "$ntype" ] && [ -f "$cs" ] && \
         grep -qE "class[[:space:]]+[A-Za-z0-9_]+[[:space:]]*:[[:space:]]*(Godot\.)?$ntype([[:space:]]|$|,)" "$cs"; then
        continue
      fi
      real="${real}${line}
"
    done <<< "$out"
    out=$(printf '%s' "$real")
  fi
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

# Godot registers a C# [Export] under its exact PascalCase name — the source generator
# emits `StringName @TitleLabelPath = "TitleLabelPath"` and SetGodotClassPropertyValue
# compares against that. A .tscn line written GDScript-style (`title_label_path = ...`)
# matches nothing, returns false, and is dropped in silence: the scene loads, the node
# runs on defaults, and nothing anywhere says so. That is exactly how every
# GameInfoBinder / AnimatedMenuComponent / SceneTransitionComponent in this folder sat
# inert across 67 assignments — no titles bound, no window title, no transition timing.
#
# Built-in Godot properties ARE snake_case (anchors_preset, custom_minimum_size) and are
# not the target here: both checks below only fire on names that correspond to a real
# [Export] in the C# addon, which no built-in does.
echo "--- C# export properties are PascalCase in scenes (Godot silently drops snake_case) ---"; found=0
# NB: grep -E is POSIX ERE — no \s. Use [[:space:]].
# Covers three [Export] member forms, all of which ship here:
#   `[Export] public T Name { get; set; } = x;`   (auto-property, one line)
#   `[Export]` \n `public T Name { get; set; }`    (auto-property, brace same line, [Export] above)
#   `[Export]` \n `public T Name`                  (full property, brace on the NEXT line — e.g.
#                                                    ThemePresetComponent.GenreName/PresetName/PaletteName)
# The old extraction required a trailing {;= on the captured line, so it MISSED the third form and
# false-flagged every scene that set GenreName/PresetName/etc. Now: take any decl line carrying an
# access modifier (but not a method — no '('), strip the initializer/body, and keep the trailing name.
EXPORTS=$(grep -rh -A1 -E '\[Export' ../../ecs ../../core 2>/dev/null \
  | grep -E '\b(public|internal|protected)\b' \
  | sed -E 's/\[Export[^]]*\]//g; s/=.*$//; s/\{.*$//; s/;.*$//' \
  | grep -vE '\(' \
  | grep -oE '[A-Za-z_][A-Za-z0-9_]*[[:space:]]*$' \
  | sed -E 's/[[:space:]]+$//' | sort -u | grep -v '^$')

EXPORT_LIST=$(mktemp); printf '%s\n' "$EXPORTS" > "$EXPORT_LIST"
for f in $(find . -name "*.tscn" | sort); do
  out=$(awk -v F="$f" -v EL="$EXPORT_LIST" '
    BEGIN { while ((getline line < EL) > 0) if (line != "") known[line]=1 }
    /^\[node /            { scripted=0 }
    /^script = ExtResource\(/ { scripted=1 }
    /^[A-Za-z_][A-Za-z0-9_]* = / {
      if (!scripted || seen[$1]++) next
      key=$1
      if (key ~ /_/) {                       # snake_case: only a bug if it names a real export
        n=split(key, part, "_"); pascal=""
        for (i=1; i<=n; i++) pascal = pascal toupper(substr(part[i],1,1)) substr(part[i],2)
        if (pascal in known)
          print "  " F " -> \x27" key "\x27 is silently ignored; Godot expects \x27" pascal "\x27"
      } else if (key ~ /^[A-Z]/) {           # PascalCase on a scripted node must name a real export
        if (!(key in known))
          print "  " F " -> \x27" key "\x27 matches no [Export] in the addon (stale or typo; ignored at load)"
      }
    }' "$f")
  [ -n "$out" ] && { echo "$out"; found=1; fail=1; }
done; rm -f "$EXPORT_LIST"; [ $found -eq 0 ] && echo "  ok"

# Archetype: some components resolve their parent as an Area2D (BodyEntered/BodyExited) and
# do nothing at all when the parent is a different node — silently. This caught the live
# InteractableComponent-on-a-CharacterBody2D bug that made the topdown player unable to
# interact with anything. Resolve each script-bearing node's PARENT type and flag the mismatch.
echo "--- Area2D-parented components actually have an Area2D parent ---"; found=0
for f in $(find . -name "*.tscn" | sort); do
  out=$(awk -v F="$f" '
    /^\[ext_resource type="Script"/ {
      id=""; p=""
      if (match($0,/id="[^"]+"/))   id=substr($0,RSTART+4,RLENGTH-5)
      if (match($0,/path="[^"]+"/)) { p=substr($0,RSTART+6,RLENGTH-7); sub(/.*\//,"",p) }
      script[id]=p; next }
    /^\[node / {
      name=""; type=""; parent="__ROOT__"
      if (match($0,/name="[^"]+"/))    name=substr($0,RSTART+6,RLENGTH-7)
      if (match($0,/type="[^"]+"/))    type=substr($0,RSTART+6,RLENGTH-7)
      if (match($0,/parent="[^"]*"/))  parent=substr($0,RSTART+8,RLENGTH-9)
      if (parent=="__ROOT__") { nodeType["."]=type }
      else { mypath=(parent=="." ? name : parent"/"name); nodeType[mypath]=type }
      curParent=parent; curName=name; next }
    /^script = ExtResource\(/ {
      id=""
      if (match($0,/ExtResource\("[^"]+"\)/)) id=substr($0,RSTART+13,RLENGTH-15)
      base=(id in script)?script[id]:""
      if (base ~ /^(Pickup|Interactable|DoorSwitch|Checkpoint|Projectile)Component\.cs$/ && curParent!="__ROOT__") {
        ptype=(curParent in nodeType)?nodeType[curParent]:""     # "" = instanced/untyped parent, cannot judge
        if (ptype!="" && ptype!="Area2D")
          print "  " F ": node \"" curName "\" (" base ") needs an Area2D parent but its parent is " ptype
      }
    }' "$f")
  [ -n "$out" ] && { echo "$out"; found=1; fail=1; }
done; [ $found -eq 0 ] && echo "  ok"

# Navigation: a screen's root C# script wires its buttons BY NODE PATH in _Ready()
# (ConnectPressed("Margin/VBox/Header/BackButton", ...), GetNodeOrNull<Button>("...")).
# If that path doesn't match a real node in the .tscn, the button is DEAD — ConnectPressed
# warns at runtime, GetNodeOrNull is silent. Either way the scene loads fine and looks wired.
# This connects the two halves the other checks never cross: script paths vs scene tree.
echo "--- screen scripts wire buttons that exist in the scene ---"; found=0
ADDON="../.."                           # templates/scenes -> addon root is two up
for f in $(find . -name "*.tscn" | sort); do
  sid=$(awk '/^\[node /{n++} n==1 && /^script = ExtResource\(/{ if (match($0,/ExtResource\("([^"]+)"\)/,m)) print m[1]; exit }' "$f")
  [ -z "$sid" ] && continue
  spath=$(awk -v id="$sid" '/^\[ext_resource / && index($0,"id=\""id"\"")>0 { if (match($0,/path="res:\/\/addons\/beep_game_builder_cs\/([^"]+)"/,m)) print m[1]; exit }' "$f")
  case "$spath" in ecs/scenes/*) ;; *) continue;; esac
  cs="$ADDON/$spath"
  [ -f "$cs" ] || { echo "  $f -> root script missing: $spath"; found=1; fail=1; continue; }
  paths=$(awk '/^\[node / { name=""; parent="__ROOT__"
      if (match($0,/name="[^"]+"/)) name=substr($0,RSTART+6,RLENGTH-7)
      if (match($0,/parent="[^"]*"/)) parent=substr($0,RSTART+8,RLENGTH-9)
      if (parent=="__ROOT__") next
      if (parent==".") print name; else print parent "/" name }' "$f")
  # Bare node NAMES too: the screen scripts now resolve with ConnectButton("BackButton") /
  # Find<T>("Tabs") so a restyle that inserts a wrapper can't kill a button (the save/load
  # regression). A reference is valid if it matches a full path OR a node name.
  names=$(awk '/^\[node / { if (match($0,/name="[^"]+"/)) print substr($0,RSTART+6,RLENGTH-7) }' "$f")
  refs=$(grep -oE '(ConnectPressed|ConnectToggled|ConnectButton|Find<[^>]*>|GetNodeOrNull<[^>]*>|GetNode<[^>]*>)\("[^"]+"' "$cs" \
         | grep -oE '"[^"]+"' | tr -d '"' | sort -u)
  for r in $refs; do
    case "$r" in res://*|"") continue;; esac
    printf '%s\n' "$paths" | grep -qx -- "$r" && continue
    printf '%s\n' "$names" | grep -qx -- "$r" && continue
    echo "  $f -> script $(basename "$cs") wires '$r', which is not a node in the scene"; found=1; fail=1
  done
done; [ $found -eq 0 ] && echo "  ok"

# The scripts resolve controls by NAME (ConnectButton/Find<T>), so a name a script looks up
# scene-wide has to identify ONE node. shooter/character_select shipped four buttons all named
# "SelectButton" and platformer/level_select two "Level1Button"s — unambiguous as paths, but a
# scene-wide name lookup returns whichever comes first in the tree, so three of four characters
# silently picked the wrong card.
#
# Only names the ROOT SCRIPT resolves scene-wide are flagged. Repeated names are perfectly fine
# when the lookup is scoped to a row — load_game_menu has one "SlotButton"/"DeleteButton" per
# slot and LoadGameMenuComponent resolves them per PanelContainer, never scene-wide. Flagging
# those would be crying wolf, and a check that cries wolf gets ignored.
echo "--- Button names the root script resolves scene-wide are unique ---"; found=0
for f in $(find . -name "*.tscn" | sort); do
  sid=$(awk '/^\[node /{n++} n==1 && /^script = ExtResource\(/{ if (match($0,/ExtResource\("([^"]+)"\)/,m)) print m[1]; exit }' "$f")
  [ -z "$sid" ] && continue
  spath=$(awk -v id="$sid" '/^\[ext_resource / && index($0,"id=\""id"\"")>0 { if (match($0,/path="res:\/\/addons\/beep_game_builder_cs\/([^"]+)"/,m)) print m[1]; exit }' "$f")
  cs="../../$spath"; [ -f "$cs" ] || continue
  # Names looked up scene-wide only. A call qualified by some other object — LoadGameMenu's
  # `container.FindChild("SlotButton")` — searches inside that row, so a per-row repeat is
  # correct and must not be flagged. Hence the leading [^A-Za-z0-9_.]: bare or `this.` only.
  lookups=$(grep -oE '(^|[^A-Za-z0-9_.])(this\.)?(ConnectButton|Find<[^>]*>|FindChild)\("[^"]+"' "$cs" \
            | grep -oE '"[^"]+"' | tr -d '"' | sort -u)
  [ -z "$lookups" ] && continue
  dupes=$(awk '/^\[node / {
      name=""; type=""
      if (match($0,/name="[^"]+"/)) name=substr($0,RSTART+6,RLENGTH-7)
      if (match($0,/type="[^"]+"/)) type=substr($0,RSTART+6,RLENGTH-7)
      if (type ~ /Button$/ && name != "" && ++n[name] == 2) print name
    }' "$f")
  for d in $dupes; do
    printf '%s\n' "$lookups" | grep -qx -- "$d" \
      && { echo "  $f -> several Buttons named \"$d\", and $(basename "$cs") resolves that name scene-wide"; found=1; fail=1; }
  done
done; [ $found -eq 0 ] && echo "  ok"

# ThemePresetComponent registers exactly four Label type variations. A typo'd one
# (theme_type_variation = &"BeepHeading") resolves to nothing and the Label falls back to the
# base font size — the flat-typography defect this whole pass exists to fix, restored in
# silence. Keep this list in step with RegisterTypography() in ThemePresetComponent.cs.
echo "--- theme_type_variation names are ones Beep registers ---"; found=0
KNOWN_VARIATIONS="BeepTitle BeepSubtitle BeepValue BeepCaption"
for f in $(find . -name "*.tscn" | sort); do
  for v in $(grep -oE '^theme_type_variation = &?"[^"]+"' "$f" | grep -oE '"[^"]+"' | tr -d '"' | sort -u); do
    case " $KNOWN_VARIATIONS " in
      *" $v "*) ;;
      *) echo "  $f -> theme_type_variation \"$v\" is not registered by ThemePresetComponent (renders at base size)"; found=1; fail=1;;
    esac
  done
done; [ $found -eq 0 ] && echo "  ok"

# Skin assets: theme.json's textures{} and geometry.json's background_image name res:// files.
# SkinCatalog returns null for a missing one and the theme falls back to a procedural box; before
# this check ALL 200 texture_path entries across the 50 shipped themes, and all 8 background_image
# paths, pointed at files that were never in the repo. Every texture toggle in the inspector did
# nothing and nothing anywhere said so. This is the check that keeps the bake honest.
echo "--- skin texture/background files referenced by the catalogs exist ---"; found=0
SKINS="../../catalogs/skins"
if [ -d "$SKINS" ]; then
  refs=$(grep -rhoE '"(texture_path|background_image)"[[:space:]]*:[[:space:]]*"[^"]+"' "$SKINS" \
         | grep -oE '"res://[^"]+"' | tr -d '"' | sort -u)
  for r in $refs; do
    # res://addons/beep_game_builder_cs/<rel>  ->  ../../<rel>
    rel=${r#res://addons/beep_game_builder_cs/}
    [ -f "../../$rel" ] || { echo "  missing: $r"; found=$((found+1)); fail=1; }
  done
  [ $found -gt 0 ] && echo "  ($found referenced skin asset(s) not on disk)"
else
  echo "  skipped: $SKINS not found"
fi
[ $found -eq 0 ] && echo "  ok"

[ $fail -eq 0 ] && echo "PASS: all scenes valid" || echo "FAIL: see above"
exit $fail

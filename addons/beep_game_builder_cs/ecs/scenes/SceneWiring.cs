using System;
using Godot;
using Beep.ECS.UI.Kit;

namespace Beep.ECS.Scenes
{
    /// <summary>
    /// Tolerant helpers for wiring a scene's own controls in <c>_Ready</c>.
    ///
    /// The per-scene scripts used to connect buttons with the throwing <c>GetNode&lt;Button&gt;(path)</c>
    /// chained in a single <c>_Ready</c>. If one hard-coded path didn't match the scene (a renamed node,
    /// a genre variant that omits a control, an edited generated scene), that call threw and every LATER
    /// button connection in the same <c>_Ready</c> was silently skipped — the "all the buttons after the
    /// first bad one are dead" failure. These helpers resolve with <c>GetNodeOrNull</c> and warn on a
    /// miss, so a stale path costs one button and a named warning, not the whole menu.
    /// </summary>
    public static class SceneWiring
    {
        /// <summary>Connect a Button's Pressed signal, or warn (naming the path) if it isn't there.</summary>
        public static void ConnectPressed(this Node self, string path, Action handler)
        {
            if (self.GetNodeOrNull<Button>(path) is { } btn)
                btn.Pressed += handler;
            else
                GD.PushWarning($"[{self.Name}] button not found at '{path}' — not connected.");
        }

        /// <summary>Connect a Button found by NAME anywhere in the scene, or warn if it isn't there.
        ///
        /// Prefer this over <see cref="ConnectPressed"/>: a path hard-codes the layout, so any
        /// restyle that inserts a wrapper container silently kills every button under it. That is
        /// exactly how the save/load menus broke when the templates gained a Margin wrapper while
        /// already-generated projects kept the old tree — the components were fixed by resolving on
        /// name instead, and this is the same fix for the screen scripts.
        ///
        /// Button names are unique within a screen (validate_scenes.sh enforces it), so a name is
        /// as precise as a path and survives the layout changing underneath it.</summary>
        public static void ConnectButton(this Node self, string name, Action handler)
        {
            switch (self.FindChild(name, recursive: true, owned: false))
            {
                case Button btn:
                    btn.Pressed += handler;
                    return;
                // Kit widgets are NOT Godot Buttons -- KitButton draws itself and inherits
                // KitControl, so an `is Button` test silently skipped them. Every screen migrated
                // onto the kit would have kept its layout and quietly lost all its wiring, which
                // is the same failure HudComponent had against Label.
                case KitButton kb:
                    kb.Pressed += () => handler();
                    return;
                case KitIconButton kib:
                    kib.Pressed += () => handler();
                    return;
                case KitNodeCard card:
                    card.Pressed += () => handler();
                    return;
                case null:
                    GD.PushWarning($"[{self.Name}] button '{name}' not found in this scene — not connected.");
                    return;
                default:
                    GD.PushWarning($"[{self.Name}] '{name}' is a {self.FindChild(name, true, false)!.GetType().Name}, "
                                 + "which is not a Button or a kit button — not connected.");
                    return;
            }
        }

        /// <summary>Find a control by NAME anywhere in the scene, or null. Same layout-independence
        /// rationale as <see cref="ConnectButton"/>; silent, because callers use this for controls
        /// that are legitimately optional per genre.</summary>
        public static T? Find<T>(this Node self, string name) where T : Node
            => self.FindChild(name, recursive: true, owned: false) as T;
    }
}

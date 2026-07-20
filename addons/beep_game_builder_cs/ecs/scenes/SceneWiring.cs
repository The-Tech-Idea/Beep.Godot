using System;
using Godot;

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
    }
}

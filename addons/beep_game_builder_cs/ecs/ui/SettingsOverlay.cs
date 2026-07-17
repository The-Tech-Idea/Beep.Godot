using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Opens the settings screen as an overlay on top of whatever is already on screen.
    ///
    /// Why this exists: callers used to do `ChangeScene(SettingsScenePath)`, which frees the
    /// current scene. From the pause menu that destroyed the running game, and because
    /// SettingsMenu's Close always navigates to the main menu, the run was unrecoverable —
    /// pause → settings → close silently threw away the session. Opening as an overlay keeps
    /// the scene underneath alive, and SettingsMenu.Close detects it is an overlay and just
    /// frees itself.
    ///
    /// Same pattern as SaveLoadManagerComponent's save/load menus and the pause overlay.
    /// </summary>
    public static class SettingsOverlay
    {
        /// <summary>Instance the settings scene as a sibling overlay above <paramref name="caller"/>.
        /// Returns the overlay, or null if it could not be opened.</summary>
        public static Node? Open(Node caller)
        {
            string? path = GameApp.Instance?.SettingsScenePath;
            if (string.IsNullOrEmpty(path) || !ResourceLoader.Exists(path))
            {
                GD.PushError($"[SettingsOverlay] Settings scene not found: '{path}'. Check GameInfo.SettingsScenePath.");
                return null;
            }

            var packed = GD.Load<PackedScene>(path);
            if (packed == null)
            {
                GD.PushError($"[SettingsOverlay] Could not load: {path}");
                return null;
            }

            var overlay = packed.Instantiate();
            // Always: settings is opened from the pause menu while the tree is paused, and a
            // Pausable overlay would render with every control inert.
            overlay.ProcessMode = Node.ProcessModeEnum.Always;

            // Parent to the current scene so the overlay dies with it rather than leaking
            // to /root and surviving scene changes.
            Node parent = caller.GetTree()?.CurrentScene ?? caller;
            parent.AddChild(overlay);
            return overlay;
        }
    }
}

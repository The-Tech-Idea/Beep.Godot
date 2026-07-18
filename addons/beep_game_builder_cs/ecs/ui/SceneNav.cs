using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Scene navigation for the per-scene screen scripts in ecs/scenes/.
    ///
    /// Why this exists: all 33 screen scripts carried a byte-identical private ChangeScene
    /// method (verified — one distinct body across 33 copies, doc comment included). Every
    /// navigation fix therefore had to be made 33 times, and the drift had already started:
    /// VehicleSelect hardcoded a res:// literal instead of going through GameInfo, and five
    /// screens carried `?? "res://…"` fallbacks that could never fire because the generator
    /// blanks unset genre paths to "" rather than null.
    ///
    /// Same factoring as SettingsOverlay — a static navigation utility pulled out of
    /// duplicated per-scene code.
    /// </summary>
    public static class SceneNav
    {
        /// <summary>Navigate to a scene. Reports why it failed instead of doing nothing —
        /// a missing/unset target used to make the button appear dead.
        ///
        /// An empty path is a real, documented state, not an error to paper over: a genre
        /// that omits a screen leaves its GameInfo path empty, meaning "this genre has no
        /// such screen" (see GameInfo's Genre Scenes group and docs/FILE_FORMATS.md).</summary>
        public static void ChangeScene(Node caller, string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                GD.PushError($"[{caller.Name}] Navigation target is not set (check GameInfo scene paths).");
                return;
            }
            if (!ResourceLoader.Exists(path))
            {
                GD.PushError($"[{caller.Name}] Navigation target does not exist: {path}");
                return;
            }

            var tree = caller.GetTree();
            if (tree == null) return;

            // If the current scene ships a SceneTransitionComponent, fade out through it and
            // swap scenes when the fade finishes. Without this the Transition node in the menus
            // spawned its rect and never played — every navigation was a hard cut. CanPlay
            // guards against a not-yet-ready transition stalling the change.
            var transition = FindTransition(tree.CurrentScene);
            if (transition is { CanPlay: true })
            {
                void OnFinished()
                {
                    transition.Finished -= OnFinished;
                    Error e = tree.ChangeSceneToFile(path);
                    if (e != Error.Ok)
                        GD.PushError($"[{caller.Name}] Failed to change scene to {path}: {e}");
                }
                transition.Finished += OnFinished;
                transition.TransitionIn();
                return;
            }

            Error err = tree.ChangeSceneToFile(path);
            if (err != Error.Ok)
                GD.PushError($"[{caller.Name}] Failed to change scene to {path}: {err}");
        }

        /// <summary>Depth-first search for a SceneTransitionComponent under the given scene root.</summary>
        private static SceneTransitionComponent? FindTransition(Node? node)
        {
            if (node == null) return null;
            if (node is SceneTransitionComponent t) return t;
            foreach (var child in node.GetChildren())
                if (FindTransition(child) is { } found) return found;
            return null;
        }

        /// <summary>Close a screen that may be either the current scene or an overlay.
        ///
        /// As an overlay — instanced over a live game by GenreScreenComponent or
        /// SettingsOverlay — navigating away would free the run underneath: the player's
        /// position, health and progress, thrown away by closing an inventory. Freeing
        /// ourselves just reveals the game again, still paused.
        ///
        /// As the current scene there is nothing behind us, so we navigate to
        /// <paramref name="fallbackPath"/>.
        ///
        /// This is SettingsMenu.OnClosePressed's logic, promoted here once the genre screens
        /// became reachable as overlays: all 14 of them closed with a bare
        /// ChangeScene(GameScenePath), which was harmless only because nothing could open
        /// them.</summary>
        public static void CloseOrReturn(Node caller, string? fallbackPath)
        {
            if (caller.GetTree()?.CurrentScene != caller)
            {
                caller.QueueFree();
                return;
            }
            ChangeScene(caller, fallbackPath);
        }
    }
}

using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Scene navigation component. Attach as a child of any node that needs to
    /// drive scene transitions (menus, game-over screens, pause overlays).
    ///
    /// Reads the canonical scene paths from the <c>GameInfo</c> autoload
    /// (res://game_info.tres, registered by BeepGenreGenerator) so paths live in
    /// ONE place. Emits <see cref="BeforeNavigate"/> before switching, giving a
    /// transition component (e.g. SceneTransitionComponent) a chance to animate.
    ///
    /// Designed to be driven by a <c>MenuComponent.ActionTriggered</c> signal:
    /// connect action "play" → <see cref="GoToGame"/>, "quit" → <see cref="Quit"/>, etc.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class NavigationComponent : GameplayComponent
    {
        /// <summary>If true and GameInfo is present, use its paths instead of the exported fields below.</summary>
        [Export] public bool UseGameInfoPaths { get; set; } = true;

        [Export] public string MainMenuPath { get; set; } = "res://scenes/ui/main_menu.tscn";
        [Export] public string GamePath { get; set; } = "res://scenes/main/main.tscn";
        [Export] public string GameOverPath { get; set; } = "res://scenes/ui/game_over.tscn";

        /// <summary>Emitted before a scene switch. Connect a transition/animation here.</summary>
        [Signal] public delegate void BeforeNavigateEventHandler(string toScene);

        /// <summary>Dispatch a named action to a navigation target. Actions: play, continue, menu, restart, quit.</summary>
        public void Dispatch(string action)
        {
            if (!IsActive) return;
            switch (action)
            {
                case "play": case "continue": case "start_game": GoToGame(); break;
                case "menu": case "main_menu": GoToMainMenu(); break;
                case "restart": case "retry": Restart(); break;
                case "game_over": GoToGameOver(); break;
                case "quit": case "exit": Quit(); break;
            }
        }

        public void GoToMainMenu() => ChangeScene(ResolvePath(MainMenuPath, g => g.MainMenuPath));
        public void GoToGame() => ChangeScene(ResolvePath(GamePath, g => g.GameScenePath));
        public void GoToGameOver() => ChangeScene(ResolvePath(GameOverPath, g => g.GameOverScenePath));
        public void Restart() { if (!IsActive) return; EmitSignal(SignalName.BeforeNavigate, GetTree().CurrentScene.SceneFilePath); GetTree().ReloadCurrentScene(); }
        public void Quit() { if (!IsActive) return; GetTree().Quit(); }

        // Pending scene change, held while a SceneTransitionComponent fades in.
        private string? _pendingPath;

        private void ChangeScene(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            EmitSignal(SignalName.BeforeNavigate, path);

            // If a sibling SceneTransitionComponent exists, gate the scene change
            // behind its fade-in: play TransitionIn, then change scene on Finished.
            // This makes transitions OPTIONAL — no transition component = instant change.
            var transition = FindSibling<UI.SceneTransitionComponent>();
            if (transition != null)
            {
                _pendingPath = path;
                transition.Finished -= OnTransitionFinished;
                transition.Finished += OnTransitionFinished;
                transition.TransitionIn();
            }
            else
            {
                GetTree().ChangeSceneToFile(path);
            }
        }

        private void OnTransitionFinished()
        {
            if (_pendingPath == null) return;
            string p = _pendingPath;
            _pendingPath = null;
            GetTree().ChangeSceneToFile(p);
        }

        /// <summary>Find a component of type T among this node's siblings (same parent).</summary>
        private T? FindSibling<T>() where T : Node
        {
            if (GetParent() == null) return null;
            foreach (var child in GetParent().GetChildren())
                if (child is T t && child != this) return t;
            return null;
        }

        // ── Scene history stack (P5) ──
        private readonly System.Collections.Generic.Stack<string> _history = new();
        private static readonly System.Collections.Generic.List<Node> _additiveScenes = new();

        /// <summary>Push the current scene onto the history stack, then change to a new scene.
        /// Call GoBack() to return.</summary>
        public void PushScene(string path)
        {
            if (!IsActive) return;
            var current = GetTree().CurrentScene;
            if (current != null) _history.Push(current.SceneFilePath);
            ChangeScene(path);
        }

        /// <summary>Pop the last pushed scene and return to it.</summary>
        public void GoBack()
        {
            if (!IsActive || _history.Count == 0) return;
            string prev = _history.Pop();
            ChangeScene(prev);
        }

        /// <summary>True if there's a scene to go back to.</summary>
        public bool CanGoBack => _history.Count > 0;

        /// <summary>Load a scene additively (instance it as a child of the current scene root,
        /// without replacing the current scene). Returns the instanced root, or null.</summary>
        public Node? LoadSceneAdditive(string path)
        {
            if (!IsActive || string.IsNullOrEmpty(path)) return null;
            var packed = ResourceLoader.Load<PackedScene>(path);
            if (packed == null) return null;
            var inst = packed.Instantiate();
            GetTree().CurrentScene.AddChild(inst);
            _additiveScenes.Add(inst);
            return inst;
        }

        /// <summary>Unload a previously additively-loaded scene instance.</summary>
        public void UnloadAdditiveScene(Node instance)
        {
            if (instance == null || !GodotObject.IsInstanceValid(instance)) return;
            _additiveScenes.Remove(instance);
            instance.QueueFree();
        }

        /// <summary>Unload ALL additively-loaded scenes.</summary>
        public void UnloadAllAdditiveScenes()
        {
            foreach (var s in _additiveScenes)
                if (GodotObject.IsInstanceValid(s)) s.QueueFree();
            _additiveScenes.Clear();
        }

        private string ResolvePath(string fallback, System.Func<GameBuilder.GameInfo, string> selector)
        {
            if (!UseGameInfoPaths) return fallback;
            var info = GameBuilder.GameInfo.Instance;
            if (info != null)
            {
                string p = selector(info);
                if (!string.IsNullOrEmpty(p)) return p;
            }
            return fallback;
        }
    }
}

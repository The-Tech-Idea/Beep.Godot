using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Scene navigation component. Attach as a child of any node that drives
    /// scene transitions (menus, game-over screens, pause overlays).
    ///
    /// ALL navigation targets are EXPORTED paths — set them in the inspector
    /// per-scene (the Godot way). No hardcoded routing. Every action maps to
    /// one exported path. Leave a path empty to disable that action.
    ///
    /// Designed to be driven by a MenuComponent.ActionTriggered signal:
    /// the menu emits "play", this component navigates to the GameScene path.
    ///
    /// Unhandled actions (e.g. "level_1", "char_marine") fire the
    /// UnhandledAction signal so the game scene can implement custom logic.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class NavigationComponent : GameplayComponent
    {
        // ════════════════════════════════════════════════════════════════
        //  Exported navigation targets — set in the inspector per scene.
        //  Empty string = action disabled (navigates to nothing).
        // ════════════════════════════════════════════════════════════════

        [ExportGroup("Core Scenes")]
        /// <summary>Drag a .tscn here. Scene to load for "new_game" / "play" / "continue".</summary>
        [Export] public PackedScene? GameScene { get; set; }
        /// <summary>Drag a .tscn here. Scene to load for "menu" / "main_menu".</summary>
        [Export] public PackedScene? MainMenu { get; set; }
        /// <summary>Drag a .tscn here. Scene for "settings". Pushed as overlay (GoBack returns).</summary>
        [Export] public PackedScene? Settings { get; set; }
        /// <summary>Drag a .tscn here. Scene for "game_over".</summary>
        [Export] public PackedScene? GameOver { get; set; }
        /// <summary>Drag a .tscn here. Scene for "credits". Pushed as overlay.</summary>
        [Export] public PackedScene? Credits { get; set; }

        [ExportGroup("Level Flow")]
        /// <summary>Drag a .tscn here. Scene for "map" / "level_select" / "level_map".</summary>
        [Export] public PackedScene? LevelSelect { get; set; }
        /// <summary>Drag a .tscn here. Scene for "next" (next level / results).</summary>
        [Export] public PackedScene? NextLevel { get; set; }
        /// <summary>Drag a .tscn here. Scene for "results" / "run_results".</summary>
        [Export] public PackedScene? Results { get; set; }

        [ExportGroup("Genre Scenes")]
        /// <summary>Drag a .tscn here. Scene for "character_select" (shooter).</summary>
        [Export] public PackedScene? CharacterSelect { get; set; }
        /// <summary>Drag a .tscn here. Scene for "level_up" (shooter).</summary>
        [Export] public PackedScene? LevelUp { get; set; }
        /// <summary>Drag a .tscn here. Scene for "codex" (shooter).</summary>
        [Export] public PackedScene? Codex { get; set; }
        /// <summary>Drag a .tscn here. Scene for "pre_level" (puzzle).</summary>
        [Export] public PackedScene? PreLevel { get; set; }
        /// <summary>Drag a .tscn here. Scene for "level_complete" (puzzle/platformer).</summary>
        [Export] public PackedScene? LevelComplete { get; set; }
        /// <summary>Drag a .tscn here. Scene for "level_failed" (puzzle).</summary>
        [Export] public PackedScene? LevelFailed { get; set; }

        // ════════════════════════════════════════════════════════════════
        //  Signals
        // ════════════════════════════════════════════════════════════════

        /// <summary>Emitted before a scene switch. Connect a transition/animation here.</summary>
        [Signal] public delegate void BeforeNavigateEventHandler(string toScene);
        /// <summary>Emitted for "load_game". Connect to show save-slot UI.</summary>
        [Signal] public delegate void LoadGameRequestedEventHandler();
        /// <summary>Emitted for "save_game". Connect to show save-slot UI.</summary>
        [Signal] public delegate void SaveGameRequestedEventHandler();
        /// <summary>Emitted for "resume" in a pause menu.</summary>
        [Signal] public delegate void ResumeRequestedEventHandler();
        /// <summary>Emitted for any action that has no exported path set.
        /// Connect to handle genre-specific actions like "level_1", "char_marine".</summary>
        [Signal] public delegate void UnhandledActionEventHandler(string action);

        // ════════════════════════════════════════════════════════════════
        //  Dispatch — maps action strings to exported paths
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Dispatch a named action. Each action maps to an exported path variable.
        /// If the path is empty, the action fires UnhandledAction instead.
        /// Actions: new_game, play, continue, menu, main_menu, settings, game_over,
        /// credits, map, next, results, character_select, level_up, codex,
        /// pre_level, level_complete, level_failed, load_game, save_game,
        /// resume, restart, retry, close, back, quit, exit.
        /// </summary>
        public void Dispatch(string action)
        {
            if (!IsActive) return;
            switch (action)
            {
                // ── Start game ──
                case "new_game": case "play": case "continue": case "start_game":
                    NavigateTo(GameScene, action); break;
                // ── Menus ──
                case "menu": case "main_menu":
                    NavigateTo(MainMenu, action); break;
                case "settings":
                    PushTo(Settings, action); break;
                case "credits":
                    PushTo(Credits, action); break;
                // ── Level flow ──
                case "map": case "world_map": case "level_select": case "level_map":
                    NavigateTo(LevelSelect, action); break;
                case "next":
                    NavigateTo(NextLevel, action); break;
                case "results": case "run_results":
                    NavigateTo(Results, action); break;
                // ── Genre scenes ──
                case "character_select":
                    NavigateTo(CharacterSelect, action); break;
                case "level_up":
                    NavigateTo(LevelUp, action); break;
                case "codex":
                    NavigateTo(Codex, action); break;
                case "pre_level":
                    NavigateTo(PreLevel, action); break;
                case "level_complete":
                    NavigateTo(LevelComplete, action); break;
                case "level_failed":
                    NavigateTo(LevelFailed, action); break;
                // ── Signals (no scene change) ──
                case "load_game": EmitSignal(SignalName.LoadGameRequested); break;
                case "save_game": EmitSignal(SignalName.SaveGameRequested); break;
                case "resume": EmitSignal(SignalName.ResumeRequested); break;
                // ── System ──
                case "restart": case "retry": Restart(); break;
                case "game_over": NavigateTo(GameOver, action); break;
                case "close": case "back": GoBack(); break;
                case "quit": case "exit": Quit(); break;
                // ── Anything else ──
                default: EmitSignal(SignalName.UnhandledAction, action); break;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  Navigation helpers
        // ════════════════════════════════════════════════════════════════

        /// <summary>Navigate to a PackedScene. If null, fire UnhandledAction.</summary>
        private void NavigateTo(PackedScene? scene, string action)
        {
            if (scene != null)
                ChangeScene(scene);
            else
                EmitSignal(SignalName.UnhandledAction, action);
        }

        /// <summary>Push a scene as an overlay (GoBack returns). If null, fire UnhandledAction.</summary>
        private void PushTo(PackedScene? scene, string action)
        {
            if (scene != null)
                PushScene(scene);
            else
                EmitSignal(SignalName.UnhandledAction, action);
        }

        public void Restart()
        {
            if (!IsActive) return;
            EmitSignal(SignalName.BeforeNavigate, GetTree().CurrentScene.SceneFilePath);
            GetTree().ReloadCurrentScene();
        }

        public void Quit() { if (!IsActive) return; GetTree().Quit(); }

        // ── Scene change mechanics ──

        private PackedScene? _pendingScene;

        private void ChangeScene(PackedScene scene)
        {
            if (scene == null) return;
            string path = scene.ResourcePath;
            EmitSignal(SignalName.BeforeNavigate, path);

            // If a sibling SceneTransitionComponent exists, gate the scene change
            // behind its fade-in. No transition component = instant change.
            var transition = FindSibling<UI.SceneTransitionComponent>();
            if (transition != null)
            {
                _pendingScene = scene;
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
            if (_pendingScene == null) return;
            var scene = _pendingScene;
            _pendingScene = null;
            GetTree().ChangeSceneToFile(scene.ResourcePath);
        }

        /// <summary>Find a component of type T among this node's siblings (same parent).</summary>
        private T? FindSibling<T>() where T : Node
        {
            if (GetParent() == null) return null;
            foreach (var child in GetParent().GetChildren())
                if (child is T t && child != this) return t;
            return null;
        }

        // ── Scene history stack (for PushScene / GoBack) ──
        private readonly System.Collections.Generic.Stack<string> _history = new();

        /// <summary>Push the current scene onto the history stack, then change to a new scene.</summary>
        public void PushScene(PackedScene scene)
        {
            if (!IsActive) return;
            var current = GetTree().CurrentScene;
            if (current != null) _history.Push(current.SceneFilePath);
            ChangeScene(scene);
        }

        /// <summary>Pop the last pushed scene and return to it.</summary>
        public void GoBack()
        {
            if (!IsActive || _history.Count == 0) return;
            string prev = _history.Pop();
            EmitSignal(SignalName.BeforeNavigate, prev);
            GetTree().ChangeSceneToFile(prev);
        }

        public bool CanGoBack => _history.Count > 0;
    }
}

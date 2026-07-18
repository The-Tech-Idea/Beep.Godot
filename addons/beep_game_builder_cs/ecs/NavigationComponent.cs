using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Scene navigation component. Attach as a child of any node that drives
    /// scene transitions. ALL navigation targets are exported PackedScene
    /// properties — drag-and-drop .tscn files in the inspector. The generator
    /// auto-wires these when stamping scenes.
    ///
    /// Designed to work with or without .tscn [connection] lines — at _Ready it
    /// auto-wires any Button in the parent tree whose name maps to a known action.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class NavigationComponent : ControllerComponent
    {
        private static readonly Dictionary<string, string> ButtonNameToAction = new()
        {
            // Core
            { "newgamebutton",     "new_game" },    { "startbutton", "new_game" },
            { "continuebutton",    "continue" },     { "loadgamebutton", "load_game" },
            { "settingsbutton",    "settings" },     { "creditsbutton", "credits" },
            { "quitbutton",        "quit" },         { "exitbutton", "quit" },
            { "restartbutton",     "restart" },      { "retrybutton", "retry" },
            { "resumebutton",      "resume" },       { "savegamebutton", "save_game" },
            { "backbutton", "back" },
            { "returnbutton",      "back" },         { "closebutton", "close" },
            { "mainmenubutton",    "main_menu" },    { "menubutton", "main_menu" },
            { "gameoverbutton",    "game_over" },    { "nextbutton", "next" },
            { "mapbutton",         "map" },
            // Genre-specific
            { "levelselectbutton", "level_select" }, { "resultsbutton", "results" },
            { "levelupbutton",     "level_up" },     { "codexbutton", "codex" },
            { "characterbutton",   "character_select" },
        };

        public override void _Ready()
        {
            AutoWireButtons();
        }

        /// <summary>Walk the parent node tree and connect every Button whose
        /// lowercased Name maps to a known action string. This makes .tscn
        /// [connection] lines optional — buttons work as long as their name
        /// ends with a recognized action suffix (e.g. "StartButton", "SettingsButton").
        /// Explicit .tscn connections take precedence (they're connected before
        /// _Ready fires).</summary>
        private void AutoWireButtons()
        {
            var parent = GetParent();
            if (parent == null) return;
            Walk(parent, node =>
            {
                if (node is Button btn)
                {
                    string name = btn.Name.ToString().ToLowerInvariant();
                    if (ButtonNameToAction.TryGetValue(name, out string action))
                    {
                        // If the scene already wired this button's Pressed (an explicit .tscn
                        // [connection]), respect it and don't add a second handler. The old guard
                        // tested IsConnected(Dispatch), but the auto-wire connects a lambda, so it
                        // never matched — a button with a .tscn connection fired twice.
                        if (btn.GetSignalConnectionList(Button.SignalName.Pressed).Count > 0) return;
                        string a = action; // capture for lambda
                        btn.Pressed += () => Dispatch(a);
                    }
                }
            });
        }

        private static void Walk(Node root, Action<Node> visit)
        {
            visit(root);
            foreach (var child in root.GetChildren())
                Walk(child, visit);
        }
        // ════════════════════════════════════════════════════════════════
        //  Exported navigation targets — drag .tscn files in the inspector
        // ════════════════════════════════════════════════════════════════

        [ExportGroup("Core Scenes")]
        [Export] public PackedScene? GameScene { get; set; }
        [Export] public PackedScene? MainMenu { get; set; }
        [Export] public PackedScene? Settings { get; set; }
        [Export] public PackedScene? GameOver { get; set; }
        [Export] public PackedScene? Credits { get; set; }

        [ExportGroup("Level Flow")]
        [Export] public PackedScene? LevelSelect { get; set; }
        [Export] public PackedScene? NextLevel { get; set; }
        [Export] public PackedScene? Results { get; set; }

        [ExportGroup("Genre Scenes")]
        [Export] public PackedScene? CharacterSelect { get; set; }
        [Export] public PackedScene? LevelUp { get; set; }
        [Export] public PackedScene? Codex { get; set; }
        [Export] public PackedScene? PreLevel { get; set; }
        [Export] public PackedScene? LevelComplete { get; set; }
        [Export] public PackedScene? LevelFailed { get; set; }

        // ════════════════════════════════════════════════════════════════
        //  Signals
        // ════════════════════════════════════════════════════════════════

        [Signal] public delegate void BeforeNavigateEventHandler(string toScene);
        [Signal] public delegate void LoadGameRequestedEventHandler();
        [Signal] public delegate void SaveGameRequestedEventHandler();
        [Signal] public delegate void ResumeRequestedEventHandler();
        [Signal] public delegate void UnhandledActionEventHandler(string action);

        // ════════════════════════════════════════════════════════════════
        //  Dispatch — maps action strings to exported PackedScene targets
        // ════════════════════════════════════════════════════════════════

        public void Dispatch(string action)
        {
            if (!IsActive) return;
            switch (action)
            {
                case "new_game": case "play": case "continue": case "start_game":
                    NavigateTo(GameScene, action); break;
                case "menu": case "main_menu":
                    NavigateTo(MainMenu, action); break;
                case "settings":
                    PushTo(Settings, action); break;
                case "credits":
                    PushTo(Credits, action); break;
                case "map": case "world_map": case "level_select": case "level_map":
                    NavigateTo(LevelSelect, action); break;
                case "next":
                    NavigateTo(NextLevel, action); break;
                case "results": case "run_results":
                    NavigateTo(Results, action); break;
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
                case "load_game": EmitSignal(SignalName.LoadGameRequested); break;
                case "save_game": EmitSignal(SignalName.SaveGameRequested); break;
                case "resume": EmitSignal(SignalName.ResumeRequested); break;
                case "restart": case "retry": Restart(); break;
                case "game_over": NavigateTo(GameOver, action); break;
                case "close": case "back": GoBack(); break;
                case "quit": case "exit": Quit(); break;
                default: EmitSignal(SignalName.UnhandledAction, action); break;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  Navigation helpers
        // ════════════════════════════════════════════════════════════════

        private void NavigateTo(PackedScene? scene, string action)
        {
            if (scene != null) ChangeScene(scene);
            else EmitSignal(SignalName.UnhandledAction, action);
        }

        private void PushTo(PackedScene? scene, string action)
        {
            if (scene != null) PushScene(scene);
            else EmitSignal(SignalName.UnhandledAction, action);
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
        private string? _pendingReturnPath;

        private void ChangeScene(PackedScene scene)
        {
            if (scene == null) return;
            string path = scene.ResourcePath;
            EmitSignal(SignalName.BeforeNavigate, path);

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
            if (_pendingScene == null && _pendingReturnPath == null) return;

            if (_pendingScene != null)
            {
                var scene = _pendingScene;
                _pendingScene = null;
                GetTree().ChangeSceneToFile(scene.ResourcePath);
            }
            else if (_pendingReturnPath != null)
            {
                string p = _pendingReturnPath;
                _pendingReturnPath = null;
                GetTree().ChangeSceneToFile(p);
            }
        }

        private T? FindSibling<T>() where T : Node
        {
            if (GetParent() == null) return null;
            foreach (var child in GetParent().GetChildren())
                if (child is T t && child != this) return t;
            return null;
        }

        // ── Scene history stack ──
        private readonly System.Collections.Generic.Stack<string> _history = new();

        public void PushScene(PackedScene scene)
        {
            if (!IsActive) return;
            var current = GetTree().CurrentScene;
            if (current != null) _history.Push(current.SceneFilePath);
            ChangeScene(scene);
        }

        public void GoBack()
        {
            if (!IsActive || _history.Count == 0) return;
            string prev = _history.Pop();
            EmitSignal(SignalName.BeforeNavigate, prev);

            var transition = FindSibling<UI.SceneTransitionComponent>();
            if (transition != null)
            {
                _pendingReturnPath = prev;
                transition.Finished -= OnTransitionFinished;
                transition.Finished += OnTransitionFinished;
                transition.TransitionIn();
            }
            else
            {
                GetTree().ChangeSceneToFile(prev);
            }
        }

        public bool CanGoBack => _history.Count > 0;
    }
}

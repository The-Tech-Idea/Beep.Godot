using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Opens one of the genre's own screens (inventory, crafting, deck builder, research…)
    /// as an overlay, on an input action.
    ///
    /// Why this exists: seven genres shipped 14 fully-built, themed, scripted screens that
    /// no player could ever open. They had no inbound edge at all — nothing instanced them,
    /// no menu listed them, and nav_wiring couldn't name them because GameInfo's scene-path
    /// properties only covered platformer/shooter/puzzle. The generator faithfully copied
    /// them into every project as unreachable files.
    ///
    /// The path half is fixed by GameInfo.GenreScenePaths (any nav_wiring key resolves); this
    /// is the other half — something that actually opens them.
    ///
    /// Overlay, never ChangeScene: these sit over a running game. Navigating to an inventory
    /// would free the run behind it — the same hazard SettingsOverlay exists to avoid, and
    /// the one LevelUpChoice shipped with. Closing is the screen's own job (QueueFree).
    ///
    /// Attach to the genre's main scene and set ScreenKey to a key the genre's nav_wiring
    /// declares:
    ///   [node name="InventoryScreen" type="Node" parent="."]
    ///   ScreenKey = "inventory"
    ///   OpenAction = "inventory"
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GenreScreenComponent : UIComponent
    {
        /// <summary>nav_wiring key of the screen to open (e.g. "inventory"). Must match a key
        /// in the genre's genre.json nav_wiring block.</summary>
        [Export] public string ScreenKey { get; set; } = "";

        /// <summary>Input action that opens it. Must exist in the input map —
        /// BeepInputMapGenerator registers the genre screen actions at generation time.</summary>
        [Export] public string OpenAction { get; set; } = "";

        /// <summary>Pause the tree while the screen is open. On for anything the player reads
        /// or manages at leisure (inventory, crafting); off for a screen meant to sit over
        /// live action.
        ///
        /// Only ever undoes its OWN pause — if something else (the pause menu, another
        /// screen) already paused, closing this leaves the tree paused.</summary>
        [Export] public bool PauseWhileOpen { get; set; } = true;

        /// <summary>CanvasLayer layer for the hosted screen. Default 20 puts it above the
        /// game, its HUD (layer 0), and the pause menu (layer 10), so a screen stays readable
        /// whatever it is opened over. Only used for Control-rooted screens, which get a host
        /// layer; a screen that is already a CanvasLayer keeps its own.</summary>
        [Export] public int ScreenLayer { get; set; } = 20;

        /// <summary>Pressing the action again closes an open screen.</summary>
        [Export] public bool Toggle { get; set; } = true;

        [Signal] public delegate void ScreenOpenedEventHandler();
        [Signal] public delegate void ScreenClosedEventHandler();

        private Node? _open;
        private Node? _host;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;

            if (string.IsNullOrEmpty(ScreenKey))
                GD.PushWarning($"[{Name}] GenreScreenComponent has no ScreenKey — it will never open anything.");
            else if (string.IsNullOrEmpty(OpenAction))
                GD.PushWarning($"[{Name}] GenreScreenComponent '{ScreenKey}' has no OpenAction — nothing can open it.");
            else if (!InputMap.HasAction(OpenAction))
                GD.PushWarning($"[{Name}] GenreScreenComponent '{ScreenKey}' listens for input action '{OpenAction}', which is not in the input map — it can never fire.");

            // Process while paused so the action can CLOSE a screen that paused the tree.
            ProcessMode = ProcessModeEnum.Always;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (Engine.IsEditorHint() || !IsActive) return;
            if (string.IsNullOrEmpty(OpenAction) || !@event.IsActionPressed(OpenAction)) return;

            if (IsOpen())
            {
                if (Toggle) { Close(); GetViewport()?.SetInputAsHandled(); }
                return;
            }
            if (Open() != null) GetViewport()?.SetInputAsHandled();
        }

        public bool IsOpen() => _open != null && GodotObject.IsInstanceValid(_open);

        /// <summary>Instance the screen over the current scene. Returns it, or null.</summary>
        public Node? Open()
        {
            if (IsOpen()) return _open;

            string path = GameBuilder.GameInfo.Instance?.GetGenreScenePath(ScreenKey) ?? "";
            if (string.IsNullOrEmpty(path))
            {
                GD.PushError($"[{Name}] No screen registered for key '{ScreenKey}'. Declare it in this genre's genre.json nav_wiring block.");
                return null;
            }
            if (!ResourceLoader.Exists(path))
            {
                GD.PushError($"[{Name}] Screen '{ScreenKey}' points at a scene that does not exist: {path}");
                return null;
            }

            var packed = GD.Load<PackedScene>(path);
            if (packed == null)
            {
                GD.PushError($"[{Name}] Could not load screen '{ScreenKey}': {path}");
                return null;
            }

            var overlay = packed.Instantiate();
            // Always: if we pause the tree below, a Pausable overlay would render inert.
            overlay.ProcessMode = ProcessModeEnum.Always;

            // Parent to the current scene so it dies with it rather than leaking to /root and
            // surviving scene changes — same reasoning as SettingsOverlay.
            Node parent = GetTree()?.CurrentScene ?? this;

            // Host a Control-rooted screen in a CanvasLayer of our own. SettingsOverlay can
            // AddChild straight onto the scene because settings_menu/pause_menu are
            // CanvasLayer-rooted; the genre screens are Control-rooted, and the genre mains
            // are Node2D. A Control parented under a Node2D joins the WORLD canvas: it
            // anchors against its parent's (empty) rect instead of the viewport, rides the
            // Camera2D transform, and draws beneath the scene's own HUD layer — i.e. the
            // screen opens as an invisible, unclickable, zero-sized node. The CanvasLayer
            // gives it the viewport-anchored, camera-independent space it was authored for.
            Node host = overlay;
            if (overlay is not CanvasLayer)
            {
                var layer = new CanvasLayer { Name = $"{ScreenKey}ScreenLayer", Layer = ScreenLayer, ProcessMode = ProcessModeEnum.Always };
                parent.AddChild(layer);
                layer.AddChild(overlay);
                host = layer;
            }
            else
            {
                parent.AddChild(overlay);
            }
            _host = host;
            _open = overlay;

            // Only pause if the tree isn't already paused — and remember that we did, so we
            // only ever undo our own pause. GetTree().Paused is one global bool with no
            // refcount and several owners (PauseComponent, GameFlowComponent, us). Unpausing
            // unconditionally on close would resume the game underneath a still-open pause
            // menu, or underneath another genre screen.
            _pausedByUs = false;
            if (PauseWhileOpen && GetTree() is { } tree && !tree.Paused)
            {
                tree.Paused = true;
                _pausedByUs = true;
            }

            // Watch the SCREEN, not the host: every screen closes itself via
            // SceneNav.CloseOrReturn → QueueFree() on its own root. Watching the host would
            // miss that entirely — the host would outlive the screen, its TreeExited would
            // never fire, and the game would stay paused behind an empty CanvasLayer.
            overlay.TreeExited += OnScreenClosed;

            EmitSignal(SignalName.ScreenOpened);
            return overlay;
        }

        public void Close()
        {
            if (!IsOpen()) return;
            _open!.QueueFree();   // OnScreenClosed does the unpausing, via TreeExited
        }

        private void OnScreenClosed()
        {
            _open = null;
            // The screen freed itself; take the host layer with it rather than leaving an
            // empty CanvasLayer behind on every open/close cycle.
            if (_host != null && GodotObject.IsInstanceValid(_host) && !_host.IsQueuedForDeletion())
                _host.QueueFree();
            _host = null;

            ReleasePause();
            EmitSignal(SignalName.ScreenClosed);
        }

        /// <summary>Undo our pause, and only ours. Unconditionally clearing Paused here is
        /// what would resume the game under a still-open pause menu or a second screen.</summary>
        private void ReleasePause()
        {
            if (!_pausedByUs) return;
            _pausedByUs = false;
            if (GetTree() is { } tree) tree.Paused = false;
        }
        private bool _pausedByUs;

        public override void _ExitTree()
        {
            if (IsOpen()) _open!.TreeExited -= OnScreenClosed;
            // Leaving the tree with our screen still open would otherwise strand the game
            // paused forever, with nothing left alive to release it.
            ReleasePause();
            base._ExitTree();
        }
    }
}

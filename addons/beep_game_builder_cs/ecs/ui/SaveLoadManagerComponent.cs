using Godot;

namespace Beep.ECS.UI
{
	/// <summary>
	/// Example: Universal save/load manager that wires SaveGameMenuComponent
	/// and LoadGameMenuComponent to actual save/load logic.
	///
	/// Usage:
	/// 1. Attach this to your GameFlow or main scene
	/// 2. Call ShowSaveMenu() when user presses "Save" button
	/// 3. Call ShowLoadMenu() when user presses "Load" button
	/// 4. This component handles the rest automatically
	/// </summary>
	[Tool]
	[GlobalClass]
	public partial class SaveLoadManagerComponent : GameplayComponent
	{
		[Export] public string SaveMenuPrefab { get; set; } = "res://scenes/ui/save_game_menu.tscn";
		[Export] public string LoadMenuPrefab { get; set; } = "res://scenes/ui/load_game_menu.tscn";

		[Signal] public delegate void SaveStartedEventHandler();
		[Signal] public delegate void SaveCompletedEventHandler(int slot);
		[Signal] public delegate void LoadCompletedEventHandler(int slot);

		private GameStateManagerComponent? _gameStateManager;

		// The one save/load overlay currently open. Guards ShowSaveMenu/ShowLoadMenu against
		// stacking a second overlay when called twice (both are public and idempotent).
		private Node? _openMenu;

		/// <summary>Load a menu scene from its prefab path, or PushError when it can't be loaded.</summary>
		private PackedScene? ResolveMenuScene(string prefabPath, string which)
		{
			var scene = GD.Load<PackedScene>(prefabPath);
			if (scene == null)
				GD.PushError($"[{Name}] Cannot load {which} menu: '{prefabPath}'. Point {which}Prefab at a valid .tscn.");
			return scene;
		}

		public override void _Ready()
		{
			if (Engine.IsEditorHint()) return;

			base._Ready();
			FindGameStateManager();
		}

		/// <summary>CanvasLayer to host the dialog on. Above GameFlowComponent's pause overlay,
		/// which sits at 100 — the save/load menu is opened FROM that menu, so it has to draw
		/// over it.</summary>
		private const int OverlayLayer = 110;

		/// <summary>Add a save/load menu over whatever is on screen.
		///
		/// Three things this has to get right:
		///  • Host a Control-rooted menu in its own CanvasLayer, so it lands in SCREEN space
		///    and above the pause overlay. See the comment in the body.
		///  • Parent inside the current scene, not at /root. Parenting to the tree root
		///    left the menu outside the scene, so it survived scene changes and lingered.
		///  • ProcessMode = Always. These menus are opened from the pause menu, i.e. while
		///    the tree is paused; the default (Pausable) means every button is inert and
		///    the overlay can't even be dismissed.
		/// </summary>
		private void AddOverlay(Node overlay)
		{
			overlay.ProcessMode = Node.ProcessModeEnum.Always;

			Node? parent = GetTree()?.CurrentScene ?? GetParent();
			if (parent == null)
			{
				GD.PushError($"[{Name}] Nowhere to add the menu — no current scene.");
				overlay.QueueFree();
				return;
			}

			// A Control-rooted menu MUST get its own CanvasLayer.
			//
			// This used to add the dialog straight to the current scene. From the main menu that
			// is a Control, so it landed in screen space and worked. DURING PLAY the current
			// scene is the Node2D game root, so the dialog joined the WORLD canvas: it rode the
			// Camera2D and drew beneath every HUD layer — and far beneath the pause overlay,
			// which GameFlowComponent hosts at layer 100. Save/Load from the pause menu built
			// the dialog, parented it, and left it underneath the menu that opened it. It looked
			// like the buttons did nothing. GameFlowComponent already documents this exact trap
			// for its own overlay; this component had the same defect.
			Node host;
			if (overlay is CanvasLayer) host = overlay;
			else
			{
				var layer = new CanvasLayer { Name = "SaveLoadOverlayLayer", Layer = OverlayLayer };
				layer.AddChild(overlay);
				host = layer;
			}
			host.ProcessMode = Node.ProcessModeEnum.Always;
			parent.AddChild(host);

			_openMenu = overlay;
			// The menu frees ITSELF (QueueFree on load/cancel), so the host layer has to be
			// torn down with it or an empty CanvasLayer is leaked on every open.
			overlay.TreeExited += () =>
			{
				if (_openMenu == overlay) _openMenu = null;
				if (host != overlay && GodotObject.IsInstanceValid(host)) host.QueueFree();
			};
		}

		/// <summary>True when a save/load overlay is already on screen. Prevents stacking.</summary>
		private bool MenuAlreadyOpen()
		{
			if (_openMenu != null && GodotObject.IsInstanceValid(_openMenu)) return true;
			_openMenu = null;
			return false;
		}

		/// <summary>Resolve the GameStateManager autoload. It is registered at
		/// /root/GameStateManager so it outlives scene changes — the save/load menus live
		/// in the main menu, a different scene from gameplay, so a per-scene manager could
		/// never be found from here. Falls back to a tree scan for projects that still
		/// place it manually in a scene.</summary>
		private void FindGameStateManager()
		{
			_gameStateManager = GameStateManagerComponent.Instance;
			if (_gameStateManager != null) return;

			var root = GetTree()?.Root;
			if (root != null) _gameStateManager = FindFirst(root);
		}

		private static GameStateManagerComponent? FindFirst(Node node)
		{
			if (node is GameStateManagerComponent gsm) return gsm;
			foreach (var child in node.GetChildren())
				if (FindFirst(child) is { } found) return found;
			return null;
		}

		// FindUILayer() removed: it looked for /root/HUD and then any CanvasLayer directly under
		// /root, but a genre's HUD lives at <GameScene>/HUD and /root holds only autoloads, so it
		// always found nothing and fell through to the world-space parent. AddOverlay now creates
		// its own layer, which is correct in both the main-menu and in-game cases.

		/// <summary>Show the save game menu. Idempotent — a second call while a menu is open is ignored.</summary>
		public void ShowSaveMenu()
		{
			if (_gameStateManager == null)
			{
				GD.PrintErr("[SaveLoadManager] GameStateManager not found");
				return;
			}
			if (MenuAlreadyOpen()) return;

			var scene = ResolveMenuScene(SaveMenuPrefab, "Save");
			if (scene == null) return;

			// Instantiate untyped then as-cast: a typed Instantiate<T> THROWS on a wrong root,
			// making the null-guard below unreachable (the GetNode<T> trap in generic form).
			if (scene.Instantiate() is not SaveGameMenuComponent saveMenu)
			{
				GD.PushError($"[{Name}] Save menu scene's root is not a SaveGameMenuComponent — cannot show it.");
				return;
			}

			EmitSignal(SignalName.SaveStarted);
			AddOverlay(saveMenu);

			// Wire signals
			saveMenu.SaveConfirmed += (slot, saveName) => OnSaveConfirmed(slot, saveName);
			saveMenu.CancelPressed += () => GD.Print("[SaveLoad] Save cancelled");
		}

		/// <summary>Show the load game menu. Idempotent — a second call while a menu is open is ignored.</summary>
		public void ShowLoadMenu()
		{
			if (_gameStateManager == null)
			{
				GD.PrintErr("[SaveLoadManager] GameStateManager not found");
				return;
			}
			if (MenuAlreadyOpen()) return;

			var scene = ResolveMenuScene(LoadMenuPrefab, "Load");
			if (scene == null) return;

			if (scene.Instantiate() is not LoadGameMenuComponent loadMenu)
			{
				GD.PushError($"[{Name}] Load menu scene's root is not a LoadGameMenuComponent — cannot show it.");
				return;
			}

			AddOverlay(loadMenu);

			// Wire signals
			loadMenu.LoadConfirmed += (slot) => OnLoadConfirmed(slot);
			loadMenu.DeleteConfirmed += (slot) => OnDeleteConfirmed(slot);
			loadMenu.CancelPressed += () => GD.Print("[SaveLoad] Load cancelled");
		}

		private void OnSaveConfirmed(int slot, string saveName)
		{
			if (_gameStateManager == null) return;

			_gameStateManager.SyncAllSaveables(); // Sync all components' state

			// SyncAllSaveables early-returns when there is no state; it does not create one.
			// The null-forgiving ! here was a live NullReferenceException on the pause menu's
			// Save button whenever a run started without state being seeded.
			var state = _gameStateManager.GetCurrentState();
			if (state == null)
			{
				GD.PushError("[SaveLoad] No game state to save — is GameFlowComponent in the scene?");
				return;
			}
			state.Metadata.SaveName = saveName;
			bool success = _gameStateManager.Save(slot);

			if (success)
			{
				GD.Print($"[SaveLoad] Game saved to slot {slot}: {saveName}");
				EmitSignal(SignalName.SaveCompleted, slot);
			}
			else
			{
				GD.PrintErr($"[SaveLoad] Failed to save to slot {slot}");
			}
		}

		private void OnLoadConfirmed(int slot)
		{
			if (_gameStateManager == null) return;

			// Queue the restore rather than applying it now — the scene change below frees
			// this scene, so anything restored here would be thrown away. GameFlowComponent
			// in the incoming gameplay scene applies it via BeginSession().
			bool success = _gameStateManager.LoadForSceneChange(slot);
			if (success)
			{
				GD.Print($"[SaveLoad] Game loaded from slot {slot}");
				EmitSignal(SignalName.LoadCompleted, slot);

				// Reload the game scene. Go through GameApp, not GameInfo directly:
				// GameInfo.GameScenePath defaults to "res://scenes/main/main.tscn", which
				// the generator never creates (it stamps <genre>_main.tscn). GameApp
				// resolves that against the skin catalog.
				var tree = GetTree();
				var gamePath = GameApp.Instance?.GameScenePath;
				if (string.IsNullOrEmpty(gamePath) || !ResourceLoader.Exists(gamePath))
				{
					GD.PushError($"[SaveLoad] Loaded slot {slot} but the game scene is missing: '{gamePath}'");
					return;
				}
				// Clear pause before leaving: the overlay that paused us dies with the old
				// scene, and the new scene would have nothing left to unpause it.
				if (tree != null)
				{
					tree.Paused = false;
					tree.ChangeSceneToFile(gamePath);
				}
			}
			else
			{
				GD.PrintErr($"[SaveLoad] Failed to load from slot {slot}");
			}
		}

		private void OnDeleteConfirmed(int slot)
		{
			if (_gameStateManager == null) return;

			bool success = _gameStateManager.DeleteSave(slot);
			if (success)
				GD.Print($"[SaveLoad] Save slot {slot} deleted");
			else
				GD.PrintErr($"[SaveLoad] Failed to delete slot {slot}");
		}
	}
}

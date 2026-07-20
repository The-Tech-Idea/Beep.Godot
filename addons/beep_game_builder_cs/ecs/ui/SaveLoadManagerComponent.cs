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
		private CanvasLayer? _uiLayer;

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
			FindUILayer();
		}

		/// <summary>Add a save/load menu over whatever is on screen.
		///
		/// Two things this has to get right:
		///  • Parent inside the current scene, not at /root. Parenting to the tree root
		///    left the menu outside the scene, so it survived scene changes and lingered.
		///  • ProcessMode = Always. These menus are opened from the pause menu, i.e. while
		///    the tree is paused; the default (Pausable) means every button is inert and
		///    the overlay can't even be dismissed.
		/// </summary>
		private void AddOverlay(Node overlay)
		{
			overlay.ProcessMode = Node.ProcessModeEnum.Always;

			Node? parent = _uiLayer;
			parent ??= GetTree()?.CurrentScene;
			parent ??= GetParent();

			if (parent == null)
			{
				GD.PushError($"[{Name}] Nowhere to add the menu — no UI layer or current scene.");
				overlay.QueueFree();
				return;
			}
			parent.AddChild(overlay);
			_openMenu = overlay;
			// Clear the guard when the overlay closes itself (QueueFree), so the next
			// ShowSaveMenu/ShowLoadMenu can open again.
			overlay.TreeExited += () => { if (_openMenu == overlay) _openMenu = null; };
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

		private void FindUILayer()
		{
			_uiLayer = GetNodeOrNull<CanvasLayer>("/root/HUD");
			if (_uiLayer != null) return;

			var root = GetTree()?.Root;
			if (root == null) return;
			foreach (var child in root.GetChildren())
				if (child is CanvasLayer layer) { _uiLayer = layer; return; }
		}

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

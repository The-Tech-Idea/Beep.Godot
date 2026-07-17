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
		[Export] public NodePath? SaveMenuScenePath { get; set; }
		[Export] public NodePath? LoadMenuScenePath { get; set; }
		[Export] public string SaveMenuPrefab { get; set; } = "res://scenes/ui/save_game_menu.tscn";
		[Export] public string LoadMenuPrefab { get; set; } = "res://scenes/ui/load_game_menu.tscn";

		[Signal] public delegate void SaveStartedEventHandler();
		[Signal] public delegate void SaveCompletedEventHandler(int slot);
		[Signal] public delegate void LoadStartedEventHandler(int slot);
		[Signal] public delegate void LoadCompletedEventHandler(int slot);

		private GameStateManagerComponent? _gameStateManager;
		private CanvasLayer? _uiLayer;

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

		/// <summary>Show the save game menu.</summary>
		public void ShowSaveMenu()
		{
			if (_gameStateManager == null)
			{
				GD.PrintErr("[SaveLoadManager] GameStateManager not found");
				return;
			}

			EmitSignal(SignalName.SaveStarted);

			// Load save menu scene
			var scene = GD.Load<PackedScene>(SaveMenuPrefab);
			if (scene == null)
			{
				GD.PrintErr($"[SaveLoadManager] Cannot load save menu: {SaveMenuPrefab}");
				return;
			}

			var saveMenu = scene.Instantiate<SaveGameMenuComponent>();
			if (saveMenu == null) return;

			AddOverlay(saveMenu);

			// Wire signals
			saveMenu.SaveConfirmed += (slot, saveName) => OnSaveConfirmed(slot, saveName);
			saveMenu.CancelPressed += () => GD.Print("[SaveLoad] Save cancelled");
		}

		/// <summary>Show the load game menu.</summary>
		public void ShowLoadMenu()
		{
			if (_gameStateManager == null)
			{
				GD.PrintErr("[SaveLoadManager] GameStateManager not found");
				return;
			}

			// Load menu scene
			var scene = GD.Load<PackedScene>(LoadMenuPrefab);
			if (scene == null)
			{
				GD.PrintErr($"[SaveLoadManager] Cannot load load menu: {LoadMenuPrefab}");
				return;
			}

			var loadMenu = scene.Instantiate<LoadGameMenuComponent>();
			if (loadMenu == null) return;

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
			_gameStateManager.GetCurrentState()!.Metadata.SaveName = saveName;
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

			bool success = _gameStateManager.Load(slot);
			if (success)
			{
				_gameStateManager.RestoreAllSaveables(); // Restore all components' state
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

using Godot;
using Beep.GameBuilder;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
	/// <summary>
	/// Feature-based game state manager (best practice for Godot 4.7 C#).
	/// Handles serialization, persistence, and auto-discovery of ISaveable components.
	///
	/// Architecture: Each game feature (movement, combat, inventory) has its own state class.
	/// The root GameStateData aggregates all features. Components implement ISaveable to participate.
	///
	/// Usage:
	/// 1. Attach this to your Game scene
	/// 2. Have your gameplay components implement ISaveable interface
	/// 3. Call Save(slot) / Load(slot) to persist all component state
	/// 4. Call GetCurrentState() to access aggregated feature state
	/// 5. Override GetAllSaveables() if you need custom discovery logic
	/// </summary>
	[Tool]
	[GlobalClass]
	public partial class GameStateManagerComponent : GameplayComponent
	{
		private static GameStateManagerComponent? _instance;

		/// <summary>The autoloaded GameStateManager, or null if not registered.
		/// Registered as an autoload by BeepGenreGenerator so save/load works from the
		/// menus, which live in a different scene from gameplay. Mirrors GameApp.Instance.</summary>
		public static GameStateManagerComponent? Instance
		{
			get
			{
				if (_instance != null && GodotObject.IsInstanceValid(_instance)) return _instance;
				if (Engine.GetMainLoop() is SceneTree tree
					&& tree.Root.GetNodeOrNull<GameStateManagerComponent>("/root/GameStateManager") is { } gsm)
				{
					_instance = gsm;
					return gsm;
				}
				return null;
			}
		}

		[Export] public string SaveDirectory { get; set; } = "user://saves";
		[Export] public int MaxSaveSlots { get; set; } = 5;
		[Export] public bool AutosaveEnabled { get; set; } = true;
		[Export] public float AutosaveIntervalSeconds { get; set; } = 300f;

		[Signal] public delegate void GameStateSavedEventHandler(int slot, string filename);
		[Signal] public delegate void GameStateLoadedEventHandler(int slot, string filename);
		[Signal] public delegate void GameStateDeletedEventHandler(int slot);
		[Signal] public delegate void AutosaveTriggeredEventHandler();

		protected GameBuilder.GameStateData? _currentState;
		private float _autosaveTimer;
		private int _currentSlot = -1;

		public override void _Ready()
		{
			base._Ready();
			BeepFileUtils.EnsureDir(SaveDirectory);
			if (AutosaveEnabled)
				_autosaveTimer = AutosaveIntervalSeconds;
		}

		public override void _Process(double delta)
		{
			if (!IsActive || !AutosaveEnabled) return;
			_autosaveTimer -= (float)delta;
			if (_autosaveTimer <= 0)
			{
				_autosaveTimer = AutosaveIntervalSeconds;
				SaveAutosave();
				EmitSignal(SignalName.AutosaveTriggered);
			}
		}

		/// <summary>Create a new game state. Override in subclass to create your custom state type.</summary>
		public virtual void NewGame(string playerName = "Player")
		{
			_currentState = new GameBuilder.GameStateData();
			_currentState.Metadata.SaveName = playerName;
			_currentState.Metadata.Timestamp = (long)Time.GetTicksMsec();
			_currentState.Metadata.CurrentLevel = GetTree()?.CurrentScene?.SceneFilePath ?? "unknown";
		}

		/// <summary>Save current state to a slot (0-4) or autosave (-1).</summary>
		public bool Save(int slot)
		{
			if (_currentState == null) return false;
			if (slot < 0 || slot >= MaxSaveSlots) return false;

			_currentSlot = slot;
			_currentState.Metadata.Timestamp = (long)Time.GetTicksMsec();
			_currentState.Metadata.CurrentLevel = GetTree()?.CurrentScene?.SceneFilePath ?? "unknown";
			_currentState.Metadata.PlayCount++;

			string filename = GetSaveFilename(slot);
			string json = _currentState.ToJson();
			bool success = BeepFileUtils.SafeWriteText(filename, json, true);

			if (success)
				EmitSignal(SignalName.GameStateSaved, slot, filename);

			return success;
		}

		/// <summary>Save to autosave slot (slot -1).</summary>
		public bool SaveAutosave()
		{
			if (_currentState == null) return false;

			_currentState.Metadata.Timestamp = (long)Time.GetTicksMsec();
			_currentState.Metadata.CurrentLevel = GetTree()?.CurrentScene?.SceneFilePath ?? "unknown";

			string filename = GetSaveFilename(-1);
			string json = _currentState.ToJson();
			return BeepFileUtils.SafeWriteText(filename, json, true);
		}

		/// <summary>Load game state from a slot. Override to instantiate your custom state type.</summary>
		public virtual bool Load(int slot)
		{
			if (slot < -1 || slot >= MaxSaveSlots) return false;

			string filename = GetSaveFilename(slot);
			if (!BeepFileUtils.FileExists(filename)) return false;

			string json = BeepFileUtils.ReadText(filename);
			_currentState = new GameBuilder.GameStateData();
			_currentState.FromJsonString(json);
			_currentSlot = slot;

			EmitSignal(SignalName.GameStateLoaded, slot, filename);
			return true;
		}

		/// <summary>Delete a save slot.</summary>
		public bool DeleteSave(int slot)
		{
			if (slot < -1 || slot >= MaxSaveSlots) return false;

			string filename = GetSaveFilename(slot);
			if (!BeepFileUtils.FileExists(filename)) return false;

			var dir = DirAccess.Open(SaveDirectory);
			if (dir != null && dir.FileExists(filename.GetFile()))
			{
				dir.Remove(filename.GetFile());
				EmitSignal(SignalName.GameStateDeleted, slot);
				return true;
			}
			return false;
		}

		/// <summary>Get all save slots with metadata.</summary>
		public List<(int slot, GameBuilder.SaveMetadata metadata)> GetSaveSlots()
		{
			var slots = new List<(int slot, GameBuilder.SaveMetadata metadata)>();
			for (int i = 0; i < MaxSaveSlots; i++)
			{
				string filename = GetSaveFilename(i);
				if (!BeepFileUtils.FileExists(filename)) continue;

				string json = BeepFileUtils.ReadText(filename);
				var state = new GameBuilder.GameStateData();
				state.FromJsonString(json);
				slots.Add((i, state.Metadata));
			}
			return slots;
		}

		/// <summary>Get current game state (or null if not initialized).</summary>
		public GameBuilder.GameStateData? GetCurrentState() => _currentState;

		/// <summary>Set game data key (into GameData dictionary).</summary>
		public void SetGameData(string key, Variant value)
		{
			if (_currentState != null)
				_currentState.GameData[key] = value;
		}

		/// <summary>Get game data key (from GameData dictionary).</summary>
		public Variant GetGameData(string key, Variant defaultValue = new())
		{
			if (_currentState != null && _currentState.GameData.TryGetValue(key, out var value))
				return value;
			return defaultValue;
		}

		/// <summary>Auto-discover and sync all ISaveable components before saving.</summary>
		public void SyncAllSaveables()
		{
			if (_currentState == null) return;

			var saveables = GetAllSaveables();
			foreach (var saveable in saveables)
			{
				saveable.Save(_currentState);
			}
		}

		/// <summary>Auto-discover and restore all ISaveable components after loading.</summary>
		public void RestoreAllSaveables()
		{
			if (_currentState == null) return;

			var saveables = GetAllSaveables();
			foreach (var saveable in saveables)
			{
				saveable.Load(_currentState);
			}
		}

		/// <summary>Override this to customize ISaveable discovery (default: tree scan).</summary>
		protected virtual List<ISaveable> GetAllSaveables()
		{
			var root = GetTree()?.Root;
			return root != null ? SaveableHelper.FindAllSaveables(root) : new List<ISaveable>();
		}

		private string GetSaveFilename(int slot)
		{
			string slotName = slot < 0 ? "autosave" : $"save_{slot}";
			return $"{SaveDirectory}/{slotName}.json";
		}
	}
}

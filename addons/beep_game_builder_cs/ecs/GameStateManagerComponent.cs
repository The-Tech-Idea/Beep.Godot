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
		private bool _pendingRestore;

		public override void _Ready()
		{
			base._Ready();
			// Its own doc says "attach this to your Game scene", so it lands in scenes users
			// open in the editor. Without this it created user://saves at design time and
			// ran the autosave countdown against the player's real save files.
			if (Engine.IsEditorHint()) return;

			// Seed from GameInfo so a genre's declared tuning (max_save_slots,
			// autosave_interval_seconds) actually reaches this component. It is registered
			// as a script-path autoload, so no scene ever supplies these exports and the
			// generator's parsed values were being silently discarded.
			var info = GameBuilder.GameInfo.Instance;
			if (info != null)
			{
				if (!string.IsNullOrEmpty(info.SaveDirectory)) SaveDirectory = info.SaveDirectory;
				if (info.MaxSaveSlots > 0) MaxSaveSlots = info.MaxSaveSlots;
				AutosaveEnabled = info.AutosaveEnabled;
				if (info.AutosaveIntervalSeconds > 0f) AutosaveIntervalSeconds = info.AutosaveIntervalSeconds;
			}

			BeepFileUtils.EnsureDir(SaveDirectory);
			if (AutosaveEnabled)
				_autosaveTimer = AutosaveIntervalSeconds;
		}

		public override void _Process(double delta)
		{
			if (Engine.IsEditorHint()) return;
			if (!IsActive || !AutosaveEnabled) return;
			_autosaveTimer -= (float)delta;
			if (_autosaveTimer <= 0)
			{
				_autosaveTimer = AutosaveIntervalSeconds;
				SaveAutosave();
				EmitSignal(SignalName.AutosaveTriggered);
			}
		}

		/// <summary>Slot index for the autosave file. Distinct from the numbered slots 0..MaxSaveSlots-1.</summary>
		public const int AutosaveSlot = -1;

		/// <summary>Wall-clock seconds. Time.GetTicksMsec() is uptime-since-launch, which made
		/// "newest save" rank by how long a session had been running (a save 10 min into
		/// yesterday's session always beat one 1 min into today's) and rendered every slot
		/// as year 0001 in the load menu.</summary>
		private static long Now() => (long)Time.GetUnixTimeFromSystem();

		/// <summary>Create a new game state. Override in subclass to create your custom state type.</summary>
		public virtual void NewGame(string playerName = "Player")
		{
			_currentState = new GameBuilder.GameStateData();
			_currentState.Metadata.SaveName = playerName;
			_currentState.Metadata.Timestamp = Now();
			_currentState.Metadata.CurrentLevel = GetTree()?.CurrentScene?.SceneFilePath ?? "unknown";
		}

		/// <summary>State to write into, creating it on demand. Nothing called NewGame() on the
		/// real New Game path, so _currentState stayed null and every save silently returned
		/// false. GameFlowComponent now seeds it, and this is the backstop for any entry point
		/// that doesn't.</summary>
		protected GameBuilder.GameStateData EnsureState()
			=> _currentState ??= new GameBuilder.GameStateData();

		/// <summary>Save current state to a numbered slot (0..MaxSaveSlots-1).
		/// Use <see cref="SaveAutosave"/> for the autosave file.</summary>
		public bool Save(int slot)
		{
			if (slot < 0 || slot >= MaxSaveSlots) return false;

			EnsureState();
			_currentSlot = slot;
			_currentState!.Metadata.Timestamp = Now();
			_currentState.Metadata.PlaytimeSeconds = GameApp.Instance?.SessionPlaytimeSeconds ?? 0f;
			_currentState.Metadata.CurrentLevel = GetTree()?.CurrentScene?.SceneFilePath ?? "unknown";
			_currentState.Metadata.PlayCount++;

			string filename = GetSaveFilename(slot);
			string json = _currentState.ToJson();
			bool success = BeepFileUtils.SafeWriteText(filename, json, true);

			if (success)
				EmitSignal(SignalName.GameStateSaved, slot, filename);

			return success;
		}

		/// <summary>Save to the autosave file.</summary>
		public bool SaveAutosave()
		{
			EnsureState();
			_currentState!.Metadata.Timestamp = Now();
			_currentState.Metadata.PlaytimeSeconds = GameApp.Instance?.SessionPlaytimeSeconds ?? 0f;
			_currentState.Metadata.CurrentLevel = GetTree()?.CurrentScene?.SceneFilePath ?? "unknown";

			string filename = GetSaveFilename(AutosaveSlot);
			string json = _currentState.ToJson();
			return BeepFileUtils.SafeWriteText(filename, json, true);
		}

		/// <summary>Load game state from a slot. Override to instantiate your custom state type.
		/// Returns false — leaving the current state untouched — if the file is missing or corrupt.</summary>
		public virtual bool Load(int slot)
		{
			if (slot < AutosaveSlot || slot >= MaxSaveSlots) return false;

			string filename = GetSaveFilename(slot);
			if (!BeepFileUtils.FileExists(filename)) return false;

			// Parse into a scratch state and only adopt it if the file was actually readable.
			// Assigning first meant a corrupt save loaded as "success" onto a default state.
			string json = BeepFileUtils.ReadText(filename);
			var loaded = new GameBuilder.GameStateData();
			if (!loaded.FromJsonString(json))
			{
				GD.PushError($"[GameStateManager] Refusing to load corrupt save: {filename}");
				return false;
			}

			_currentState = loaded;
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

		/// <summary>Get all populated save slots with metadata. Pass includeAutosave to also
		/// report the autosave file, which the in-game Save button and the autosave timer
		/// both write to — without it the player's only in-game save was invisible to every
		/// load menu and could never be loaded back.</summary>
		public List<(int slot, GameBuilder.SaveMetadata metadata)> GetSaveSlots(bool includeAutosave = false)
		{
			var slots = new List<(int slot, GameBuilder.SaveMetadata metadata)>();

			if (includeAutosave) TryAddSlot(slots, AutosaveSlot);
			for (int i = 0; i < MaxSaveSlots; i++) TryAddSlot(slots, i);

			return slots;
		}

		/// <summary>Append a slot's metadata if its file exists and parses. Corrupt files are
		/// skipped with a warning rather than listed as a valid-looking empty save.</summary>
		private void TryAddSlot(List<(int slot, GameBuilder.SaveMetadata metadata)> slots, int slot)
		{
			string filename = GetSaveFilename(slot);
			if (!BeepFileUtils.FileExists(filename)) return;

			var state = new GameBuilder.GameStateData();
			if (!state.FromJsonString(BeepFileUtils.ReadText(filename)))
			{
				GD.PushWarning($"[GameStateManager] Skipping corrupt save: {filename}");
				return;
			}
			slots.Add((slot, state.Metadata));
		}

		/// <summary>Get current game state (or null if not initialized).</summary>
		public GameBuilder.GameStateData? GetCurrentState() => _currentState;

		/// <summary>Load a save that is about to be followed by a scene change, deferring the
		/// restore until the gameplay scene actually exists.
		///
		/// Callers used to do Load → RestoreAllSaveables → ChangeSceneToFile, which pushed the
		/// state into the menu scene that was then freed; the incoming gameplay scene's
		/// components were never touched, so loading a save restored nothing. This manager is
		/// an autoload, so the state survives the transition — only the restore has to wait.
		/// Pair with <see cref="BeginSession"/> from the gameplay scene.</summary>
		public bool LoadForSceneChange(int slot)
		{
			if (!Load(slot)) return false;
			_pendingRestore = true;
			return true;
		}

		/// <summary>Called by GameFlowComponent._Ready once the gameplay scene is up. Applies a
		/// save queued by <see cref="LoadForSceneChange"/>, or seeds fresh state for a new run.
		///
		/// The seeding half matters: NewGame() had no caller outside the doc template, so
		/// _currentState was permanently null and every Save() silently returned false.</summary>
		public void BeginSession(string playerName = "Player")
		{
			if (_pendingRestore)
			{
				_pendingRestore = false;
				// Deferred, not immediate: _Ready propagates bottom-up, so the caller's sibling
				// components may not have run theirs yet — HealthComponent._Ready setting
				// CurrentHealth = MaxHealth would overwrite the health we just restored.
				// Deferring puts the restore after the whole scene has readied.
				Callable.From(RestoreAllSaveables).CallDeferred();
				return;
			}
			if (_currentState == null) NewGame(playerName);
		}

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

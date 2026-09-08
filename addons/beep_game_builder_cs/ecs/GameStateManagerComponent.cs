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
		// No static Instance and no /root/GameStateManager lookup: GameApp owns
		// this component and hands it out as GameApp.Instance.Saves. It still
		// outlives scene changes. Discovery is limited to the current gameplay
		// scope, with GameApp included explicitly for persistent session state.

		[Export] public string SaveDirectory { get; set; } = "user://saves";
		/// <summary>Optional gameplay subtree. Empty uses CurrentScene; headless hosts without
		/// a current scene use the tree root. GameApp is included separately for session state.</summary>
		[Export] public NodePath SaveRootPath { get; set; } = new("");
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
		private bool _capturingSnapshot;
		private int _sessionGeneration;
		private bool _sessionRequested;
		private bool _loadFailed;
		private readonly HashSet<Node> _loading = new();
		private readonly List<(Node Owner, Action Action)> _whenReady = new();
		public bool IsSessionReady { get; private set; }
		public bool RestoreInFlight { get; private set; }
		public bool IsRestorePending => _pendingRestore;

		public void SuspendSession()
		{
			_sessionGeneration++;
			IsSessionReady = false;
			_sessionRequested = false;
			_loading.Clear();
			_whenReady.Clear();
			_loadFailed = false;
			GameApp.Instance?.SetGameRunning(false);
		}
		public bool CanSave => IsActive && IsSessionReady && !RestoreInFlight && !_pendingRestore
			&& (GameApp.Instance?.IsGameRunning ?? true);
		[Signal] public delegate void SessionReadyEventHandler();
		[Signal] public delegate void SessionFailedEventHandler(string reason);
		public int EffectiveMaxSaveSlots => Mathf.Max(1, MaxSaveSlots);
		public float EffectiveAutosaveIntervalSeconds => Mathf.Max(0.1f, float.IsFinite(AutosaveIntervalSeconds) ? AutosaveIntervalSeconds : 300f);

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
				_autosaveTimer = EffectiveAutosaveIntervalSeconds;
		}

		public override void _Process(double delta)
		{
			if (Engine.IsEditorHint()) return;
			if (!CanSave || !AutosaveEnabled) return;
			_autosaveTimer = Mathf.Max(0f, (float.IsFinite(_autosaveTimer) ? _autosaveTimer : EffectiveAutosaveIntervalSeconds) - DeltaSeconds(delta));
			if (_autosaveTimer <= 0)
			{
				_autosaveTimer = EffectiveAutosaveIntervalSeconds;
				if (SaveAutosave()) EmitSignal(SignalName.AutosaveTriggered);
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
			_sessionGeneration++;
			_pendingRestore = false;
			IsSessionReady = false;
			_sessionRequested = false;
			_loadFailed = false;
			_loading.Clear();
			_whenReady.Clear();
			_currentSlot = -1;
			_autosaveTimer = EffectiveAutosaveIntervalSeconds;
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
			if (!CanSave || slot < 0 || slot >= EffectiveMaxSaveSlots) return false;

			EnsureState();
			// Sync here rather than trusting callers to do it first. It was the caller's job,
			// and the autosave timer forgot — it wrote whatever _currentState last held, so
			// timed autosaves persisted stale values. Callers that still sync are harmless.
			if (!TryCaptureSnapshot()) return false;
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
			if (!CanSave) return false;
			EnsureState();
			// Same reason as Save(int): the _Process timer calls straight in here without
			// syncing, which is exactly the caller that must not be trusted to remember.
			if (!TryCaptureSnapshot()) return false;
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
			if (!IsActive || RestoreInFlight || slot < AutosaveSlot || slot >= EffectiveMaxSaveSlots) return false;

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
			_sessionGeneration++;
			IsSessionReady = false;
			GameApp.Instance?.SetGameRunning(false);

			EmitSignal(SignalName.GameStateLoaded, slot, filename);
			return true;
		}

		/// <summary>Delete a save slot.</summary>
		public bool DeleteSave(int slot)
		{
			if (slot < -1 || slot >= EffectiveMaxSaveSlots) return false;

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
			for (int i = 0; i < EffectiveMaxSaveSlots; i++) TryAddSlot(slots, i);

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
			GameApp.Instance?.SetGameRunning(false);
			_loading.Clear();
			_whenReady.Clear();
			_loadFailed = false;
			_sessionRequested = false;

			// Restore the autoload's progression NOW rather than with the rest. GameApp
			// survives the scene change, and the incoming scene's LevelLoaderComponent reads
			// GameApp.CurrentLevel in its _Ready to decide which level to instance — a
			// deferred restore lands after that read, so every load reopened on level 1.
			// Safe to do early precisely because it is an autoload: no scene component's
			// _Ready writes over it, which is the hazard the deferral exists for.
			if (_currentState != null) GameApp.Instance?.Load(_currentState);

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
			EnsureState();
			_sessionRequested = true;
			IsSessionReady = false;
			GameApp.Instance?.SetGameRunning(false);
			ScheduleReady();
		}

		public void BeginWorldLoad(Node owner)
		{
			_loading.Add(owner);
			IsSessionReady = false;
			GameApp.Instance?.SetGameRunning(false);
		}

		public void CompleteWorldLoad(Node owner, bool success = true)
		{
			if (!_loading.Remove(owner)) return;
			if (!success)
			{
				_loadFailed = true;
				EmitSignal(SignalName.SessionFailed, $"World load failed: {owner.Name}");
			}
			ScheduleReady();
		}

		public bool HasPendingSaveRecord(string key)
			=> _pendingRestore && _currentState != null && _currentState.GameData.TryGetValue(key, out var value)
				&& value.VariantType == Variant.Type.Dictionary;

		internal static void AfterWorldReady(Node owner, Action action, string? savedKey = null)
		{
			if (GameApp.Instance?.Saves is { } saves)
			{
				if (savedKey != null && saves.HasPendingSaveRecord(savedKey)) return;
				saves.WhenSessionReady(owner, action);
			}
			else Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(owner) && owner.IsInsideTree()) action();
			}).CallDeferred();
		}

		internal void WhenSessionReady(Node owner, Action action)
		{
			if (IsSessionReady) action();
			else _whenReady.Add((owner, action));
		}

		private void ScheduleReady()
		{
			int generation = _sessionGeneration;
			Callable.From(() =>
			{
				if (!IsInsideTree() || generation != _sessionGeneration || !_sessionRequested
					|| _loadFailed || IsSessionReady || _loading.Count != 0) return;
				try
				{
					if (_pendingRestore) RestoreAllSaveables();
					_pendingRestore = false;
					var callbacks = _whenReady.ToArray();
					_whenReady.Clear();
					foreach (var callback in callbacks)
					{
						if (generation != _sessionGeneration) return;
						if (GodotObject.IsInstanceValid(callback.Owner) && callback.Owner.IsInsideTree()) callback.Action();
					}
					if (generation != _sessionGeneration || _loadFailed || _loading.Count != 0) return;
					if (_whenReady.Count > 0) { ScheduleReady(); return; }
					IsSessionReady = true;
					GameApp.Instance?.SetGameRunning(true);
					EmitSignal(SignalName.SessionReady);
				}
				catch (Exception error)
				{
					_loadFailed = true;
					EmitSignal(SignalName.SessionFailed, error.Message);
					GD.PushError($"[Session] Initialization failed: {error}");
				}
			}).CallDeferred();
		}

		/// <summary>Set an explicit game choice, retained independently of component snapshots.</summary>
		public void SetGameData(string key, Variant value)
		{
			if (_currentState != null)
				_currentState.CustomData[key] = value;
		}

		/// <summary>Get an explicit game choice written by SetGameData.</summary>
		public Variant GetGameData(string key, Variant defaultValue = new())
		{
			if (_currentState != null && _currentState.CustomData.TryGetValue(key, out var value))
				return value;
			return defaultValue;
		}

		/// <summary>Auto-discover and sync all ISaveable components before saving.</summary>
		public void SyncAllSaveables()
			=> TryCaptureSnapshot();

		private bool TryCaptureSnapshot()
		{
			if (_currentState == null || !CanSave || _capturingSnapshot) return false;
			_capturingSnapshot = true;
			int generation = _sessionGeneration;
			try
			{
				var snapshot = new GameBuilder.GameStateData
				{
					Metadata = GameBuilder.SaveMetadata.FromDict(_currentState.Metadata.ToDict())
				};
				foreach (var saveable in GetAllSaveables())
				{
					saveable.Save(snapshot);
					if (generation != _sessionGeneration || !CanSave) return false;
				}
				// Copy after component callbacks so choices changed during capture are not lost.
				snapshot.CustomData = GameBuilder.GodotConv.ToVariantDict(
					GameBuilder.GodotConv.ToDict(_currentState.CustomData).Duplicate(true));
				_currentState = snapshot;
				return true;
			}
			catch (Exception error)
			{
				GD.PushWarning($"[Save] Snapshot rejected: {error.Message}");
				return false;
			}
			finally { _capturingSnapshot = false; }
		}

		/// <summary>Auto-discover and restore all ISaveable components after loading.</summary>
		public void RestoreAllSaveables()
		{
			if (_currentState == null) return;
			if (RestoreInFlight) return;
			RestoreInFlight = true;
			try
			{
				var participants = GetAllSaveables();
				// Terrain, jobs and inventories must exist before actor references are restored.
				foreach (var saveable in participants)
					if (saveable is not ActorRegistryComponent) saveable.Load(_currentState);
				foreach (var saveable in participants)
					if (saveable is ActorRegistryComponent) saveable.Load(_currentState);
			}
			finally { RestoreInFlight = false; }
		}

		/// <summary>Discover participants only within the gameplay scope and the owning app.</summary>
		protected virtual List<ISaveable> GetAllSaveables()
		{
			var tree = GetTree();
			Node? root = SaveRootPath.IsEmpty ? tree?.CurrentScene ?? tree?.Root : GetNodeOrNull(SaveRootPath);
			if (root == null) throw new InvalidOperationException("SaveRootPath does not resolve to a gameplay node.");
			var saveables = SaveableHelper.FindAllSaveables(root);
			if (GameApp.Instance is { } app && !app.IsQueuedForDeletion() && !saveables.Contains(app))
				saveables.Insert(0, app);
			return saveables;
		}

		private string GetSaveFilename(int slot)
		{
			string slotName = slot < 0 ? "autosave" : $"save_{slot}";
			return $"{SaveDirectory}/{slotName}.json";
		}

		private static float DeltaSeconds(double delta) =>
			double.IsFinite(delta) ? Mathf.Max(0f, (float)delta) : 0f;
	}
}

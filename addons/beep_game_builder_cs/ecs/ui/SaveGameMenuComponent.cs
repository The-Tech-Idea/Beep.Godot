using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI
{
	/// <summary>
	/// Universal save game menu component. Works across all game genres.
	/// Displays available save slots and handles save operations.
	/// Usage: Attach to a Control node in your save menu scene.
	/// Signals: SaveConfirmed (slot), CancelPressed
	/// </summary>
	[Tool]
	[GlobalClass]
	public partial class SaveGameMenuComponent : UIComponent
	{
		[Export] public int MaxSlots { get; set; } = 5;
		[Export] public bool ShowAutosaveSlot { get; set; } = true;

		/// <summary>Guard against clobbering an occupied slot. When true (default), the first Save
		/// press on a slot that already holds a save arms a confirm state (the Save button reads
		/// "Confirm Overwrite") and a second press commits. When false, saving over an occupied
		/// slot is immediate.</summary>
		[Export] public bool ConfirmOverwrite { get; set; } = true;

		[Signal] public delegate void SaveConfirmedEventHandler(int slot, string saveName);
		[Signal] public delegate void CancelPressedEventHandler();

		private LineEdit? _nameInput;
		private VBoxContainer? _slotsVBox;
		private Button? _saveButton;
		private Button? _cancelButton;
		private int _selectedSlot = -1;
		private int _pendingOverwriteSlot = -1;
		private readonly HashSet<int> _occupiedSlots = new();
		private GameStateManagerComponent? _gameStateManager;
		private readonly List<System.Action> _slotDisconnectors = new();

		public override void _Ready()
		{
			base._Ready();
			if (Engine.IsEditorHint()) return;
			_nameInput = GetNodeOrNull<LineEdit>("PanelContainer/VBox/SaveNameContainer/NameInput");
			_slotsVBox = GetNodeOrNull<VBoxContainer>("PanelContainer/VBox/SlotsScroll/SlotsVBox");
			_saveButton = GetNodeOrNull<Button>("PanelContainer/VBox/ButtonHBox/SaveButton");
			_cancelButton = GetNodeOrNull<Button>("PanelContainer/VBox/ButtonHBox/CancelButton");

			if (_saveButton != null) _saveButton.Pressed += OnSavePressed;
			if (_cancelButton != null) _cancelButton.Pressed += OnCancelPressed;

			FindGameStateManager();
			PopulateSlots();
			WireSlotButtons();
			UpdateSaveButtonState();

			// Grab initial focus so a controller/keyboard-only player can operate the menu.
			Callable.From(GrabInitialFocus).CallDeferred();
		}

		private void GrabInitialFocus()
		{
			if (_nameInput != null) { _nameInput.GrabFocus(); return; }
			foreach (var b in SlotButtons()) { b.GrabFocus(); return; }
		}

		private void FindGameStateManager()
		{
			var root = GetTree()?.Root;
			if (root != null)
			{
				foreach (var node in root.GetChildren())
				{
					if (node is GameStateManagerComponent gsm)
					{
						_gameStateManager = gsm;
						break;
					}
				}
			}

			// Keep the menu's row cap in step with the manager's actual slot count, so the
			// player can't select a slot the manager will reject on Save (Save(int) bounds-checks
			// against MaxSaveSlots and silently returns false past it).
			if (_gameStateManager != null && _gameStateManager.MaxSaveSlots > 0)
				MaxSlots = _gameStateManager.MaxSaveSlots;
		}

		private void PopulateSlots()
		{
			if (_slotsVBox == null || _gameStateManager == null) return;

			var slots = _gameStateManager.GetSaveSlots();
			var slotDict = new System.Collections.Generic.Dictionary<int, GameBuilder.SaveMetadata>();
			_occupiedSlots.Clear();
			foreach (var (slot, meta) in slots)
			{
				slotDict[slot] = meta;
				_occupiedSlots.Add(slot);
			}

			// Drive the loop off the buttons the scene actually has, not MaxSlots —
			// GetChild(i) beyond the child count throws, and the two must agree.
			var buttons = SlotButtons();
			for (int i = 0; i < buttons.Count; i++)
			{
				var button = buttons[i];

				if (slotDict.TryGetValue(i, out var meta))
				{
					// Metadata.Timestamp is Unix seconds. It was read as DateTime ticks
					// (100ns since year 1), so every slot rendered as year 0001.
					var time = System.DateTimeOffset.FromUnixTimeSeconds(meta.Timestamp).LocalDateTime;
					button.Text = $"Slot {i + 1} - {meta.SaveName} ({time:MM/dd/yyyy})";
				}
				else
				{
					button.Text = $"Slot {i + 1} - Empty";
				}
			}
		}

		/// <summary>The slot buttons present in the scene, capped at MaxSlots. Returns
		/// empty rather than throwing when the scene supplies none.</summary>
		private System.Collections.Generic.List<Button> SlotButtons()
		{
			var list = new System.Collections.Generic.List<Button>();
			if (_slotsVBox == null) return list;
			foreach (var child in _slotsVBox.GetChildren())
			{
				if (child is Button b) list.Add(b);
				if (list.Count >= MaxSlots) break;
			}
			if (list.Count == 0)
				GD.PushWarning($"[{Name}] No slot buttons found under SlotsVBox — check the scene.");
			return list;
		}

		private void WireSlotButtons()
		{
			var buttons = SlotButtons();
			for (int i = 0; i < buttons.Count; i++)
			{
				int slot = i; // Capture for closure
				var btn = buttons[i];
				System.Action handler = () => OnSlotSelected(slot);
				btn.Pressed += handler;
				_slotDisconnectors.Add(() => { if (GodotObject.IsInstanceValid(btn)) btn.Pressed -= handler; });
			}
		}

		private void OnSlotSelected(int slot)
		{
			_selectedSlot = slot;
			// Selecting a different slot cancels any armed overwrite confirmation.
			if (_pendingOverwriteSlot != slot) ResetOverwritePrompt();
			var buttons = SlotButtons();
			for (int i = 0; i < buttons.Count; i++)
				buttons[i].SetPressed(i == slot);
		}

		private void OnSavePressed()
		{
			if (_selectedSlot < 0)
			{
				GD.PrintErr("[SaveGameMenu] No slot selected");
				return;
			}

			// Overwrite guard: the first press on an occupied slot arms a confirmation instead of
			// clobbering the existing save. A second press (slot already armed) commits.
			bool overwriting = _occupiedSlots.Contains(_selectedSlot);
			if (overwriting && ConfirmOverwrite && _pendingOverwriteSlot != _selectedSlot)
			{
				_pendingOverwriteSlot = _selectedSlot;
				if (_saveButton != null) _saveButton.Text = "Confirm Overwrite";
				return;
			}

			string saveName = _nameInput?.Text ?? "Save Game";
			if (string.IsNullOrEmpty(saveName))
				saveName = $"Slot {_selectedSlot + 1}";

			EmitSignal(SignalName.SaveConfirmed, _selectedSlot, saveName);
			QueueFree();
		}

		private void ResetOverwritePrompt()
		{
			_pendingOverwriteSlot = -1;
			if (_saveButton != null) _saveButton.Text = "Save";
		}

		private void OnCancelPressed()
		{
			EmitSignal(SignalName.CancelPressed);
			QueueFree();
		}

		private void UpdateSaveButtonState()
		{
			bool gameRunning = GameApp.Instance?.IsGameRunning ?? false;
			if (_saveButton != null)
			{
				_saveButton.Disabled = !gameRunning;
				_saveButton.TooltipText = gameRunning ? "Save current game progress" : "No active game to save";
			}
		}

		public override void _ExitTree()
		{
			base._ExitTree();
			if (_saveButton != null) _saveButton.Pressed -= OnSavePressed;
			if (_cancelButton != null) _cancelButton.Pressed -= OnCancelPressed;
			foreach (var disconnect in _slotDisconnectors) disconnect();
			_slotDisconnectors.Clear();
		}
	}
}

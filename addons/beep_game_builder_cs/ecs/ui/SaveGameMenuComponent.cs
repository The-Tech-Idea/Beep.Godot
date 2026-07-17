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

		[Signal] public delegate void SaveConfirmedEventHandler(int slot, string saveName);
		[Signal] public delegate void CancelPressedEventHandler();
		[Signal] public delegate void OverwriteConfirmedEventHandler(int slot);

		private LineEdit? _nameInput;
		private VBoxContainer? _slotsVBox;
		private Button? _saveButton;
		private Button? _cancelButton;
		private int _selectedSlot = -1;
		private GameStateManagerComponent? _gameStateManager;

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
		}

		private void PopulateSlots()
		{
			if (_slotsVBox == null || _gameStateManager == null) return;

			var slots = _gameStateManager.GetSaveSlots();
			var slotDict = new System.Collections.Generic.Dictionary<int, GameBuilder.SaveMetadata>();
			foreach (var (slot, meta) in slots)
				slotDict[slot] = meta;

			// Drive the loop off the buttons the scene actually has, not MaxSlots —
			// GetChild(i) beyond the child count throws, and the two must agree.
			var buttons = SlotButtons();
			for (int i = 0; i < buttons.Count; i++)
			{
				var button = buttons[i];

				if (slotDict.TryGetValue(i, out var meta))
				{
					var time = new System.DateTime((long)meta.Timestamp);
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
				buttons[i].Pressed += () => OnSlotSelected(slot);
			}
		}

		private void OnSlotSelected(int slot)
		{
			_selectedSlot = slot;
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

			string saveName = _nameInput?.Text ?? "Save Game";
			if (string.IsNullOrEmpty(saveName))
				saveName = $"Slot {_selectedSlot + 1}";

			EmitSignal(SignalName.SaveConfirmed, _selectedSlot, saveName);
			QueueFree();
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
			if (_saveButton != null) _saveButton.Pressed -= OnSavePressed;
			if (_cancelButton != null) _cancelButton.Pressed -= OnCancelPressed;
		}
	}
}

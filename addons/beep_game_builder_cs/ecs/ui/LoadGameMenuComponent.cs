using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI
{
	/// <summary>
	/// Universal load game menu component. Works across all game genres.
	/// Displays saved games with metadata and handles load/delete operations.
	/// Usage: Attach to a Control node in your load menu scene.
	/// Signals: LoadConfirmed (slot), DeleteConfirmed (slot), CancelPressed
	/// </summary>
	[Tool]
	[GlobalClass]
	public partial class LoadGameMenuComponent : UIComponent
	{
		[Export] public int MaxSlots { get; set; } = 5;

		[Signal] public delegate void LoadConfirmedEventHandler(int slot);
		[Signal] public delegate void DeleteConfirmedEventHandler(int slot);
		[Signal] public delegate void CancelPressedEventHandler();

		private VBoxContainer? _slotsVBox;
		private Button? _loadButton;
		private Button? _cancelButton;
		private int _selectedSlot = -1;
		private GameStateManagerComponent? _gameStateManager;
		private Dictionary<int, GameBuilder.SaveMetadata> _slotMetadata = new();

		public override void _Ready()
		{
			base._Ready();
			if (Engine.IsEditorHint()) return;
			_slotsVBox = GetNodeOrNull<VBoxContainer>("PanelContainer/VBox/SlotsScroll/SlotsVBox");
			_loadButton = GetNodeOrNull<Button>("PanelContainer/VBox/ButtonHBox/LoadButton");
			_cancelButton = GetNodeOrNull<Button>("PanelContainer/VBox/ButtonHBox/CancelButton");

			if (_loadButton != null) _loadButton.Pressed += OnLoadPressed;
			if (_cancelButton != null) _cancelButton.Pressed += OnCancelPressed;

			FindGameStateManager();
			PopulateSlots();
			WireSlotButtons();
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

		/// <summary>The slot rows the scene actually has, capped at MaxSlots. Mirrors
		/// SaveGameMenuComponent.SlotButtons: GetChild&lt;PanelContainer&gt;(i) up to MaxSlots
		/// indexed past the child count and hard-cast, so it threw before the `== null`
		/// check below could ever run — a scene with a different row count, or a plain
		/// separator among the rows, took the menu down.</summary>
		private System.Collections.Generic.List<PanelContainer> SlotContainers()
		{
			var list = new System.Collections.Generic.List<PanelContainer>();
			if (_slotsVBox == null) return list;
			foreach (var child in _slotsVBox.GetChildren())
			{
				if (child is PanelContainer p) list.Add(p);
				if (list.Count >= MaxSlots) break;
			}
			if (list.Count == 0)
				GD.PushWarning($"[{Name}] No slot rows found under SlotsVBox — check the scene.");
			return list;
		}

		private void PopulateSlots()
		{
			if (_slotsVBox == null || _gameStateManager == null) return;

			var slots = _gameStateManager.GetSaveSlots();
			_slotMetadata.Clear();
			foreach (var (slot, meta) in slots)
			{
				_slotMetadata[slot] = meta;
			}

			var containers = SlotContainers();
			for (int i = 0; i < containers.Count; i++)
			{
				var container = containers[i];

				var slotButton = container.FindChild("SlotButton", owned: false) as Button;
				var nameLabel = container.FindChild("SlotNameLabel", owned: false) as Label;
				var levelLabel = container.FindChild("SlotLevelLabel", owned: false) as Label;
				var timeLabel = container.FindChild("SlotPlaytimeLabel", owned: false) as Label;

				if (_slotMetadata.TryGetValue(i, out var meta))
				{
					if (slotButton != null) slotButton.Text = $"Slot {i + 1}";
					if (nameLabel != null) nameLabel.Text = meta.SaveName;
					if (levelLabel != null) levelLabel.Text = $"Level: {meta.CurrentLevel}";
					if (timeLabel != null)
					{
						int hours = (int)(meta.PlaytimeSeconds / 3600);
						int minutes = (int)((meta.PlaytimeSeconds % 3600) / 60);
						timeLabel.Text = $"Time: {hours}h {minutes}m";
					}
				}
				else
				{
					if (slotButton != null) slotButton.Text = $"Slot {i + 1}";
					if (nameLabel != null) nameLabel.Text = "Empty Slot";
					if (levelLabel != null) levelLabel.Text = "Level: --";
					if (timeLabel != null) timeLabel.Text = "Time: --";
				}
			}
		}

		/// <summary>Wire the per-row buttons. Called once from _Ready — PopulateSlots used to
		/// subscribe the delete buttons on every call and OnDeletePressed re-invoked both, so
		/// handlers accumulated with each delete and later presses emitted DeleteConfirmed
		/// once per accumulated subscription.</summary>
		private void WireSlotButtons()
		{
			var containers = SlotContainers();
			for (int i = 0; i < containers.Count; i++)
			{
				int slot = i;

				if (containers[i].FindChild("SlotButton", owned: false) is Button slotButton)
					slotButton.Pressed += () => OnSlotSelected(slot);

				if (containers[i].FindChild("DeleteButton", owned: false) is Button deleteButton)
					deleteButton.Pressed += () => OnDeletePressed(slot);
			}
		}

		private void OnSlotSelected(int slot)
		{
			bool hasData = _slotMetadata.ContainsKey(slot);
			_selectedSlot = hasData ? slot : -1;

			if (_loadButton != null)
				_loadButton.Disabled = !hasData;

			var containers = SlotContainers();
			for (int i = 0; i < containers.Count; i++)
			{
				containers[i].AddThemeStyleboxOverride("panel",
					i == slot ? new StyleBoxFlat { BgColor = new Color(0.2f, 0.4f, 0.6f) } : new StyleBoxFlat());
			}
		}

		private void OnLoadPressed()
		{
			if (_selectedSlot < 0 || !_slotMetadata.ContainsKey(_selectedSlot)) return;
			EmitSignal(SignalName.LoadConfirmed, _selectedSlot);
			QueueFree();
		}

		private void OnDeletePressed(int slot)
		{
			if (!_slotMetadata.ContainsKey(slot)) return;
			EmitSignal(SignalName.DeleteConfirmed, slot);
			_slotMetadata.Remove(slot);
			// Refresh labels only — the buttons are already wired from _Ready.
			PopulateSlots();
		}

		private void OnCancelPressed()
		{
			EmitSignal(SignalName.CancelPressed);
			QueueFree();
		}

		public override void _ExitTree()
		{
			if (_loadButton != null) _loadButton.Pressed -= OnLoadPressed;
			if (_cancelButton != null) _cancelButton.Pressed -= OnCancelPressed;
		}
	}
}

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
		// int.MinValue = nothing selected. Plain -1 can't be the sentinel because -1 is the
		// autosave slot's own number (GameStateManagerComponent.AutosaveSlot).
		private int _selectedSlot = int.MinValue;
		private GameStateManagerComponent? _gameStateManager;
		private Dictionary<int, GameBuilder.SaveMetadata> _slotMetadata = new();

		public override void _Ready()
		{
			base._Ready();
			if (Engine.IsEditorHint()) return;
			// Resolve by NAME, not a fixed path: the scene layout changed (a Margin wrapper was
			// added for padding), and a project generated from an older template still has the
			// pre-Margin scene. FindChild(recursive) locates each control whether or not the
			// wrapper exists, so the menu works with both layouts.
			_slotsVBox = FindChild("SlotsVBox", recursive: true, owned: false) as VBoxContainer;
			_loadButton = FindChild("LoadButton", recursive: true, owned: false) as Button;
			_cancelButton = FindChild("CancelButton", recursive: true, owned: false) as Button;

			if (_loadButton != null) _loadButton.Pressed += OnLoadPressed;
			if (_cancelButton != null) _cancelButton.Pressed += OnCancelPressed;

			FindGameStateManager();
			PopulateSlots();
			WireSlotButtons();
			NormalizeLayout();

			// Grab initial focus so a controller/keyboard-only player can operate the menu.
			// Deferred so the slot buttons exist and are laid out first.
			Callable.From(GrabInitialFocus).CallDeferred();
		}

		/// <summary>Enforce consistent sizing/spacing in code, so the menu looks right on ANY scene
		/// version — a project generated from an older template keeps its stale .tscn, and a scene
		/// overwrite is easy to forget. Delivered by a plain C# rebuild; no regenerate needed.</summary>
		private void NormalizeLayout()
		{
			var panel = FindChild("PanelContainer", recursive: true, owned: false) as PanelContainer;
			if (panel != null)
				panel.CustomMinimumSize = new Vector2(Mathf.Max(panel.CustomMinimumSize.X, 640), Mathf.Max(panel.CustomMinimumSize.Y, 520));

			// Main VBox (button row's grandparent): consistent separation.
			if (_loadButton?.GetParent()?.GetParent() is VBoxContainer vbox)
				vbox.AddThemeConstantOverride("separation", 18);
			// Bottom button row: uniform spacing.
			if (_loadButton?.GetParent() is HBoxContainer hbox)
				hbox.AddThemeConstantOverride("separation", 12);
			if (_loadButton != null) _loadButton.CustomMinimumSize = new Vector2(0, 44);
			if (_cancelButton != null) _cancelButton.CustomMinimumSize = new Vector2(0, 44);
			// Slot rows: uniform height, delete button sized, row spacing.
			if (_slotsVBox != null) _slotsVBox.AddThemeConstantOverride("separation", 10);
			foreach (var (container, _) in SlotRows())
			{
				if (container is Control c) c.CustomMinimumSize = new Vector2(0, 58);
				if (container.FindChild("DeleteButton", owned: false) is Button del)
					del.CustomMinimumSize = new Vector2(96, 44);
			}
		}

		private void GrabInitialFocus()
		{
			if (_slotsVBox != null)
				foreach (var child in _slotsVBox.GetChildren())
					if (child is Button b) { b.GrabFocus(); return; }
			_loadButton?.GrabFocus();
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

			// Keep the menu's row cap in step with the manager's actual slot count. When these
			// diverged, real saves in slots past MaxSlots were unreachable, or the menu showed
			// "Empty" rows for slots the manager would reject on Save.
			if (_gameStateManager != null && _gameStateManager.MaxSaveSlots > 0)
				MaxSlots = _gameStateManager.MaxSaveSlots;
		}

		/// <summary>The slot rows the scene has, paired with the save slot each maps to.
		/// A row named "AutosaveContainer" maps to the autosave slot (-1) so the in-game
		/// timed autosave is loadable; every other PanelContainer row maps to 0,1,2… in order,
		/// capped at MaxSlots. Mapping by explicit slot number (not positional index) is what
		/// lets the autosave row sit anywhere among the numbered rows without shifting them.</summary>
		private System.Collections.Generic.List<(PanelContainer container, int slot)> SlotRows()
		{
			var list = new System.Collections.Generic.List<(PanelContainer, int)>();
			if (_slotsVBox == null) return list;
			int numbered = 0;
			foreach (var child in _slotsVBox.GetChildren())
			{
				if (child is not PanelContainer p) continue;
				if (p.Name == "AutosaveContainer")
				{
					list.Add((p, GameStateManagerComponent.AutosaveSlot));
					continue;
				}
				if (numbered >= MaxSlots) continue;   // extra numbered rows past the cap are ignored
				list.Add((p, numbered));
				numbered++;
			}
			if (list.Count == 0)
				GD.PushWarning($"[{Name}] No slot rows found under SlotsVBox — check the scene.");
			return list;
		}

		private void PopulateSlots()
		{
			if (_slotsVBox == null || _gameStateManager == null) return;

			// includeAutosave so the in-game/timed autosave appears when the scene has a row for it.
			var slots = _gameStateManager.GetSaveSlots(includeAutosave: true);
			_slotMetadata.Clear();
			foreach (var (slot, meta) in slots)
			{
				_slotMetadata[slot] = meta;
			}

			foreach (var (container, slot) in SlotRows())
			{
				var slotButton = container.FindChild("SlotButton", owned: false) as Button;
				var nameLabel = container.FindChild("SlotNameLabel", owned: false) as Label;
				var levelLabel = container.FindChild("SlotLevelLabel", owned: false) as Label;
				var timeLabel = container.FindChild("SlotPlaytimeLabel", owned: false) as Label;

				string slotName = slot == GameStateManagerComponent.AutosaveSlot ? "Autosave" : $"Slot {slot + 1}";
				if (slotButton != null) slotButton.Text = slotName;

				if (_slotMetadata.TryGetValue(slot, out var meta))
				{
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
					if (nameLabel != null) nameLabel.Text = slot == GameStateManagerComponent.AutosaveSlot ? "No autosave" : "Empty Slot";
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
			foreach (var (container, slot) in SlotRows())
			{
				if (container.FindChild("SlotButton", owned: false) is Button slotButton)
					slotButton.Pressed += () => OnSlotSelected(slot);

				if (container.FindChild("DeleteButton", owned: false) is Button deleteButton)
					deleteButton.Pressed += () => OnDeletePressed(slot);
			}
		}

		private void OnSlotSelected(int slot)
		{
			bool hasData = _slotMetadata.ContainsKey(slot);
			_selectedSlot = hasData ? slot : int.MinValue;

			if (_loadButton != null)
				_loadButton.Disabled = !hasData;

			foreach (var (container, rowSlot) in SlotRows())
			{
				container.AddThemeStyleboxOverride("panel",
					rowSlot == slot ? new StyleBoxFlat { BgColor = new Color(0.2f, 0.4f, 0.6f) } : new StyleBoxFlat());
			}
		}

		private void OnLoadPressed()
		{
			// int.MinValue = nothing selected. A plain "< 0" here would reject the autosave (-1).
			if (_selectedSlot == int.MinValue || !_slotMetadata.ContainsKey(_selectedSlot)) return;
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
			base._ExitTree();
			if (_loadButton != null) _loadButton.Pressed -= OnLoadPressed;
			if (_cancelButton != null) _cancelButton.Pressed -= OnCancelPressed;
		}
	}
}

using Godot;
using System.Collections.Generic;
using Beep.ECS.UI.Kit;

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
	public partial class LoadGameMenuComponent : UIScreenComponent
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

			// Both deferred: the row styles read colours off the generated Theme, and
			// ThemePresetComponent/GameInfoBinder are siblings that may not have applied it yet.
			Callable.From(ApplyRowStyles).CallDeferred();
			// Grab initial focus so a controller/keyboard-only player can operate the menu.
			// Deferred so the slot buttons exist and are laid out first.
			Callable.From(GrabInitialFocus).CallDeferred();
		}

		/// <summary>Enforce consistent sizing/spacing in code, so the menu looks right on ANY scene
		/// version — a project generated from an older template keeps its stale .tscn, and a scene
		/// overwrite is easy to forget. Delivered by a plain C# rebuild; no regenerate needed.</summary>
		private void NormalizeLayout()
		{
			// Every number comes from BeepDialogLayout, which the .tscn is authored to as
			// well. These used to be literals that disagreed with the scene (panel 640x520 vs
			// the scene's 700x550, 18px sections vs the scene's 20) and with the save menu's
			// own copy — so the two screens rendered at different sizes and rhythms.
			BeepDialogLayout.ApplyShell(this);

			// Main VBox (button row's grandparent): consistent separation.
			if (_loadButton?.GetParent()?.GetParent() is VBoxContainer vbox)
				KitChrome.SetConstantOverrideIfChanged(vbox, "separation", BeepDialogLayout.SectionGap);
			// Bottom button row: uniform spacing.
			if (_loadButton?.GetParent() is HBoxContainer hbox)
				KitChrome.SetConstantOverrideIfChanged(hbox, "separation", BeepDialogLayout.ButtonGap);
			if (_loadButton != null)
				_loadButton.CustomMinimumSize = new Vector2(0, BeepDialogLayout.ActionButton(this));
			if (_cancelButton != null)
				_cancelButton.CustomMinimumSize = new Vector2(0, BeepDialogLayout.ActionButton(this));
			// Slot rows: uniform height, delete button sized, row spacing.
			if (_slotsVBox != null)
				KitChrome.SetConstantOverrideIfChanged(_slotsVBox, "separation", BeepDialogLayout.RowGap);
			foreach (var (container, _) in SlotRows())
			{
				// container is already a PanelContainer (which IS a Control), so no type test
				// is needed. The `is Control c` here made the line depend on how the name
				// `Control` resolves in the consuming project, and failed with CS8121 in a
				// game that has its own Control type.
				container.CustomMinimumSize = new Vector2(0, BeepDialogLayout.Row(this));
				if (container.FindChild("SlotHBox", owned: false) is HBoxContainer row)
					KitChrome.SetConstantOverrideIfChanged(row, "separation", BeepDialogLayout.RowInnerGap);
				if (container.FindChild("SlotInfo", owned: false) is VBoxContainer info)
					KitChrome.SetConstantOverrideIfChanged(info, "separation", BeepDialogLayout.RowInfoGap);
				if (container.FindChild("SlotButton", owned: false) is Button slot)
					slot.CustomMinimumSize = new Vector2(
						BeepDialogLayout.SlotButtonWidth, BeepDialogLayout.SlotButtonHeight);
				if (container.FindChild("DeleteButton", owned: false) is Button del)
					del.CustomMinimumSize = new Vector2(
						BeepDialogLayout.DeleteButtonWidth, BeepDialogLayout.DeleteButtonHeight);
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
					if (levelLabel != null) levelLabel.Text = $"Level: {PrettyLevel(meta.CurrentLevel)}";
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

		/// <summary>Human-readable level for a slot row.
		///
		/// <c>SaveMetadata.CurrentLevel</c> is a free-form string, and what the framework
		/// actually stores in it is the gameplay scene's res:// path. The row therefore read
		/// "Level: res://scenes/main/citybuilder_main.tscn" — meaningless to a player, and long
		/// enough that its label's minimum width dragged the whole slot list wider than the
		/// dialog, which is what put a horizontal scrollbar under the list and pushed every
		/// Delete button off the right edge.</summary>
		private static string PrettyLevel(string raw)
		{
			if (string.IsNullOrWhiteSpace(raw)) return "--";
			string s = raw.Contains('/') ? raw.GetFile() : raw;
			if (s.EndsWith(".tscn", System.StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - 5);
			s = s.Replace('_', ' ').Trim();
			return s.Length == 0 ? "--" : s;
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
			_highlightedSlot = slot;

			if (_loadButton != null)
				_loadButton.Disabled = !hasData;

			ApplyRowStyles();
		}

		/// <summary>Row the player last clicked. Separate from <see cref="_selectedSlot"/>,
		/// which is int.MinValue for an empty slot — clicking an empty row still has to move
		/// the highlight, or the click reads as ignored.</summary>
		private int _highlightedSlot = int.MinValue;

		/// <summary>Paint every slot row from the ACTIVE THEME.
		///
		/// The rows used to carry a hardcoded near-black <c>StyleBoxFlat</c> baked into the
		/// scene (0.15, 0.15, 0.19) while the theme supplied the text colour. Under any light
		/// theme — urban, eco, paper, candy — that put near-black text on a near-black row:
		/// the save name was barely legible and the "Level:"/"Time:" captions vanished
		/// completely. Selection then replaced it with a bare <c>new StyleBoxFlat()</c>, which
		/// is fully transparent, so clicking any row erased every other row's background.</summary>
		private void ApplyRowStyles()
		{
			foreach (var (container, rowSlot) in SlotRows())
				KitChrome.SetStyleboxOverrideIfChanged(container, "panel", BuildRowStyle(container, rowSlot == _highlightedSlot));
		}

		/// <summary>A row background that works on light and dark themes, and over a textured
		/// panel. Deliberately built from TRANSLUCENT overlays rather than an opaque colour:
		/// the dialog's own panel may be a 9-patch texture, so an opaque row would punch a
		/// flat rectangle through the artwork. Tinting whatever is behind keeps the skin.</summary>
		// Godot.Control spelled out, not `Control`: a consuming game that defines its own
		// Control type shadows the name here and this stops compiling (CS1503). The same trap
		// already bit NormalizeLayout with CS8121.
		private StyleBox BuildRowStyle(Godot.Control ctx, bool selected)
		{
			// Which way to tint is decided by the theme's own text colour: dark text means a
			// light theme (so darken the row), light text means a dark theme (so lighten it).
			Color text = ctx.GetThemeColor("font_color", "Label");
			bool lightTheme = text.Luminance < 0.5f;
			Color rowBg = lightTheme ? new Color(0f, 0f, 0f, 0.07f) : new Color(1f, 1f, 1f, 0.08f);
			Color accent = ctx.GetThemeColor("font_color", "BeepValue");

			var box = new StyleBoxFlat
			{
				BgColor = selected ? new Color(accent, 0.28f) : rowBg,
				BorderColor = selected ? accent : new Color(accent, 0.16f),
			};
			box.SetCornerRadiusAll(BeepDialogLayout.RowCornerRadius);
			box.SetBorderWidthAll(selected ? 2 : 1);
			box.ContentMarginLeft = BeepDialogLayout.RowPadX;
			box.ContentMarginRight = BeepDialogLayout.RowPadX;
			box.ContentMarginTop = BeepDialogLayout.RowPadY;
			box.ContentMarginBottom = BeepDialogLayout.RowPadY;
			return box;
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

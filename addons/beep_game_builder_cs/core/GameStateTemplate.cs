using Godot;
using System;
using System.Collections.Generic;

namespace Beep.GameBuilder
{
	/// <summary>
	/// FEATURE-BASED STATE PATTERN (Godot 4.7 Best Practice):
	///
	/// Each game feature (platformer physics, combat, inventory, progression) has its own state class.
	/// The root GameStateData aggregates all features via composition.
	/// Components implement ISaveable to sync their local state with the global GameStateData.
	///
	/// EXAMPLE: Platformer-specific state
	/// This example shows how to extend GameStateData with platformer features,
	/// and how gameplay components implement ISaveable to participate in saves.
	/// </summary>

	/// <summary>
	/// Example: Platformer-specific combat state (extends the base combat state).
	/// Different game types can add their own feature-specific state classes.
	/// </summary>
	public partial class PlatformerCombatStateData : GameBuilder.PlayerCombatStateData
	{
		// Platformer-specific: combo tracking, wall-slide cooldown
		public int ComboCounter { get; set; } = 0;
		public float WallSlideCooldown { get; set; } = 0;
	}

	/// <summary>
	/// Example: Platformer game features state (goes into GameStateData.Features dictionary).
	/// </summary>
	public partial class PlatformerFeatures
	{
		public int CurrentLevelIndex { get; set; } = 0;
		public int CollectedCoins { get; set; } = 0;
		public List<string> UnlockedPowerUps { get; set; } = new();
		public int TotalDeaths { get; set; } = 0;

		public Godot.Collections.Dictionary ToDict() => new()
		{
			{ "current_level", CurrentLevelIndex },
			{ "collected_coins", CollectedCoins },
			{ "unlocked_powerups", GodotConv.ToArray(UnlockedPowerUps) },
			{ "total_deaths", TotalDeaths }
		};

		public static PlatformerFeatures FromDict(Godot.Collections.Dictionary d) => new()
		{
			CurrentLevelIndex = d.TryGetValue("current_level", out var cl) ? (int)cl : 0,
			CollectedCoins = d.TryGetValue("collected_coins", out var cc) ? (int)cc : 0,
			UnlockedPowerUps = new List<string>(d.TryGetValue("unlocked_powerups", out var up) ? up.AsStringArray() : Array.Empty<string>()),
			TotalDeaths = d.TryGetValue("total_deaths", out var td) ? (int)td : 0
		};
	}

	/// <summary>
	/// TEMPLATE: Implement ISaveable on your gameplay components to participate in saves.
	/// This component syncs health state with GameStateData when Save/Load is called.
	///
	/// In your game:
	/// 1. Attach GameStateManagerComponent to the Game/World node
	/// 2. Attach this component (or similar) to your player
	/// 3. Call GameStateManagerComponent.Save(slot) — it auto-discovers this component
	/// 4. This component's Save() method is invoked, syncing state to GameStateData
	/// 5. Call GameStateManagerComponent.Load(slot) — it restores this component
	/// </summary>
	public partial class PlatformerHealthComponent : Beep.ECS.HealthComponent, Beep.ECS.ISaveable
	{
		/// <summary>Platformer-specific: lives are not part of the base HealthComponent.</summary>
		[Export] public int Lives { get; set; } = 3;

		public void Save(GameBuilder.GameStateData state)
		{
			// Sync this component's state TO the global GameStateData
			state.Combat.Health = CurrentHealth;
			state.Combat.MaxHealth = MaxHealth;
			state.Combat.Lives = Lives;
		}

		public void Load(GameBuilder.GameStateData state)
		{
			// Restore this component's state FROM the global GameStateData
			CurrentHealth = state.Combat.Health;
			MaxHealth = state.Combat.MaxHealth;
			Lives = state.Combat.Lives;
		}
	}

	/// <summary>
	/// TEMPLATE: Movement component that implements ISaveable.
	/// </summary>
	public partial class PlatformerMovementComponent : Beep.ECS.EntityComponent, Beep.ECS.ISaveable
	{
		private Node2D? _node2D;

		public override void _Ready()
		{
			base._Ready();
			_node2D = GetParent() as Node2D;
		}

		public void Save(GameBuilder.GameStateData state)
		{
			if (_node2D == null) return;
			state.Movement.PositionX = _node2D.GlobalPosition.X;
			state.Movement.PositionY = _node2D.GlobalPosition.Y;
			state.Movement.Rotation = _node2D.Rotation;
		}

		public void Load(GameBuilder.GameStateData state)
		{
			if (_node2D == null) return;
			_node2D.GlobalPosition = new Vector2(state.Movement.PositionX, state.Movement.PositionY);
			_node2D.Rotation = state.Movement.Rotation;
		}
	}

	/// <summary>
	/// TEMPLATE: Usage example in your game startup.
	/// </summary>
	public partial class GameStartup : Node
	{
		private Beep.ECS.GameStateManagerComponent? _stateManager;

		public override void _Ready()
		{
			_stateManager = GetNode<Beep.ECS.GameStateManagerComponent>("StateManager");
		}

		public void OnNewGamePressed()
		{
			_stateManager?.NewGame("Player1");
			_stateManager?.SyncAllSaveables(); // Optional: capture current state immediately
			_stateManager?.Save(0);
		}

		public void OnLoadGamePressed(int slot)
		{
			_stateManager?.Load(slot);
			_stateManager?.RestoreAllSaveables(); // Auto-discover and restore all ISaveable
		}

		public void OnSaveGamePressed(int slot)
		{
			_stateManager?.SyncAllSaveables(); // Sync all components' state
			_stateManager?.Save(slot); // Then save to disk
		}
	}
}

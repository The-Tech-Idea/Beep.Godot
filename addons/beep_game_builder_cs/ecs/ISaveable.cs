using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
	/// <summary>
	/// Interface for components that participate in save/load via feature-based architecture.
	/// Implement this on any component that needs to be persisted (Movement, Combat, Inventory, etc).
	///
	/// IMPORTANT — implementing this is not enough. A component only participates once it has
	/// joined the <see cref="SaveableHelper.Group"/> group, which the ISaveable components do
	/// in _Ready when their ParticipatesInSave export is on.
	///
	/// Why: GameStateData is deliberately player-centric — Combat, Movement and Inventory are
	/// single slots, not per-entity. Discovery used to walk the whole tree and call Save() on
	/// every ISaveable, so every enemy's HealthComponent wrote the same state.Combat.Health
	/// (last one scanned won) and Load then set *every* entity to that value. Opting in keeps
	/// the one player's components as the only writers, matching the format's shape.
	///
	/// If you later need enemies/chests to persist individually, that's a per-entity key on
	/// this interface plus a reshaped GameStateData — not a wider scan.
	///
	/// Example (Combat Component):
	///   public partial class HealthComponent : GameplayComponent, ISaveable
	///   {
	///       public void Save(GameBuilder.GameStateData state)
	///       {
	///           state.Combat.Health = CurrentHealth;
	///           state.Combat.MaxHealth = MaxHealth;
	///       }
	///
	///       public void Load(GameBuilder.GameStateData state)
	///       {
	///           SetHealth(state.Combat.Health);
	///       }
	///   }
	/// </summary>
	public interface ISaveable
	{
		/// <summary>Sync component state TO the global GameStateData (for saving).</summary>
		void Save(GameBuilder.GameStateData state);

		/// <summary>Restore component state FROM the global GameStateData (after loading).</summary>
		void Load(GameBuilder.GameStateData state);
	}

	/// <summary>
	/// Helper utility for manually managing ISaveable components.
	/// GameStateManagerComponent auto-discovers these, but you can use this for custom logic.
	/// </summary>
	// No [GlobalClass]: this is a plain static utility, not a registrable Godot type.
	public static partial class SaveableHelper
	{
		/// <summary>Components in this group are the ones a save is built from. Joined in
		/// _Ready by ISaveable components whose ParticipatesInSave export is on.</summary>
		public const string Group = "saveables";

		/// <summary>The ISaveable components taking part in saves.
		///
		/// Group membership, not a tree walk: walking the tree collected every enemy's
		/// HealthComponent alongside the player's, and they all write the same single
		/// state.Combat slot. See the note on ISaveable.</summary>
		public static List<ISaveable> FindAllSaveables(Node root)
		{
			var saveables = new List<ISaveable>();
			var tree = root.GetTree();
			if (tree == null) return saveables;

			foreach (var node in tree.GetNodesInGroup(Group))
				if (node is ISaveable saveable)
					saveables.Add(saveable);

			return saveables;
		}

		/// <summary>Participating ISaveables of a specific component type (e.g. HealthComponent).</summary>
		public static List<T> FindSaveablesOfType<T>(Node root) where T : class, ISaveable
		{
			var result = new List<T>();
			foreach (var saveable in FindAllSaveables(root))
				if (saveable is T typed)
					result.Add(typed);
			return result;
		}
	}
}

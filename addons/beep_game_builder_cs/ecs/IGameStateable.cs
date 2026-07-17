using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
	/// <summary>
	/// Interface for components that participate in save/load via feature-based architecture.
	/// Implement this on any component that needs to be persisted (Movement, Combat, Inventory, etc).
	///
	/// Best Practice Pattern (from Godot community):
	/// Each feature (movement, combat, inventory) has its own state class.
	/// Components implement ISaveable to sync their local state with the global GameStateData.
	///
	/// Example (Combat Component):
	///   public partial class HealthComponent : GameplayComponent, ISaveable
	///   {
	///       private GameBuilder.GameStateData _state;
	///
	///       public void Save(GameBuilder.GameStateData state)
	///       {
	///           _state = state;
	///           _state.Combat.Health = CurrentHealth;
	///           _state.Combat.MaxHealth = MaxHealth;
	///       }
	///
	///       public void Load(GameBuilder.GameStateData state)
	///       {
	///           _state = state;
	///           SetHealth(_state.Combat.Health);
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
		/// <summary>Find all ISaveable components in a node tree.</summary>
		public static List<ISaveable> FindAllSaveables(Node root)
		{
			var saveables = new List<ISaveable>();
			Collect(root, saveables);
			return saveables;
		}

		/// <summary>Find all ISaveable of a specific component type (e.g., HealthComponent).</summary>
		public static List<T> FindSaveablesOfType<T>(Node root) where T : class, ISaveable
		{
			var result = new List<T>();
			foreach (var node in root.GetChildren())
			{
				if (node is T typed)
					result.Add(typed);
				result.AddRange(FindSaveablesOfType<T>(node));
			}
			return result;
		}

		private static void Collect(Node node, List<ISaveable> list)
		{
			if (node is ISaveable saveable)
				list.Add(saveable);

			foreach (var child in node.GetChildren())
				Collect(child, list);
		}
	}
}

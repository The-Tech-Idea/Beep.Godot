using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Loads a level's content into a container node at runtime, keyed by
    /// <see cref="GameApp.CurrentLevel"/>. The gameplay main scene holds the pieces that
    /// persist across levels (player, HUD, atmosphere, game flow); each level is a separate
    /// PackedScene (tilemaps, parallax, enemies, items) instanced into the LevelContainer.
    ///
    /// This is what makes level selection meaningful — the level-select screen sets
    /// GameApp.CurrentLevel and loads the gameplay scene; this loader reads that number and
    /// instances the matching level, instead of every level showing the same baked content.
    ///
    /// Attach to the world root (or any node) alongside a LevelContainer child.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class LevelLoaderComponent : WorldComponent
    {
        /// <summary>The per-level scenes, in order. Index 0 corresponds to level
        /// <see cref="FirstLevelIndex"/>.</summary>
        [Export] public Godot.Collections.Array<PackedScene> Levels { get; set; } = new();

        /// <summary>Node the level instance is added under. If unset, the loader's parent is used.</summary>
        [Export] public NodePath? LevelContainerPath { get; set; }

        /// <summary>The level number that maps to <see cref="Levels"/>[0]. Levels are usually
        /// 1-based while GameApp.CurrentLevel starts at -1, so this normalizes the offset.</summary>
        [Export] public int FirstLevelIndex { get; set; } = 1;

        /// <summary>Optional player to reposition to the loaded level's "PlayerSpawn" Marker2D.</summary>
        [Export] public NodePath? PlayerPath { get; set; }

        [Signal] public delegate void LevelLoadedEventHandler(int level);
        [Signal] public delegate void LevelLoadFailedEventHandler(int level, string reason);

        private Node? _container;
        private Node? _currentLevelInstance;

        /// <summary>The level currently loaded (FirstLevelIndex-based), or -1 if none.</summary>
        public int CurrentLevel { get; private set; } = -1;

        public override void _Ready()
        {
            base._Ready();
            // Runtime only: instancing a level into the tree would pollute the scene in-editor.
            if (Engine.IsEditorHint()) return;

            int level = GameApp.Instance?.CurrentLevel ?? FirstLevelIndex;
            if (level < FirstLevelIndex) level = FirstLevelIndex; // e.g. fresh start (CurrentLevel = -1)
            CallDeferred(nameof(LoadLevel), level);
        }

        /// <summary>Load a level by its number (FirstLevelIndex-based). Frees the current level
        /// first, so this doubles as a runtime level transition.</summary>
        public void LoadLevel(int level)
        {
            if (!IsActive) return;

            _container = LevelContainerPath != null ? GetNodeOrNull(LevelContainerPath) : GetParent();
            if (_container == null)
            {
                GD.PushError($"[{Name}] LevelContainer not found (LevelContainerPath={LevelContainerPath}).");
                EmitSignal(SignalName.LevelLoadFailed, level, "container not found");
                return;
            }

            int idx = level - FirstLevelIndex;
            if (idx < 0 || idx >= Levels.Count || Levels[idx] == null)
            {
                GD.PushError($"[{Name}] No level scene for level {level} (index {idx} of {Levels.Count} levels).");
                EmitSignal(SignalName.LevelLoadFailed, level, "no scene for level");
                return;
            }

            if (_currentLevelInstance != null && GodotObject.IsInstanceValid(_currentLevelInstance))
                _currentLevelInstance.QueueFree();

            _currentLevelInstance = Levels[idx].Instantiate();
            _container.AddChild(_currentLevelInstance);
            CurrentLevel = level;

            MovePlayerToSpawn();
            EmitSignal(SignalName.LevelLoaded, level);
        }

        /// <summary>Reposition the player onto the level's "PlayerSpawn" marker, if both exist.</summary>
        private void MovePlayerToSpawn()
        {
            if (PlayerPath == null || _currentLevelInstance == null) return;
            if (GetNodeOrNull(PlayerPath) is not Node2D player) return;
            if (_currentLevelInstance.FindChild("PlayerSpawn", true, false) is Marker2D spawn)
                player.GlobalPosition = spawn.GlobalPosition;
        }
    }
}

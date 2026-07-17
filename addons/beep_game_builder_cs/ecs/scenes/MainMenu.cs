using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class MainMenu : Control
    {
        private UI.SaveLoadManagerComponent? _saveLoadManager;

        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            _saveLoadManager = GetNode<UI.SaveLoadManagerComponent>("SaveLoadManager");

            GetNode<Button>("Center/MenuVBox/NewGameButton").Pressed  += () => ChangeScene(GameApp.Instance?.GameScenePath);

            // Continue resumes the newest save. It used to be byte-identical to New Game,
            // so a player with a save silently started over. Hidden when nothing is saved.
            var continueBtn = GetNode<Button>("Center/MenuVBox/ContinueButton");
            continueBtn.Pressed += OnContinuePressed;
            continueBtn.Visible = NewestSlot() >= 0;
            GetNode<Button>("Center/MenuVBox/SaveGameButton").Pressed += OnSaveGamePressed;
            GetNode<Button>("Center/MenuVBox/LoadGameButton").Pressed += OnLoadGamePressed;
            GetNode<Button>("Center/MenuVBox/SettingsButton").Pressed += () => ChangeScene(GameApp.Instance?.SettingsScenePath);
            GetNode<Button>("Center/MenuVBox/QuitButton").Pressed     += () => GetTree().Quit();
        }

        private void OnSaveGamePressed()
        {
            _saveLoadManager?.ShowSaveMenu();
        }

        private void OnLoadGamePressed()
        {
            _saveLoadManager?.ShowLoadMenu();
        }

        /// <summary>Slot of the most recent save, or -1 when there are none.
        /// GameStateManager is an autoload, so it is reachable from the menu even though
        /// no game scene is loaded.</summary>
        private static int NewestSlot()
        {
            var manager = GameStateManagerComponent.Instance;
            if (manager == null) return -1;

            int best = -1;
            long newest = long.MinValue;
            foreach (var (slot, metadata) in manager.GetSaveSlots())
            {
                if (metadata.Timestamp <= newest) continue;
                newest = metadata.Timestamp;
                best = slot;
            }
            return best;
        }

        private void OnContinuePressed()
        {
            var manager = GameStateManagerComponent.Instance;
            int slot = NewestSlot();
            if (manager == null || slot < 0)
            {
                GD.PushError($"[{Name}] Continue pressed but there is no save to load.");
                return;
            }

            if (!manager.Load(slot))
            {
                GD.PushError($"[{Name}] Failed to load save slot {slot}.");
                return;
            }

            manager.RestoreAllSaveables();
            ChangeScene(GameApp.Instance?.GameScenePath);
        }

        /// <summary>Navigate to a scene. Reports why it failed instead of doing nothing —
        /// a missing/unset target used to make the button appear dead.</summary>
        private void ChangeScene(string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                GD.PushError($"[{Name}] Navigation target is not set (check GameInfo scene paths).");
                return;
            }
            if (!ResourceLoader.Exists(path))
            {
                GD.PushError($"[{Name}] Navigation target does not exist: {path}");
                return;
            }
            Error err = GetTree().ChangeSceneToFile(path);
            if (err != Error.Ok)
                GD.PushError($"[{Name}] Failed to change scene to {path}: {err}");
        }
    }
}

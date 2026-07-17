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

            // Not the throwing GetNode<T>: the ?. guards at every use site are unreachable if
            // this throws, and because it runs before the wiring below, a menu missing this
            // node lost *every* button — New Game, Settings, Quit — not just save/load.
            _saveLoadManager = GetNodeOrNull<UI.SaveLoadManagerComponent>("SaveLoadManager");

            GetNode<Button>("Center/MenuVBox/NewGameButton").Pressed  += () => ChangeScene(GameApp.Instance?.GameScenePath);

            // Continue resumes the newest save. It used to be byte-identical to New Game,
            // so a player with a save silently started over. Hidden when nothing is saved.
            var continueBtn = GetNode<Button>("Center/MenuVBox/ContinueButton");
            continueBtn.Pressed += OnContinuePressed;
            continueBtn.Visible = NewestSlot() != null;

            // Saving needs a running game to capture, so from the main menu the save dialog's
            // button is permanently disabled. Hide the entry rather than ship a dead menu —
            // same treatment as Continue above.
            var saveBtn = GetNode<Button>("Center/MenuVBox/SaveGameButton");
            saveBtn.Pressed += OnSaveGamePressed;
            saveBtn.Visible = false;
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

        /// <summary>Slot of the most recent save, or null when there are none. Nullable rather
        /// than a -1 sentinel because -1 is the autosave slot — and the autosave is included
        /// here, since the in-game Save button and the autosave timer both write only there.
        /// GameStateManager is an autoload, so it is reachable from the menu even though
        /// no game scene is loaded.</summary>
        private static int? NewestSlot()
        {
            var manager = GameStateManagerComponent.Instance;
            if (manager == null) return null;

            int? best = null;
            long newest = long.MinValue;
            foreach (var (slot, metadata) in manager.GetSaveSlots(includeAutosave: true))
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
            int? slot = NewestSlot();
            if (manager == null || slot == null)
            {
                GD.PushError($"[{Name}] Continue pressed but there is no save to load.");
                return;
            }

            // Queue the restore; GameFlowComponent applies it once the gameplay scene exists.
            // Restoring here pushed the save into main_menu.tscn — which has no player, no
            // health, no inventory — and then freed it, so Continue silently started fresh.
            if (!manager.LoadForSceneChange(slot.Value))
            {
                GD.PushError($"[{Name}] Failed to load save slot {slot.Value}.");
                return;
            }

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

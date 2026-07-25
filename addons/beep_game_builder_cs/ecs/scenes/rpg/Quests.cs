using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class Quests : Control
    {
        /// <summary>GameData key holding the selected quest's node name ("Quest1"…). Quest
        /// content — objectives, progress, rewards — is the game's; this screen records which
        /// entry the player picked and shows its title, the same choice-record pattern as
        /// BuildMenu / Research / Crafting.</summary>
        public const string SelectionKey = "quest_selection";

        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            this.ConnectButton("CloseButton", () => UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.GameScenePath));

            // Every entry in the list is a real button, so every entry must do something —
            // an unwired button in this repo is indistinguishable from a broken one.
            WireQuest("Quest1");
            WireQuest("Quest2");
            WireQuest("Quest3");
        }

        private void WireQuest(string buttonName)
        {
            if (this.Find<Button>(buttonName) is not { } btn) return;
            btn.Pressed += () =>
            {
                GameStateManagerComponent.Instance?.SetGameData(SelectionKey, buttonName);
                if (this.Find<Label>("QuestTitle") is { } title) title.Text = btn.Text;
            };
        }
    }
}

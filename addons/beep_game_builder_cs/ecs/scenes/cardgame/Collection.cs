using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class Collection : Control
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            // CloseOrReturn, not ChangeScene: Collection is opened as an overlay over the running
            // cardgame (ScreenKey="collection"), so Back must free the overlay and reveal the game
            // — ChangeScene(MainMenu) tore the live run down. Matches every sibling overlay screen.
            this.ConnectPressed("Margin/VBox/Header/BackButton", () => UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.MainMenuPath));
        }
    }
}

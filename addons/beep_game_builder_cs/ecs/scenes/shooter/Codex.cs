using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class Codex : Control
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            // Codex opens as an overlay over the running game (GenreScreenComponent, "codex"
            // action), so Back must close the overlay, not ChangeScene to the menu and tear the
            // run down. CloseOrReturn handles the current-scene case too.
            this.ConnectPressed("Margin/VBox/Header/BackButton", () => UI.SceneNav.CloseOrReturn(this, GameApp.Instance?.MainMenuPath));
        }
    }
}

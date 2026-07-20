using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Sets a themed custom mouse cursor for the scene it lives in. Opt-in — drop it on a HUD or
    /// scene root and assign the cursor textures (Kenney cursor art ships under
    /// <c>res://addons/beep_game_builder_cs/textures/cursors/</c>, CC0).
    ///
    /// Resets to the system cursors on <c>_ExitTree</c> so the custom cursor does NOT bleed into
    /// other scenes — a custom mouse cursor is a global on the Input singleton, so it must be
    /// restored when this scene leaves (same lifecycle discipline as the HUD subscription leaks).
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class CursorComponent : UIComponent
    {
        /// <summary>The default arrow cursor. Leave null to keep the system arrow.</summary>
        [Export] public Texture2D? ArrowCursor { get; set; }
        /// <summary>Cursor shown over clickable Controls (buttons/links). Leave null to keep the system hand.</summary>
        [Export] public Texture2D? PointingHandCursor { get; set; }
        /// <summary>Click hot-spot within the texture (pixels from top-left).</summary>
        [Export] public Vector2 Hotspot { get; set; } = Vector2.Zero;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            if (ArrowCursor != null)
                Input.SetCustomMouseCursor(ArrowCursor, Input.CursorShape.Arrow, Hotspot);
            if (PointingHandCursor != null)
                Input.SetCustomMouseCursor(PointingHandCursor, Input.CursorShape.PointingHand, Hotspot);
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (Engine.IsEditorHint()) return;
            // Restore the system cursors so the custom one doesn't persist into menus/other scenes.
            Input.SetCustomMouseCursor(null, Input.CursorShape.Arrow);
            Input.SetCustomMouseCursor(null, Input.CursorShape.PointingHand);
        }
    }
}

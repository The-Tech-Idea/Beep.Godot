using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A flat tint over whatever it covers: the plate a screen fade, a damage flash or a click
    /// ripple animates.
    ///
    /// Deliberately the thinnest widget in the kit. It takes no genre, no silhouette and no
    /// material, because a fade that picked up a genre's shear or corner would stop being a fade.
    /// The colour is animated by whoever owns it — <see cref="SceneTransitionComponent"/>,
    /// <see cref="ScreenFlashComponent"/>, <see cref="RippleComponent"/> — and this only draws it.
    ///
    /// Ignores the mouse, so an overlay left at zero alpha cannot quietly eat the input of every
    /// control beneath it.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitColorOverlay : Control
    {
        [Export]
        public bool AutoInputDefaults
        {
            get => _autoInputDefaults;
            set { if (_autoInputDefaults == value) return; _autoInputDefaults = value; }
        }
        private bool _autoInputDefaults = true;

        [Export]
        public Color Color
        {
            get => _color;
            set
            {
                if (_color == value) return;
                _color = value;
                RefreshVisualAndRedraw();
            }
        }

        private Color _color = new(0, 0, 0, 0);

        private void RefreshVisualAndRedraw()
        {
            QueueRedraw();
        }

        public override void _Ready()
        {
            base._Ready();
            KitChrome.ApplyInputDefaults(this, AutoInputDefaults, MouseFilterEnum.Ignore);
        }

        public override void _Draw()
        {
            if (Size.X <= 0f || Size.Y <= 0f || _color.A <= 0f) return;
            DrawRect(new Rect2(Vector2.Zero, Size), _color);
        }
    }
}

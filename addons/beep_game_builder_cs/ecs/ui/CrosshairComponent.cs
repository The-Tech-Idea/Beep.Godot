using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Center-screen crosshair. Draws via _Draw on the parent Control (which should
    /// fill the screen). Spread grows when the parent moves/fires and recovers over
    /// time. Auto-hides when a menu opens (detected via the tree's paused state).
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class CrosshairComponent : UIComponent
    {
        public enum CrosshairStyle { Cross, Dot, Circle, CrossDot }

        [Export] public CrosshairStyle Style { get; set; } = CrosshairStyle.Cross;
        [Export] public Color Color { get; set; } = new(1, 1, 1, 0.9f);
        [Export] public float Size { get; set; } = 10f;
        [Export] public float Thickness { get; set; } = 2f;
        [Export] public float MinSpread { get; set; } = 2f;
        [Export] public float MaxSpread { get; set; } = 16f;
        [Export] public float SpreadRecoverSpeed { get; set; } = 6f;
        /// <summary>Current dynamic spread (px from center). Increase on fire/move; it recovers automatically.</summary>
        [Export] public float CurrentSpread { get; set; } = 2f;
        [Export] public bool HideWhenPaused { get; set; } = true;

        private Godot.Control? _canvas;
        private bool _drawerConnected;

        public override void _Ready()
        {
            base._Ready();
            _canvas = GetParent() as Godot.Control;
            ConnectDrawer();
        }

        private void ConnectDrawer()
        {
            if (_canvas == null || _drawerConnected) return;
            _canvas.Draw += DrawCrosshair;
            _drawerConnected = true;
        }

        public override void _Process(double delta)
        {
            if (!IsActive) return;
            // Recover spread toward minimum.
            CurrentSpread = Mathf.MoveToward(CurrentSpread, MinSpread, SpreadRecoverSpeed * (float)delta);
            _canvas?.QueueRedraw();
        }

        /// <summary>Kick the spread (call on fire or fast movement).</summary>
        public void AddSpread(float amount) { if (IsActive) CurrentSpread = Mathf.Min(MaxSpread, CurrentSpread + amount); }

        private void DrawCrosshair()
        {
            if (_canvas == null) return;
            if (HideWhenPaused && GetTree().Paused) return;

            Vector2 center = _canvas.Size * 0.5f;
            float s = CurrentSpread;

            switch (Style)
            {
                case CrosshairStyle.Cross or CrosshairStyle.CrossDot:
                    DrawLine(center + new Vector2(-s - Size, 0), center + new Vector2(-s, 0));
                    DrawLine(center + new Vector2(s, 0), center + new Vector2(s + Size, 0));
                    DrawLine(center + new Vector2(0, -s - Size), center + new Vector2(0, -s));
                    DrawLine(center + new Vector2(0, s), center + new Vector2(0, s + Size));
                    break;
                case CrosshairStyle.Circle:
                    _canvas.DrawArc(center, s + Size * 0.5f, 0, Mathf.Tau, 24, Color, Thickness, true);
                    break;
            }
            if (Style is CrosshairStyle.Dot or CrosshairStyle.CrossDot)
                _canvas.DrawCircle(center, Thickness * 0.5f, Color);

            void DrawLine(Vector2 a, Vector2 b) => _canvas.DrawLine(a, b, Color, Thickness);
        }
    }
}

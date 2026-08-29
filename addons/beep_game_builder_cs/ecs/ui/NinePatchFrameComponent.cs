using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Disables legacy 9-patch frame chrome on a parent Control.
    ///
    /// WHEN TO USE THIS — and when not to:
    /// This is not part of the UI kit skin pipeline. The kit uses procedural theme colors,
    /// geometry, and style tokens. The previous texture override path made kit screens drift
    /// back into atlas-backed chrome, so this component now only suppresses old authored frames.
    ///
    /// Drop it under the Control you want cleaned and point FramePath at a scene-authored
    /// NinePatchRect. If FramePath is empty, a child named BeepFrame is used by convention.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class NinePatchFrameComponent : UIComponent
    {
        /// <summary>Tint applied over the frame.</summary>
        [Export] public Color Modulate { get; set; } = new(1, 1, 1, 1);

        /// <summary>Authored NinePatchRect to style. Empty uses a sibling/child named BeepFrame.</summary>
        [Export] public NodePath FramePath { get; set; } = new("");

        /// <summary>Refresh authored frame styling while viewing the scene in the editor.</summary>
        [Export] public bool BuildInEditor { get; set; } = true;

        private NinePatchRect? _rect;

        public override void _Ready()
        {
            base._Ready();
            if (!Engine.IsEditorHint() || BuildInEditor)
                Apply();
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            _rect = null;
            base._ExitTree();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (FindFrame() == null)
                return new[] { "NinePatchFrameComponent is a legacy cleanup helper. Set FramePath to an authored NinePatchRect or add a BeepFrame child if an old scene still contains one to suppress." };
            return System.Array.Empty<string>();
        }

        /// <summary>Suppress the legacy frame. Public and idempotent.</summary>
        public void Apply()
        {
            if (GetParent() is not Godot.Control)
            {
                if (!Engine.IsEditorHint())
                    GD.PushWarning($"[{Name}] NinePatchFrameComponent's parent is {GetParent()?.GetType().Name ?? "null"}, not a Control - no legacy frame is suppressed.");
                return;
            }

            _rect = FindFrame();
            if (_rect == null || !GodotObject.IsInstanceValid(_rect))
                return;

            _rect.Texture = null;
            _rect.Visible = false;
            _rect.MouseFilter = Control.MouseFilterEnum.Ignore;
            _rect.SelfModulate = Modulate;
        }

        private NinePatchRect? FindFrame()
        {
            if (!FramePath.IsEmpty && GetNodeOrNull<NinePatchRect>(FramePath) is { } pathFrame)
                return pathFrame;

            if (FindChild("BeepFrame", recursive: true, owned: false) is NinePatchRect childFrame)
                return childFrame;

            return GetParent()?.FindChild("BeepFrame", recursive: true, owned: false) as NinePatchRect;
        }

        public bool UsesSceneControls()
            => FindFrame() != null;
    }
}

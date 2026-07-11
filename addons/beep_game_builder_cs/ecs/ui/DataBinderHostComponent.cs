using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Node host for the static <c>BeepDataBinder</c>. Place in a scene to give the
    /// data-binder a _Process tick so it auto-refreshes one-way bindings every
    /// ~0.1s. Without a host, you must call BeepDataBinder.RefreshAll() manually.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class DataBinderHostComponent : UIComponent
    {
        [Export] public bool AutoRefresh { get; set; } = true;

        public override void _Process(double delta)
        {
            if (!IsActive || !AutoRefresh || Engine.IsEditorHint()) return;
            Beep.GameBuilder.BeepDataBinder.RefreshAll(delta);
        }
    }
}

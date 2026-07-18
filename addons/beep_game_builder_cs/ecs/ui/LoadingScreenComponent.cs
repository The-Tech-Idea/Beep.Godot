using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Async loading screen with a progress bar. Place on a Control/CanvasLayer that
    /// covers the screen. Call LoadScene(path) to start loading a scene in the
    /// background — the progress bar fills, then the scene swaps when ready.
    ///
    /// Pairs with NavigationComponent: connect NavigationComponent.BeforeNavigate →
    /// a method that shows this loading screen and calls LoadScene(targetPath).
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class LoadingScreenComponent : UIComponent
    {
        [Export] public NodePath ProgressBarPath { get; set; } = new("ProgressBar");
        [Export] public NodePath LabelPath { get; set; } = new("LoadingLabel");
        [Export] public string LoadingText { get; set; } = "Loading…";
        [Export] public float MinDisplayTime { get; set; } = 0.5f;

        [Signal] public delegate void LoadCompleteEventHandler();

        private ProgressBar? _bar;
        private Label? _label;
        private string? _pendingPath;
        private double _minTimer;

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(Init));
        }

        private void Init()
        {
            if (Engine.IsEditorHint()) return;
            if (GetParent() is not Node parent) return;
            _bar = parent.GetNodeOrNull<ProgressBar>(ProgressBarPath);
            _label = parent.GetNodeOrNull<Label>(LabelPath);
            if (_bar == null)
                GD.PushWarning($"[{Name}] LoadingScreenComponent found no ProgressBar at '{ProgressBarPath}' under its parent — the progress bar won't fill.");
            if (_bar != null) _bar.Value = 0;
            if (_label != null) _label.Text = LoadingText;
            Hide();
        }

        // Show/Hide toggle the parent's visibility. Handle both a Control parent and a
        // CanvasLayer parent (the doc invites either) — CanvasLayer also has Visible. Doing
        // only Control meant a CanvasLayer-hosted loading screen never showed or hid.
        private void Hide() => SetParentVisible(false);
        private void Show() => SetParentVisible(true);

        private void SetParentVisible(bool visible)
        {
            switch (GetParent())
            {
                case Godot.Control c: c.Visible = visible; break;
                case CanvasLayer cl: cl.Visible = visible; break;
                default:
                    GD.PushWarning($"[{Name}] LoadingScreenComponent's parent is '{GetParent()?.GetType().Name ?? "null"}' — it can't be shown/hidden. Parent it to a Control or CanvasLayer.");
                    break;
            }
        }

        /// <summary>Begin loading a scene asynchronously. Shows the loading screen,
        /// fills the progress bar, then changes the scene when ready.</summary>
        public void LoadScene(string path)
        {
            if (!IsActive || string.IsNullOrEmpty(path)) return;
            _pendingPath = path;
            _minTimer = MinDisplayTime;
            // Start threaded background loading.
            ResourceLoader.LoadThreadedRequest(path);
            if (_bar != null) _bar.Value = 0;
            if (_label != null) _label.Text = LoadingText;
            Show();
        }

        public override void _Process(double delta)
        {
            if (!IsActive || _pendingPath == null) return;
            _minTimer -= delta;

            // Poll the threaded loader status.
            var progress = new Godot.Collections.Array();
            var status = ResourceLoader.LoadThreadedGetStatus(_pendingPath, progress);
            if (_bar != null && progress.Count > 0)
                _bar.Value = progress[0].AsSingle() * 100f;

            if (status is ResourceLoader.ThreadLoadStatus.Failed or ResourceLoader.ThreadLoadStatus.InvalidResource)
            {
                // Don't sit on the loading screen forever with nothing logged (the old code only
                // handled Loaded). Report and give up.
                GD.PushError($"[{Name}] Threaded load of '{_pendingPath}' failed ({status}).");
                _pendingPath = null;
                Hide();
                return;
            }

            if (status == ResourceLoader.ThreadLoadStatus.Loaded && _minTimer <= 0)
            {
                // Loading complete + minimum display time elapsed → swap scene.
                if (ResourceLoader.LoadThreadedGet(_pendingPath) is not PackedScene packed)
                {
                    GD.PushError($"[{Name}] '{_pendingPath}' loaded but is not a PackedScene.");
                    _pendingPath = null;
                    Hide();
                    return;
                }
                EmitSignal(SignalName.LoadComplete);
                _pendingPath = null;
                GetTree().ChangeSceneToPacked(packed);
            }
        }
    }
}

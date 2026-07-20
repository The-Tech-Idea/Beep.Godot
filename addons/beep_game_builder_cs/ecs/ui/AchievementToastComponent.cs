using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Shows a toast when an achievement unlocks. Drop it on a HUD / CanvasLayer that also holds a
    /// <see cref="ToastNotificationComponent"/> (which owns the actual toast rendering).
    ///
    /// Bridges the two achievement sources that otherwise fired with no listener:
    ///  • <c>GameApp.AchievementUnlocked</c> — the autoload signal (id string), and
    ///  • <c>BeepAchievementSystem.AchievementUnlocked</c> — the static util/debug Action.
    /// Optional — a game that renders its own achievement popup simply doesn't add this.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class AchievementToastComponent : UIComponent
    {
        [Export] public bool ListenToGameApp { get; set; } = true;
        [Export] public bool ListenToAchievementSystem { get; set; } = true;

        private GameApp? _app;
        private System.Action<Beep.GameBuilder.BeepAchievementSystem.Achievement>? _systemHandler;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;

            if (ListenToGameApp)
            {
                if (GameApp.Instance is { } app)
                {
                    _app = app;
                    _app.AchievementUnlocked += OnGameAppAchievement;
                }
                else
                {
                    GD.PushWarning($"[{Name}] AchievementToastComponent: ListenToGameApp is on but no GameApp autoload was found at _Ready — GameApp achievements won't toast. Enable the GameApp autoload, or turn ListenToGameApp off.");
                }
            }
            if (ListenToAchievementSystem)
            {
                _systemHandler = OnSystemAchievement;
                Beep.GameBuilder.BeepAchievementSystem.AchievementUnlocked += _systemHandler;
            }
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            // GameApp (autoload) and the static Action both outlive this node — detach both, or the
            // static Action holds a freed component forever (and fires callbacks on it).
            if (_app != null && GodotObject.IsInstanceValid(_app))
                _app.AchievementUnlocked -= OnGameAppAchievement;
            if (_systemHandler != null)
                Beep.GameBuilder.BeepAchievementSystem.AchievementUnlocked -= _systemHandler;
            _systemHandler = null;
        }

        private void OnGameAppAchievement(string achievementId)
        {
            // Prefer the registered title; fall back to the raw id when it isn't registered.
            string title = Beep.GameBuilder.BeepAchievementSystem.Get(achievementId)?.Title ?? achievementId;
            ShowToast(title);
        }

        private void OnSystemAchievement(Beep.GameBuilder.BeepAchievementSystem.Achievement ach)
            => ShowToast(ach?.Title ?? "Achievement");

        // Dedupe: a game that records one unlock in BOTH GameApp and BeepAchievementSystem would
        // otherwise fire two toasts. Suppress a repeat of the same title within a short window.
        private string _lastTitle = "";
        private ulong _lastToastMs;
        private const ulong DedupeWindowMs = 750;

        private void ShowToast(string title)
        {
            ulong now = Time.GetTicksMsec();
            if (title == _lastTitle && now - _lastToastMs < DedupeWindowMs) return;
            _lastTitle = title;
            _lastToastMs = now;
            ToastNotificationComponent.Show($"🏆 Achievement Unlocked: {title}", ToastNotificationComponent.ToastType.Success);
        }
    }
}

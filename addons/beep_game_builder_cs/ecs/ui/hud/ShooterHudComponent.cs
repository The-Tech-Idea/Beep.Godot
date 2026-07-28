using Godot;

namespace Beep.ECS.UI
{
    /// <summary>Shooter HUD: Score/Level/Lives/Health bind live; Ammo and Wave come from
    /// <see cref="ShooterCombatComponent"/>.
    ///
    /// Both were previously registered as <c>Placeholder(...)</c>, so they showed whatever text
    /// was typed into the scene and never moved. Placeholder is now the FALLBACK for a scene with
    /// no combat component, not the normal path.</summary>
    [Tool]
    [GlobalClass]
    public partial class ShooterHudComponent : GenreHudComponent
    {
        [Export] public NodePath ScorePath { get; set; } = "TopLeft/StatsVBox/ScoreLabel";
        [Export] public NodePath LevelPath { get; set; } = "TopLeft/StatsVBox/LevelLabel";
        [Export] public NodePath LivesPath { get; set; } = "TopLeft/StatsVBox/LivesLabel";
        [Export] public NodePath HealthPath { get; set; } = "TopLeft/StatsVBox/HealthLabel";
        [Export] public NodePath AmmoPath { get; set; } = "BottomRight/AmmoLabel";
        [Export] public NodePath WavePath { get; set; } = "BottomRight/WaveLabel";

        /// <summary>Optional toast host for wave-cleared alerts.</summary>
        [Export] public NodePath AlertHostPath { get; set; } = new("");

        protected override string Genre => "shooter";

        private ShooterCombatComponent? _combat;
        private ToastNotificationComponent? _alerts;
        private Godot.Control? _ammo, _wave;

        protected override void Wire()
        {
            BindScore(ScorePath);
            BindLevel(LevelPath);
            BindLives(LivesPath);
            BindHealth(HealthPath);

            _combat = FindInScene<ShooterCombatComponent>();
            if (_combat == null)
            {
                // No simulation in this scene: fall back to developer-driven readouts and say so
                // once. This is the only path that should ever warn.
                Placeholder(AmmoPath, "ammo");
                Placeholder(WavePath, "wave");
                return;
            }

            _ammo = ResolveReadout(AmmoPath, "ammo");
            _wave = ResolveReadout(WavePath, "wave");
            _alerts = ResolveNode<ToastNotificationComponent>(AlertHostPath);

            _combat.AmmoChanged += OnAmmo;
            _combat.WaveChanged += OnWave;
            _combat.WaveCleared += OnWaveCleared;
            OnAmmo();
            OnWave(_combat.Wave);
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (_combat != null && GodotObject.IsInstanceValid(_combat))
            {
                _combat.AmmoChanged -= OnAmmo;
                _combat.WaveChanged -= OnWave;
                _combat.WaveCleared -= OnWaveCleared;
            }
            _combat = null;
        }

        private void OnAmmo()
        {
            if (_combat == null) return;

            // While reloading the readout shows the reload's own progress as its fill, so the
            // player reads "how long until I can shoot" from the same place as "how many left".
            string text = _combat.IsReloading ? "RELOADING"
                                              : $"{_combat.Magazine} / {_combat.Reserve}";
            float fill = _combat.IsReloading ? _combat.ReloadProgress : _combat.MagazineFraction;
            SetReadout(_ammo, text, fill);

            Tint(_ammo, _combat.IsOutOfAmmo ? UiSurface.Role.Danger
                 : _combat.IsReloading ? UiSurface.Role.Info
                 : _combat.MagazineFraction <= _combat.LowThreshold ? UiSurface.Role.Warning
                 : null);

            // The wave readout shares this refresh, because a kill changes both.
            ShowWave();
        }

        private void OnWave(int wave) => ShowWave();

        private void ShowWave()
        {
            if (_combat == null || _wave == null) return;
            SetReadout(_wave, $"Wave {_combat.Wave}  ({_combat.EnemiesRemaining} left)",
                       _combat.WaveFraction);
        }

        private void OnWaveCleared(int wave)
            => _alerts?.ShowToast($"Wave {wave} cleared", ToastNotificationComponent.ToastType.Success);
    }
}

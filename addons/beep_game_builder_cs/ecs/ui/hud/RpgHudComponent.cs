using Godot;

namespace Beep.ECS.UI
{
    /// <summary>RPG HUD: Level, Health, Mana and the tracked Quest.
    ///
    /// Driven by <see cref="RpgPartyComponent"/>. Health, Mana and Quest were previously
    /// registered as <c>Placeholder(...)</c>, so those three readouts kept whatever text was
    /// typed into the scene and never moved. Placeholder is now the FALLBACK for a scene with no
    /// party component, not the normal path.
    ///
    /// Level still falls back to <c>BindLevel</c> (GameApp) when there is no party component, so
    /// a project that only tracks a level in save data keeps working.</summary>
    [Tool]
    [GlobalClass]
    public partial class RpgHudComponent : GenreHudComponent
    {
        [Export] public NodePath LevelPath { get; set; } = "TopLeft/StatsVBox/LevelLabel";
        [Export] public NodePath HealthPath { get; set; } = "TopLeft/StatsVBox/HealthLabel";
        [Export] public NodePath ManaPath { get; set; } = "TopLeft/StatsVBox/ManaLabel";
        [Export] public NodePath QuestPath { get; set; } = "QuestBox/QuestLabel";

        /// <summary>Optional toast host for level-ups and death.</summary>
        [Export] public NodePath AlertHostPath { get; set; } = new("");

        protected override string Genre => "rpg";

        private RpgPartyComponent? _party;
        private ToastNotificationComponent? _alerts;
        private Godot.Control? _level, _health, _mana, _quest;

        protected override void Wire()
        {
            _party = FindInScene<RpgPartyComponent>();

            if (_party == null)
            {
                // No simulation in this scene: keep the GameApp-bound level and fall back to
                // developer-driven readouts. This is the only path that should ever warn.
                BindLevel(LevelPath);
                Placeholder(HealthPath, "health");
                Placeholder(ManaPath, "mana");
                Placeholder(QuestPath, "quest");
                return;
            }

            _level = ResolveReadout(LevelPath, "level");
            _health = ResolveReadout(HealthPath, "health");
            _mana = ResolveReadout(ManaPath, "mana");
            _quest = ResolveReadout(QuestPath, "quest");
            _alerts = ResolveNode<ToastNotificationComponent>(AlertHostPath);

            _party.StatsChanged += OnStats;
            _party.QuestChanged += OnQuest;
            _party.LeveledUp += OnLevelUp;
            _party.Died += OnDied;
            OnStats();
            OnQuest();
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (_party != null && GodotObject.IsInstanceValid(_party))
            {
                _party.StatsChanged -= OnStats;
                _party.QuestChanged -= OnQuest;
                _party.LeveledUp -= OnLevelUp;
                _party.Died -= OnDied;
            }
            _party = null;
        }


        private void OnStats()
        {
            if (_party == null) return;

            // Level shows progress toward the NEXT level as its fill — a bare level number says
            // nothing about how close the next one is.
            SetReadout(_level, $"Lv {_party.Level}", _party.XpFraction);

            SetReadout(_health, $"{_party.Health} / {_party.MaxHealth}", _party.HealthFraction);
            Tint(_health, _party.IsDead ? UiSurface.Role.Danger
                 : _party.HealthFraction <= _party.LowThreshold ? UiSurface.Role.Warning
                 : null);

            SetReadout(_mana, $"{_party.Mana} / {_party.MaxMana}", _party.ManaFraction);
        }

        private void OnQuest()
        {
            if (_party == null) return;
            var q = _party.ActiveQuest;
            SetReadout(_quest, q == null ? "No active quest" : q.ToString());
            Tint(_quest, q is { IsComplete: true } ? UiSurface.Role.Success : null);
        }

        private void OnLevelUp(int level)
            => _alerts?.ShowToast($"Level {level}", ToastNotificationComponent.ToastType.Success);

        private void OnDied()
            => _alerts?.ShowToast("You have fallen", ToastNotificationComponent.ToastType.Error);
    }
}

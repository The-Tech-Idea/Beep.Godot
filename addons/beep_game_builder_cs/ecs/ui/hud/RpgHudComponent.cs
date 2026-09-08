using Godot;

namespace Beep.ECS.UI
{
    /// <summary>RPG stats and quest readouts. An explicit player path follows possession;
    /// otherwise the scene-level RPG state drives an actorless genre HUD.</summary>
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
        /// <summary>When assigned, bind only the possessed actor's stats. Relative to the HUD host.</summary>
        [Export] public NodePath PlayerPath { get; set; } = new("");

        protected override string Genre => "rpg";

        private RpgPartyComponent? _party;
        private PlayerContextComponent? _player;
        private ToastNotificationComponent? _alerts;
        private Godot.Control? _level, _health, _mana, _quest;

        protected override void Wire()
        {
            DisconnectParty();
            if (GodotObject.IsInstanceValid(_player)) _player!.PossessionChanged -= OnPossession;
            _player = null;
            if (!PlayerPath.IsEmpty)
            {
                _level = ResolveReadout(LevelPath, "level");
                _health = ResolveReadout(HealthPath, "health");
                _mana = ResolveReadout(ManaPath, "mana");
                _quest = ResolveReadout(QuestPath, "quest");
                _alerts = ResolveNode<ToastNotificationComponent>(AlertHostPath);
                _player = ResolveNode<PlayerContextComponent>(PlayerPath);
                if (_player is not null) _player.PossessionChanged += OnPossession;
                OnPossession(_player?.PossessedActorId ?? "");
                return;
            }
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

            ConnectParty();
        }

        private void ConnectParty()
        {
            if (_party is null) return;
            _party.StatsChanged += OnStats;
            _party.QuestChanged += OnQuest;
            _party.LeveledUp += OnLevelUp;
            _party.Died += OnDied;
            OnStats();
            OnQuest();
        }

        private void OnPossession(string actorId)
        {
            DisconnectParty();
            if (_player?.Registry?.FindActor(actorId)?.Body is { } body)
                foreach (Node child in body.GetChildren())
                    if (child is RpgPartyComponent stats) { _party = stats; break; }
            if (_party is not null) { ConnectParty(); return; }
            foreach (var control in new[] { _level, _health, _mana, _quest })
            {
                SetReadout(control, "", 0);
                if (control is not null) control.TooltipText = "";
                Tint(control, null);
            }
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (GodotObject.IsInstanceValid(_player)) _player!.PossessionChanged -= OnPossession;
            _player = null;
            DisconnectParty();
            RequestReady();
        }

        private void DisconnectParty()
        {
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
            SetReadout(_level, _party.Level.ToString(), _party.XpFraction);

            SetReadout(_health, "HP", _party.HealthFraction);
            if (_health != null) _health.TooltipText = $"HP {_party.Health} / {_party.MaxHealth}";
            Tint(_health, _party.IsDead ? UiSurface.Role.Danger
                 : _party.HealthFraction <= _party.LowThreshold ? UiSurface.Role.Warning
                 : null);

            SetReadout(_mana, "MP", _party.ManaFraction);
            if (_mana != null) _mana.TooltipText = $"MP {_party.Mana} / {_party.MaxMana}";
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

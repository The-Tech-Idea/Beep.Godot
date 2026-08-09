using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Adaptive (rubber-band) difficulty. Watches the player's performance and eases a 0–1
    /// <see cref="Difficulty"/> scalar that other systems read to soften or stiffen the challenge
    /// — spawners shorten their interval, drop tables raise their multiplier, hazards tick faster.
    ///
    /// The signal it reads is deliberately simple and genre-agnostic: the player's
    /// <see cref="HealthComponent"/> HP fraction and death count. Doing well (high HP, no recent
    /// deaths) pushes difficulty up; struggling (low HP, repeated deaths) pulls it down.
    ///
    /// Consumers multiply their base value by <see cref="GetSpawnIntervalScale"/> /
    /// <see cref="GetDropMultiplier"/> (or read <see cref="Difficulty"/> directly). It changes
    /// nothing on its own — it is a sensor + signal, not an actor.
    ///
    /// In the Add Node tree: EntityComponent → WorldComponent → AdaptiveDifficultyComponent
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class AdaptiveDifficultyComponent : WorldComponent
    {
        [ExportGroup("Tracking")]
        /// <summary>Group the player body is in. Its HealthComponent is the performance signal.</summary>
        [Export] public string PlayerGroup { get; set; } = "players";

        [ExportGroup("Difficulty Curve")]
        /// <summary>Starting and resting difficulty (0 = easiest, 1 = hardest).</summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float BaseDifficulty { get; set; } = 0.5f;
        /// <summary>How fast difficulty drifts toward its computed target, per second.</summary>
        [Export] public float AdaptSpeed { get; set; } = 0.2f;
        /// <summary>HP fraction below which the player counts as "struggling" (eases difficulty down).</summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float StruggleHealthThreshold { get; set; } = 0.35f;
        /// <summary>Each recent death subtracts this much difficulty (decays over DeathMemorySeconds).</summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float DeathPenalty { get; set; } = 0.15f;
        /// <summary>Seconds a death "counts" against difficulty before being forgotten.</summary>
        [Export] public float DeathMemorySeconds { get; set; } = 60f;

        [Signal] public delegate void DifficultyChangedEventHandler(float difficulty);

        /// <summary>Current difficulty, 0–1. Read by spawners, drop tables, hazards.</summary>
        public float Difficulty { get; private set; }

        private HealthComponent? _playerHealth;
        private float _recentDeaths;   // decays toward 0 over DeathMemorySeconds
        private float _lastEmitted = -1f;

        public override void _Ready()
        {
            base._Ready();
            Difficulty = Mathf.Clamp(BaseDifficulty, 0f, 1f);
            if (Engine.IsEditorHint()) return;
            if (!IsInGroup("adaptive_difficulty")) AddToGroup("adaptive_difficulty");

            var player = FindPlayer();
            _playerHealth = player != null
                ? EntityComponent.FindComponent<HealthComponent>(player, true)
                : null;
            if (_playerHealth != null)
                _playerHealth.Died += OnPlayerDied;
            else
                GD.PushWarning($"[{Name}] No HealthComponent found on a '{PlayerGroup}' member — adaptive difficulty has no performance signal and will sit at BaseDifficulty. Add a HealthComponent to the player.");
        }

        public override void _ExitTree()
        {
            if (_playerHealth != null && GodotObject.IsInstanceValid(_playerHealth))
                _playerHealth.Died -= OnPlayerDied;
            base._ExitTree();
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint() || !IsActive) return;
            float dt = (float)delta;

            // Forget old deaths so a rough patch early on doesn't punish the player forever.
            if (_recentDeaths > 0f && DeathMemorySeconds > 0f)
                _recentDeaths = Mathf.Max(0f, _recentDeaths - dt / DeathMemorySeconds);

            // Compute the target: base, raised when healthy, lowered per recent death.
            float target = BaseDifficulty;
            if (_playerHealth != null && GodotObject.IsInstanceValid(_playerHealth))
            {
                float hp = _playerHealth.HealthPercent;
                // Above the struggle threshold, creep up toward harder; below it, ease down.
                target += hp >= StruggleHealthThreshold
                    ? (1f - BaseDifficulty) * (hp - StruggleHealthThreshold) / Mathf.Max(0.0001f, 1f - StruggleHealthThreshold) * 0.5f
                    : -(BaseDifficulty) * (StruggleHealthThreshold - hp) / Mathf.Max(0.0001f, StruggleHealthThreshold) * 0.5f;
            }
            target -= _recentDeaths * DeathPenalty;
            target = Mathf.Clamp(target, 0f, 1f);

            Difficulty = Mathf.MoveToward(Difficulty, target, AdaptSpeed * dt);
            if (!Mathf.IsEqualApprox(Difficulty, _lastEmitted))
            {
                _lastEmitted = Difficulty;
                EmitSignal(SignalName.DifficultyChanged, Difficulty);
            }
        }

        /// <summary>Multiply a spawner's base interval by this: harder → shorter interval (more
        /// spawns), easier → longer. At Difficulty 0 returns 1.5×, at 1 returns 0.6×.</summary>
        public float GetSpawnIntervalScale() => Mathf.Lerp(1.5f, 0.6f, Difficulty);

        /// <summary>Drop-rate / loot multiplier: harder → more loot (rewards scale with risk).
        /// At Difficulty 0 returns 0.75×, at 1 returns 1.5×.</summary>
        public float GetDropMultiplier() => Mathf.Lerp(0.75f, 1.5f, Difficulty);

        private void OnPlayerDied() => _recentDeaths += 1f;

        private Node? FindPlayer()
        {
            var tree = GetTree();
            if (tree == null) return null;
            foreach (var n in tree.GetNodesInGroup(PlayerGroup))
                if (n is Node2D body) return body;
            return null;
        }
    }
}

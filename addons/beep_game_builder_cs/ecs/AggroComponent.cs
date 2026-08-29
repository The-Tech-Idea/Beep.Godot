using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Aggro/threat management component. Blind — tracks threat from multiple sources.
    /// Works for enemy AI targeting, boss mechanics, taunt abilities.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class AggroComponent : GameplayComponent
    {
        /// <summary>Distance beyond which a target is dropped from the threat table (it fled /
        /// broke line of sight). Proximity DETECTION of new targets is AIController's job
        /// (DetectionRange) — this is a threat table, so its one spatial rule is letting go.</summary>
        [Export] public float DeaggroRange { get; set; } = 500f;
        [Export] public float ThreatDecayRate { get; set; } = 10f;

        [Signal] public delegate void TargetAcquiredEventHandler(Node2D target);
        [Signal] public delegate void TargetLostEventHandler(Node2D oldTarget);
        [Signal] public delegate void ThreatChangedEventHandler(Node2D source, float threat);

        public Dictionary<Node2D, float> ThreatTable { get; private set; } = new();
        public Node2D? CurrentTarget { get; private set; }
        public float EffectiveDeaggroRange => NonNegative(DeaggroRange);
        public float EffectiveThreatDecayRate => NonNegative(ThreatDecayRate);

        private Node2D? _body;

        public override void _Ready()
        {
            base._Ready();
            _body = GetParent() as Node2D;
            if (!Engine.IsEditorHint() && _body == null)
                GD.PushWarning($"[{Name}] AggroComponent needs a Node2D parent to measure the deaggro leash; got '{GetParent()?.GetType().Name ?? "null"}'. Threats will be tracked but the distance-based deaggro won't apply.");
        }

        public void AddThreat(Node2D source, float amount)
        {
            if (!IsActive) return;
            if (source == null || !GodotObject.IsInstanceValid(source) || !float.IsFinite(amount) || amount <= 0f) return;
            ThreatTable.TryGetValue(source, out float current);
            ThreatTable[source] = current + amount;
            EmitSignal(SignalName.ThreatChanged, source, ThreatTable[source]);
            UpdateTarget();
        }

        private void UpdateTarget()
        {
            Node2D? highest = null;
            float maxThreat = 0;
            foreach (var kv in ThreatTable)
            {
                if (kv.Value > maxThreat) { maxThreat = kv.Value; highest = kv.Key; }
            }

            if (highest != CurrentTarget)
            {
                if (CurrentTarget != null) EmitSignal(SignalName.TargetLost, CurrentTarget);
                CurrentTarget = highest;
                if (CurrentTarget != null) EmitSignal(SignalName.TargetAcquired, CurrentTarget);
            }
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint() || !IsActive) return;
            float dt = double.IsFinite(delta) ? Mathf.Max(0f, (float)delta) : 0f;
            var toRemove = new List<Node2D>();
            foreach (var kv in new List<KeyValuePair<Node2D, float>>(ThreatTable))
            {
                // Drop a freed target, a decayed one, or one that fled beyond DeaggroRange.
                bool fled = _body != null && GodotObject.IsInstanceValid(kv.Key)
                            && _body.GlobalPosition.DistanceTo(kv.Key.GlobalPosition) > EffectiveDeaggroRange;
                float currentThreat = float.IsFinite(kv.Value) ? kv.Value : 0f;
                float nextThreat = currentThreat - EffectiveThreatDecayRate * dt;
                ThreatTable[kv.Key] = nextThreat;
                if (!GodotObject.IsInstanceValid(kv.Key) || nextThreat <= 0 || fled)
                    toRemove.Add(kv.Key);
            }
            foreach (var k in toRemove) ThreatTable.Remove(k);
            if (toRemove.Count > 0) UpdateTarget();
        }

        private static float NonNegative(float value) => float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;
    }
}

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
        [Export] public float AggroRange { get; set; } = 300f;
        [Export] public float DeaggroRange { get; set; } = 500f;
        [Export] public float ThreatDecayRate { get; set; } = 10f;

        [Signal] public delegate void TargetAcquiredEventHandler(Node2D target);
        [Signal] public delegate void TargetLostEventHandler(Node2D oldTarget);
        [Signal] public delegate void ThreatChangedEventHandler(Node2D source, float threat);

        public Dictionary<Node2D, float> ThreatTable { get; private set; } = new();
        public Node2D? CurrentTarget { get; private set; }

        public override void _Ready() { base._Ready(); }

        public void AddThreat(Node2D source, float amount)
        {
            if (!IsActive) return;
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
            if (!IsActive) return;
            var toRemove = new List<Node2D>();
            foreach (var kv in ThreatTable)
            {
                ThreatTable[kv.Key] -= ThreatDecayRate * (float)delta;
                if (ThreatTable[kv.Key] <= 0) toRemove.Add(kv.Key);
            }
            foreach (var k in toRemove) ThreatTable.Remove(k);
            if (toRemove.Count > 0) UpdateTarget();
        }
    }
}

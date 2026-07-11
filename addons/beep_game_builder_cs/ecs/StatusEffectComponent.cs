using Godot;
using Godot.Collections;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Status effects component. Blind — attach to any entity for buffs/debuffs.
    /// Each effect is a Resource with duration, tick interval, and modifier callbacks.
    /// Works for poison, burning, healing, speed boost, armor, invincibility.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class StatusEffectComponent : EntityComponent
    {
        [Signal] public delegate void EffectAppliedEventHandler(string effectId);
        [Signal] public delegate void EffectExpiredEventHandler(string effectId);
        [Signal] public delegate void EffectTickedEventHandler(string effectId, float remaining);

        public List<ActiveEffect> ActiveEffects { get; private set; } = new();

        public class ActiveEffect
        {
            public string Id;
            public float Duration;
            public float TickInterval;
            public float TimeSinceTick;
            public float TotalDuration;
            public bool IsBuff;

            public float Progress => TotalDuration > 0 ? 1f - (Duration / TotalDuration) : 1f;
            public bool IsExpired => Duration <= 0;
        }

        public void ApplyEffect(string id, float duration, float tickInterval = 1f, bool isBuff = true)
        {
            if (!IsActive) return;
            ActiveEffects.Add(new ActiveEffect
            {
                Id = id, Duration = duration, TickInterval = tickInterval,
                TotalDuration = duration, IsBuff = isBuff
            });
            EmitSignal(SignalName.EffectApplied, id);
        }

        public void RemoveEffect(string id)
        {
            ActiveEffects.RemoveAll(e => e.Id == id);
            EmitSignal(SignalName.EffectExpired, id);
        }

        public bool HasEffect(string id)
        {
            foreach (var e in ActiveEffects)
                if (e.Id == id) return true;
            return false;
        }

        public override void _Process(double delta)
        {
            if (!IsActive) return;
            for (int i = ActiveEffects.Count - 1; i >= 0; i--)
            {
                var e = ActiveEffects[i];
                e.Duration -= (float)delta;
                e.TimeSinceTick += (float)delta;

                if (e.TimeSinceTick >= e.TickInterval)
                {
                    e.TimeSinceTick = 0;
                    EmitSignal(SignalName.EffectTicked, e.Id, e.Duration);
                }

                if (e.IsExpired)
                {
                    EmitSignal(SignalName.EffectExpired, e.Id);
                    ActiveEffects.RemoveAt(i);
                }
            }
        }
    }
}

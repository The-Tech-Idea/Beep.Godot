using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Beep.ECS
{
    /// <summary>
    /// Status effects component. Blind — attach to any entity for behavioural status FLAGS:
    /// stun, invincibility, hunger, thirst, poison, burning — anything queried by presence
    /// (<see cref="HasEffect"/>) and duration, with stacking/refresh/extend.
    ///
    /// It no longer carries stat modifiers. Numeric buffs/debuffs (a +10 damage, a ×1.5 speed)
    /// are <see cref="StatModifier"/>s added to the entity's <see cref="StatsComponent"/> with a
    /// Duration — StatsComponent ticks and expires them, so there is ONE modifier channel, not two.
    /// The old GetModifier/ApplyEffectWithModifiers API had zero callers and was removed.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class StatusEffectComponent : GameplayComponent
    {
        public enum StackBehavior { Stack, Refresh, Extend }

        [Signal] public delegate void EffectAppliedEventHandler(string effectId, int stackCount);
        [Signal] public delegate void EffectExpiredEventHandler(string effectId);
        [Signal] public delegate void EffectTickedEventHandler(string effectId, float remaining);
        [Signal] public delegate void EffectRefreshedEventHandler(string effectId, float newDuration);

        public List<ActiveEffect> ActiveEffects { get; private set; } = new();

        public class ActiveEffect
        {
            public string Id;
            public float Duration;
            public float TickInterval;
            public float TimeSinceTick;
            public float TotalDuration;
            public bool IsBuff;
            public int StackCount = 1;
            public int MaxStacks = 10;

            public float Progress => TotalDuration > 0 ? 1f - (Duration / TotalDuration) : 1f;

            /// <summary>A negative authored Duration means permanent: it never ticks down and
            /// never expires. This is the channel a level-up upgrade / equipment modifier lives
            /// in — a duration-0 status was the only "modifier" shape before, and it expired on
            /// the first frame. (Duration == 0 remains a valid instantaneous effect.)</summary>
            public bool IsPermanent => Duration < 0f;
            public bool IsExpired => !IsPermanent && Duration <= 0f;
            public bool CanStack => StackCount < MaxStacks;
            public float RemainingPercent => Mathf.Clamp(Duration / TotalDuration, 0f, 1f);
        }

        public void ApplyEffect(string id, float duration, float tickInterval = 1f, bool isBuff = true,
            StackBehavior stackBehavior = StackBehavior.Stack, int maxStacks = 10)
        {
            if (!IsActive) return;

            var existing = ActiveEffects.FirstOrDefault(e => e.Id == id);

            if (existing != null)
            {
                // Handle existing effect based on stack behavior
                switch (stackBehavior)
                {
                    case StackBehavior.Refresh:
                        existing.Duration = duration;
                        existing.TotalDuration = duration;
                        existing.TimeSinceTick = 0;
                        EmitSignal(SignalName.EffectRefreshed, id, duration);
                        return;

                    case StackBehavior.Extend:
                        existing.Duration += duration;
                        if (existing.CanStack) existing.StackCount++;
                        EmitSignal(SignalName.EffectApplied, id, existing.StackCount);
                        return;

                    case StackBehavior.Stack:
                        if (!existing.CanStack) return;  // Hit stack limit
                        break;
                }
            }

            // Add new effect
            var newEffect = new ActiveEffect
            {
                Id = id,
                Duration = duration,
                TickInterval = tickInterval,
                TotalDuration = duration,
                IsBuff = isBuff,
                StackCount = 1,
                MaxStacks = maxStacks
            };
            ActiveEffects.Add(newEffect);
            EmitSignal(SignalName.EffectApplied, id, 1);
        }

        public void RemoveEffect(string id)
        {
            ActiveEffects.RemoveAll(e => e.Id == id);
            EmitSignal(SignalName.EffectExpired, id);
        }

        public bool HasEffect(string id) => ActiveEffects.Any(e => e.Id == id);

        public int GetActiveEffectCount(string id) => ActiveEffects.Count(e => e.Id == id);

        public List<string> GetActiveEffectIds() => ActiveEffects.Select(e => e.Id).Distinct().ToList();

        /// <summary>Get progress (0-1) for the first instance of an effect (for UI bars).</summary>
        public float GetEffectProgress(string id)
        {
            var effect = ActiveEffects.FirstOrDefault(e => e.Id == id);
            return effect?.RemainingPercent ?? 0f;
        }

        /// <summary>Get remaining duration in seconds for the first instance of an effect.</summary>
        public float GetEffectDuration(string id)
        {
            var effect = ActiveEffects.FirstOrDefault(e => e.Id == id);
            return effect?.Duration ?? 0f;
        }

        /// <summary>Get all effects as UI-friendly data (for HUD rendering).</summary>
        public List<EffectDisplayData> GetDisplayEffects()
        {
            var result = new List<EffectDisplayData>();
            foreach (var id in GetActiveEffectIds())
            {
                var effect = ActiveEffects.FirstOrDefault(e => e.Id == id);
                if (effect != null)
                {
                    result.Add(new EffectDisplayData
                    {
                        Id = id,
                        Progress = effect.RemainingPercent,
                        StackCount = effect.StackCount,
                        IsBuff = effect.IsBuff,
                        Duration = effect.Duration
                    });
                }
            }
            return result;
        }

        public class EffectDisplayData
        {
            public string Id;
            public float Progress;      // 0-1 for progress bars
            public int StackCount;      // For stack badges
            public bool IsBuff;         // For color coding
            public float Duration;      // For tooltips
        }

        public override void _Process(double delta)
        {
            if (!IsActive) return;
            for (int i = ActiveEffects.Count - 1; i >= 0; i--)
            {
                var e = ActiveEffects[i];
                if (!e.IsPermanent) e.Duration -= (float)delta;
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

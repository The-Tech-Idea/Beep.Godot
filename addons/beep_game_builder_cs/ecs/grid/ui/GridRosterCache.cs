using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// A HUD list panel's incrementally-maintained roster of scene components: the
    /// nullable-cache lifecycle the production and worker-status panels each carried a
    /// byte-for-byte copy of. <see cref="Invalidate"/> drops it; <see cref="Members"/>
    /// rebuilds from the panel's own collector (then sorts) when stale and otherwise prunes
    /// freed members - each handed to an optional callback so a side map (a per-member key
    /// cache) stays in step; <see cref="Append"/> inserts a newly-appeared member in order
    /// without a full rebuild. The collection and the sort are the panel's; only the cache
    /// mechanics live here.
    /// </summary>
    public sealed class GridRosterCache<T> where T : GodotObject
    {
        private List<T>? _members;

        /// <summary>Drop the cache; the next <see cref="Members"/> call rebuilds.</summary>
        public void Invalidate() => _members = null;

        /// <summary>Whether a roster is currently cached - an append handler skips a stale cache.</summary>
        public bool HasCache => _members is not null;

        /// <summary>
        /// The current roster: rebuilt from <paramref name="collect"/> and sorted when stale,
        /// otherwise pruned of freed members - each removed member passed to
        /// <paramref name="onRemoved"/> so a side map stays in step.
        /// </summary>
        public List<T> Members(Func<List<T>> collect, Comparison<T> sort, Action<T>? onRemoved = null)
        {
            if (_members is null)
            {
                _members = collect();
                _members.Sort(sort);
            }
            else
            {
                for (int i = _members.Count - 1; i >= 0; i--)
                {
                    if (GodotObject.IsInstanceValid(_members[i]))
                        continue;
                    onRemoved?.Invoke(_members[i]);
                    _members.RemoveAt(i);
                }
            }
            return _members;
        }

        /// <summary>
        /// Insert a newly-appeared member, keeping order, without a rebuild. No-op when the
        /// cache is stale (a rebuild will pick the member up) or it is already present.
        /// Returns true when it was added, so the caller can fill a side map for it.
        /// </summary>
        public bool Append(T member, Comparison<T> sort)
        {
            if (_members is null || _members.Contains(member))
                return false;
            _members.Add(member);
            _members.Sort(sort);
            return true;
        }
    }
}

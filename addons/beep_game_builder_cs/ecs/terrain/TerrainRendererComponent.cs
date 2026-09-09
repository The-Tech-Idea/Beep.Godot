using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// The lifecycle a terrain renderer shares with every other: one deferred
    /// rebuild per frame however many changes arrive, and a rebuild when the node
    /// becomes visible again after being drawn at least once.
    ///
    /// Nine renderers each carried a byte-identical copy of this coalescer. That
    /// is where the eviction-aware invalidation of ENH-01 has to land, and landing
    /// it nine times is how the painted renderer became the only chunk-aware
    /// listener while eight others stayed blind. It lives here once now.
    ///
    /// What this base deliberately does NOT own is how each renderer resolves and
    /// subscribes to its source. Those genuinely diverge - the resource and relief
    /// views also listen to a grid's geometry, the isometric block view subscribes
    /// to cell data inside its own rebuild, the isometric feature view follows
    /// another renderer's SurfaceRebuilt, and the tile view routes a cell change to
    /// a water-only requeue rather than a full rebuild - so each keeps its own
    /// resolution. Only the rebuild coalescer is one thing here.
    ///
    /// The two seams below exist for the two renderers whose coalescing is not
    /// quite the common shape: the painted view declines while an off-thread
    /// snapshot is being prepared (<see cref="CanQueueRebuild"/>), and the
    /// isometric autotile view time-slices a large map instead of rebuilding it
    /// inline (<see cref="PerformQueuedRebuild"/>).
    /// </summary>
    public abstract partial class TerrainRendererComponent : Node2D
    {
        /// <summary>Rebuild once this renderer is ready, unless a caller drives it.</summary>
        [Export] public bool RefreshOnReady { get; set; } = true;

        private bool _rebuildQueued;

        /// <summary>
        /// True once a rebuild has been attempted at least once. Until then a
        /// visibility change has nothing to redraw and must not force a build.
        /// </summary>
        protected bool HasRebuildAttempt { get; set; }

        /// <summary>Rebuilds this renderer from its configured source.</summary>
        public abstract void Rebuild();

        /// <summary>
        /// Coalesces any number of changes in a frame into one deferred rebuild.
        /// The flag is checked again inside the deferred call so a rebuild driven
        /// directly in the meantime cancels the queued one.
        /// </summary>
        protected void QueueRebuild()
        {
            if (_rebuildQueued || !CanQueueRebuild() || !IsInsideTree() || !IsVisibleInTree())
                return;
            _rebuildQueued = true;
            Callable.From(() =>
            {
                if (!_rebuildQueued)
                    return;
                _rebuildQueued = false;
                if (IsInsideTree() && IsVisibleInTree())
                    PerformQueuedRebuild();
            }).CallDeferred();
        }

        /// <summary>
        /// Clears the pending-rebuild flag. A subclass calls this at the top of a
        /// rebuild it runs directly, so a queued rebuild that fires afterwards
        /// sees the flag already consumed and does nothing.
        /// </summary>
        protected void ClearRebuildQueued() => _rebuildQueued = false;

        /// <summary>
        /// Whether a rebuild may be queued right now. The painted renderer overrides
        /// this to decline while it is preparing a visual snapshot off-thread.
        /// </summary>
        protected virtual bool CanQueueRebuild() => true;

        /// <summary>
        /// Runs the coalesced rebuild. The default rebuilds inline; the isometric
        /// autotile renderer overrides it to time-slice a large map.
        /// </summary>
        protected virtual void PerformQueuedRebuild() => Rebuild();

        /// <summary>
        /// Reacts to <c>GridCellDataComponent.CellsChanged</c>. A Residency-only change -
        /// a chunk evicted or reloaded with the same content - moves nothing this renderer
        /// draws, so it is ignored; any content change queues a rebuild. A renderer whose
        /// requeue is not a full rebuild (the tile view's coast) overrides this.
        /// </summary>
        protected virtual void OnCellsChangedSignal(int kind, Godot.Collections.Array<Vector2I> chunks)
        {
            if (((TerrainChangeKind)kind & TerrainChangeKind.Content) != 0)
                QueueRebuild();
        }

        public override void _Notification(int what)
        {
            if (what == NotificationVisibilityChanged && HasRebuildAttempt
                && IsInsideTree() && IsVisibleInTree() && !Engine.IsEditorHint())
                QueueRebuild();
        }
    }
}

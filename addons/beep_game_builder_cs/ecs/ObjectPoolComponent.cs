using System.Collections.Generic;
using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Scene instance pool. Attach to a Node that will own the pool (e.g. a "Projectiles"
    /// or "Effects" container). Set the PackedScene + preload count in the inspector.
    /// Get() returns an instance (instantiating if the pool is empty); Release() returns
    /// one to the pool (hides it instead of freeing).
    /// Replaces the old GDScript object_pool.gd.template.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ObjectPoolComponent : GameplayComponent
    {
        [Export] public PackedScene? Scene { get; set; }
        [Export] public int PreloadCount { get; set; } = 5;
        [Export] public int MaxSize { get; set; } = 50;

        private readonly Queue<Node> _pool = new();
        // TOTAL live instances (checked-out + idle), not the idle-queue size — MaxSize caps this.
        // The old _poolCount tracked only the idle queue (it equalled _pool.Count), so once every
        // instance was checked out it read 0 and Expand minted a fresh node on every Get(), unbounded.
        private int _totalAllocated;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            for (int i = 0; i < PreloadCount; i++)
                Expand();
        }

        private void Expand()
        {
            if (Scene == null || _totalAllocated >= MaxSize) return;
            var inst = Scene.Instantiate();
            GetParent().AddChild(inst);
            if (inst is CanvasItem ci) ci.Visible = false;
            if (inst is Node2D n2d) n2d.SetProcess(false);
            _pool.Enqueue(inst);
            _totalAllocated++;
        }

        /// <summary>Get an instance from the pool (or instantiate a new one if under MaxSize).
        /// Returns null if no Scene is set, or if the pool is at MaxSize with none idle.</summary>
        public Node? Get()
        {
            if (!IsActive || Scene == null) return null;

            // Purge instances that were freed while idle (e.g. a self-freeing projectile that was
            // Release()d then QueueFree()d). Without this, Get() could hand out a freed node and
            // AddChild would fail. Each purge frees a slot back under MaxSize.
            while (_pool.Count > 0 && !GodotObject.IsInstanceValid(_pool.Peek()))
            {
                _pool.Dequeue();
                _totalAllocated--;
            }

            if (_pool.Count == 0) Expand();      // self-caps at MaxSize; no-op when at the ceiling
            if (_pool.Count == 0) return null;   // at cap and everything is checked out

            var inst = _pool.Dequeue();          // total unchanged — it's still alive, just checked out
            if (inst is CanvasItem ci) ci.Visible = true;
            if (inst is Node2D n2d) n2d.SetProcess(true);
            return inst;
        }

        /// <summary>Return an instance to the pool (hide + deactivate instead of freeing).</summary>
        public void Release(Node inst)
        {
            if (inst == null || !GodotObject.IsInstanceValid(inst)) return;
            // Ignore a double-Release: without this, the same node sits in the queue twice and two
            // Get() callers receive it, both driving the one instance.
            if (_pool.Contains(inst)) return;
            if (inst is CanvasItem ci) ci.Visible = false;
            if (inst is Node2D n2d) n2d.SetProcess(false);
            _pool.Enqueue(inst);   // already counted in _totalAllocated when it was created
        }

        public int Available => _pool.Count;
    }
}

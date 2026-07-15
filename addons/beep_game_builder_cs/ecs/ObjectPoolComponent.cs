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
        private int _poolCount;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            for (int i = 0; i < PreloadCount; i++)
                Expand();
        }

        private void Expand()
        {
            if (Scene == null || _poolCount >= MaxSize) return;
            var inst = Scene.Instantiate();
            GetParent().AddChild(inst);
            if (inst is CanvasItem ci) ci.Visible = false;
            if (inst is Node2D n2d) n2d.SetProcess(false);
            _pool.Enqueue(inst);
            _poolCount++;
        }

        /// <summary>Get an instance from the pool (or instantiate a new one if empty).
        /// Returns null if no Scene is set.</summary>
        public Node? Get()
        {
            if (!IsActive || Scene == null) return null;
            if (_pool.Count == 0 && _poolCount < MaxSize) Expand();
            if (_pool.Count == 0) return null;

            var inst = _pool.Dequeue();
            _poolCount--;
            if (inst is CanvasItem ci) ci.Visible = true;
            if (inst is Node2D n2d) n2d.SetProcess(true);
            return inst;
        }

        /// <summary>Return an instance to the pool (hide + deactivate instead of freeing).</summary>
        public void Release(Node inst)
        {
            if (inst == null || !GodotObject.IsInstanceValid(inst)) return;
            if (inst is CanvasItem ci) ci.Visible = false;
            if (inst is Node2D n2d) n2d.SetProcess(false);
            _pool.Enqueue(inst);
            _poolCount++;
        }

        public int Available => _pool.Count;
    }
}

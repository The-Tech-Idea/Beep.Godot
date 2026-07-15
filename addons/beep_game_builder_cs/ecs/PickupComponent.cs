using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Pickup component. Blind — attach to any Area2D to make it collectible.
    /// Works for coins, health packs, keys, power-ups, loot drops.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class PickupComponent : GameplayComponent
    {
        [Export] public string ItemId { get; set; } = "coin";
        [Export] public int Quantity { get; set; } = 1;
        [Export] public float FloatAmplitude { get; set; } = 5f;
        [Export] public float FloatSpeed { get; set; } = 2f;
        [Export] public bool AutoRotate { get; set; } = true;
        [Export] public float RespawnSeconds { get; set; } = 0f; // 0 = no respawn

        [Signal] public delegate void CollectedEventHandler(string itemId, int quantity);
        [Signal] public delegate void RespawnedEventHandler();

        private Vector2 _startPos;
        private float _time;
        private bool _collected;
        private float _respawnTimer;

        private Area2D? _area;

        public override void _Ready()
        {
            base._Ready();
            if (GetParent() is Node2D parent) _startPos = parent.Position;
            _area = GetParent() as Area2D;
            if (_area != null) _area.BodyEntered += OnBodyEntered;
        }

        private void OnBodyEntered(Node2D body) => Collect()

        public override void _Process(double delta)
        {
            if (_collected)
            {
                if (RespawnSeconds > 0)
                {
                    _respawnTimer -= (float)delta;
                    if (_respawnTimer <= 0) Respawn();
                }
                return;
            }
            _time += (float)delta;
            if (GetParent() is Node2D p)
            {
                p.Position = _startPos + new Vector2(0, Mathf.Sin(_time * FloatSpeed) * FloatAmplitude);
                if (AutoRotate) p.Rotation += (float)delta * 2f;
            }
        }

        private void Collect()
        {
            if (_collected || !IsActive) return;
            _collected = true;
            EmitSignal(SignalName.Collected, ItemId, Quantity);
            if (GetParent() is Node2D p) { p.Visible = false; p.ProcessMode = ProcessModeEnum.Disabled; }
            if (RespawnSeconds > 0) _respawnTimer = RespawnSeconds;
            else if (GetParent() is Node parent) parent.QueueFree();
        }

        private void Respawn()
        {
            _collected = false;
            _time = 0;
            if (GetParent() is Node2D p) { p.Visible = true; p.ProcessMode = ProcessModeEnum.Inherit; p.Position = _startPos; }
            EmitSignal(SignalName.Respawned);
        }

        public override void _ExitTree()
        {
            if (_area != null)
                _area.BodyEntered -= OnBodyEntered;
        }
    }
}

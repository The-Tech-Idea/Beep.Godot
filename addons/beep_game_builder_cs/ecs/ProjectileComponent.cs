using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Projectile component. Attach to any Area2D to make it a projectile.
    /// Blind — works for bullets, arrows, spell orbs, thrown items, sports balls.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ProjectileComponent : GameplayComponent
    {
        [Export] public float Speed { get; set; } = 400f;
        [Export] public float MaxLifetime { get; set; } = 5f;
        [Export] public float Damage { get; set; } = 10f;
        /// <summary>The damage type this projectile deals, met by a target's ResistanceComponent.
        /// A ranged weapon sets it from GameWeapon.DamageType when it spawns the shot.</summary>
        [Export] public DamageType DamageType { get; set; } = DamageType.Physical;
        [Export] public bool UseGravity { get; set; } = false;
        [Export] public float GravityStrength { get; set; } = 980f;
        [Export] public bool Pierce { get; set; } = false;

        [ExportGroup("Height (2.5D)")]
        /// <summary>When true, the projectile travels in a ballistic arc over the ground plane: it
        /// gains a sibling HeightComponent, is lobbed upward with <see cref="ArcHeight"/> peak, and
        /// lands (Height 0) at the end — firing its own Hit/impact instead of expiring mid-air.
        /// For top-down/isometric grenades, thrown items, lobbed shots. Flat bullets leave this off.</summary>
        [Export] public bool UseArc { get; set; } = false;
        /// <summary>Peak arc height in px when <see cref="UseArc"/> is on.</summary>
        [Export] public float ArcHeight { get; set; } = 80f;
        /// <summary>When true, a projectile with a HeightComponent only hits targets whose height
        /// band overlaps its own — so a low shot passes UNDER a flyer. Off = legacy flat behavior
        /// (hits anything the Area2D touches). Turn on for 2.5D games that use HeightComponent.</summary>
        [Export] public bool RespectHeight { get; set; } = false;

        [Signal] public delegate void HitEventHandler(Node? hitNode, Vector2 point);
        [Signal] public delegate void ExpiredEventHandler();

        /// <summary>Who fired this. Set by the shooter before <see cref="Launch"/>; the
        /// projectile and everything under it is excluded from collision, so a shooter can't
        /// hit itself.
        ///
        /// Must be explicit because projectiles are normally parented to a pool node, not to
        /// the shooter — inferring the owner from GetParent() yields the pool, and the
        /// exclusion silently never matches. Falls back to the parent for the case where a
        /// projectile IS spawned as a child of its shooter.</summary>
        public Node2D? Shooter { get; set; }

        private Vector2 _velocity;
        private float _lifetime;
        private Area2D? _area;
        private Node2D? _owner;
        // When a ProjectileModifierComponent sibling owns movement (Homing/Bounce/Straight), THIS
        // component must not also translate the node, or the projectile travels at ~2× speed.
        private bool _movementDelegated;
        // 2.5D arc state. _height is the logical Z (drives the sibling HeightComponent); _vVel is
        // vertical velocity. Only used when UseArc is on.
        private HeightComponent? _height;
        private float _vVel;
        private float _arcGravity;
        public float EffectiveSpeed => NonNegative(Speed);
        public float EffectiveMaxLifetime => NonNegative(MaxLifetime);
        public float EffectiveDamage => NonNegative(Damage);
        public float EffectiveGravityStrength => NonNegative(GravityStrength);
        public float EffectiveArcGravity => float.IsFinite(GravityStrength) && GravityStrength > 0f ? GravityStrength : 980f;
        public float EffectiveArcHeight => float.IsFinite(ArcHeight) ? Mathf.Max(1f, ArcHeight) : 1f;

        public override void _Ready()
        {
            base._Ready();
            _area = GetParent() as Area2D;
            if (_area == null)
            {
                GD.PushError($"[Projectile] Parent must be Area2D, got {GetParent()?.GetType().Name}");
                return;
            }

            _movementDelegated = GetSiblingComponent<ProjectileModifierComponent>() is { IsActive: true };
            _area.BodyEntered += OnBodyEntered;
            _area.AreaEntered += OnAreaEntered;

            if (UseArc && !Engine.IsEditorHint()) SetupArc();
        }

        /// <summary>Ensure a sibling HeightComponent exists and seed the arc's vertical velocity so the
        /// projectile peaks at <see cref="ArcHeight"/> then falls back to the ground. The arc is pure
        /// kinematics on the logical Z; ground-plane motion stays flat (Speed × direction).</summary>
        private void SetupArc()
        {
            _height = GetSiblingComponent<HeightComponent>();
            if (_height == null)
            {
                _height = new HeightComponent { Name = "Height" };
                _area!.AddChild(_height);
            }
            // v0 = sqrt(2·g·h) to just reach ArcHeight; g chosen so the flight time roughly matches
            // how long a flat shot would take to leave the screen, keeping the lob readable.
            _arcGravity = EffectiveArcGravity;
            _vVel = Mathf.Sqrt(2f * _arcGravity * EffectiveArcHeight);
        }

        private void OnBodyEntered(Node n)
        {
            OnCollision(n);
        }

        private void OnAreaEntered(Area2D n)
        {
            OnCollision(n);
        }

        /// <summary>Whether a collided node belongs to whoever fired this. Covers descendants,
        /// not just the shooter node itself — a hurtbox/hitbox Area2D is a CHILD of the body,
        /// so an identity check alone would let a shooter hit its own hurtbox.
        ///
        /// Resolved here rather than in _Ready: AddChild fires _Ready, so a spawner can only
        /// set Shooter after that. By first collision it is always set.</summary>
        private bool IsOwnedByShooter(Node n)
        {
            _owner ??= Shooter ?? _area?.GetParent() as Node2D;
            return _owner != null && (n == _owner || _owner.IsAncestorOf(n));
        }

        private void OnCollision(Node n)
        {
            if (_area == null || IsOwnedByShooter(n)) return;
            if (!HeightGatePasses(n)) return;

            var health = EntityComponent.FindComponent<HealthComponent>(n, false);
            if (health != null)
            {
                health.TakeDamage(new GameDamage(EffectiveDamage, DamageType, _owner));

                var knockback = EntityComponent.FindComponent<KnockbackComponent>(n, false);
                if (knockback != null && n is Node2D)
                    knockback.ApplyKnockback(_area.GlobalPosition);
            }

            EmitSignal(SignalName.Hit, n, _area.GlobalPosition);
            if (!Pierce) _area.QueueFree();
        }

        public void Launch(Vector2 direction)
        {
            var dir = IsFinite(direction) && direction != Vector2.Zero ? direction.Normalized() : Vector2.Right;
            _velocity = dir * EffectiveSpeed;
            _lifetime = EffectiveMaxLifetime;
            // If a ProjectileModifierComponent owns motion, hand it the spawner-set speed and
            // fire direction — it initialized from its own default Speed in _Ready (before the
            // spawner set Speed), so the weapon's projectile speed was silently dropped.
            if (_movementDelegated)
                GetSiblingComponent<ProjectileModifierComponent>()?.SetLaunch(dir, EffectiveSpeed);
        }

        /// <summary>2.5D hit gate. When <see cref="RespectHeight"/> is on AND this projectile has a
        /// height (arcing), a target is hittable only if its height band overlaps the projectile's.
        /// A target with no HeightComponent is grounded (band 0), so an arcing shot high overhead
        /// passes over it; a grounded projectile still hits grounded targets. Legacy flat behavior
        /// when RespectHeight is off.</summary>
        private bool HeightGatePasses(Node n)
        {
            if (!RespectHeight) return true;
            _height ??= GetSiblingComponent<HeightComponent>();
            if (_height == null) return true;   // flat projectile — no band to gate on
            var targetHeight = EntityComponent.FindComponent<HeightComponent>(n, false);
            if (targetHeight == null) return _height.HeightOverlaps(0f, 16f);   // grounded target
            return _height.HeightOverlaps(targetHeight);
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint()) return;
            if (_area == null || !IsActive) return;
            float dt = double.IsFinite(delta) ? Mathf.Max(0f, (float)delta) : 0f;
            if (!IsFinite(_velocity)) _velocity = Vector2.Zero;
            if (!_movementDelegated)   // a ProjectileModifierComponent sibling, if present, owns motion
            {
                if (UseGravity) _velocity.Y += EffectiveGravityStrength * dt;
                _area.Position += _velocity * dt;
            }

            // Ballistic arc: integrate the logical Z. Ground-plane motion above stays flat, so the
            // shot covers ground at Speed while rising/falling on its own axis. Landing (height 0)
            // fires Hit at the impact point and frees — an arcing shot never "expires" mid-air.
            if (UseArc && _height != null)
            {
                _vVel -= _arcGravity * dt;
                float h = _height.Height + _vVel * dt;
                if (h <= 0f && _vVel < 0f)
                {
                    _height.SetHeight(0f);
                    // No specific node was hit — the projectile landed on the ground. Pass the area
                    // itself (not null: a null Node marshals to a non-nullable Variant and fails to
                    // compile); listeners tell a landing from a target hit by checking hit == this.
                    EmitSignal(SignalName.Hit, _area, _area.GlobalPosition);
                    _area.QueueFree();
                    return;
                }
                _height.SetHeight(h);
            }

            _lifetime -= dt;
            if (_lifetime <= 0)
            {
                EmitSignal(SignalName.Expired);
                _area?.QueueFree();
            }
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (_area != null)
            {
                _area.BodyEntered -= OnBodyEntered;
                _area.AreaEntered -= OnAreaEntered;
            }
        }

        private static float NonNegative(float value) => float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;

        private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}

using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// A 2D ball for sports / physics play — soccer, basketball, a bouncing grenade, a marble.
    /// Attach to a CharacterBody2D. Unlike a projectile (which lands and despawns), a ball
    /// BOUNCES: it owns ground-plane roll (friction) plus a logical Z handled by a sibling
    /// <see cref="HeightComponent"/>, re-launching with damped velocity each landing until it
    /// rolls to a stop.
    ///
    /// Possession: a player (a body in <see cref="PlayerGroup"/>) that touches the ball claims
    /// it. While owned, the ball sticks to the owner's feet (dribble) until <see cref="Kick"/>
    /// sends it loose with a ground impulse and an optional lob. This is deliberately NOT
    /// PickupComponent — a pickup is collected into an inventory and freed/hidden; a ball must
    /// persist in the world and be re-kickable, so it owns its own claim logic.
    ///
    /// Composes, never modifies: flat bullets stay on ProjectileComponent, which this doesn't touch.
    /// The ball reuses the 2.5D height band, so a lofted ball passes over a grounded player and a
    /// sliding tackle (a ground hazard) only connects when the ball is low.
    ///
    /// In the Add Node tree: EntityComponent → GameplayComponent → BallComponent
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class BallComponent : GameplayComponent
    {
        [ExportGroup("Motion")]
        /// <summary>Ground friction (px/s²) bleeding off speed while rolling. Higher = shorter roll.</summary>
        [Export] public float RollFriction { get; set; } = 400f;
        /// <summary>Fraction of vertical velocity kept on each bounce (0–1). 0.6 = a lively soccer
        /// ball; lower = a heavy medicine ball that dies fast.</summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float Restitution { get; set; } = 0.6f;
        /// <summary>Fraction of ground speed kept on each bounce (landing scrubs some roll).</summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float BounceGroundRetention { get; set; } = 0.8f;
        /// <summary>Vertical speed below which a bounce is too small to see — the ball settles and
        /// rolls instead of micro-bouncing forever.</summary>
        [Export] public float SettleThreshold { get; set; } = 40f;
        /// <summary>Gravity pulling the ball back to the ground plane on its logical Z.</summary>
        [Export] public float Gravity { get; set; } = 1800f;

        [ExportGroup("Possession")]
        /// <summary>Group whose members can claim the ball by touching it.</summary>
        [Export] public string PlayerGroup { get; set; } = "players";
        /// <summary>When true, the ball auto-claims to a player who steps into its claim radius —
        /// the arcade default. When false, possession is YOURS to drive: call <see cref="TryPossess"/>
        /// from your player controller (e.g. on an input action or a precise foot Area2D) for
        /// deliberate, sim-style control. Off by default so a controller-driven game isn't fighting
        /// an auto-claim it didn't ask for.</summary>
        [Export] public bool AutoPossess { get; set; } = false;
        /// <summary>Optional sibling Area2D used as the claim radius for <see cref="AutoPossess"/>.
        /// Null = auto-find a sibling Area2D named "ClaimRadius", else create one. The ball's body is
        /// a CharacterBody2D (not an Area2D), so the claim zone is a sibling — the same sibling-area
        /// pattern AmbientAudioComponent/WindFieldComponent use.</summary>
        [Export] public NodePath? ClaimAreaPath { get; set; }
        /// <summary>Radius of the auto-created claim zone when none is supplied. Slightly larger than
        /// the ball so a player claims it on approach, not only on exact overlap.</summary>
        [Export] public float ClaimRadius { get; set; } = 28f;
        /// <summary>Distance ahead of the owner's facing the ball sits while dribbled.</summary>
        [Export] public float DribbleOffset { get; set; } = 24f;
        /// <summary>Seconds after a kick before the kicker (or anyone) can re-claim the loose ball —
        /// stops the kicker instantly re-collecting their own pass.</summary>
        [Export] public float ReclaimDelay { get; set; } = 0.25f;

        [Signal] public delegate void KickedEventHandler(Node2D kicker, Vector2 velocity, float lob);
        [Signal] public delegate void PossessedEventHandler(Node2D owner);
        [Signal] public delegate void PossessionLostEventHandler();
        [Signal] public delegate void BouncedEventHandler(Vector2 groundVelocity, float height);
        [Signal] public delegate void SettledEventHandler();

        /// <summary>Current possessor, or null while loose. Read to drive AI ("chase the loose ball").</summary>
        public Node2D? Possessor { get; private set; }
        public bool IsOwned => Possessor != null;
        public bool IsAirborne => _height != null && _height.IsAirborne;

        private CharacterBody2D? _body;
        private HeightComponent? _height;
        private HeightComponent? _landedSignalSource;
        private Area2D? _claimArea;
        private Vector2 _groundVelocity;
        private float _verticalVelocity;
        private float _reclaimTimer;
        private bool _settledEmitted;

        public float EffectiveRollFriction => NonNegative(RollFriction);
        public float EffectiveRestitution => Mathf.Clamp(FiniteOr(Restitution, 0.6f), 0f, 1f);
        public float EffectiveBounceGroundRetention => Mathf.Clamp(FiniteOr(BounceGroundRetention, 0.8f), 0f, 1f);
        public float EffectiveSettleThreshold => NonNegative(SettleThreshold);
        public float EffectiveGravity => NonNegative(Gravity);
        public float EffectiveClaimRadius => NonNegative(ClaimRadius);
        public float EffectiveDribbleOffset => NonNegative(DribbleOffset);
        public float EffectiveReclaimDelay => NonNegative(ReclaimDelay);

        public override void _Ready()
        {
            base._Ready();
            _body = GetParent() as CharacterBody2D;
            if (_body == null)
                GD.PushWarning($"[{Name}] BallComponent needs a CharacterBody2D parent to roll and collide; got '{GetParent()?.GetType().Name ?? "null"}'. The ball will sit inert.");
            // The ball NEEDS a HeightComponent for its bounce — add one if the template lacks it.
            _height = GetSiblingComponent<HeightComponent>();
            if (_height == null && _body != null && !Engine.IsEditorHint())
            {
                CallDeferred(nameof(EnsureHeightComponent));
            }
            else if (_height != null)
            {
                ConnectHeightSignals();
            }

            if (AutoPossess && !Engine.IsEditorHint()) SetupClaimArea();
        }

        public void EnsureHeightComponent()
        {
            if (_body == null || Engine.IsEditorHint()) return;
            _height = GetSiblingComponent<HeightComponent>();
            if (_height == null)
            {
                _height = new HeightComponent { Name = "Height" };
                _body.AddChild(_height);
            }
            ConnectHeightSignals();
        }

        private void ConnectHeightSignals()
        {
            if (_height == null || !GodotObject.IsInstanceValid(_height)) return;
            if (_landedSignalSource == _height && GodotObject.IsInstanceValid(_landedSignalSource)) return;
            if (_landedSignalSource != null && GodotObject.IsInstanceValid(_landedSignalSource))
                _landedSignalSource.Landed -= OnLanded;
            _height.Landed += OnLanded;
            _landedSignalSource = _height;
        }

        /// <summary>Resolve (or create) the claim-radius Area2D and wire contact claiming. A sibling
        /// of the ball body, NOT a child — a child Area2D would move with the ball body's transform
        /// twice over and a CharacterBody2D parent can't host an Area2D's collision directly.</summary>
        private void SetupClaimArea()
        {
            if (_body == null) return;
            _claimArea = ClaimAreaPath != null ? GetNodeOrNull<Area2D>(ClaimAreaPath) : null;
            _claimArea ??= _body.GetNodeOrNull<Area2D>("ClaimRadius");
            if (_claimArea == null)
            {
                _claimArea = new Area2D { Name = "ClaimRadius" };
                var shape = new CollisionShape2D { Shape = new CircleShape2D { Radius = EffectiveClaimRadius } };
                _claimArea.AddChild(shape);
                _body.AddChild(_claimArea);
            }
            _claimArea.BodyEntered += OnClaimAreaBodyEntered;
        }

        /// <summary>A body stepped into the claim radius — claim the ball for it if it's a player
        /// and the post-kick reclaim delay has elapsed. The ball's height doesn't gate claiming: a
        /// player chests down a descending ball, so a loose airborne ball is still claimable.</summary>
        private void OnClaimAreaBodyEntered(Node2D body)
        {
            if (!IsActive) return;
            TryPossess(body);
        }

        /// <summary>Launch the ball loose with a ground impulse and an optional upward lob.
        /// Clears possession — a kicked ball is a loose ball until the reclaim delay passes.</summary>
        public void Kick(Vector2 direction, float power, float lob = 0f, Node2D? kicker = null)
        {
            if (_body == null) return;
            var dir = IsFinite(direction) && direction.LengthSquared() > 0.0001f ? direction.Normalized() : Vector2.Right;
            _groundVelocity = dir * NonNegative(power);
            float effectiveLob = NonNegative(lob);
            if (effectiveLob > 0f && _height != null)
            {
                // v0 = sqrt(2·g·h) reaches the lob height before falling back.
                _verticalVelocity = Mathf.Sqrt(2f * EffectiveGravity * effectiveLob);
                _height.SetHeight(Mathf.Max(_height.Height, 0.001f));  // become airborne
            }
            _settledEmitted = false;
            SetOwner(null);
            _reclaimTimer = EffectiveReclaimDelay;
            EmitSignal(SignalName.Kicked, kicker ?? _body, _groundVelocity, effectiveLob);
        }

        /// <summary>Claim the ball for a body. No-op if already owned by it or the reclaim delay
        /// is still running after a kick.</summary>
        public bool TryPossess(Node2D claimant)
        {
            if (_body == null || claimant == null) return false;
            if (!claimant.IsInGroup(PlayerGroup)) return false;
            if (_reclaimTimer > 0f) return false;
            if (Possessor == claimant) return true;
            SetOwner(claimant);
            return true;
        }

        /// <summary>Release possession without kicking (a steal, a fumble).</summary>
        public void Release()
        {
            SetOwner(null);
            _reclaimTimer = EffectiveReclaimDelay;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (Engine.IsEditorHint() || !IsActive || _body == null) return;
            float dt = DeltaSeconds(delta);
            _reclaimTimer = Mathf.Max(0f, FiniteOr(_reclaimTimer, 0f) - dt);
            if (!IsFinite(_groundVelocity)) _groundVelocity = Vector2.Zero;
            _verticalVelocity = FiniteOr(_verticalVelocity, 0f);

            if (IsOwned)
            {
                FollowOwner(dt);
                return;
            }

            // Loose ball: roll on the ground plane with friction, and integrate the bounce Z.
            IntegrateBounce(dt);
            _groundVelocity = _groundVelocity.MoveToward(Vector2.Zero, EffectiveRollFriction * dt);
            _body.Velocity = _groundVelocity;
            _body.MoveAndSlide();
            _groundVelocity = _body.Velocity;   // collisions may have stopped it; keep the truth

            // Emit Settled once when the ball is both grounded and (nearly) stopped.
            if (!_settledEmitted && !IsAirborne && _groundVelocity.LengthSquared() < 4f)
            {
                _settledEmitted = true;
                EmitSignal(SignalName.Settled);
            }
        }

        /// <summary>While owned, the ball rides at the owner's feet in their facing direction
        /// (dribble). The body tracks the owner directly rather than via physics velocity, so a
        /// dribbling player can't be shoved off the ball by a wall.</summary>
        private void FollowOwner(double delta)
        {
            if (Possessor == null || !GodotObject.IsInstanceValid(Possessor)) { SetOwner(null); return; }
            // Facing = the owner's current velocity direction, or right when idle.
            Vector2 facing = Vector2.Right;
            if (Possessor is CharacterBody2D cb && cb.Velocity.LengthSquared() > 1f)
                facing = cb.Velocity.Normalized();
            else if (Possessor is Node2D n)
                facing = Vector2.FromAngle(n.Rotation);

            if (!IsFinite(facing) || facing.LengthSquared() <= 0.0001f) facing = Vector2.Right;
            Vector2 ownerPosition = IsFinite(Possessor.GlobalPosition) ? Possessor.GlobalPosition : Vector2.Zero;
            _body!.GlobalPosition = ownerPosition + facing * EffectiveDribbleOffset;
            _groundVelocity = Vector2.Zero;
            // A dribbled ball is on the ground.
            _height?.SetHeight(0f);
        }

        /// <summary>Integrate the logical Z while loose: rise/fall under gravity, bounce on landing
        /// with restitution until the bounce is too small to see (then it just rolls).</summary>
        private void IntegrateBounce(double dt)
        {
            if (_height == null) return;
            if (!IsAirborne && _verticalVelocity <= 0f) return;

            float effectiveDelta = double.IsFinite(dt) ? Mathf.Max(0f, (float)dt) : 0f;
            _verticalVelocity = FiniteOr(_verticalVelocity, 0f) - EffectiveGravity * effectiveDelta;
            float h = Mathf.Max(0f, FiniteOr(_height.Height, 0f)) + _verticalVelocity * effectiveDelta;
            if (h <= 0f && _verticalVelocity < 0f)
            {
                // Landing: HeightComponent.Landed fires OnLanded, which applies the bounce impulse.
                _height.SetHeight(0f);
            }
            else
            {
                _height.SetHeight(h);
            }
        }

        /// <summary>On landing, bounce back up with damped vertical + ground speed — unless the
        /// impact is too weak to see, in which case the ball settles into a roll.</summary>
        private void OnLanded()
        {
            if (IsOwned) return;   // a possessed ball doesn't bounce; it's dribbled
            float impact = -_verticalVelocity;   // downward speed at contact
            if (impact < EffectiveSettleThreshold)
            {
                _verticalVelocity = 0f;
                return;   // settle into a roll
            }
            _verticalVelocity = impact * EffectiveRestitution;
            _groundVelocity *= EffectiveBounceGroundRetention;
            _height?.SetHeight(0.001f);   // become airborne for the next bounce arc
            EmitSignal(SignalName.Bounced, _groundVelocity, _verticalVelocity);
        }

        private void SetOwner(Node2D? owner)
        {
            if (Possessor == owner) return;
            var previous = Possessor;
            Possessor = owner;
            if (owner != null) EmitSignal(SignalName.Possessed, owner);
            else if (previous != null) EmitSignal(SignalName.PossessionLost);
        }

        public override void _ExitTree()
        {
            if (_landedSignalSource != null && GodotObject.IsInstanceValid(_landedSignalSource))
                _landedSignalSource.Landed -= OnLanded;
            if (_claimArea != null && GodotObject.IsInstanceValid(_claimArea))
                _claimArea.BodyEntered -= OnClaimAreaBodyEntered;
            base._ExitTree();
        }

        private static float NonNegative(float value) =>
            float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;

        private static float DeltaSeconds(double delta) =>
            double.IsFinite(delta) ? Mathf.Max(0f, (float)delta) : 0f;

        private static float FiniteOr(float value, float fallback) =>
            float.IsFinite(value) ? value : fallback;

        private static bool IsFinite(Vector2 value) =>
            float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}

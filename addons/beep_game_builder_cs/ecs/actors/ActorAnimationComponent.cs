using Godot;

namespace Beep.ECS;

/// <summary>Observes gameplay; native animation nodes own playback and authored transitions.</summary>
[Tool, GlobalClass]
public partial class ActorAnimationComponent : EntityComponent
{
    [Export] public NodePath AnimationTargetPath { get; set; } = new("../AnimatedSprite2D");
    [Export] public NodePath FlipSpritePath { get; set; } = new("");
    [Export] public NodePath WorkerPath { get; set; } = new("");
    [Export] public ActorAnimationProfile? Profile { get; set; }
    [Signal] public delegate void AnimationStateChangedEventHandler(int state, string clip, Vector2 facing);
    public ActorAnimationState CurrentState { get; private set; } = ActorAnimationState.Idle;
    public string CurrentClip { get; private set; } = "";
    public Vector2 Facing { get; private set; } = Vector2.Right;

    private Node2D? _body;
    private Node? _target;
    private Node2D? _flipSprite;
    private ActorComponent? _actor;
    private HealthComponent? _health;
    private AttackComponent? _attack;
    private ShooterController? _shooter;
    private Node? _worker;
    private DashComponent? _dash;
    private SlideComponent? _slide;
    private HoverComponent? _hover;
    private GlideComponent? _glide;
    private FlyComponent? _flight;
    private KnockbackComponent? _knockback;
    private AnimationNodeStateMachine? _machine;
    private AnimationNodeStateMachinePlayback? _playback;
    private Vector2 _lastPosition;
    private float _attackTime, _hurtTime, _interactTime;
    private ActorAnimationState? _restartAction;
    private bool _wasActive;

    public override void _Ready()
    {
        base._Ready();
        if (Engine.IsEditorHint()) return;
        ProcessPhysicsPriority = 100;
        RefreshBindings();
    }

    public void RefreshBindings()
    {
        DisconnectSources();
        _body = GetParent() as Node2D;
        _target = GetNodeOrNull(AnimationTargetPath);
        _flipSprite = FlipSpritePath.IsEmpty ? _target as AnimatedSprite2D : GetNodeOrNull<Node2D>(FlipSpritePath);
        _actor = GetSiblingComponent<ActorComponent>();
        _health = GetSiblingComponent<HealthComponent>();
        _attack = GetSiblingComponent<AttackComponent>();
        _shooter = GetSiblingComponent<ShooterController>();
        _worker = WorkerPath.IsEmpty ? FindComponent<IWorker>(GetParent(), false) as Node : GetNodeOrNull(WorkerPath);
        if (!GridWorkerPorts.AnswersWorkerShape(_worker)) _worker = null;
        _dash = GetSiblingComponent<DashComponent>();
        _slide = GetSiblingComponent<SlideComponent>();
        _hover = GetSiblingComponent<HoverComponent>();
        _glide = GetSiblingComponent<GlideComponent>();
        _flight = GetSiblingComponent<FlyComponent>();
        _knockback = GetSiblingComponent<KnockbackComponent>();
        _machine = null; _playback = null;
        if (_target is AnimationTree { TreeRoot: AnimationNodeStateMachine machine } tree)
        {
            _machine = machine;
            _playback = tree.Get("parameters/playback").As<AnimationNodeStateMachinePlayback>();
        }
        if (_health is not null) _health.Damaged += OnDamaged;
        if (_attack is not null) _attack.Attacked += OnAttacked;
        if (_shooter is not null) _shooter.FireFired += OnFired;
        if (_actor is not null) _actor.CommandFinished += OnCommandFinished;
        _lastPosition = _body?.GlobalPosition ?? Vector2.Zero;
        _attackTime = _hurtTime = _interactTime = 0;
        _restartAction = null; _wasActive = false;
        CurrentClip = ""; Facing = Vector2.Right;
        if (_body is null || Profile is null || (_target is not (AnimatedSprite2D or AnimationPlayer) && _playback is null))
            GD.PushWarning($"[{Name}] Assign an animation profile and an AnimatedSprite2D, AnimationPlayer or root state-machine AnimationTree on a Node2D actor.");
    }

    private void DisconnectSources()
    {
        if (GodotObject.IsInstanceValid(_health)) _health!.Damaged -= OnDamaged;
        if (GodotObject.IsInstanceValid(_attack)) _attack!.Attacked -= OnAttacked;
        if (GodotObject.IsInstanceValid(_shooter)) _shooter!.FireFired -= OnFired;
        if (GodotObject.IsInstanceValid(_actor)) _actor!.CommandFinished -= OnCommandFinished;
    }

    public override void _ExitTree()
    {
        DisconnectSources();
        _body = null; _target = null; _flipSprite = null; _actor = null;
        _health = null; _attack = null; _shooter = null; _worker = null;
        _dash = null; _slide = null; _hover = null; _glide = null; _flight = null; _knockback = null;
        _machine = null; _playback = null;
        RequestReady();
        base._ExitTree();
    }

    public void RequestAction(ActorAnimationState state, float seconds)
    {
        if (!IsActive || !float.IsFinite(seconds) || seconds <= 0) return;
        switch (state)
        {
            case ActorAnimationState.Attack: _attackTime = seconds; break;
            case ActorAnimationState.Hurt: _hurtTime = seconds; break;
            case ActorAnimationState.Interact: _interactTime = seconds; break;
            default: return;
        }
        _restartAction = state;
    }

    private void OnAttacked(Vector2 target, float damage)
    {
        if (_body is not null) Face(target - _body.GlobalPosition);
        RequestAction(ActorAnimationState.Attack, Profile?.AttackSeconds ?? 0);
    }
    private void OnFired(Vector2 position, Vector2 direction)
    {
        Face(direction);
        RequestAction(ActorAnimationState.Attack, Profile?.AttackSeconds ?? 0);
    }
    private void OnDamaged(float amount, float health)
    {
        if (amount > 0) RequestAction(ActorAnimationState.Hurt, Profile?.HurtSeconds ?? 0);
    }
    private void OnCommandFinished(int action, bool success, string reason)
    {
        if (success && action == (int)ActorAction.Interact)
            RequestAction(ActorAnimationState.Interact, Profile?.InteractSeconds ?? 0);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint() || !GodotObject.IsInstanceValid(_body)) return;
        Vector2 position = _body!.GlobalPosition;
        Vector2 displacement = position - _lastPosition;
        _lastPosition = position;
        if (!IsActive || Profile is null || !GodotObject.IsInstanceValid(_target)) { _wasActive = false; return; }
        if (!_wasActive) { CurrentClip = ""; _wasActive = true; }
        float dt = double.IsFinite(delta) ? Mathf.Max(0, (float)delta) : 0;
        float threshold = float.IsFinite(Profile.MovingSpeedThreshold) ? Mathf.Max(0, Profile.MovingSpeedThreshold) : 5;
        bool moving = dt > 0 && displacement.IsFinite() && displacement.Length() > threshold * dt;
        if (_attackTime <= 0)
        {
            if (Profile.FaceAim && GodotObject.IsInstanceValid(_actor)) Face(_actor!.AimIntent);
            else if (moving) Face(displacement);
        }
        var state = SelectState(moving);
        string clip = ResolveClip(state);
        string requested = Profile.Clip(state);
        bool restart = _restartAction == state && !string.IsNullOrEmpty(requested)
            && (clip == requested || clip == requested + Profile.Suffix(Facing));
        if (clip.Length > 0 && (clip != CurrentClip || restart)) Play(clip, restart);
        if (Profile.Directions == ActorAnimationDirections.FlipHorizontal && Facing.X != 0 && GodotObject.IsInstanceValid(_flipSprite))
        {
            if (_flipSprite is AnimatedSprite2D animated) animated.FlipH = Facing.X < 0;
            else if (_flipSprite is Sprite2D sprite) sprite.FlipH = Facing.X < 0;
        }
        if (state != CurrentState || clip != CurrentClip)
            EmitSignal(SignalName.AnimationStateChanged, (int)state, clip, Facing);
        CurrentState = state; CurrentClip = clip;
        _restartAction = null;
        _attackTime = Mathf.Max(0, _attackTime - dt);
        _hurtTime = Mathf.Max(0, _hurtTime - dt);
        _interactTime = Mathf.Max(0, _interactTime - dt);
    }

    private ActorAnimationState SelectState(bool moving)
    {
        if (GodotObject.IsInstanceValid(_health) && _health!.IsDead) return ActorAnimationState.Death;
        if (_hurtTime > 0 || (GodotObject.IsInstanceValid(_knockback) && _knockback!.IsKnockedBack)) return ActorAnimationState.Hurt;
        if (_attackTime > 0) return ActorAnimationState.Attack;
        if (GodotObject.IsInstanceValid(_dash) && _dash!.IsActive && _dash.IsDashing) return ActorAnimationState.Dash;
        if (GodotObject.IsInstanceValid(_slide) && (_slide!.IsSliding || _slide.IsCollisionReduced)) return ActorAnimationState.Slide;
        if (GodotObject.IsInstanceValid(_hover) && _hover!.IsHovering) return ActorAnimationState.Hover;
        if (GodotObject.IsInstanceValid(_glide) && _glide!.IsGliding) return ActorAnimationState.Glide;
        if (GodotObject.IsInstanceValid(_flight) && _flight!.IsActive && _actor?.CanDrive(_flight) != false) return ActorAnimationState.Fly;
        if (Profile!.UseGroundedStates && _body is CharacterBody2D body && !CharacterMotion.IsOnFloor(body))
            return body.Velocity.Y < 0 ? ActorAnimationState.Jump : ActorAnimationState.Fall;
        if (GodotObject.IsInstanceValid(_worker) && _worker is not EntityComponent { IsActive: false }
            && GridWorkerPorts.IsWorking(_worker!)) return ActorAnimationState.Work;
        if (_interactTime > 0) return ActorAnimationState.Interact;
        return moving ? ActorAnimationState.Move : ActorAnimationState.Idle;
    }

    private void Face(Vector2 direction)
    {
        if (direction.IsFinite() && direction.LengthSquared() > 0.0001f && float.IsFinite(direction.LengthSquared()))
            Facing = direction.Normalized();
    }

    private string ResolveClip(ActorAnimationState state)
    {
        string name = Profile!.Clip(state);
        string suffix = Profile.Suffix(Facing);
        if (!string.IsNullOrEmpty(name))
        {
            if (HasClip(name + suffix)) return name + suffix;
            if (HasClip(name)) return name;
        }
        if (!string.IsNullOrEmpty(Profile.Idle))
        {
            if (HasClip(Profile.Idle + suffix)) return Profile.Idle + suffix;
            if (HasClip(Profile.Idle)) return Profile.Idle;
        }
        return "";
    }

    private bool HasClip(string name) => !string.IsNullOrEmpty(name) && _target switch
    {
        AnimatedSprite2D sprite => sprite.SpriteFrames is { } frames && frames.HasAnimation(name) && frames.GetFrameCount(name) > 0,
        AnimationPlayer player => player.HasAnimation(name),
        AnimationTree => _machine?.HasNode(name) == true,
        _ => false
    };

    private void Play(string clip, bool restart)
    {
        switch (_target)
        {
            case AnimatedSprite2D sprite:
                if (restart) sprite.Stop();
                sprite.Play(clip);
                break;
            case AnimationPlayer player:
                if (restart) player.Stop();
                player.Play(clip);
                break;
            case AnimationTree when _playback is not null:
                if (restart || !_playback.IsPlaying()) _playback.Start(clip, true);
                else _playback.Travel(clip);
                break;
        }
    }
}

using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Logical 2.5D height — the "off the ground" axis Godot's 2D world doesn't have. Attach to a
    /// Node2D (the entity body). It owns a <see cref="Height"/> scalar and keeps the three things
    /// that must agree about that height in sync:
    ///
    ///   • VISUAL — a sprite child is drawn <c>Height</c> px above the body, and a ground shadow
    ///     stays at the true ground position (shrinks/fades as height grows). The body itself never
    ///     moves vertically, so collision stays on the ground plane.
    ///   • DRAW ORDER — <see cref="CanvasItem.ZIndex"/> rises with height so higher things draw on top.
    ///   • COLLISION — <see cref="HeightOverlaps"/> lets projectiles/hazards test whether two height
    ///     bands intersect, so a low bullet passes UNDER a flyer and a ground hazard skips it.
    ///
    /// Genre meaning of "height": in a SIDE-SCROLLER the Y axis already is height — don't use this;
    /// use real physics (FlyComponent). This is for TOP-DOWN / ISOMETRIC games where the screen-Y is
    /// the ground plane and lift must be faked. Blind — works for flying enemies, arcing projectiles,
    /// jumping-over-obstacles in a top-down game.
    ///
    /// In the Add Node tree: EntityComponent → GameplayComponent → HeightComponent
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class HeightComponent : GameplayComponent
    {
        [ExportGroup("Height")]
        /// <summary>Current height above the ground plane, in pixels. 0 = grounded.</summary>
        [Export] public float Height { get; set; } = 0f;
        /// <summary>The vertical half-thickness of this entity's hit band. Two entities collide only
        /// when their [Height ± HalfThickness] intervals overlap. A tall flyer has a bigger band.</summary>
        [Export] public float HalfThickness { get; set; } = 16f;

        [ExportGroup("Visuals")]
        /// <summary>Optional sprite to lift. Null = auto-find the first Sprite2D/AnimatedSprite2D child,
        /// matching FlyComponent's bank-sprite lookup. The body is NOT moved — only the visual.</summary>
        [Export] public NodePath? SpritePath { get; set; }
        /// <summary>Draw the ground shadow. The shadow marks the true ground position so the player
        /// can read where the flyer actually is.</summary>
        [Export] public bool EnableShadow { get; set; } = true;
        /// <summary>Shadow color (alpha fades as height grows).</summary>
        [Export] public Color ShadowColor { get; set; } = new(0f, 0f, 0f, 0.35f);
        /// <summary>Pixels of ZIndex added per pixel of height, so a higher flyer draws above a lower
        /// one. 1:1 by default; iso games may want less.</summary>
        [Export] public float ZIndexPerPixel { get; set; } = 1f;
        /// <summary>Height (px) at which the shadow is fully faded out.</summary>
        [Export] public float ShadowFadeHeight { get; set; } = 200f;

        [Signal] public delegate void HeightChangedEventHandler(float newHeight);
        /// <summary>Fired when the entity returns to Height 0 (an arcing projectile lands, a flyer
        /// drops). Listeners can trigger impact damage, a dust burst, a landing sound.</summary>
        [Signal] public delegate void LandedEventHandler();

        private Node2D? _body;
        private Node2D? _sprite;
        private Sprite2D? _shadow;
        private int _baseZIndex;
        private bool _warnedNoSprite;

        /// <summary>True while off the ground.</summary>
        public bool IsAirborne => EffectiveHeight > 0.001f;
        public float EffectiveHeight => NonNegative(Height);
        public float EffectiveHalfThickness => NonNegative(HalfThickness);
        public float EffectiveZIndexPerPixel => float.IsFinite(ZIndexPerPixel) ? ZIndexPerPixel : 0f;
        public float EffectiveShadowFadeHeight => Mathf.Max(0.0001f, float.IsFinite(ShadowFadeHeight) ? ShadowFadeHeight : 200f);

        public override void _Ready()
        {
            base._Ready();
            _body = GetParent() as Node2D;
            if (_body == null)
            {
                GD.PushWarning($"[{Name}] HeightComponent's parent is {GetParent()?.GetType().Name ?? "null"}, not a Node2D — there is nothing to lift. Parent it under the entity body.");
                return;
            }
            _baseZIndex = _body.ZIndex;
            _sprite = ResolveSprite();
            if (_sprite == null && !Engine.IsEditorHint())
            {
                _warnedNoSprite = true;
                GD.PushWarning($"[{Name}] no sprite child found to lift — height will gate collision and ZIndex but nothing will LOOK airborne. Assign SpritePath or add a Sprite2D child.");
            }
            if (EnableShadow && !Engine.IsEditorHint()) CallDeferred(nameof(EnsureShadow));
            ApplyVisuals();
        }

        /// <summary>Set the height, driving visuals, draw order, and firing HeightChanged/Landed.
        /// Clamped ≥ 0 — there is no below-ground in this model.</summary>
        public void SetHeight(float height)
        {
            float clamped = NonNegative(height);
            bool landed = clamped <= 0.001f && EffectiveHeight > 0.001f;
            if (Mathf.IsEqualApprox(clamped, Height)) return;
            Height = clamped;
            ApplyVisuals();
            EmitSignal(SignalName.HeightChanged, Height);
            if (landed) EmitSignal(SignalName.Landed);
        }

        /// <summary>Vertical overlap test between this entity's and another's height bands.
        /// Two entities interact only when their [Height ± HalfThickness] intervals intersect —
        /// the gate ProjectileComponent and HazardComponent read.</summary>
        public bool HeightOverlaps(HeightComponent other)
        {
            if (other == null) return true;   // a target with no height is grounded — always overlappable
            float lo = Mathf.Max(EffectiveHeight - EffectiveHalfThickness, other.EffectiveHeight - other.EffectiveHalfThickness);
            float hi = Mathf.Min(EffectiveHeight + EffectiveHalfThickness, other.EffectiveHeight + other.EffectiveHalfThickness);
            return lo <= hi;
        }

        /// <summary>Convenience for the common case: does a band at <paramref name="height"/> (with
        /// half-thickness <paramref name="halfThickness"/>) overlap this entity's band?</summary>
        public bool HeightOverlaps(float height, float halfThickness)
        {
            float effectiveOtherHeight = NonNegative(height);
            float effectiveOtherHalfThickness = NonNegative(halfThickness);
            float lo = Mathf.Max(EffectiveHeight - EffectiveHalfThickness, effectiveOtherHeight - effectiveOtherHalfThickness);
            float hi = Mathf.Min(EffectiveHeight + EffectiveHalfThickness, effectiveOtherHeight + effectiveOtherHalfThickness);
            return lo <= hi;
        }

        // ── Internals ──

        private Node2D? ResolveSprite()
        {
            if (SpritePath != null && GetNodeOrNull<Node2D>(SpritePath) is { } explicit_) return explicit_;
            if (_body == null) return null;
            foreach (var child in _body.GetChildren())
                if (child is Sprite2D or AnimatedSprite2D) return (Node2D)child;
            return null;
        }

        public void EnsureShadow()
        {
            if (_body == null || !GodotObject.IsInstanceValid(_body) || Engine.IsEditorHint()) return;
            _shadow = _body.GetNodeOrNull<Sprite2D>("HeightShadow");
            if (_shadow != null) return;
            // A 1×1 white texture scaled to an ellipse; tinted by ShadowColor. Cheap and art-free —
            // replace with a real sprite by adding a child named "HeightShadow" before _Ready.
            var img = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
            img.Fill(new Color(1, 1, 1, 1));
            _shadow = new Sprite2D
            {
                Name = "HeightShadow",
                Texture = ImageTexture.CreateFromImage(img),
                Modulate = SanitizedColor(ShadowColor, new Color(0f, 0f, 0f, 0.35f)),
                Scale = new Vector2(4f, 2f),   // wide, flat ellipse reads as a ground shadow
                ZIndex = _baseZIndex - 1,       // under the body
            };
            _body.AddChild(_shadow);
        }

        private void ApplyVisuals()
        {
            if (_body == null) return;
            float height = EffectiveHeight;
            // Lift the sprite UP (negative Y) by Height; the body stays grounded for collision.
            if (_sprite != null && GodotObject.IsInstanceValid(_sprite))
                _sprite.Position = new Vector2(float.IsFinite(_sprite.Position.X) ? _sprite.Position.X : 0f, -height);

            if (EnableShadow && _shadow != null && GodotObject.IsInstanceValid(_shadow))
            {
                float t = Mathf.Clamp(height / EffectiveShadowFadeHeight, 0f, 1f);
                // Higher = smaller, fainter shadow (light source is effectively overhead).
                Color shadowColor = SanitizedColor(ShadowColor, new Color(0f, 0f, 0f, 0.35f));
                _shadow.Modulate = new Color(shadowColor, shadowColor.A * (1f - t));
                float squash = Mathf.Lerp(1f, 0.4f, t);
                _shadow.Scale = new Vector2(4f * squash, 2f * squash);
            }

            if (!Mathf.IsZeroApprox(EffectiveZIndexPerPixel))
                _body.ZIndex = _baseZIndex + (int)(height * EffectiveZIndexPerPixel);
        }

        private static float NonNegative(float value) =>
            float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;

        private static Color SanitizedColor(Color value, Color fallback) => new(
            float.IsFinite(value.R) ? value.R : fallback.R,
            float.IsFinite(value.G) ? value.G : fallback.G,
            float.IsFinite(value.B) ? value.B : fallback.B,
            float.IsFinite(value.A) ? Mathf.Clamp(value.A, 0f, 1f) : fallback.A);
    }
}

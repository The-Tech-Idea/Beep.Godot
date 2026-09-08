using Godot;

namespace Beep.ECS;

/// <summary>Final velocity arbitration, immediately before the owning controller integrates the native body.</summary>
internal static class CharacterMotion
{
    // Native contact flags still describe the pre-load body until its first integration.
    public static bool IsOnFloor(CharacterBody2D body) => ActorComponent.ForBody(body)?.RestoredFloorContact ?? body.IsOnFloor();

    public static bool HasKnockback(CharacterBody2D body)
    {
        foreach (Node child in EntityComponent.ReadDirectChildren(body))
            if (child is KnockbackComponent { IsKnockedBack: true }) return true;
        return false;
    }

    public static void Move(CharacterBody2D body)
    {
        DashComponent? dash = null;
        KnockbackComponent? knockback = null;
        WallJumpComponent? wall = null;
        GlideComponent? glide = null;
        HoverComponent? hover = null;
        SlideComponent? slide = null;
        TerrainMotionGateComponent? terrain = null;
        foreach (Node child in EntityComponent.ReadDirectChildren(body))
        {
            if (child is DashComponent { IsActive: true, IsDashing: true } d) dash ??= d;
            if (child is KnockbackComponent { IsKnockedBack: true } k) knockback ??= k;
            if (child is WallJumpComponent { IsActive: true } w) wall ??= w;
            if (child is GlideComponent { IsGliding: true } g) glide ??= g;
            if (child is HoverComponent { IsHovering: true } h) hover ??= h;
            if (child is SlideComponent { IsSliding: true } s) slide ??= s;
            if (child is TerrainMotionGateComponent { Enabled: true } gate) terrain ??= gate;
        }
        if (glide is not null) body.Velocity = glide.ApplyVelocity(body.Velocity);
        if (hover is not null) body.Velocity = hover.ApplyVelocity(body.Velocity);
        if (wall is not null) body.Velocity = wall.ApplyVelocity(body.Velocity);
        if (slide is not null) body.Velocity = slide.ApplyVelocity(body.Velocity);
        if (knockback is not null) body.Velocity = knockback.CurrentImpulse;
        else if (dash is not null) body.Velocity = dash.ApplyVelocity(body.Velocity);
        if (!body.Velocity.IsFinite()) body.Velocity = Vector2.Zero;
        if (terrain is not null && !terrain.PrepareMotion(body.Velocity * (float)body.GetPhysicsProcessDeltaTime()))
        {
            body.Velocity = Vector2.Zero;
            return;
        }
        body.MoveAndSlide();
        wall?.CompleteIntegration();
        if (ActorComponent.ForBody(body) is { } actor)
        {
            actor.RestoredFloorContact = null;
            actor.SynchronizePosition();
        }
    }
}

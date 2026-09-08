using Beep.ECS;
using Godot;
using System;

public partial class ComponentLookupSmoke : Node
{
    public bool Run()
    {
        var identity = new ActorComponent();
        var firstBody = new Node2D();
        var secondBody = new Node2D();
        var nonBody = new Node();
        if (identity.Body is not null) return Fail("Orphan actor has a body");
        firstBody.AddChild(identity);
        if (identity.Body != firstBody) return Fail("Off-tree body attachment missed");
        identity.Reparent(secondBody);
        if (identity.Body != secondBody) return Fail("Off-tree body reparent missed");
        secondBody.RemoveChild(identity);
        if (identity.Body is not null) return Fail("Removed actor retained body");
        nonBody.AddChild(identity);
        if (identity.Body is not null) return Fail("Non-Node2D parent accepted as body");
        identity.Reparent(firstBody);
        if (identity.Body != firstBody) return Fail("Actor did not recover valid body");
        firstBody.Free();
        secondBody.Free();
        nonBody.Free();
        var priorScene = GetTree().CurrentScene;
        var scene = new Node();
        GetTree().Root.AddChild(scene);
        GetTree().CurrentScene = scene;
        var owner = new Node();
        scene.AddChild(owner);
        var outside = new GridWorkClockComponent();
        AddChild(outside);
        if (GridWorkClockComponent.FindFor(owner, new("")) is not null) return Fail("Clock leaked across scene scope");
        var clock = new GridWorkClockComponent { Name = "Clock" };
        scene.AddChild(clock);
        if (GridWorkClockComponent.FindFor(owner, new("")) != clock) return Fail("Clock group discovery failed");
        if (GridWorkClockComponent.FindFor(owner, new("../Missing")) is not null) return Fail("Missing explicit clock fell back");
        if (GridWorkClockComponent.FindFor(owner, owner.GetPathTo(outside)) != outside) return Fail("Explicit clock outside scene rejected");
        scene.RemoveChild(clock);
        if (GridWorkClockComponent.FindFor(owner, new("")) is not null) return Fail("Detached clock remained discoverable");
        scene.AddChild(clock);
        if (GridWorkClockComponent.FindFor(owner, new("")) != clock) return Fail("Reattached clock not discoverable");
        GetTree().CurrentScene = priorScene;
        scene.Free();
        outside.Free();
        var mover = new CharacterBody2D();
        AddChild(mover);
        if (CharacterMotion.HasKnockback(mover)) return Fail("Empty mover has knockback");
        var inactive = new KnockbackComponent();
        var active = new KnockbackComponent();
        mover.AddChild(inactive);
        mover.AddChild(active);
        active.ApplyKnockback(Vector2.Left);
        if (!CharacterMotion.HasKnockback(mover)) return Fail("Later active ability ignored");
        active.IsActive = false;
        if (CharacterMotion.HasKnockback(mover)) return Fail("Ability state cached");
        active.IsActive = true;
        for (int i = 0; i < 100; i++) _ = CharacterMotion.HasKnockback(mover);
        long motionBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++)
            if (!CharacterMotion.HasKnockback(mover)) return Fail("Live ability lost");
        long motionAllocated = GC.GetAllocatedBytesForCurrentThread() - motionBefore;
        if (motionAllocated > 4096) return Fail($"Motion lookup allocated {motionAllocated} bytes");
        active.Free();
        if (CharacterMotion.HasKnockback(mover)) return Fail("Removed ability retained");
        mover.Free();
        var body = new Node();
        AddChild(body);
        if (EntityComponent.FindDirectComponent<Node2D>(body) is not null) return Fail("Missing component lookup");
        var first = new Node2D();
        var second = new Node2D();
        body.AddChild(first);
        body.AddChild(second);
        if (EntityComponent.FindDirectComponent<Node2D>(body) != first
            || EntityComponent.FindDirectComponent<Node2D>(body, first) != second) return Fail("Addition or self exclusion");
        body.MoveChild(second, 0);
        if (EntityComponent.FindDirectComponent<Node2D>(body) != second) return Fail("Reorder retained old first match");
        body.RemoveChild(second);
        if (EntityComponent.FindDirectComponent<Node2D>(body) != first) return Fail("Removed component cached");
        first.Free();
        if (EntityComponent.FindDirectComponent<Node2D>(body) is not null) return Fail("Freed component cached");
        body.AddChild(second);
        RemoveChild(body);
        if (EntityComponent.FindDirectComponent<Node2D>(body) != second) return Fail("Detached body lookup");
        var third = new Node2D();
        body.AddChild(third);
        body.MoveChild(third, 0);
        if (EntityComponent.FindDirectComponent<Node2D>(body) != third) return Fail("Off-tree reorder");
        AddChild(body);
        second.Reparent(this);
        if (EntityComponent.FindDirectComponent<Node2D>(this) != second
            || EntityComponent.FindDirectComponent<Node2D>(body, third) is not null) return Fail("Reparenting cache");
        for (int i = 0; i < 100; i++) _ = EntityComponent.FindDirectComponent<Node2D>(body);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++)
            if (EntityComponent.FindDirectComponent<Node2D>(body) != third) return Fail("Stable lookup changed");
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        if (allocated > 4096) return Fail($"Repeated lookup allocated {allocated} bytes");
        body.Free();
        second.Free();
        GD.Print($"[component-lookup] mutations, detach, reparent, self exclusion, live motion abilities OK; 10000 reads allocated {allocated} bytes; motion {motionAllocated} bytes");
        return true;
    }
    private static bool Fail(string message) { GD.PushError(message); return false; }
}

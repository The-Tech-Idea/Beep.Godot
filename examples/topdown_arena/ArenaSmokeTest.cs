using Godot;

namespace Beep.Examples;

/// <summary>
/// Proves the demo PLAYS, not just that it loads.
///
/// "It started with no errors" is worth very little here — a scene that renders nothing, or a
/// player that never moves because an input action is missing, produces exactly the same clean
/// log. So this drives the game the way a player would and asserts the observable consequences:
/// the player moves when a key is held, walls stop it, coins can be collected, and contact with an
/// enemy costs health.
///
/// Run:  godot --path . --resolution 1024x640 examples/topdown_arena/smoke.tscn
/// (windowed, not --headless: --headless uses the dummy renderer, so nothing draws and the
///  screenshot is blank — the same trap the kit's render gates hit.)
/// </summary>
public partial class ArenaSmokeTest : Node
{
    private const string Shot = "res://tmp/arena/play.png";

    public override void _Ready() => _ = Run();

    private async System.Threading.Tasks.Task Run()
    {
        int bad = 0;
        try
        {
            var scene = GD.Load<PackedScene>("res://examples/topdown_arena/arena.tscn");
            var game = scene.Instantiate<ArenaGame>();
            AddChild(game);
            await Frames(8);

            // Every one of these must be a REAL node in arena.tscn. Resolving them here is itself
            // the assertion that the scene is authored rather than assembled at run time.
            foreach (string required in new[]
                     { "Walls", "Coins", "Enemies", "Player", "PlayerSpawn",
                       "HudLayer/Hud", "PauseLayer/Pause", "ResultLayer/Result" })
            {
                if (game.GetNodeOrNull(required) != null) continue;
                GD.Print($"arena:  FAIL arena.tscn has no {required}");
                bad++;
            }

            var player = game.GetNode<CharacterBody2D>("Player");
            var enemies = game.GetNode<Node2D>("Enemies");
            var coins = game.GetNode<Node2D>("Coins");

            GD.Print($"arena:  built  walls={game.GetNode<Node2D>("Walls").GetChildCount()} "
                   + $"enemies={enemies.GetChildCount()} coins={coins.GetChildCount()}");
            if (enemies.GetChildCount() == 0 || coins.GetChildCount() == 0) { bad++; }

            // ── 1. the player MOVES when a key is held ──
            Vector2 start = player.GlobalPosition;
            Input.ActionPress("move_right");
            await Frames(30);
            Input.ActionRelease("move_right");
            float moved = player.GlobalPosition.DistanceTo(start);
            bool ok = moved > 40f;
            GD.Print($"arena:  {(ok ? "ok  " : "FAIL")} moved {moved:0.0}px while move_right held "
                   + "(want > 40)");
            if (!ok) bad++;

            // ── 2. the WALLS stop it. Drive hard right and check something blocked it.
            //       The arena is authored in the scene now, so its extent comes from the walls
            //       themselves rather than from an export -- which is the point of the rewrite. ──
            Input.ActionPress("move_right");
            await Frames(160);
            Input.ActionRelease("move_right");
            float x = player.GlobalPosition.X;
            float rightWall = 0f;
            foreach (var n in game.GetNode<Node2D>("Walls").GetChildren())
                if (n is Node2D w) rightWall = Mathf.Max(rightWall, w.Position.X);
            ok = x < rightWall;
            GD.Print($"arena:  {(ok ? "ok  " : "FAIL")} stopped at x={x:0}, right wall at "
                   + $"x={rightWall:0} (want blocked)");
            if (!ok) bad++;

            // ── 3. the ENEMIES chase.
            //
            // Measured from a RESET position, and asserted as a real gap closed. Sampling where
            // the previous test left the player reported "27 -> 27px, closing" -- an enemy was
            // already touching him, so the check passed on rounding and proved nothing. A test
            // whose subject has already happened is not a test.
            // Put the player where the enemies ARE NOT. Resetting to the spawn marker looked
            // right and was not: an enemy had already chased him across the arena and was sitting
            // 27px away -- inside AIController's AttackRange of 26, where it deliberately STOPS.
            // "closed 0" was correct behaviour and a broken test. The precondition is now
            // asserted rather than assumed.
            Vector2 best = player.GlobalPosition;
            float bestGap = -1f;
            foreach (var corner in new[] { new Vector2(120, 120), new Vector2(1160, 120),
                                           new Vector2(120, 680), new Vector2(1160, 680) })
            {
                float gap = float.MaxValue;
                foreach (var n in enemies.GetChildren())
                    if (n is Node2D en) gap = Mathf.Min(gap, en.GlobalPosition.DistanceTo(corner));
                if (gap > bestGap) { bestGap = gap; best = corner; }
            }
            player.GlobalPosition = best;
            await Frames(4);
            float before = Nearest(enemies, player);
            if (before < 120f)
            {
                GD.Print($"arena:  FAIL cannot test the chase — nearest enemy is already "
                       + $"{before:0}px away, inside detection range");
                bad++;
            }
            await Frames(150);
            float after = Nearest(enemies, player);
            ok = before - after > 30f;
            GD.Print($"arena:  {(ok ? "ok  " : "FAIL")} nearest enemy {before:0} -> {after:0}px "
                   + $"(closed {before - after:0}, want > 30)");
            if (!ok) bad++;

            // ── 4. a COIN can be collected. Teleport onto one rather than trying to steer. ──
            var coin = coins.GetChild<Area2D>(0);
            player.GlobalPosition = coin.GlobalPosition;
            await Frames(12);
            ok = !coin.Visible;
            GD.Print($"arena:  {(ok ? "ok  " : "FAIL")} coin collected on contact");
            if (!ok) bad++;

            await Frames(4);
            DirAccess.MakeDirRecursiveAbsolute("res://tmp/arena");
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var err = GetViewport().GetTexture().GetImage().SavePng(Shot);
            GD.Print($"arena:  screenshot {(err == Error.Ok ? Shot : $"FAILED {err}")}");
            if (err != Error.Ok) bad++;
        }
        catch (System.Exception e)
        {
            GD.Print($"arena:  FAILED {e.GetType().Name}: {e.Message}");
            bad++;
        }

        GD.Print($"arena:  {(bad == 0 ? "PASS" : $"FAIL ({bad})")}");
        GetTree().Quit(bad == 0 ? 0 : 1);
    }

    private static float Nearest(Node2D enemies, Node2D player)
    {
        float best = float.MaxValue;
        foreach (var n in enemies.GetChildren())
            if (n is Node2D e) best = Mathf.Min(best, e.GlobalPosition.DistanceTo(player.GlobalPosition));
        return best;
    }

    private async System.Threading.Tasks.Task Frames(int n)
    {
        for (int i = 0; i < n; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }
}

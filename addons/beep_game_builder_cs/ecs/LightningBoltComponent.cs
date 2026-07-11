using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Procedural 2D lightning bolt built from a Line2D. Generates a jagged
    /// zig-zag path from a start point to a target point, with optional smaller
    /// sub-branches that split off the main path. Auto-fades out and frees
    /// itself after the strike.
    ///
    /// Usage (typically driven by WeatherSystemComponent):
    ///   var bolt = new LightningBoltComponent();
    ///   container.AddChild(bolt);
    ///   bolt.Strike(startPos, endPos);
    ///
    /// The bolt is a Node2D (Line2D) so it lives in world space and renders
    /// correctly against the level. For HDR glow, set the line's default_color
    /// channels above 1.0 and enable Glow on the WorldEnvironment.
    ///
    /// Pattern from the weathersystem.txt reference: midpoint-displacement
    /// jagged path + recursive sub-branches + quick exponential fade-out.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class LightningBoltComponent : Line2D
    {
        /// <summary>Number of segments along the main path. More = smoother zig-zag.</summary>
        [Export] public int Segments { get; set; } = 14;
        /// <summary>Max perpendicular displacement (px) of each point from the straight line.</summary>
        [Export] public float Displacement { get; set; } = 40f;
        /// <summary>Probability [0..1] that each segment spawns a sub-branch.</summary>
        [Export] public float BranchChance { get; set; } = 0.15f;
        /// <summary>Seconds before the bolt fades out and frees itself.</summary>
        [Export] public float Lifetime { get; set; } = 0.25f;
        /// <summary>Color of the bolt. Push channels &gt; 1.0 for HDR glow.</summary>
        [Export] public Color EnergyColor { get; set; } = new(1.5f, 1.7f, 2.5f, 1f);

        private float _age;
        private bool _struck;

        public override void _Ready()
        {
            Width = 5f;
            DefaultColor = EnergyColor;
            // Additive blending makes overlapping segments bloom into a bright core.
            // Line2D doesn't expose blend mode directly in Godot 4.7 — set it via
            // a CanvasItemMaterial on the Material slot.
            Material = new CanvasItemMaterial
            {
                BlendMode = CanvasItemMaterial.BlendModeEnum.Add
            };
        }

        /// <summary>Build the bolt geometry from start to end and begin the fade-out.</summary>
        public void Strike(Vector2 start, Vector2 end)
        {
            ClearPoints();
            BuildPath(start, end, Segments, Displacement);
            _struck = true;
            _age = 0;
        }

        public override void _Process(double delta)
        {
            if (!_struck) return;
            _age += (float)delta;
            float t = Mathf.Clamp(_age / Lifetime, 0f, 1f);
            // Exponential fade-out — bright flash that dies quickly.
            Modulate = new Color(1, 1, 1, 1f - t);
            if (_age >= Lifetime)
            {
                _struck = false;
                QueueFree();
            }
        }

        // ────────────────────────────────────────────────────────────────
        //  Geometry
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Build the main zig-zag path by walking the straight line from start
        /// to end in `segments` steps and displacing each midpoint perpendicular
        /// to the path by a random amount (less displacement near the ends so the
        /// bolt stays anchored to start/end).
        /// </summary>
        private void BuildPath(Vector2 start, Vector2 end, int segs, float disp)
        {
            AddPoint(start);
            Vector2 direction = (end - start);
            float totalLen = direction.Length();
            if (totalLen < 1f) { AddPoint(end); return; }
            Vector2 dirNorm = direction / totalLen;
            Vector2 normal = new(-dirNorm.Y, dirNorm.X); // perpendicular

            for (int i = 1; i < segs; i++)
            {
                float progress = (float)i / segs;
                Vector2 center = start + dirNorm * (totalLen * progress);
                // Less sway near the endpoints (anchored) and more in the middle.
                float sway = (float)GD.RandRange(-disp, disp) * (1f - Mathf.Abs(progress - 0.5f));
                Vector2 pt = center + normal * sway;
                AddPoint(pt);

                // Maybe fork a sub-branch off this point.
                if (GD.Randf() < BranchChance && i < segs - 3)
                    SpawnBranch(pt, dirNorm, segs - i, disp * 0.6f);
            }
            AddPoint(end);
        }

        /// <summary>A smaller child Line2D that veers away from the main path.</summary>
        private void SpawnBranch(Vector2 origin, Vector2 mainDir, int branchSegs, float branchDisp)
        {
            var branch = new Line2D
            {
                Width = Width * 0.5f,
                DefaultColor = DefaultColor,
                Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }
            };
            AddChild(branch);

            // Veer the branch off at a random angle from the main direction.
            float angleOffset = (float)GD.RandRange(0.3, 0.7) * (GD.Randf() > 0.5f ? -1f : 1f);
            Vector2 branchDir = mainDir.Rotated(angleOffset);
            Vector2 branchNormal = new(-branchDir.Y, branchDir.X);
            float segLen = 15f;

            branch.AddPoint(Vector2.Zero); // relative to this bolt's origin
            Vector2 cur = Vector2.Zero;
            int count = Mathf.Max(2, branchSegs / 2);
            for (int i = 1; i < count; i++)
            {
                float sway = (float)GD.RandRange(-branchDisp, branchDisp);
                cur += branchDir * segLen + branchNormal * sway;
                branch.AddPoint(cur);
            }

            // Position the branch at the fork point and fade it with the parent.
            branch.Position = ToLocal(origin);
            branch.Modulate = new Color(1, 1, 1, 0.8f);
        }
    }
}

using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Drifting SPRITE clouds, as an alternative to the procedural density field.
    ///
    /// WHY THIS EXISTS
    /// ---------------
    /// The shader-based cloud renders a tiling noise field, and at the resolution it samples it
    /// reads as blocky low-frequency mush with visible tile seams — repeatedly and correctly
    /// judged as the worst part of the weather system. Noise makes convincing FOG; it does not
    /// make convincing clouds, because a cloud has a silhouette and noise has none.
    ///
    /// Drawn cloud art has that silhouette for free. This layer drifts a set of sprites across the
    /// view at several depths, which is the standard 2D approach and looks right without any
    /// tuning at all.
    ///
    /// PARALLAX BY SIZE. The shipped set gives each cloud shape at five sizes, and that maps
    /// directly onto depth: the largest sprites are nearest and drift fastest, the smallest are
    /// far and barely move. Rendering all sizes at one speed is what makes sprite clouds look like
    /// wallpaper, so size and speed are tied together here rather than exposed separately.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class CloudSpriteLayer : Node2D
    {
        /// <summary>The cloud sprites to drift. Empty = this layer draws nothing and says so.</summary>
        [Export] public Texture2D[] Sprites { get; set; } = System.Array.Empty<Texture2D>();

        [Export(PropertyHint.Range, "0,60,1")] public int Count { get; set; } = 14;

        /// <summary>Base drift, px/sec, for a NEAR cloud. Far ones are scaled down from it.</summary>
        [Export] public float DriftSpeed { get; set; } = 22f;

        /// <summary>Normalized 2D wind direction. Side-view platformers usually want mostly X.</summary>
        [Export] public Vector2 WindDirection { get; set; } = Vector2.Right;

        /// <summary>Overall opacity — the weather system drives this as cover thickens.</summary>
        [Export] public float Opacity { get; set; } = 1f;

        /// <summary>Tint. Weather darkens it: white in fair cover, grey overcast, near-black storm.</summary>
        [Export] public Color Tint { get; set; } = Colors.White;

        /// <summary>Area the clouds inhabit. Set from the viewport by the weather system.</summary>
        [Export] public Vector2 Field { get; set; } = new(1280, 720);

        /// <summary>Normalized top of the sky band used by sprite clouds.</summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float BandTop { get; set; } = 0.02f;

        /// <summary>Normalized bottom of the sky band used by sprite clouds.</summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float BandBottom { get; set; } = 0.34f;

        /// <summary>Scale applied to the smallest/farthest cloud art.</summary>
        [Export] public float FarScale { get; set; } = 0.9f;

        /// <summary>Scale applied to the largest/nearest cloud art.</summary>
        [Export] public float NearScale { get; set; } = 1.65f;

        [Export] public int Seed { get; set; } = 9271;

        private readonly List<Sprite2D> _clouds = new();
        private readonly List<float> _speed = new();
        private readonly List<Vector2> _baseScale = new();
        private bool _warned;

        public override void _Ready() => Rebuild();

        /// <summary>
        /// IDEMPOTENT — clears what it made before making it again. Every setter can call this and
        /// the editor calls setters freely; an append-only version would stack a new sky on the old
        /// one on every change.
        /// </summary>
        public void Rebuild()
        {
            foreach (var c in _clouds) c.QueueFree();
            _clouds.Clear();
            _speed.Clear();
            _baseScale.Clear();

            if (Sprites.Length == 0)
            {
                if (!_warned)
                {
                    _warned = true;
                    GD.PushWarning($"[{Name}] CloudSpriteLayer has no Sprites assigned, so it "
                                 + "draws nothing. Point it at cloud textures (the addon ships a "
                                 + "set under textures/clouds/), or remove the layer.");
                }
                return;
            }

            var rng = new RandomNumberGenerator { Seed = (ulong)Seed };
            float top = Mathf.Clamp(Mathf.Min(BandTop, BandBottom), 0f, 1f);
            float bottom = Mathf.Clamp(Mathf.Max(BandTop, BandBottom), top + 0.02f, 1f);
            float bandHeight = Mathf.Max(24f, (bottom - top) * Field.Y);
            float maxWidth = 1f;
            foreach (var tex in Sprites)
                if (tex != null) maxWidth = Mathf.Max(maxWidth, tex.GetWidth());

            for (int i = 0; i < Count; i++)
            {
                var tex = Sprites[rng.RandiRange(0, Sprites.Length - 1)];
                // Depth is seeded per cloud and then lightly influenced by the source art size.
                // This keeps the shipped size variants useful without letting one filename family
                // dominate the whole sky.
                float sourceSize = Mathf.Clamp(tex.GetWidth() / maxWidth, 0.25f, 1f);
                float near = Mathf.Clamp(Mathf.Lerp(0.2f, 1f, rng.Randf()) * Mathf.Lerp(0.8f, 1.05f, sourceSize), 0.18f, 1f);
                float scale = Mathf.Lerp(FarScale, NearScale, near) * rng.RandfRange(0.92f, 1.08f);
                var s = new Sprite2D
                {
                    Texture = tex,
                    Centered = true,
                    Position = new Vector2(rng.RandfRange(-0.15f, 1.15f) * Field.X,
                                           top * Field.Y + rng.RandfRange(0f, bandHeight)),
                    Scale = new Vector2(scale, scale),
                    // Far clouds sit fainter as well as smaller — aerial perspective, and it is
                    // what stops the far layer reading as clutter.
                    Modulate = new Color(1, 1, 1, Mathf.Lerp(0.42f, 0.92f, near)),
                    ZIndex = Mathf.RoundToInt(near * 10f),
                };
                AddChild(s);
                _clouds.Add(s);
                _speed.Add(Mathf.Lerp(0.25f, 1f, near) * rng.RandfRange(0.8f, 1.2f));
                _baseScale.Add(s.Scale);
            }
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint() || _clouds.Count == 0) return;

            Modulate = Tint with { A = Mathf.Clamp(Opacity, 0f, 1f) };

            for (int i = 0; i < _clouds.Count; i++)
            {
                var c = _clouds[i];
                Vector2 dir = WindDirection.LengthSquared() > 0.0001f
                    ? WindDirection.Normalized()
                    : Vector2.Right;
                c.Position += dir * DriftSpeed * _speed[i] * (float)delta;
                c.Scale = _baseScale[i];

                // Wrap round the far side once fully off-screen, using the sprite's own width so a
                // big cloud does not pop while its trailing edge is still visible.
                float w = (c.Texture?.GetWidth() ?? 0f) * Mathf.Abs(c.Scale.X);
                float h = (c.Texture?.GetHeight() ?? 0f) * Mathf.Abs(c.Scale.Y);
                float top = Mathf.Clamp(Mathf.Min(BandTop, BandBottom), 0f, 1f);
                float bottom = Mathf.Clamp(Mathf.Max(BandTop, BandBottom), top + 0.02f, 1f);
                float bandHeight = Mathf.Max(24f, (bottom - top) * Field.Y);
                if (dir.X >= 0f && c.Position.X - w > Field.X)
                    c.Position = new Vector2(-w, top * Field.Y + GD.Randf() * bandHeight);
                else if (dir.X < 0f && c.Position.X + w < 0f)
                    c.Position = new Vector2(Field.X + w, top * Field.Y + GD.Randf() * bandHeight);

                if (dir.Y >= 0f && c.Position.Y - h > Field.Y)
                    c.Position = new Vector2(c.Position.X, top * Field.Y - h);
                else if (dir.Y < 0f && c.Position.Y + h < 0f)
                    c.Position = new Vector2(c.Position.X, bottom * Field.Y + h);
            }
        }
    }
}

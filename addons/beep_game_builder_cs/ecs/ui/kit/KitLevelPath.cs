using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// The zig-zag level map — CATALOGUE-FROM-ART.md F.2's `LevelNodeGrid`, and the screen the
    /// puzzle and platformer genres are built around (`level_map.tscn`, `level_select.tscn`).
    ///
    /// Not <see cref="KitTree"/> with different data: a tree branches and a level path does not.
    /// A path is a SEQUENCE with one current position, so it owns a serpentine layout, per-node
    /// star scores, and the "you are here" marker a tree has no concept of.
    ///
    /// Node states follow the settled rules: a locked node is a dark silhouette with **no
    /// number** (skilltree.md), and stars use the same drained-not-hidden treatment as
    /// <see cref="KitStarRating"/> so a player can see what a level is worth before playing it.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitLevelPath : KitControl
    {
        public enum LevelState { Locked, Available, Complete }

        public sealed class Level
        {
            public string Label = "";
            public LevelState State = LevelState.Locked;
            /// <summary>0..3 earned. Only shown when complete.</summary>
            public int Stars;
        }

        public readonly List<Level> Levels = new();

        /// <summary>Nodes per row before the path reverses — the serpentine.</summary>
        [Export(PropertyHint.Range, "2,10,1")] public int PerRow { get => _per; set { _per = Mathf.Max(2, value); QueueRedraw(); } }
        private int _per = 4;

        /// <summary>Index of the player's current position. -1 for none.</summary>
        [Export] public int Current { get => _cur; set { _cur = value; QueueRedraw(); } }
        private int _cur = 2;
        private int _hover = -1;

        [Signal] public delegate void LevelActivatedEventHandler(int index);

        public override void _Ready()
        {
            base._Ready();
            MouseFilter = MouseFilterEnum.Stop;
            if (Levels.Count == 0)
                for (int i = 0; i < 8; i++)
                    Levels.Add(new Level
                    {
                        Label = (i + 1).ToString(),
                        State = i < 3 ? LevelState.Complete : i == 3 ? LevelState.Available : LevelState.Locked,
                        Stars = i < 3 ? 3 - i : 0,
                    });
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                int rows = Mathf.CeilToInt(Levels.Count / (float)_per);
                CustomMinimumSize = new Vector2(fs * 3.6f * _per, fs * 4.2f * rows);
            }
        }

        private Vector2 NodeAt(int i)
        {
            int row = i / _per, col = i % _per;
            // Serpentine: odd rows run right-to-left, which is what makes it a PATH and not a grid.
            if (row % 2 == 1) col = _per - 1 - col;
            float cw = Size.X / _per;
            int rows = Mathf.Max(1, Mathf.CeilToInt(Levels.Count / (float)_per));
            float ch = Size.Y / rows;
            return new Vector2(cw * (col + 0.5f), ch * (row + 0.5f));
        }

        private float NodeRadius()
        {
            int rows = Mathf.Max(1, Mathf.CeilToInt(Levels.Count / (float)_per));
            return Mathf.Min(Size.X / _per, Size.Y / rows) * 0.28f;
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseMotion mm)
            {
                int next = HitLevel(mm.Position);
                if (next != _hover)
                {
                    _hover = next;
                    QueueRedraw();
                }
                return;
            }

            if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
                return;
            int hit = HitLevel(mb.Position);
            if (hit >= 0)
            {
                if (Levels[hit].State == LevelState.Locked) return;
                EmitSignal(SignalName.LevelActivated, hit);
                AcceptEvent();
            }
        }

        private int HitLevel(Vector2 p)
        {
            float r = NodeRadius();
            for (int i = 0; i < Levels.Count; i++)
                if (p.DistanceTo(NodeAt(i)) <= r * 1.2f) return i;
            return -1;
        }

        public override void _Draw()
        {
            if (Size.X < 30f || Size.Y < 30f || Levels.Count == 0) return;

            Color face = FaceColor();
            Color ink = InkColor();
            var font = KitFont();
            int fs = UiSurface.FontSize(this);
            float r = NodeRadius();

            // The track, drawn first so nodes sit on it. Dashed beyond the furthest unlocked
            // level: "dashed stroke = path / provisional" (4 references).
            for (int i = 0; i < Levels.Count - 1; i++)
            {
                Vector2 a = NodeAt(i), b = NodeAt(i + 1);
                bool solid = Levels[i].State != LevelState.Locked;
                Color col = solid
                    ? new Color(face.R * 1.25f, face.G * 1.25f, face.B * 1.2f, 1f)
                    : new Color(face.R * 0.6f, face.G * 0.6f, face.B * 0.65f, 1f);
                float w = Mathf.Max(2f, r * 0.28f);
                if (solid) DrawLine(a, b, col, w);
                else
                {
                    int seg = 6;
                    for (int s = 0; s < seg; s += 2)
                        DrawLine(a.Lerp(b, s / (float)seg), a.Lerp(b, (s + 1) / (float)seg), col, w);
                }
            }

            for (int i = 0; i < Levels.Count; i++)
            {
                Level lv = Levels[i];
                Vector2 p = NodeAt(i);

                Color plate = lv.State switch
                {
                    LevelState.Complete => UiSurface.Semantic(this, UiSurface.Role.Success),
                    LevelState.Available => UiSurface.Semantic(this, UiSurface.Role.Accent),
                    _ => new Color(face.R * 0.28f, face.G * 0.28f, face.B * 0.32f, 1f),
                };

                DrawCircle(p, r, plate);
                DrawArc(p, r, 0f, Mathf.Tau, 28, ink, Mathf.Max(1.5f, r * 0.14f));

                if (lv.State == LevelState.Available)
                    DrawArc(p, r * 1.18f, 0f, Mathf.Tau, 32,
                            UiSurface.Semantic(this, UiSurface.Role.Info),
                            Mathf.Max(1.8f, r * 0.10f));

                // "You are here": a ring outside the node, so it does not restyle the node
                // itself. The COLOUR comes from the palette rather than a hardcoded cream, so it
                // reskins with the theme like every other selection cue.
                if (i == _cur)
                    DrawArc(p, r * 1.28f, 0f, Mathf.Tau, 32,
                            UiSurface.Semantic(this, UiSurface.Role.Accent),
                            Mathf.Max(2f, r * 0.14f));

                if (i == _hover && lv.State != LevelState.Locked && i != _cur)
                    DrawArc(p, r * 1.30f, 0f, Mathf.Tau, 32,
                            UiSurface.Semantic(this, UiSurface.Role.Info),
                            Mathf.Max(1.5f, r * 0.09f));

                // A locked node shows NO number.
                if (lv.State != LevelState.Locked && font != null && !string.IsNullOrEmpty(lv.Label))
                {
                    int lf = UiSurface.FitRole(this, UiSurface.TextRole.Value,
                                               new Vector2(r * 1.45f, r * 0.90f),
                                               lv.Label, font, min: 8);
                    Vector2 m = font.GetStringSize(lv.Label, HorizontalAlignment.Left, -1, lf);
                    DrawText(font, new Vector2(p.X - m.X * 0.5f, p.Y + m.Y * 0.32f),
                               lv.Label, lf, UiSurface.Luminance(plate) > 0.5f
                                   ? new Color(0.10f, 0.09f, 0.08f) : new Color(0.98f, 0.96f, 0.92f));
                }

                if (lv.State != LevelState.Complete) continue;
                // Stars beneath, drained when unearned rather than omitted.
                Color star = UiSurface.Semantic(this, UiSurface.Role.Warning);
                float l = UiSurface.Luminance(star);
                Color dim = new(Mathf.Lerp(star.R, l, 0.9f) * 0.6f, Mathf.Lerp(star.G, l, 0.9f) * 0.6f,
                                Mathf.Lerp(star.B, l, 0.9f) * 0.6f, 1f);
                float sr = r * 0.26f;
                for (int s = 0; s < 3; s++)
                    DrawCircle(p + new Vector2((s - 1) * sr * 2.4f, r * 1.15f), sr,
                               s < lv.Stars ? star : dim);
            }
        }
    }
}

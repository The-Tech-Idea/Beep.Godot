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

        [Export]
        public string[] LevelLabels
        {
            get
            {
                var labels = new string[Levels.Count];
                for (int i = 0; i < Levels.Count; i++)
                    labels[i] = Levels[i].Label;
                return labels;
            }
            set => SetLevelLabels(value);
        }

        [Export]
        public int[] LevelStates
        {
            get
            {
                var states = new int[Levels.Count];
                for (int i = 0; i < Levels.Count; i++)
                    states[i] = (int)Levels[i].State;
                return states;
            }
            set => SetLevelStates(value);
        }

        [Export]
        public int[] LevelStars
        {
            get
            {
                var stars = new int[Levels.Count];
                for (int i = 0; i < Levels.Count; i++)
                    stars[i] = Levels[i].Stars;
                return stars;
            }
            set => SetLevelStars(value);
        }

        public void SetLevels(IEnumerable<Level>? levels, int current = -1)
        {
            List<Level> next = NormalizeLevels(levels);
            int normalizedCurrent = NormalizeCurrent(current, next.Count);
            if (SameLevels(Levels, next) && _cur == normalizedCurrent)
                return;
            Levels.Clear();
            Levels.AddRange(next);
            _cur = normalizedCurrent;
            RefreshLevels();
        }

        public void SetLevelLabels(string[]? labels)
        {
            int count = labels?.Length ?? 0;
            bool changed = Levels.Count != count;
            while (Levels.Count > count)
                Levels.RemoveAt(Levels.Count - 1);
            for (int i = 0; i < count; i++)
            {
                EnsureLevel(i);
                string next = labels![i] ?? "";
                if (Levels[i].Label == next) continue;
                Levels[i].Label = next;
                changed = true;
            }
            if (!changed) return;
            RefreshLevels();
        }

        public void SetLevelStates(int[]? states)
        {
            if (states == null)
            {
                bool changed = false;
                for (int i = 0; i < Levels.Count; i++)
                {
                    if (Levels[i].State == LevelState.Locked) continue;
                    Levels[i].State = LevelState.Locked;
                    changed = true;
                }
                if (!changed) return;
                RefreshLevels();
                return;
            }

            bool updated = false;
            for (int i = 0; i < states.Length; i++)
            {
                EnsureLevel(i);
                LevelState next = StateFromOrdinal(states[i]);
                if (Levels[i].State == next) continue;
                Levels[i].State = next;
                updated = true;
            }
            for (int i = states.Length; i < Levels.Count; i++)
            {
                if (Levels[i].State == LevelState.Locked) continue;
                Levels[i].State = LevelState.Locked;
                updated = true;
            }
            if (!updated) return;
            RefreshLevels();
        }

        public void SetLevelStars(int[]? stars)
        {
            if (stars == null)
            {
                bool changed = false;
                for (int i = 0; i < Levels.Count; i++)
                {
                    if (Levels[i].Stars == 0) continue;
                    Levels[i].Stars = 0;
                    changed = true;
                }
                if (!changed) return;
                RefreshLevels();
                return;
            }

            bool updated = false;
            for (int i = 0; i < stars.Length; i++)
            {
                EnsureLevel(i);
                int next = Mathf.Clamp(stars[i], 0, 3);
                if (Levels[i].Stars == next) continue;
                Levels[i].Stars = next;
                updated = true;
            }
            for (int i = stars.Length; i < Levels.Count; i++)
            {
                if (Levels[i].Stars == 0) continue;
                Levels[i].Stars = 0;
                updated = true;
            }
            if (!updated) return;
            RefreshLevels();
        }

        public void AddLevel(string label, LevelState state = LevelState.Locked, int stars = 0)
        {
            Levels.Add(new Level { Label = label ?? "", State = StateFromOrdinal((int)state), Stars = Mathf.Clamp(stars, 0, 3) });
            RefreshLevels();
        }

        public void RefreshLevels()
        {
            if (Levels.Count == 0)
            {
                _cur = -1;
                _hover = -1;
                _focusIndex = -1;
            }
            else
            {
                _cur = Mathf.Clamp(_cur, -1, Levels.Count - 1);
                if (_hover >= Levels.Count)
                    _hover = -1;
                if (_focusIndex >= Levels.Count || _focusIndex < 0)
                    _focusIndex = FirstPlayableIndex();
            }
            if (IsInsideTree())
            {
                KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
                UpdateMinimumSize();
            }
            QueueRedraw();
        }

        private void EnsureLevel(int index)
        {
            while (Levels.Count <= index)
                Levels.Add(new Level());
        }

        private static List<Level> NormalizeLevels(IEnumerable<Level>? levels)
        {
            var next = new List<Level>();
            if (levels == null)
                return next;

            foreach (Level? level in levels)
            {
                next.Add(new Level
                {
                    Label = level?.Label ?? "",
                    State = StateFromOrdinal((int)(level?.State ?? LevelState.Locked)),
                    Stars = Mathf.Clamp(level?.Stars ?? 0, 0, 3),
                });
            }
            return next;
        }

        private static bool SameLevels(IReadOnlyList<Level> left, IReadOnlyList<Level> right)
        {
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
            {
                if ((left[i].Label ?? "") != right[i].Label) return false;
                if (StateFromOrdinal((int)left[i].State) != right[i].State) return false;
                if (Mathf.Clamp(left[i].Stars, 0, 3) != right[i].Stars) return false;
            }
            return true;
        }

        private static LevelState StateFromOrdinal(int value)
            => (LevelState)Mathf.Clamp(value, (int)LevelState.Locked, (int)LevelState.Complete);

        /// <summary>Nodes per row before the path reverses — the serpentine.</summary>
        [Export(PropertyHint.Range, "2,10,1")]
        public int PerRow
        {
            get => _per;
            set
            {
                int next = Mathf.Max(2, value);
                if (_per == next) return;
                _per = next;
                KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
                UpdateMinimumSize();
                QueueRedraw();
            }
        }
        private int _per = 4;

        /// <summary>Index of the player's current position. -1 for none.</summary>
        [Export] public int Current
        {
            get => _cur;
            set
            {
                int next = NormalizeCurrent(value);
                if (_cur == next) return;
                _cur = next;
                QueueRedraw();
            }
        }
        private int _cur = 2;
        private int _hover = -1;
        private int _focusIndex = -1;
        private bool _eventsHooked;

        [Signal] public delegate void LevelActivatedEventHandler(int index);

        public override void _Ready()
        {
            base._Ready();
            ApplyInputDefaults(MouseFilterEnum.Stop, FocusModeEnum.All);
            if (!_eventsHooked)
            {
                MouseExited += ClearHover;
                _eventsHooked = true;
            }
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
            _focusIndex = FirstPlayableIndex();
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (KitChrome.ShouldClearPointerState(this, what))
                ClearHover();
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
            if (@event is InputEventKey key)
            {
                Vector2I dir = KitChrome.DirectionFromKey(key);
                if (dir != Vector2I.Zero)
                {
                    MoveFocus(dir);
                    AcceptEvent();
                    return;
                }
                if (KitChrome.IsConfirmKey(key) && _focusIndex >= 0)
                {
                    ActivateLevel(_focusIndex);
                    AcceptEvent();
                    return;
                }
            }

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
                GrabFocus();
                ActivateLevel(hit);
                AcceptEvent();
            }
        }

        private void ActivateLevel(int index)
        {
            if (index < 0 || index >= Levels.Count || Levels[index].State == LevelState.Locked) return;
            _focusIndex = index;
            EmitSignal(SignalName.LevelActivated, index);
            QueueRedraw();
        }

        private int FirstPlayableIndex()
        {
            if (_cur >= 0 && _cur < Levels.Count && Levels[_cur].State != LevelState.Locked) return _cur;
            for (int i = 0; i < Levels.Count; i++)
                if (Levels[i].State != LevelState.Locked) return i;
            return Levels.Count > 0 ? 0 : -1;
        }

        private int NormalizeCurrent(int value)
            => NormalizeCurrent(value, Levels.Count);

        private static int NormalizeCurrent(int value, int levelCount)
            => levelCount == 0
                ? -1
                : Mathf.Clamp(value, -1, levelCount - 1);

        private void ClearHover()
        {
            if (_hover < 0) return;
            _hover = -1;
            QueueRedraw();
        }

        private void MoveFocus(Vector2I dir)
        {
            if (Levels.Count == 0) return;
            if (_focusIndex < 0) _focusIndex = FirstPlayableIndex();
            if (dir.X <= -9999) _focusIndex = 0;
            else if (dir.X >= 9999) _focusIndex = Levels.Count - 1;
            else _focusIndex = Mathf.Clamp(_focusIndex + dir.X + dir.Y * _per, 0, Levels.Count - 1);
            QueueRedraw();
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            int rows = Mathf.Max(1, Mathf.CeilToInt(Levels.Count / (float)_per));
            return new Vector2(fs * 3.6f * _per, fs * 4.2f * rows);
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
            if (Size.X < 30f || Size.Y < 30f) return;
            if (Levels.Count == 0)
            {
                KitChrome.DrawEmptyPreview(this, KitChrome.GenreOf(this), new Rect2(Vector2.Zero, Size),
                                           ActiveShape, "Levels");
                return;
            }

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

                if (HasFocus() && i == _focusIndex)
                    DrawArc(p, r * 1.42f, 0f, Mathf.Tau, 36,
                            UiSurface.Semantic(this, UiSurface.Role.Info),
                            Mathf.Max(1.8f, r * 0.10f));

                // A locked node shows NO number.
                if (lv.State != LevelState.Locked && font != null && !string.IsNullOrEmpty(lv.Label))
                {
                    string label = KitCase(lv.Label);
                    float labelWidth = r * 1.45f;
                    int lf = UiSurface.FitRole(this, UiSurface.TextRole.Value,
                                               new Vector2(labelWidth, r * 0.90f),
                                               label, font, min: 8);
                    label = KitChrome.EllipsizeText(font, label, lf, labelWidth);
                    if (string.IsNullOrEmpty(label)) continue;
                    Vector2 m = font.GetStringSize(label, HorizontalAlignment.Left, -1, lf);
                    DrawText(font, new Vector2(p.X - m.X * 0.5f, p.Y + m.Y * 0.32f),
                               label, lf, UiSurface.Luminance(plate) > 0.5f
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

            KitChrome.DrawFocusRing(this, KitChrome.GenreOf(this), new Rect2(Vector2.Zero, Size), ActiveShape, 0.8f);
        }
    }
}

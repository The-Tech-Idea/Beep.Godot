using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A radar / spider chart — INDEX.md lists this as "a missing primitive, fully procedural,
    /// useful to racing, rpg and strategy", measured from `racing3.png`.
    ///
    /// It is the one comparison widget in the folder: vehicle stats, class loadouts and faction
    /// traits are all "five numbers you compare at a glance", and a stack of bars answers "how
    /// big is each" while a radar answers "what SHAPE is this thing" — which is the actual
    /// question on a character-select or vehicle-select screen.
    ///
    /// Fully procedural by design: no art, so it reskins with the palette like everything else.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitRadarChart : KitControl
    {
        /// <summary>Axis labels. The chart draws one spoke per entry.</summary>
        public readonly List<string> Axes = new();
        /// <summary>Values 0..1, parallel to <see cref="Axes"/>.</summary>
        public readonly List<float> Values = new();

        [Export]
        public string[] AxisLabels
        {
            get => Axes.ToArray();
            set => SetAxisLabels(value);
        }

        [Export]
        public float[] AxisValues
        {
            get => Values.ToArray();
            set => SetAxisValues(value);
        }

        public void SetData(IEnumerable<string>? axes, IEnumerable<float>? values)
        {
            List<string> nextAxes = NormalizeAxes(axes);
            List<float> nextValues = NormalizeValues(values);
            NormalizeParallelData(nextAxes, nextValues);
            if (SameStrings(Axes, nextAxes) && SameFloats(Values, nextValues))
                return;
            Axes.Clear();
            Values.Clear();
            Axes.AddRange(nextAxes);
            Values.AddRange(nextValues);
            RefreshData();
        }

        public void SetAxisLabels(string[]? labels)
        {
            int count = labels?.Length ?? 0;
            bool changed = Axes.Count != count || Values.Count != count;
            while (Axes.Count > count)
                Axes.RemoveAt(Axes.Count - 1);
            while (Values.Count > count)
                Values.RemoveAt(Values.Count - 1);
            for (int i = 0; i < count; i++)
            {
                if (i >= Axes.Count)
                    Axes.Add("");
                if (i >= Values.Count)
                    Values.Add(0f);
                string next = labels![i] ?? "";
                if (Axes[i] == next) continue;
                Axes[i] = next;
                changed = true;
            }
            if (!changed) return;
            RefreshData();
        }

        public void SetAxisValues(float[]? values)
        {
            int count = values?.Length ?? 0;
            bool changed = Values.Count != count || Axes.Count < count;
            while (Values.Count > count)
                Values.RemoveAt(Values.Count - 1);
            for (int i = 0; i < count; i++)
            {
                if (i >= Values.Count)
                    Values.Add(0f);
                if (i >= Axes.Count)
                    Axes.Add("");
                float next = Mathf.Clamp(values![i], 0f, 1f);
                if (Mathf.IsEqualApprox(Values[i], next)) continue;
                Values[i] = next;
                changed = true;
            }
            if (!changed) return;
            RefreshData();
        }

        public void AddAxis(string axis, float value)
        {
            Axes.Add(axis ?? "");
            Values.Add(Mathf.Clamp(value, 0f, 1f));
            RefreshData();
        }

        public bool RemoveAxis(int index)
        {
            int count = Mathf.Max(Axes.Count, Values.Count);
            if (index < 0 || index >= count)
                return false;

            if (index < Axes.Count)
                Axes.RemoveAt(index);
            if (index < Values.Count)
                Values.RemoveAt(index);
            RefreshData();
            return true;
        }

        public void ClearAxes()
        {
            if (Axes.Count == 0 && Values.Count == 0)
                return;

            Axes.Clear();
            Values.Clear();
            RefreshData();
        }

        public void RefreshData()
        {
            int count = Count();
            if (count <= 0)
                _activeAxis = -1;
            else if (_activeAxis >= count)
                _activeAxis = count - 1;
            RefreshVisualAndRedraw();
        }

        [Export]
        public UiSurface.Role Role
        {
            get => _role;
            set { if (_role == value) return; _role = value; RefreshVisualAndRedraw(); }
        }
        private UiSurface.Role _role = UiSurface.Role.Accent;

        /// <summary>Concentric guide rings. 0 draws none.</summary>
        [Export(PropertyHint.Range, "0,6,1")]
        public int Rings
        {
            get => _rings;
            set
            {
                int next = Mathf.Clamp(value, 0, 6);
                if (_rings == next) return;
                _rings = next;
                RefreshVisualAndRedraw();
            }
        }
        private int _rings = 3;

        [Export]
        public bool ShowLabels
        {
            get => _showLabels;
            set { if (_showLabels == value) return; _showLabels = value; RefreshVisualAndRedraw(); }
        }
        private bool _showLabels = true;

        [Export]
        public bool Editable
        {
            get => _editable;
            set
            {
                if (_editable == value) return;
                _editable = value;
                if (!_editable) _activeAxis = -1;
                RefreshVisualAndRedraw();
            }
        }
        private bool _editable = true;

        [Signal] public delegate void ValueChangedEventHandler(int axis, float value);

        private int _activeAxis = -1;

        public override void _Ready()
        {
            base._Ready();
            ApplyInputDefaults(MouseFilterEnum.Stop, FocusModeEnum.All);
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            return new Vector2(fs * 8f, fs * 8f);
        }

        public void SetValue(int i, float v)
        {
            if (i < 0 || i >= Values.Count) return;
            float next = Mathf.Clamp(v, 0f, 1f);
            if (Mathf.IsEqualApprox(Values[i], next)) return;
            Values[i] = next;
            RefreshVisualAndRedraw();
        }

        private void RefreshVisualAndRedraw()
        {
            QueueRedraw();
        }

        private static List<string> NormalizeAxes(IEnumerable<string>? axes)
        {
            var next = new List<string>();
            if (axes == null)
                return next;

            foreach (string? axis in axes)
                next.Add(axis ?? "");
            return next;
        }

        private static List<float> NormalizeValues(IEnumerable<float>? values)
        {
            var next = new List<float>();
            if (values == null)
                return next;

            foreach (float value in values)
                next.Add(Mathf.Clamp(value, 0f, 1f));
            return next;
        }

        private static void NormalizeParallelData(List<string> axes, List<float> values)
        {
            int count = Mathf.Max(axes.Count, values.Count);
            while (axes.Count < count)
                axes.Add("");
            while (values.Count < count)
                values.Add(0f);
        }

        private static bool SameStrings(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
                if ((left[i] ?? "") != right[i]) return false;
            return true;
        }

        private static bool SameFloats(IReadOnlyList<float> left, IReadOnlyList<float> right)
        {
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
                if (!Mathf.IsEqualApprox(Mathf.Clamp(left[i], 0f, 1f), right[i])) return false;
            return true;
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (!Editable)
            {
                _activeAxis = -1;
                return;
            }
            switch (@event)
            {
                case InputEventKey key:
                    Vector2I dir = KitChrome.DirectionFromKey(key);
                    if (dir.X != 0)
                    {
                        int n = Count();
                        if (n <= 0) return;
                        if (_activeAxis < 0) _activeAxis = 0;
                        if (dir.X <= -9999) _activeAxis = 0;
                        else if (dir.X >= 9999) _activeAxis = n - 1;
                        else _activeAxis = Mathf.PosMod(_activeAxis + dir.X, n);
                        QueueRedraw();
                        AcceptEvent();
                    }
                    else if (dir.Y != 0 && _activeAxis >= 0)
                    {
                        float delta = dir.Y < 0 ? 0.05f : -0.05f;
                        SetValue(_activeAxis, Values[_activeAxis] + delta);
                        EmitSignal(SignalName.ValueChanged, _activeAxis, Values[_activeAxis]);
                        AcceptEvent();
                    }
                    break;
                case InputEventMouseButton { ButtonIndex: MouseButton.Left } mb:
                    if (mb.Pressed)
                    {
                        GrabFocus();
                        _activeAxis = NearestAxis(mb.Position);
                        ApplyPointerValue(mb.Position);
                    }
                    else
                    {
                        _activeAxis = -1;
                    }
                    AcceptEvent();
                    break;
                case InputEventMouseMotion mm when _activeAxis >= 0:
                    ApplyPointerValue(mm.Position);
                    AcceptEvent();
                    break;
            }
        }

        private int Count() => Mathf.Min(Axes.Count, Values.Count);

        private Vector2 Centre() => Size * 0.5f;

        private float Radius()
        {
            float d = Mathf.Min(Size.X, Size.Y);
            return d * 0.5f * (ShowLabels ? 0.68f : 0.88f);
        }

        private Vector2 AxisDirection(int i, int n)
        {
            float ang = -Mathf.Pi * 0.5f + i * Mathf.Tau / n;
            return new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
        }

        private int NearestAxis(Vector2 p)
        {
            int n = Count();
            if (n < 3) return -1;
            Vector2 v = p - Centre();
            if (v.LengthSquared() < 1f) return 0;

            int best = 0;
            float bestDot = -999f;
            Vector2 dir = v.Normalized();
            for (int i = 0; i < n; i++)
            {
                float dot = dir.Dot(AxisDirection(i, n));
                if (dot <= bestDot) continue;
                bestDot = dot;
                best = i;
            }
            return best;
        }

        private void ApplyPointerValue(Vector2 p)
        {
            int n = Count();
            if (_activeAxis < 0 || _activeAxis >= n) return;
            Vector2 dir = AxisDirection(_activeAxis, n);
            float value = (p - Centre()).Dot(dir) / Mathf.Max(1f, Radius());
            SetValue(_activeAxis, value);
            EmitSignal(SignalName.ValueChanged, _activeAxis, Values[_activeAxis]);
        }

        public override void _Draw()
        {
            int n = Count();
            float d = Mathf.Min(Size.X, Size.Y);
            if (d < 24f) return;
            if (n < 3)
            {
                KitChrome.DrawEmptyPreview(this, KitChrome.GenreOf(this), new Rect2(Vector2.Zero, Size),
                                           ActiveShape, "Axes");
                return;
            }

            var c = Centre();
            // Leave room for labels outside the web rather than clipping them.
            float r = Radius();
            KitState state = Editable ? KitState.Normal : KitState.Disabled;
            Color fill = KitChrome.StateFace(UiSurface.Semantic(this, Role), state);
            Color ink = KitChrome.StateFace(InkColor(), state);
            Color face = FaceColor();
            var font = KitFont();
            int fs = UiSurface.FontSize(this, UiSurface.TextRole.Small, min: 8);

            Vector2 At(int i, float t)
            {
                return c + AxisDirection(i, n) * r * t;
            }

            // Guide web: rings in the surface's own hue driven dark, never grey.
            Color guide = new(face.R * 0.55f, face.G * 0.55f, face.B * 0.6f, 1f);
            for (int ring = 1; ring <= Rings; ring++)
            {
                float t = ring / (float)Rings;
                for (int i = 0; i < n; i++)
                    DrawLine(At(i, t), At((i + 1) % n, t), guide, Mathf.Max(1f, r * 0.012f));
            }
            for (int i = 0; i < n; i++)
                DrawLine(c, At(i, 1f), guide, Mathf.Max(1f, r * 0.012f));

            // The value polygon.
            var poly = new Vector2[n];
            for (int i = 0; i < n; i++) poly[i] = At(i, Mathf.Clamp(Values[i], 0f, 1f));
            DrawColoredPolygon(poly, new Color(fill.R, fill.G, fill.B, 0.45f));
            var closed = new Vector2[n + 1];
            poly.CopyTo(closed, 0);
            closed[n] = poly[0];
            DrawPolyline(closed, fill, Mathf.Max(2f, r * 0.035f));
            foreach (var p in poly) DrawCircle(p, Mathf.Max(2f, r * 0.045f), fill);

            if (Editable && _activeAxis >= 0 && _activeAxis < n)
                DrawCircle(poly[_activeAxis], Mathf.Max(3f, r * 0.07f), UiSurface.Semantic(this, UiSurface.Role.Info));
            if (Editable)
                KitChrome.DrawFocusRing(this, KitChrome.GenreOf(this), new Rect2(Vector2.Zero, Size), ActiveShape, 0.8f);

            if (!ShowLabels || font == null) return;
            for (int i = 0; i < n; i++)
            {
                string t = KitCase(Axes[i] ?? "");
                if (t.Length == 0) continue;
                float labelWidth = d * 0.18f;
                int tf = UiSurface.FitRole(this, UiSurface.TextRole.Small,
                                           new Vector2(labelWidth, d * 0.08f), t, font, min: 7);
                t = KitChrome.EllipsizeText(font, t, tf, labelWidth);
                if (string.IsNullOrEmpty(t)) continue;
                Vector2 m = font.GetStringSize(t, HorizontalAlignment.Left, -1, tf);
                var at = At(i, 1.28f);
                var badge = new Rect2(at.X - m.X * 0.5f - tf * 0.35f, at.Y - tf * 0.55f,
                                      m.X + tf * 0.70f, tf * 1.25f);
                DrawShape(badge, KitShape.Pill, new Color(face.R * 0.85f, face.G * 0.85f, face.B * 0.90f, 0.92f),
                          ink with { A = 0.55f }, Mathf.Max(1f, tf * 0.08f));
                DrawText(font, new Vector2(at.X - m.X * 0.5f, at.Y + m.Y * 0.32f),
                           t, tf, UiSurface.Text(this));
            }
        }
    }
}

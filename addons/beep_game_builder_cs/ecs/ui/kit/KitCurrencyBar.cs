using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A row of currency / resource readouts: one skinned HUD strip with one segment per value.
    ///
    /// CATALOGUE-FROM-ART.md section A lists this first: it "appears in nearly every picture",
    /// and its build order puts it in the top tier because it carries the most screens per unit
    /// of work. Measured from citybuilder5's StoneCapsule row (x6 across the top of that HUD):
    ///
    /// | part          | measured                                              |
    /// |---------------|-------------------------------------------------------|
    /// | capsule       | **35px** tall                                         |
    /// | frame         | 7px top, 5px bottom — the frame is ASYMMETRIC         |
    /// | body          | one dark capsule, not a text field inside a capsule   |
    /// | icon cell     | coloured cell inside the same strip                   |
    ///
    /// The surface rule is important here: the icon and value are drawn as one procedural
    /// HUD readout. Drawing separate icon buttons and text fields inside the readout makes
    /// the widget read like a row of form controls instead of a single resource bar.
    ///
    /// Icon overhang is per-skin (citybuilder1 1.48x vs citybuilder2 1.0x), so
    /// <see cref="IconOverhang"/> is exposed rather than fixed.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitCurrencyBar : KitControl
    {
        /// <summary>A bar: takes the theme's bar corner, which the
        /// references vary independently of the button corner.</summary>
        protected override KitWidgetClass WidgetClass => KitWidgetClass.Bar;

        public sealed class Entry
        {
            public string Value = "0";
            public Texture2D? Icon;
            /// <summary>Fallback when there is no icon, so a capsule is never blank.</summary>
            public string Glyph = "";
            public UiSurface.Role Accent = UiSurface.Role.Warning;
        }

        public readonly List<Entry> Entries = new();

        [Export]
        public string[] EntryValues
        {
            get
            {
                var values = new string[Entries.Count];
                for (int i = 0; i < Entries.Count; i++)
                    values[i] = Entries[i].Value;
                return values;
            }
            set => SetEntryValues(value);
        }

        [Export]
        public string[] EntryGlyphs
        {
            get
            {
                var glyphs = new string[Entries.Count];
                for (int i = 0; i < Entries.Count; i++)
                    glyphs[i] = Entries[i].Glyph;
                return glyphs;
            }
            set => SetEntryGlyphs(value);
        }

        [Export]
        public Texture2D[] EntryIcons
        {
            get
            {
                var icons = new Texture2D[Entries.Count];
                for (int i = 0; i < Entries.Count; i++)
                    icons[i] = Entries[i].Icon!;
                return icons;
            }
            set => SetEntryIcons(value);
        }

        [Export]
        public int[] EntryAccentRoles
        {
            get
            {
                var accents = new int[Entries.Count];
                for (int i = 0; i < Entries.Count; i++)
                    accents[i] = (int)Entries[i].Accent;
                return accents;
            }
            set => SetEntryAccentRoles(value);
        }

        public void SetEntries(IEnumerable<Entry>? entries)
        {
            List<Entry> next = NormalizeEntries(entries);
            if (SameEntries(Entries, next)) return;
            Entries.Clear();
            Entries.AddRange(next);
            RefreshEntries();
        }

        public void SetEntryValues(string[]? values)
        {
            int count = values?.Length ?? 0;
            bool changed = Entries.Count != count;
            while (Entries.Count > count)
                Entries.RemoveAt(Entries.Count - 1);
            for (int i = 0; i < count; i++)
            {
                EnsureEntry(i);
                string next = values![i] ?? "";
                if (Entries[i].Value == next) continue;
                Entries[i].Value = next;
                changed = true;
            }
            if (!changed) return;
            RefreshEntries();
        }

        public void SetEntryGlyphs(string[]? glyphs)
        {
            if (glyphs == null)
            {
                bool changed = false;
                for (int i = 0; i < Entries.Count; i++)
                {
                    if (Entries[i].Glyph == "") continue;
                    Entries[i].Glyph = "";
                    changed = true;
                }
                if (!changed) return;
                RefreshEntries();
                return;
            }

            bool updated = false;
            for (int i = 0; i < glyphs.Length; i++)
            {
                EnsureEntry(i);
                string next = glyphs[i] ?? "";
                if (Entries[i].Glyph == next) continue;
                Entries[i].Glyph = next;
                updated = true;
            }
            for (int i = glyphs.Length; i < Entries.Count; i++)
            {
                if (Entries[i].Glyph == "") continue;
                Entries[i].Glyph = "";
                updated = true;
            }
            if (!updated) return;
            RefreshEntries();
        }

        public void SetEntryIcons(Texture2D[]? icons)
        {
            if (icons == null)
            {
                bool changed = false;
                for (int i = 0; i < Entries.Count; i++)
                {
                    if (Entries[i].Icon == null) continue;
                    Entries[i].Icon = null;
                    changed = true;
                }
                if (!changed) return;
                RefreshEntries();
                return;
            }

            bool updated = false;
            for (int i = 0; i < icons.Length; i++)
            {
                EnsureEntry(i);
                if (Entries[i].Icon == icons[i]) continue;
                Entries[i].Icon = icons[i];
                updated = true;
            }
            for (int i = icons.Length; i < Entries.Count; i++)
            {
                if (Entries[i].Icon == null) continue;
                Entries[i].Icon = null;
                updated = true;
            }
            if (!updated) return;
            RefreshEntries();
        }

        public void SetEntryAccentRoles(int[]? accents)
        {
            if (accents == null)
            {
                bool changed = false;
                for (int i = 0; i < Entries.Count; i++)
                {
                    if (Entries[i].Accent == UiSurface.Role.Warning) continue;
                    Entries[i].Accent = UiSurface.Role.Warning;
                    changed = true;
                }
                if (!changed) return;
                RefreshEntries();
                return;
            }

            bool updated = false;
            for (int i = 0; i < accents.Length; i++)
            {
                EnsureEntry(i);
                UiSurface.Role next = RoleFromOrdinal(accents[i]);
                if (Entries[i].Accent == next) continue;
                Entries[i].Accent = next;
                updated = true;
            }
            for (int i = accents.Length; i < Entries.Count; i++)
            {
                if (Entries[i].Accent == UiSurface.Role.Warning) continue;
                Entries[i].Accent = UiSurface.Role.Warning;
                updated = true;
            }
            if (!updated) return;
            RefreshEntries();
        }

        public void AddEntry(string value, string glyph = "", Texture2D? icon = null,
                             UiSurface.Role accent = UiSurface.Role.Warning)
        {
            Entries.Add(new Entry { Value = value ?? "", Glyph = glyph ?? "", Icon = icon, Accent = accent });
            RefreshEntries();
        }

        public bool RemoveEntry(int index)
        {
            if (index < 0 || index >= Entries.Count)
                return false;

            Entries.RemoveAt(index);
            RefreshEntries();
            return true;
        }

        public void ClearEntries()
        {
            if (Entries.Count == 0)
                return;

            Entries.Clear();
            RefreshEntries();
        }

        public void RefreshEntries()
        {
            RefreshFootprint();
        }

        private void EnsureEntry(int index)
        {
            while (Entries.Count <= index)
                Entries.Add(new Entry());
        }

        private static List<Entry> NormalizeEntries(IEnumerable<Entry>? entries)
        {
            var next = new List<Entry>();
            if (entries == null)
                return next;

            foreach (Entry? entry in entries)
            {
                next.Add(new Entry
                {
                    Value = entry?.Value ?? "",
                    Glyph = entry?.Glyph ?? "",
                    Icon = entry?.Icon,
                    Accent = RoleFromOrdinal((int)(entry?.Accent ?? UiSurface.Role.Warning)),
                });
            }
            return next;
        }

        private static bool SameEntries(IReadOnlyList<Entry> left, IReadOnlyList<Entry> right)
        {
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
            {
                if ((left[i].Value ?? "") != right[i].Value) return false;
                if ((left[i].Glyph ?? "") != right[i].Glyph) return false;
                if (!ReferenceEquals(left[i].Icon, right[i].Icon)) return false;
                if (RoleFromOrdinal((int)left[i].Accent) != right[i].Accent) return false;
            }
            return true;
        }

        private static UiSurface.Role RoleFromOrdinal(int value)
            => (UiSurface.Role)Mathf.Clamp(value, (int)UiSurface.Role.Neutral, (int)UiSurface.Role.Info);

        /// <summary>Icon cell width as a multiple of the strip height. Kept under the old
        /// property name so existing scenes keep loading.</summary>
        [Export(PropertyHint.Range, "0.6,1.8,0.01")]
        public float IconOverhang
        {
            get => _iconOverhang;
            set
            {
                float next = Mathf.Clamp(value, 0.6f, 1.8f);
                if (Mathf.IsEqualApprox(_iconOverhang, next)) return;
                _iconOverhang = next;
                RefreshFootprint();
            }
        }
        private float _iconOverhang = 1.2f;

        /// <summary>Internal spacing between resource segments, as a multiple of strip height.</summary>
        [Export(PropertyHint.Range, "0.1,1.5,0.05")]
        public float Spacing
        {
            get => _spacing;
            set
            {
                float next = Mathf.Clamp(value, 0.1f, 1.5f);
                if (Mathf.IsEqualApprox(_spacing, next)) return;
                _spacing = next;
                RefreshFootprint();
            }
        }
        private float _spacing = 0.5f;

        public override void _Ready()
        {
            base._Ready();
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            float h = fs * 2.2f;
            float iconW = h * Mathf.Clamp(IconOverhang, 0.75f, 1.35f);
            float capR = iconW;
            float gap = h * Spacing * 0.35f;
            float padX = Mathf.Max(3f, h * 0.14f);
            float width = padX * 2f;
            Font? font = KitFont();

            if (Entries.Count == 0)
                width += h * 3.0f;
            else
            {
                for (int i = 0; i < Entries.Count; i++)
                {
                    Entry entry = Entries[i];
                    float valueWidth = TextWidth(font, entry.Value, UiSurface.FontSize(this, UiSurface.TextRole.Value));
                    width += Mathf.Max(h * 2.45f, capR + padX * 2f + valueWidth);
                    if (i < Entries.Count - 1)
                        width += gap;
                }
            }

            return new Vector2(width, h * 1.10f);
        }

        private void RefreshFootprint()
        {
            if (IsInsideTree())
            {
                KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
                UpdateMinimumSize();
            }
            QueueRedraw();
        }

        /// <summary>Set one entry's value by index, the call a HUD binder makes each tick.</summary>
        public void SetValue(int index, string value)
        {
            if (index < 0 || index >= Entries.Count) return;
            string next = value ?? "";
            if (Entries[index].Value == next) return;
            Entries[index].Value = next;
            RefreshFootprint();
        }

        private static float TextWidth(Font? font, string? text, int fs)
            => string.IsNullOrEmpty(text)
                ? 0f
                : font?.GetStringSize(text, HorizontalAlignment.Left, -1, fs).X ?? text.Length * fs * 0.56f;

        public override void _Draw()
        {
            if (Size.X <= 8 || Size.Y <= 6) return;
            if (Entries.Count == 0)
            {
                KitChrome.DrawEmptyPreview(this, KitChrome.GenreOf(this), new Rect2(Vector2.Zero, Size),
                                           KitShape.Pill, "Entries");
                DrawAttachments();
                return;
            }

            var g = Geo;
            Color face = FaceColor();
            Color ink = InkColor();
            var font = KitFont();
            int fs = UiSurface.FontSize(this);

            float h = Mathf.Min(Size.Y * 0.86f, fs * 2.2f);
            float y = (Size.Y - h) * 0.5f;
            var strip = new Rect2(0f, y, Size.X, h);
            Color bodyFace = KitChrome.WellFace(face);
            string genre = KitChrome.GenreOf(this);
            KitShape barShape = KitChrome.Shape(genre, KitWidgetClass.Bar);
            KitChrome.DrawPlate(this, KitChrome.GenreOf(this), strip, bodyFace, KitState.Normal,
                                fs / 14f, KitWidgetClass.Bar);

            float iconW = h * Mathf.Clamp(IconOverhang, 0.75f, 1.35f);
            float gap = h * Spacing * 0.35f;
            float padX = Mathf.Max(3f, h * 0.14f);
            float dividerW = Mathf.Max(1f, g.Rim * 0.55f * (fs / 14f));

            float[] desired = new float[Entries.Count];
            float desiredTotal = padX * 2f + gap * Mathf.Max(0, Entries.Count - 1);
            int valueFs = UiSurface.FontSize(this, UiSurface.TextRole.Value);
            for (int i = 0; i < Entries.Count; i++)
            {
                desired[i] = Mathf.Max(h * 2.45f, iconW + padX * 2.35f + TextWidth(font, Entries[i].Value, valueFs));
                desiredTotal += desired[i];
            }

            float scale = desiredTotal > Size.X
                ? Mathf.Max(0.45f, (Size.X - padX * 2f - gap * Mathf.Max(0, Entries.Count - 1))
                                    / Mathf.Max(1f, desiredTotal - padX * 2f - gap * Mathf.Max(0, Entries.Count - 1)))
                : 1f;
            float extra = Mathf.Max(0f, Size.X - desiredTotal);
            float x = padX;

            for (int i = 0; i < Entries.Count; i++)
            {
                var e = Entries[i];
                if (x >= Size.X) break;
                float segW = desired[i] * scale + extra / Entries.Count;
                var segment = new Rect2(x, y, Mathf.Min(segW, Size.X - x - padX), h);
                if (segment.Size.X <= 4f) break;

                if (i > 0)
                {
                    float dx = x - gap * 0.5f;
                    DrawLine(new Vector2(dx, strip.Position.Y + h * 0.18f),
                             new Vector2(dx, strip.End.Y - h * 0.18f),
                             ink with { A = 0.34f }, dividerW);
                }

                float iconInsetY = Mathf.Max(2f, h * 0.18f);
                float iconWActual = Mathf.Min(iconW, segment.Size.X * 0.34f);
                var iconCell = new Rect2(segment.Position.X + padX * 0.70f,
                                         segment.Position.Y + iconInsetY,
                                         iconWActual,
                                         segment.Size.Y - iconInsetY * 2f);
                Color accent = UiSurface.Semantic(this, e.Accent);
                if (accent.A < 0.02f)
                    accent = ink;

                float accentW = Mathf.Clamp(h * 0.08f, 2f, 5f);
                var accentRail = new Rect2(segment.Position.X + padX * 0.35f,
                                           segment.Position.Y + h * 0.24f,
                                           accentW,
                                           h * 0.52f);
                KitChrome.DrawShape(this, genre, accentRail, KitShape.Pill,
                                    accent with { A = 0.78f }, new Color(0, 0, 0, 0), 0f,
                                    KitWidgetClass.Bar);

                if (e.Icon != null)
                    DrawTextureRect(e.Icon, iconCell.Grow(-iconCell.Size.Y * 0.12f), false,
                                    Colors.White with { A = 0.94f });
                else if (font != null && !string.IsNullOrEmpty(e.Glyph))
                {
                    string glyph = KitCase(e.Glyph);
                    float glyphWidth = iconCell.Size.X * 0.70f;
                    int gs = UiSurface.FitRole(this, UiSurface.TextRole.Value,
                                               new Vector2(glyphWidth, iconCell.Size.Y * 0.60f), glyph, font, min: 8);
                    glyph = KitChrome.EllipsizeText(font, glyph, gs, glyphWidth);
                    if (!string.IsNullOrEmpty(glyph))
                    {
                        Vector2 m = font.GetStringSize(glyph, HorizontalAlignment.Left, -1, gs);
                        DrawText(font, new Vector2(iconCell.Position.X + (iconCell.Size.X - m.X) * 0.5f,
                                                   iconCell.Position.Y + (iconCell.Size.Y + m.Y * 0.6f) * 0.5f),
                                 glyph, gs, accent with { A = 0.96f });
                    }
                }

                if (font != null && !string.IsNullOrEmpty(e.Value))
                {
                    float textX0 = iconCell.End.X + padX;
                    float avail = segment.End.X - padX * 0.75f - textX0;
                    if (avail > 6f)
                    {
                        int vf = UiSurface.FitRole(this, UiSurface.TextRole.Value,
                                                   new Vector2(avail, segment.Size.Y * 0.52f),
                                                   e.Value, font, min: 8);
                        string value = KitChrome.EllipsizeText(font, e.Value, vf, avail);
                        if (string.IsNullOrEmpty(value)) continue;
                        Vector2 m = font.GetStringSize(value, HorizontalAlignment.Left, -1, vf);
                        float tx = textX0;
                        DrawText(font, new Vector2(tx, segment.Position.Y + (segment.Size.Y + m.Y * 0.6f) * 0.5f),
                                 value, vf, UiSurface.Text(this) with { A = 0.96f });
                    }
                }

                x += segment.Size.X + gap;
            }

            DrawAttachments();
        }
    }
}

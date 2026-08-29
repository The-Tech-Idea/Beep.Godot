using Godot;
using Beep.ECS.UI.Kit;

namespace Beep.ECS.UI
{
    /// <summary>
    /// The one layout spec every framework dialog shares (save, load, settings).
    ///
    /// Both menus ship a hand-authored .tscn AND a <c>NormalizeLayout()</c> that restamps
    /// sizing at runtime (so a project generated from an older template still looks right
    /// without regenerating its scenes). That means the numbers exist in two places per
    /// menu — four copies in total — and they had drifted apart in every one of them:
    /// the load scene asked for a 700x550 panel with 12px row gaps while its C# forced
    /// 640x520 and 10px, and the save menu used a different panel size, a different VBox
    /// separation and 40px rows against the load menu's 92px ones. The two screens sit one
    /// click apart in the same menu and did not look related.
    ///
    /// Every number lives here now. The .tscn files are authored to these values and both
    /// NormalizeLayout methods stamp these same values, so scene and code cannot disagree
    /// and the two menus cannot drift from each other.
    ///
    /// Spacing is a 4px scale: 4 / 10 / 12 / 16 / 20 / 24.
    ///
    /// Control heights are FLOORS, not targets — the theme sizes a button from its font and
    /// content padding (about 41px at a 16px base). A floor above that silently inflates every
    /// control it touches, which is what made these dialogs feel oversized.
    /// </summary>
    internal static class BeepDialogLayout
    {
        // ── Dialog shell ──
        /// <summary>Panel min size. Sized to hold the title, the slot list, the rule and the
        /// action row at 1280x720 with room to spare around the dialog; the list scrolls once
        /// the slot count outgrows it.</summary>
        public const int PanelWidth = 720;
        public const int PanelHeight = 620;

        /// <summary>Right gutter reserved inside the slot list so the scrollbar does not sit
        /// on top of the rows (it overlapped every row's Delete button).</summary>
        public const int ScrollGutter = 14;

        /// <summary>Corner radius and padding of one slot row's background.</summary>
        public const int RowCornerRadius = 6;
        public const int RowPadX = 12;
        public const int RowPadY = 8;

        /// <summary>Padding between the panel edge and its content.</summary>
        public const int OuterMargin = 24;

        /// <summary>Gap between the dialog's major sections (title / list / rule / actions).</summary>
        public const int SectionGap = 20;

        // ── Slot list ──
        /// <summary>Gap between slot rows.</summary>
        public const int RowGap = 10;

        /// <summary>Min height of one slot row. Holds a name line plus a meta line.</summary>
        /// <summary>Row/button/input heights, as multiples of the theme's body font.
        ///
        /// These were fixed pixels authored against 14pt type. A save-slot row 60px tall holds
        /// 14pt comfortably and 24pt not at all, which is why rows clipped on the large-font
        /// genres. Static class, so the caller passes the Control that supplies the theme.</summary>
        public static int Row(Node n) => Mathf.RoundToInt(UiSurface.FontSize(n) * 4.3f);
        public static int ActionButton(Node n) => Mathf.RoundToInt(UiSurface.FontSize(n) * 3.15f);
        public static int Input(Node n) => Mathf.RoundToInt(UiSurface.FontSize(n) * 2.85f);
        public static int SettingsRow(Node n) => Mathf.RoundToInt(UiSurface.FontSize(n) * 3.15f);

        public const int RowHeight = 60;

        /// <summary>Width of the load menu's per-row slot selector button.</summary>
        public const int SlotButtonWidth = 128;
        public const int SlotButtonHeight = 44;

        /// <summary>The load menu's per-row Delete button.</summary>
        public const int DeleteButtonWidth = 88;
        public const int DeleteButtonHeight = 36;

        /// <summary>Gap between a row's selector, its info block and its delete button.</summary>
        public const int RowInnerGap = 16;

        /// <summary>Gap between the two lines inside a row's info block.</summary>
        public const int RowInfoGap = 4;

        // ── Action row ──
        /// <summary>Gap between the confirm and cancel buttons.</summary>
        public const int ButtonGap = 12;

        /// <summary>Height of the Load / Save / Cancel buttons.</summary>
        public const int ActionButtonHeight = 44;

        /// <summary>Height of the save menu's name field.</summary>
        public const int InputHeight = 40;

        /// <summary>Width of the save menu's "Save Name:" caption, so the field lines up.</summary>
        public const int FieldLabelWidth = 110;

        // ── Settings form ──
        /// <summary>Fixed width of a settings row's LABEL column.
        ///
        /// The label must be a fixed, non-expanding column or the control beside it lands at a
        /// different x on every tab. Settings had exactly that: Fullscreen/Subtitles/ScreenShake
        /// labels carried size_flags_horizontal = 3 (expand) while Master/Resolution/Language did
        /// not, so switching tabs shifted every control sideways.</summary>
        public const int SettingsLabelColumn = 190;

        /// <summary>Fixed width of the right-hand read-out column ("80%"), right-aligned so the
        /// digits form a straight edge down the dialog.</summary>
        public const int SettingsValueColumn = 64;

        /// <summary>Min height of one settings row, so a slider row, a checkbox row and an
        /// option-button row all sit on the same rhythm.</summary>
        public const int SettingsRowHeight = 44;

        /// <summary>Height reserved for the tab body. Fixed, so switching tabs does not resize
        /// the dialog under the pointer. Sized to the TALLEST tab (Game: four 44px rows, a rule
        /// and their gaps = 220, plus the ~40px tab bar TabContainer counts inside this value).
        /// Shorter tabs leave some space at the bottom, which is the correct trade for a dialog
        /// that does not jump when you click a tab.</summary>
        public const int SettingsTabHeight = 280;

        /// <summary>Settings panel min WIDTH. Its height is deliberately left to the content.
        ///
        /// Settings used the shared 720x620 shell, but it holds a tab strip and one short form,
        /// not a scrolling slot list — about 480px of content. The panel was therefore 140px
        /// taller than anything in it, and because the TabContainer expands, every one of those
        /// pixels landed in the tab body: three volume sliders sat in a 420px box with a dead
        /// zone beneath them. Sizing to content is what removes the emptiness.</summary>
        public const int SettingsPanelWidth = 680;

        /// <summary>Backfill shell defaults (panel min size and outer padding) into a
        /// menu that already has the conventional PanelContainer > Margin > VBox spine.
        /// Existing authored values are left alone; every lookup is name-based and null-tolerant.</summary>
        public static void ApplyShell(Node root)
            => ApplyShellDefaults(root);

        public static void ApplyShellDefaults(Node root)
        {
            if (root.FindChild("PanelContainer", recursive: true, owned: false) is PanelContainer panel)
                ApplyMinimumIfUnset(panel, PanelWidth, PanelHeight);

            if (root.FindChild("Margin", recursive: true, owned: false) is MarginContainer margin)
            {
                foreach (string side in new[] { "margin_left", "margin_right", "margin_bottom" })
                    SetConstantIfUnset(margin, side, OuterMargin);

                // The TOP is not a free choice when the panel carries a title banner. The banner
                // is drawn from the frame's y=0 downward and OVERLAPS the panel's top border by
                // design, so content starting at a flat OuterMargin lands inside it. This stamped
                // 24 over the 46 settings_menu.tscn had set for the banner, and the tab row ended
                // up 10px inside the "Settings" plaque (banner 164-198, tabs from 188).
                int top = OuterMargin;
                if (margin.GetParent() is { } framedHost)
                    foreach (var sib in framedHost.GetChildren())
                        if (sib is PanelFrameComponent pf && pf.BannerRoom > 0)
                            top = Mathf.Max(top, pf.BannerRoom + 6);
                SetConstantIfUnset(margin, "margin_top", top);
            }
        }

        private static void SetConstantIfUnset(Godot.Control control, string name, int value)
        {
            if (!control.HasThemeConstantOverride(name))
                KitChrome.SetConstantOverrideIfChanged(control, name, value);
        }

        private static void ApplyMinimumIfUnset(Godot.Control control, int x, int y)
        {
            Vector2 current = control.CustomMinimumSize;
            float nextX = current.X <= 0f ? x : current.X;
            float nextY = current.Y <= 0f ? y : current.Y;
            if (Mathf.IsEqualApprox(current.X, nextX) && Mathf.IsEqualApprox(current.Y, nextY))
                return;

            control.CustomMinimumSize = new Vector2(nextX, nextY);
        }
    }
}

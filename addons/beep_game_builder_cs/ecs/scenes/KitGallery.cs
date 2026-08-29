using Godot;
using Beep.ECS.UI;
using Beep.ECS.UI.Kit;

namespace Beep.ECS.Scenes
{
    /// <summary>
    /// Every Game UI Kit widget in ONE real scene, under a real
    /// <see cref="ThemePresetComponent"/>, inside real Containers.
    ///
    /// This is the migration half of PLAN.md phase B. Building the widgets proves they can be
    /// drawn; only putting them in a scene proves they are usable — that a container sizes them,
    /// that they resolve the palette through the generated Theme rather than a probe's stub, and
    /// that their exports survive being written into a `.tscn`.
    ///
    /// It is the kit's counterpart to `theme_gallery.tscn`, and it exists for the same reason:
    /// a place where every widget and state is on screen at once, so a skin change can be judged
    /// rather than guessed at.
    ///
    /// The slot grid and tree carry demo contents because their data is a C# list rather than an
    /// export — a scene cannot author them, and an empty grid would say nothing about whether
    /// the widget works.
    /// </summary>
    [GlobalClass]
    public partial class KitGallery : Control
    {
        public override void _Ready()
        {
            // Kit widgets read the genre from SkinCatalog. A gallery opened on its own has no
            // GameInfo driving it, so fall back to the active skin or a sensible default rather
            // than rendering every widget in the neutral register with no explanation.
            if (!SkinCatalog.HasActiveSkin)
            {
                SkinCatalog.SetActiveSkin("citybuilder", "oilfield_days", "Default", "");
                GD.Print("[KitGallery] No active skin; defaulting to 'citybuilder/oilfield_days' so the kit has a "
                         + "register to draw. Set one via GameInfo/BeepGenreScene to see another.");
            }

            PopulateBag();
            PopulateSkills();
        }

        private void PopulateBag()
        {
            if (this.FindChild("Bag", true, false) is not KitSlotGrid bag) return;
            bag.SetSlots(new[]
            {
                new KitSlotGrid.Slot { Kind = KitSlotGrid.SlotKind.Filled, Count = 12, Tint = UiSurface.Role.Info },
                new KitSlotGrid.Slot { Kind = KitSlotGrid.SlotKind.Filled, Count = 3 },
                new KitSlotGrid.Slot { Kind = KitSlotGrid.SlotKind.Invite },
                new KitSlotGrid.Slot { Kind = KitSlotGrid.SlotKind.Blank },
                new KitSlotGrid.Slot { Kind = KitSlotGrid.SlotKind.Locked, Requirement = "Lv 12" },
            });
        }

        private void PopulateSkills()
        {
            if (this.FindChild("Skills", true, false) is not KitTree tree) return;
            tree.SetNodes(new[]
            {
                new KitTree.Node { Column = 1, Tier = 0, Branch = 0, State = KitTree.NodeState.Owned, Cost = 1 },
                new KitTree.Node { Column = 0, Tier = 1, Branch = 0, State = KitTree.NodeState.Available, Cost = 2, Parents = { 0 } },
                new KitTree.Node { Column = 2, Tier = 1, Branch = 1, State = KitTree.NodeState.Available, Cost = 2, Parents = { 0 } },
                new KitTree.Node { Column = 1, Tier = 2, Branch = 2, State = KitTree.NodeState.Locked, Parents = { 1 } },
                new KitTree.Node { Column = 3, Tier = 2, Branch = 3, State = KitTree.NodeState.Locked, Parents = { 2 } },
            });
        }
    }
}

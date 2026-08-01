using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// What a screen IS, so it can be recognised before it is read.
    ///
    /// Art-pass file 25 is the whole argument: victory, restart and settings are told apart by
    /// their ORNAMENT alone — a crown, crossed weapons, a gear — at a glance and from across the
    /// room, before any text is legible. Every one of those panels is otherwise the same plate.
    ///
    /// The kit had <see cref="KitAttach"/> (a sub-element pinned to an anchor, free to overhang)
    /// and <see cref="KitOrnament"/> (something to draw there), but nothing joining them: every
    /// screen had to place its own decoration by hand, so in practice none of them did and all
    /// ten genres' result screens were identical rectangles.
    /// </summary>
    public enum KitArchetype
    {
        /// <summary>No ornament. The default — most panels are not a screen.</summary>
        None,
        /// <summary>Level complete, victory, reward. Crown.</summary>
        Victory,
        /// <summary>Death, failure, retry. Trophy inverted into a plain marker; deliberately
        /// quieter than Victory, because a defeat screen that celebrates itself reads wrong.</summary>
        Defeat,
        /// <summary>Paused. A single centred marker, no celebration.</summary>
        Pause,
        /// <summary>Options, audio, controls. Gear.</summary>
        Settings,
        /// <summary>Store, purchase, currency. Starburst.</summary>
        Shop,
        /// <summary>Bag, equipment, loadout. Laurel flanks.</summary>
        Inventory,
        /// <summary>Level up, skill unlock, upgrade. Wings.</summary>
        LevelUp,
    }

    /// <summary>One ornament in an archetype's set.</summary>
    public readonly struct KitOrnamentSpec
    {
        public readonly KitOrnament.OrnamentKind Kind;
        public readonly KitAnchor Anchor;
        /// <summary>Size as a fraction of the host's SHORT edge, so an ornament stays in
        /// proportion on a wide result banner and a square dialog alike.</summary>
        public readonly float SizeRatio;
        public readonly UiSurface.Role Role;

        public KitOrnamentSpec(KitOrnament.OrnamentKind kind, KitAnchor anchor,
                               float sizeRatio, UiSurface.Role role)
        { Kind = kind; Anchor = anchor; SizeRatio = sizeRatio; Role = role; }
    }

    /// <summary>The archetype → ornament-set table.</summary>
    public static class KitArchetypes
    {
        private static readonly KitOrnamentSpec[] Empty = System.Array.Empty<KitOrnamentSpec>();

        public static KitOrnamentSpec[] For(KitArchetype a) => a switch
        {
            // A crown OVER the top edge — the single most legible "you won" in the folder.
            KitArchetype.Victory => new[]
            {
                new KitOrnamentSpec(KitOrnament.OrnamentKind.Crown, KitAnchor.Above, 0.34f,
                                    UiSurface.Role.Warning),
            },
            // Deliberately restrained: one small centred marker, no laurels, no gold.
            KitArchetype.Defeat => new[]
            {
                new KitOrnamentSpec(KitOrnament.OrnamentKind.RibbonTail, KitAnchor.Above, 0.22f,
                                    UiSurface.Role.Danger),
            },
            KitArchetype.Pause => new[]
            {
                new KitOrnamentSpec(KitOrnament.OrnamentKind.RibbonTail, KitAnchor.Above, 0.20f,
                                    UiSurface.Role.Neutral),
            },
            KitArchetype.Settings => new[]
            {
                new KitOrnamentSpec(KitOrnament.OrnamentKind.Starburst, KitAnchor.Above, 0.24f,
                                    UiSurface.Role.Info),
            },
            KitArchetype.Shop => new[]
            {
                new KitOrnamentSpec(KitOrnament.OrnamentKind.Starburst, KitAnchor.Above, 0.28f,
                                    UiSurface.Role.Warning),
            },
            // FLANKS, not a single crest: the reference bags are framed either side rather than
            // crowned, which is what stops an inventory reading as a reward screen.
            KitArchetype.Inventory => new[]
            {
                new KitOrnamentSpec(KitOrnament.OrnamentKind.Laurel, KitAnchor.MiddleLeft, 0.30f,
                                    UiSurface.Role.Neutral),
                new KitOrnamentSpec(KitOrnament.OrnamentKind.Laurel, KitAnchor.MiddleRight, 0.30f,
                                    UiSurface.Role.Neutral),
            },
            KitArchetype.LevelUp => new[]
            {
                new KitOrnamentSpec(KitOrnament.OrnamentKind.Wings, KitAnchor.Above, 0.40f,
                                    UiSurface.Role.Success),
            },
            _ => Empty,
        };

        /// <summary>
        /// Build (or rebuild) an archetype's ornaments as child nodes of <paramref name="host"/>.
        ///
        /// IDEMPOTENT — it removes what it made last time before making it again. Every setter
        /// that touches the archetype calls this, and the editor calls setters freely; an
        /// append-only version would stack a new crown on the old one every redraw, which is
        /// exactly the defect the kit's public-API rule exists to prevent.
        /// </summary>
        public static void Apply(Godot.Control host, KitArchetype archetype)
        {
            foreach (var child in host.GetChildren())
                if (child is KitOrnament o && o.HasMeta(MadeByUs))
                    o.QueueFree();

            var specs = For(archetype);
            if (specs.Length == 0) return;

            float shortEdge = Mathf.Max(48f, Mathf.Min(host.Size.X, host.Size.Y));
            foreach (var spec in specs)
            {
                float d = shortEdge * spec.SizeRatio;
                var orn = new KitOrnament
                {
                    Name = $"Ornament{spec.Kind}{spec.Anchor}",
                    Kind = spec.Kind,
                    Role = spec.Role,
                    CustomMinimumSize = new Vector2(d, d),
                    Size = new Vector2(d, d),
                    // Background art: an ornament that eats clicks would block the button under it.
                    MouseFilter = Godot.Control.MouseFilterEnum.Ignore,
                };
                orn.SetMeta(MadeByUs, true);
                // Positioned from the same KitAttach resolve everything else overhangs by, so an
                // ornament and a badge cross the edge by the same rule.
                orn.Position = new KitAttach
                {
                    Anchor = spec.Anchor,
                    Size = new Vector2(d, d),
                    Overhang = 0.5f,
                }.Resolve(host.Size).Position;
                host.AddChild(orn);
            }
        }

        private static readonly StringName MadeByUs = "kit_archetype_ornament";
    }
}

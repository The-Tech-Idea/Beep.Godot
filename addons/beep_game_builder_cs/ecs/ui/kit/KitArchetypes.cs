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
        /// Apply an archetype's ornament specs to authored children of <paramref name="host"/>.
        ///
        /// Scene-authored ornaments are the normal path. A child named Ornament{Kind}{Anchor}
        /// is matched first, then any unclaimed KitOrnament child. Legacy generation and cleanup
        /// exist only when explicitly requested by the owning control.
        /// </summary>
        public static void Apply(Godot.Control host, KitArchetype archetype, bool generateWhenMissing = false)
        {
            var specs = For(archetype);
            if (specs.Length == 0)
            {
                if (generateWhenMissing)
                    RemoveGeneratedOrnaments(host);
                return;
            }

            float shortEdge = Mathf.Max(48f, Mathf.Min(host.Size.X, host.Size.Y));
            var claimed = new System.Collections.Generic.HashSet<KitOrnament>();
            foreach (var spec in specs)
            {
                var ornament = FindAuthoredOrnament(host, spec, claimed);
                if (ornament == null)
                {
                    if (!generateWhenMissing)
                        continue;

                    ornament = FindGeneratedOrnament(host, spec, claimed)
                        ?? BuildGeneratedOrnament(host, spec);
                }

                claimed.Add(ornament);
                StyleOrnament(host, ornament, spec, shortEdge);
            }

            if (generateWhenMissing)
                RemoveUnclaimedGeneratedOrnaments(host, claimed);
        }

        private static KitOrnament? FindAuthoredOrnament(
            Godot.Control host,
            KitOrnamentSpec spec,
            System.Collections.Generic.HashSet<KitOrnament> claimed)
        {
            string preferredName = OrnamentName(spec);
            foreach (var child in host.GetChildren())
            {
                if (child is KitOrnament ornament
                    && !ornament.HasMeta(MadeByUs)
                    && !claimed.Contains(ornament)
                    && ornament.Name.ToString() == preferredName)
                    return ornament;
            }

            foreach (var child in host.GetChildren())
            {
                if (child is KitOrnament ornament
                    && !ornament.HasMeta(MadeByUs)
                    && !claimed.Contains(ornament))
                    return ornament;
            }

            return null;
        }

        private static KitOrnament? FindGeneratedOrnament(
            Godot.Control host,
            KitOrnamentSpec spec,
            System.Collections.Generic.HashSet<KitOrnament> claimed)
        {
            string preferredName = OrnamentName(spec);
            foreach (var child in host.GetChildren())
            {
                if (child is KitOrnament ornament
                    && ornament.HasMeta(MadeByUs)
                    && !claimed.Contains(ornament)
                    && ornament.Name.ToString() == preferredName)
                    return ornament;
            }

            return null;
        }

        private static KitOrnament BuildGeneratedOrnament(Godot.Control host, KitOrnamentSpec spec)
        {
            var ornament = new KitOrnament
            {
                Name = OrnamentName(spec),
                MouseFilter = Godot.Control.MouseFilterEnum.Ignore,
            };
            ornament.SetMeta(MadeByUs, true);
            host.AddChild(ornament);
            if (Engine.IsEditorHint())
                ornament.Owner = host.GetTree()?.EditedSceneRoot;
            return ornament;
        }

        private static void StyleOrnament(Godot.Control host, KitOrnament ornament, KitOrnamentSpec spec, float shortEdge)
        {
            float d = shortEdge * spec.SizeRatio;
            ornament.Kind = spec.Kind;
            ornament.Role = spec.Role;
            ornament.Size = new Vector2(d, d);
            KitChrome.RefreshAutoMinimumSize(ornament, new Vector2(d, d), force: true);
            ornament.MouseFilter = Godot.Control.MouseFilterEnum.Ignore;
            ornament.Position = new KitAttach
            {
                Anchor = spec.Anchor,
                Size = new Vector2(d, d),
                Overhang = 0.5f,
            }.Resolve(host.Size).Position;
        }

        private static void RemoveGeneratedOrnaments(Godot.Control host)
        {
            foreach (var child in host.GetChildren())
                if (child is KitOrnament ornament && ornament.HasMeta(MadeByUs))
                    ornament.QueueFree();
        }

        private static void RemoveUnclaimedGeneratedOrnaments(
            Godot.Control host,
            System.Collections.Generic.HashSet<KitOrnament> claimed)
        {
            foreach (var child in host.GetChildren())
                if (child is KitOrnament ornament
                    && ornament.HasMeta(MadeByUs)
                    && !claimed.Contains(ornament))
                    ornament.QueueFree();
        }

        private static string OrnamentName(KitOrnamentSpec spec)
            => $"Ornament{spec.Kind}{spec.Anchor}";

        private static readonly StringName MadeByUs = "kit_archetype_ornament";
    }
}

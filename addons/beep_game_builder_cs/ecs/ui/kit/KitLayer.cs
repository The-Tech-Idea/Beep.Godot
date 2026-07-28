using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI.Kit
{
    /// <summary>What a single layer in a widget's build does.</summary>
    public enum KitLayerKind
    {
        /// <summary>Solid plate at this layer's inset and shade. The structural layers.</summary>
        Plate,
        /// <summary>A hairline ring at this inset — a keyline between frame and plate.</summary>
        Keyline,
        /// <summary>Light along the top-left, dark along the bottom-right (inverted when sunken).</summary>
        Bevel,
        /// <summary>Sheen band across the upper face.</summary>
        Gloss,
        /// <summary>Vertical shading from the top of the plate to its bottom. The layer that
        /// makes a face read as PAINTED rather than flat.</summary>
        Shade,
        /// <summary>Corner studs / rivets.</summary>
        Studs,
        /// <summary>Corner sparkle accent.</summary>
        Sparkle,
    }

    /// <summary>
    /// One layer in a widget's build — the primitive PLAN.md phase A lists next to `KitControl`
    /// and `KitMaterial` ("Layers : KitLayer[] ordered, each shape or 9-patch, role-coloured")
    /// and which was never actually built.
    ///
    /// Until now the stack was HARDCODED inside DrawMaterial: frame, plate, bevel, gloss, studs,
    /// sparkle, in that order, always. That is why a genre could only ever be a re-tinted version
    /// of one build — the thing §4.1 exists to prevent — and why the carved register could not be
    /// pushed toward the painted look: there was nowhere to put another layer.
    ///
    /// A layer is deliberately DATA. A genre declares a stack; the renderer walks it. Adding a
    /// carved keyline or a deeper face shade is then an entry in a list rather than another
    /// branch in a 90-line method.
    /// </summary>
    public sealed class KitLayer
    {
        public KitLayerKind Kind = KitLayerKind.Plate;

        /// <summary>Inset from the widget rect, as a fraction of its HEIGHT. Negative means
        /// "use the genre's frame thickness", so a stack does not restate FramePx.</summary>
        public float Inset = -1f;

        /// <summary>Multiplier on the face colour for a Plate, or on the effect for the rest.</summary>
        public float Shade = 1f;

        /// <summary>Strength 0..1. For Bevel/Gloss/Shade this is the alpha of the effect.</summary>
        public float Amount = 1f;

        /// <summary>Cut to this shape instead of the host silhouette. Null = inherit, which is
        /// almost always right: a rounded highlight inside an angular outline was a real defect.</summary>
        public KitShape? Shape;

        /// <summary>Draw this layer's own rim. 0 = none.</summary>
        public float Rim;

        public KitLayer() { }

        public KitLayer(KitLayerKind kind, float inset = -1f, float shade = 1f,
                        float amount = 1f, float rim = 0f)
        { Kind = kind; Inset = inset; Shade = shade; Amount = amount; Rim = rim; }
    }

    /// <summary>The ordered stacks each register is built from.</summary>
    public static class KitStacks
    {
        /// <summary>
        /// CARVED — frame, a dark recess line, the plate, an inner keyline, then a strong face
        /// shade and a restrained gloss.
        ///
        /// The extra layers exist to hit the PAINTED band the art pass measured (bottom:peak
        /// 0.18-0.27, rim:body 1.78-2.05x). The register previously rendered at 0.26-0.49 and
        /// 1.05-1.28 -- too flat in the face and too dim in the rim to read as painted -- because
        /// the hardcoded stack had exactly one plate and one bevel to work with. PLAN.md calls
        /// this register "not reachable procedurally"; it is reachable, it just needs more layers
        /// than the old build allowed.
        /// </summary>
        public static readonly KitLayer[] Carved =
        {
            new(KitLayerKind.Plate, inset: 0f, shade: 1.00f, rim: 1f),          // outer frame
            new(KitLayerKind.Keyline, inset: -1f, shade: 0.34f, amount: 0.85f), // recess line
            new(KitLayerKind.Plate, inset: -1f, shade: 0.88f, rim: 0.55f),      // inner plate
            new(KitLayerKind.Shade, inset: -1f, amount: 0.78f),                 // painted falloff
            new(KitLayerKind.Bevel, inset: -1f, amount: 1.0f),
            new(KitLayerKind.Gloss, inset: -1f, amount: 0.55f),
            new(KitLayerKind.Studs),
            new(KitLayerKind.Sparkle),
        };

        /// <summary>CASUAL — one flat plate, a discrete top band, a thick dark outline. Already
        /// lands in its measured band (0.67-0.83 against a 0.76-0.84 flat target), so its stack
        /// is deliberately shallow and carries NO face shade: a gradient down the face IS the
        /// painted reading, and this family must not have one.</summary>
        public static readonly KitLayer[] Casual =
        {
            new(KitLayerKind.Plate, inset: 0f, shade: 1.00f, rim: 1f),
            new(KitLayerKind.Gloss, inset: -1f, amount: 1.0f),
            new(KitLayerKind.Bevel, inset: -1f, amount: 0.55f),
            new(KitLayerKind.Sparkle),
        };

        /// <summary>TECHNICAL — hairline keyline, thin light rim, minimal sculpt.</summary>
        public static readonly KitLayer[] Technical =
        {
            new(KitLayerKind.Plate, inset: 0f, shade: 1.00f, rim: 1f),
            new(KitLayerKind.Plate, inset: -1f, shade: 0.92f),
            new(KitLayerKind.Gloss, inset: -1f, amount: 0.7f),
            new(KitLayerKind.Bevel, inset: -1f, amount: 0.4f),
            new(KitLayerKind.Sparkle),
        };

        public static KitLayer[] For(KitRegister r) => r switch
        {
            KitRegister.Casual => Casual,
            KitRegister.Technical => Technical,
            _ => Carved,
        };
    }
}

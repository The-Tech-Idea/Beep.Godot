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
        /// <summary>The widget's SHADOW, drawn first, under everything. Kind and geometry come
        /// from the theme (<see cref="KitShadowDef"/>) rather than the stack, because two themes
        /// of one genre differ by shadow more than by layer order.</summary>
        Shadow,
        /// <summary>The genre's MATERIAL — a tiling alpha mask (wood grain, stone, plate,
        /// weave) modulated into the plate colour. The third axis; see <see cref="KitGrain"/>.
        /// Amount scales the measured per-genre amplitude, so a stack can carry a lighter or
        /// heavier grain without changing what the material IS.</summary>
        Grain,
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
            // THE MEASURED EDGE STACK from citybuilder5.md, widget 1. The edge of a carved
            // control is FOUR BANDS, not a plate with a border:
            //
            //     world | rim  | bezel | shadow |  plate
            //           | 2px  |  4px  |  5px   |
            //           |2.05x | 1.14x | 0.76x  |  1.00
            //
            // This is the fix for "it looks like a regular Godot UI button". It did, because the
            // frame was shade 1.00 and the inner plate 0.88 -- a 12% step, which is invisible. A
            // frame you cannot SEE is not a frame, and the widget reads as one plate with an
            // outline no matter what silhouette it is cut to.
            //
            // Insets are STEPS, expressed against height because the source was measured on a
            // 107px tile: 2/107, 4/107, 5/107.
            new(KitLayerKind.Plate, inset: 0f,     shade: 2.05f, rim: 1f),   // bright outer rim
            new(KitLayerKind.Plate, inset: 0.019f, shade: 1.14f),            // stone bezel
            new(KitLayerKind.Plate, inset: 0.037f, shade: 0.76f),            // inner shadow
            new(KitLayerKind.Plate, inset: 0.047f, shade: 1.00f, rim: 0.4f), // the plate itself

            // MATERIAL, on the plate and under the lighting. Carved is the wood/stone family
            // (rpg planks, citybuilder/strategy stone), so it carries the grain at full
            // measured strength. It sits after the plates and before Shade/Bevel/Gloss on
            // purpose: light falls ON the material, so a gloss band drawn over the grain reads
            // as a sheen, while a grain drawn over the gloss reads as dirt on the lens.
            new(KitLayerKind.Grain, inset: 0.047f, amount: 1.0f),

            new(KitLayerKind.Shade, inset: -1f, amount: 0.78f),              // painted falloff
            new(KitLayerKind.Bevel, inset: -1f, amount: 1.0f),
            new(KitLayerKind.Gloss, inset: -1f, amount: 0.55f),
            new(KitLayerKind.Studs),
            new(KitLayerKind.Sparkle),
        };

        /// <summary>
        /// CASUAL — ui1 / ui2 / skilltree1 / store: "thick uniform DARK outline, flat saturated
        /// fill + top band, large corner radius, hard drop shadow".
        ///
        /// Its edge is banded too, just inverted from carved: the outermost band is a THICK DARK
        /// outline rather than a bright rim. It previously drew one plate with a 1px border,
        /// which is exactly the "regular Godot UI button" reading — the outline has to be a
        /// visible BAND, not a border property.
        ///
        /// Deliberately NO Shade layer. A gradient down the face is the painted reading, and this
        /// family measures flat (bottom:peak 0.76-0.84). Its depth comes from the outline, the
        /// discrete top band and the drop shadow instead.
        /// </summary>
        public static readonly KitLayer[] Casual =
        {
            new(KitLayerKind.Plate, inset: 0f,     shade: 0.16f),            // thick dark outline
            new(KitLayerKind.Plate, inset: 0.085f, shade: 1.00f),            // flat saturated fill
            // A RESTRAINED grain. This family measures FLAT (bottom:peak 0.76-0.84) and its
            // materials are printed surfaces -- rubber, card, graph paper -- so a full-strength
            // grain would push it into the carved reading and undo the register distinction.
            new(KitLayerKind.Grain, inset: 0.085f, amount: 0.55f),
            new(KitLayerKind.Gloss, inset: -1f, amount: 0.35f),              // discrete top band
            new(KitLayerKind.Bevel, inset: -1f, amount: 0.30f),
            new(KitLayerKind.Sparkle),
        };

        /// <summary>
        /// TECHNICAL — rpgui1 / racing4 / rpgui2: a constant hairline that does not scale, and a
        /// THIN LIGHT rim (the flat family's measured rim:body is 1.3-1.5x, against carved's
        /// 1.78-2.05x). Three bands rather than carved's four, and much tighter: the whole point
        /// of this register is that the edge is a line, not a carving.
        /// </summary>
        public static readonly KitLayer[] Technical =
        {
            new(KitLayerKind.Plate, inset: 0f,     shade: 1.42f, rim: 0.8f), // thin light rim
            new(KitLayerKind.Plate, inset: 0.030f, shade: 0.62f),            // keyline gap
            new(KitLayerKind.Plate, inset: 0.022f, shade: 1.00f),            // face
            // Metal: brushed and diamond plate. Between carved and casual in strength, because
            // a machined surface has real texture but shallow relief.
            new(KitLayerKind.Grain, inset: 0.022f, amount: 0.80f),
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

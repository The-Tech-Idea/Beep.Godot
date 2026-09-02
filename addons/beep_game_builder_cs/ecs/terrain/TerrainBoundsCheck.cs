using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Warns a renderer when its own BoundsSize disagrees with the generator's.
    ///
    /// "How big is the map" is decided once, on the generator, but every
    /// renderer that can also run standalone re-declares BoundsSize as its own
    /// export so it has something to loop over without a generator at hand.
    /// Wiring a renderer to a generator without also hand-matching that export
    /// silently under- or over-scans the map - no error, just a map that stops
    /// short of its edge or reads out of bounds into whatever the generator
    /// returns there. This is the one place that says so.
    ///
    /// A mismatch only WARNS, deliberately: changing a renderer's own BoundsSize
    /// to match on read would be a behaviour change for anything relying on a
    /// deliberately different size (a minimap rendering a cropped region, a
    /// probe exercising a mismatch on purpose), which is not this fix's call to
    /// make.
    /// </summary>
    public static class TerrainBoundsCheck
    {
        public static void WarnIfMismatched(string owner, Vector2I rendererBounds, Vector2I generatorBounds)
        {
            if (rendererBounds == generatorBounds)
                return;

            GD.PushWarning(
                $"[{owner}] BoundsSize {rendererBounds} does not match the generator's "
                + $"BoundsSize {generatorBounds}; this renderer will under- or over-scan "
                + "the map. Set BoundsSize to match the generator, or ignore this warning "
                + "if the mismatch is deliberate.");
        }
    }
}

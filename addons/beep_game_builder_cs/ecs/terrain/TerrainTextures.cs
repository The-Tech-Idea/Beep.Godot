using Godot;
using System;

namespace Beep.ECS
{
    /// <summary>
    /// Loads terrain art, ONCE, for every renderer.
    ///
    /// Two things have to be right about a terrain texture, and both are easy to
    /// get wrong silently:
    ///
    /// GD.Load only resolves res:// paths. Handed an absolute one it fails with
    /// "no loader found" and returns null, which is what an art folder outside
    /// the project gives - and these renderers are wired to exactly such a
    /// folder.
    ///
    /// A texture read straight off disk has NO MIP CHAIN. Image.LoadFromFile
    /// gives an image with one level and ImageTexture.CreateFromImage keeps
    /// whatever it is given, so a shader sampler declared filter_linear_mipmap,
    /// or a TileMapLayer set to LinearWithMipmaps, silently falls back to plain
    /// linear. At map zoom a 64-pixel tile is drawn at about nine and samples
    /// one texel in fifty: the ground aliases into a shimmering grid.
    ///
    /// This existed four times over - in the splat renderer, the isometric
    /// renderer, the transition layer, and as a bare GD.Load in the tile
    /// renderer's water. Three copies were right and one was not, so the tile
    /// view alone drew its atlases unimported and unmipped, and looked worse
    /// than the other two drawing the identical map. That is what a second
    /// implementation of one fact costs: the bug is not that someone wrote it
    /// wrong, it is that there was somewhere for it to be wrong on its own.
    /// </summary>
    public static class TerrainTextures
    {
        /// <summary>
        /// The texture at <paramref name="path"/>, or null with a warning naming
        /// what failed to load.
        ///
        /// Returning null rather than a blank placeholder is deliberate: a
        /// caller has to be able to tell art that loaded from art that did not.
        /// The isometric renderer switches its whole surf path on whether a foam
        /// sheet is there, and a stand-in texture would turn that path on with
        /// nothing behind it - drawing no surf at all, and reporting success.
        /// </summary>
        /// <param name="path">A res:// resource path, or an absolute file path.</param>
        /// <param name="owner">Node name, so a warning says which node failed.</param>
        /// <param name="what">What the texture is for, named in the warning.</param>
        public static Texture2D? Load(string path, string owner, string what)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (path.StartsWith("res://", StringComparison.Ordinal))
            {
                var imported = GD.Load<Texture2D>(path);
                if (imported is null)
                    GD.PushWarning($"[{owner}] could not load {what} '{path}'.");

                // An imported texture already carries whatever mip chain its
                // .import file asks for; regenerating here would do nothing.
                return imported;
            }

            Image image = Image.LoadFromFile(path);
            if (image.IsEmpty())
            {
                GD.PushWarning($"[{owner}] could not load {what} '{path}'.");
                return null;
            }

            image.GenerateMipmaps();
            return ImageTexture.CreateFromImage(image);
        }
    }
}

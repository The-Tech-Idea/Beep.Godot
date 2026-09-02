# TerrainTextures

Support utility: the single texture-loading entry point shared by every terrain renderer.

Loads a terrain art asset from either a `res://` path (imported project resource) or an absolute filesystem path (art living outside the project), handling two failure modes that are otherwise easy to get wrong silently: `GD.Load` only resolves `res://` paths and returns null with no explanation for anything else, and a texture read straight from disk via `Image.LoadFromFile` has no mip chain, so a shader or `TileMapLayer` expecting `filter_linear_mipmap`/`LinearWithMipmaps` silently degrades to plain linear filtering and shimmers at distance. Per its own doc comment, this logic used to be duplicated four times across different renderers, and one of the four copies was missing the mip-chain fix; centralizing it here removed that divergence.

## Public API

- `static Texture2D? Load(string path, string owner, string what)` — returns `null` immediately if `path` is null/whitespace. If `path` starts with `"res://"`, loads via `GD.Load<Texture2D>(path)` and pushes a warning (`[{owner}] could not load {what} '{path}'.`) if that returns null; does not regenerate mipmaps for this branch, since an imported resource already carries whatever mip chain its `.import` settings specify. Otherwise treats `path` as an absolute filesystem path: loads via `Image.LoadFromFile`, pushes the same warning and returns `null` if the resulting image `IsEmpty()`, otherwise calls `image.GenerateMipmaps()` and returns `ImageTexture.CreateFromImage(image)`.

## Dependencies

- No dependencies on other files in `addons/beep_game_builder_cs/ecs/terrain/` — this is a leaf utility with only Godot API calls (`GD.Load`, `Image.LoadFromFile`, `ImageTexture.CreateFromImage`).
- Consumed by (callers in this directory, all going through this one method rather than reimplementing loading): `SeededTerrainPropScatterComponent`, `TerrainFeatureRendererComponent`, `TerrainIsometricFeatureRendererComponent`, `TerrainIsometricRendererComponent`, `TerrainPaintedRendererComponent`, `TerrainReliefRendererComponent`, `TerrainResourceRendererComponent`, `TerrainTransitionLayerComponent`, `TerrainTileRendererComponent`.

## Notes

- Returning `null` on failure (rather than a placeholder texture) is a deliberate, documented design choice: callers such as the isometric renderer branch their surf-drawing path on whether a texture loaded, and a stand-in texture would make that path silently draw nothing while reporting success. Confirmed correct as designed, not a gap.
- The `res://` branch does not call `image.GenerateMipmaps()` even if the imported texture happens to lack mips (e.g. import settings misconfigured) — the comment's stated assumption is that the `.import` file already handled it, so this is a documented trust boundary, not an oversight, but it means a misconfigured import will still alias with no warning from this code path.
- The `.import` files for assets loaded via the `res://` branch are outside this file's control; nothing here verifies the import actually enabled mipmaps, so the class's own stated guarantee ("every terrain texture gets a mip chain") is enforced by convention/import settings for that branch, not by code.

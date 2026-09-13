# Individual Water Animations

Eight cartoon water features from the original sheet, each with 16 frames in a 4x4 PNG and a SpriteFrames resource. The approved narrow waterfall remains unchanged in ../fixed_source_animation/.

Open index.html for live playback. Each feature links to its own PNG and .tres file.

## Godot

Assign the matching .tres to an AnimatedSprite2D and play the flow animation. Speed is 13.333333 FPS, producing a 1.2-second loop. All frames of an asset have identical dimensions. Frame sizes and original crop coordinates are in manifest.json.

The source green background is intentionally retained. Use a suitable green-key material or a separate alpha export before placing the animation over game terrain. No scene integration or collision geometry was added.

## Verification

- All eight PNGs imported into an isolated Godot 4.7 project.
- All eight SpriteFrames resources loaded with 16 nonempty frames.
- All frames preserve original pixels outside the water masks.
- Exact animation-period endpoints match frame zero.
- Browser playback, mobile layout and frame exports checked; results in verification.json.

## Reproducibility

assets.js holds per-feature crop and water-region coordinates. animate.js builds frames from the embedded original source image in index.html. The gallery exposes window.waterGallery.assets; each asset has an exportSheet() method that produces its PNG data URL. Existing source artwork is not repainted or warped; moving water and foam are confined to masks.


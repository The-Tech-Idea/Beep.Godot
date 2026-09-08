# Separate Terrain Ramps

Seven material themes matching the Mountain and Hill Template Collection.
Each theme has nine transparent ramp PNGs, nine AtlasTexture resources,
nine independent Sprite2D scenes, and a manifest.

Rows: nominal quarter, half, and full rise.
Columns: approach from left, approach from front, approach from right.
Names use `ramp_<quarter|half|full>_<left|front|right>`.

Each `.tscn` is an independent visual placement prefab. Its Sprite2D has a
uniform default scale that keeps the widest front-facing sprite at 64 pixels
for that theme. The same scale is applied to the other eight ramps, preserving
the sheet's proportions. Source PNGs and AtlasTexture resources retain full
resolution. Developers can adjust the scene scale and position manually.
The scene origin is the bottom-center of the sprite, not an attachment socket.

The ramps are not baked into mountains. No collision, navigation or automatic
attachment behavior is included. The quarter/half/full names describe visual
height targets; exact world elevation and plate-to-ramp alignment have not
been calibrated or tested. Side approach endpoints follow the source art and
are not interchangeable with front approach endpoints.

Run `python tools/package_template_terrain.py --ramps` to rebuild the package
from its local source sheets. Packaging only crops and exports artwork.
`all_themes_preview.png` displays every ramp on a dark inspection background.

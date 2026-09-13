# River / waterfall connection review

This is a live composition study, not a seamless tile atlas or an approved prefab.
Open index.html. The waterfall uses the approved 16-frame animation unchanged.
Only the waterfall is animated; upstream and downstream are existing static river art.

Layers, back to front: woodland grass, lower river, upper river, keyed waterfall.
The original green-backed files are preserved. sources.js embeds those images for local browser use.

The review intentionally exposes unresolved compatibility issues:
- River banks and woodland grass have different texture density and colors.
- The waterfall pool is wider than its inlet and needs a purpose-drawn outlet transition.
- River pieces are scaled to meet the waterfall; this is not a common tile-grid specification.
- Straight river tiles do not yet have matching animated surface frames.

Next production work: choose one common tile size and palette, draw inlet/outlet and
grass-bank transition tiles against these exact boundaries, then validate on a tile grid.
Do not disguise these joins with global blur or distort the approved waterfall frames.

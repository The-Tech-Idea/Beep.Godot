# Explicit Terrain Flow Connections

The opt-in GDScript resources work without the C# terrain engine. They validate
authored connections; they do not generate missing artwork or choose flow.

`ecs/terrain/TerrainConnectorProfile.gd` records a stable ID, logical cardinal
direction, projection, material/bank profile, opening width in game pixels,
elevation, inlet/outlet role and animation contract. The profile references an
actual TileSet atlas region. Opening width is not the tile's footprint width.

Call `outlet.connection_issues(inlet)` before joining two pieces. An empty result
means the declared contracts match. Missing regions, incompatible projections,
widths, elevations, banks, motion roles or phases produce specific issues.
Different motion roles require authored transition pieces, not texture stretching.
This check does not certify raster edge alignment or artistic quality.

`TerrainFlowConnections.gd.validate_route(placements, external_ports)` validates
logical adjacency. Each placement contains `cell: Vector2i` and an explicit
`ports: Array` of profiles. External ports are a dictionary from logical cell to
an array of cardinal indices: north 0, east 1, south 2, west 3. Undeclared open
ports, absent facing ports and duplicate side assignments are errors.

The existing 12 square river modules now have 24 candidate port resources under
`generated/dev/cartoon/water/river_connectors_v1/`. Their opening is derived from
the existing analytic wet-interior boundary; raster review remains required.
The existing two-bend river route passes with explicitly declared endpoints.
Missing segments and undeclared endpoints fail the maintained probe.

Wide rivers, junctions, isometric successors and river/lake/waterfall adapters
remain incomplete. These resources do not change renderer defaults, navigation,
logical elevation rules or production status.

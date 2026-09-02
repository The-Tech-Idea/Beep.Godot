using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Seeded resource-node scatter for farms, settlements, survival maps, and
    /// top-down/isometric builders. It populates trees, rocks, crates, or other
    /// gatherable nodes without hand-placing every resource prop.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridResourceScatterComponent : Node
    {
        [Signal] public delegate void ResourceScatterRebuiltEventHandler(int count);

        [Export] public NodePath GridPath { get; set; } = new("");
        [Export] public NodePath ResourceRootPath { get; set; } = new("");
        [Export] public NodePath PlacementPath { get; set; } = new("");
        [Export] public NodePath CellDataPath { get; set; } = new("");

        /// <summary>
        /// The generated map's published resources. Point this at a
        /// TerrainDataLayersComponent and the deposits go WHERE THE MAP SAYS -
        /// iron in the hills it generated, fish in its shallows - instead of one
        /// resource id scattered at random over anything not blocked.
        ///
        /// Reading the published layers rather than the generator is deliberate:
        /// the generator is a build-time object, and a saved map has only its tile
        /// layers, so a game loading a level still finds its resources.
        ///
        /// Left empty, the seeded random scatter below is used, which is what an
        /// authored map with no generated terrain needs.
        /// </summary>
        [Export] public NodePath DataLayersPath { get; set; } = new("");

        /// <summary>
        /// Whether the map's LIQUID-stratum resources - fish and kin - also get
        /// walk-up nodes, on their water cells. They are gathered by boat
        /// workers: a worker whose path follower points at a navigation
        /// component authored inverse (land blocked, water allowed).
        /// Underground deposits never get nodes; they are building-extracted.
        /// </summary>
        [Export] public bool IncludeLiquidResources { get; set; } = true;

        /// <summary>
        /// The shared resource catalog - the same one the generator places from.
        /// It decides which generated resources are worth a deposit, and each
        /// placed node takes its rules from it.
        /// </summary>
        [Export] public ResourceCatalog? Catalog { get; set; }
        [Export] public NodePath ResourceWalletPath { get; set; } = new("");
        [Export] public NodePath JobQueuePath { get; set; } = new("");
        [Export] public PackedScene? ResourceScene { get; set; }
        [Export] public bool GenerateOnReady { get; set; } = false;
        [Export] public bool ClearPreviousGenerated { get; set; } = true;
        [Export] public bool AvoidOccupiedCells { get; set; } = true;
        [Export] public bool AvoidCellDataBlocked { get; set; } = true;
        [Export] public bool AvoidBlockedTerrainKinds { get; set; } = true;
        [Export] public bool MarkGeneratedCellsOccupied { get; set; } = false;
        [Export] public Godot.Collections.Array<string> BlockedTerrainKinds { get; set; }
            = GridTerrainRules.DefaultBlockedTerrainKinds();
        [Export] public Godot.Collections.Array<string> AllowedTerrainKinds { get; set; } = new();
        [Export] public int Seed { get; set; } = 12345;
        [Export] public Vector2I BoundsOrigin { get; set; } = Vector2I.Zero;
        [Export] public Vector2I BoundsSize { get; set; } = new(32, 32);
        [Export(PropertyHint.Range, "0,1,0.01")] public float Density { get; set; } = 0.12f;
        [Export(PropertyHint.Range, "0,4096,1")] public int MaxNodes { get; set; } = 128;
        [Export] public string ResourceId { get; set; } = "wood";
        [Export(PropertyHint.Range, "1,9999,1")] public int MinAmount { get; set; } = 2;
        [Export(PropertyHint.Range, "1,9999,1")] public int MaxAmount { get; set; } = 6;
        [Export(PropertyHint.Range, "1,9999,1")] public int AmountPerGather { get; set; } = 1;
        [Export] public string GatherJobKind { get; set; } = "gather";
        [Export(PropertyHint.Range, "0.01,600,0.01")] public float GatherSeconds { get; set; } = 1.5f;
        [Export] public int GatherPriority { get; set; } = 0;
        [Export] public bool SetZIndexFromY { get; set; } = true;

        private const string GeneratedMeta = "grid_resource_scatter_generated";
        private GridProjectionComponent? _grid;
        private TerrainDataLayersComponent? _dataLayers;
        private Node? _resourceRoot;
        private GridPlacementComponent? _placement;
        private GridCellDataComponent? _cellData;

        public Vector2I EffectiveBoundsSize => new(Mathf.Clamp(BoundsSize.X, 0, 1024), Mathf.Clamp(BoundsSize.Y, 0, 1024));
        public float EffectiveDensity => Mathf.Clamp(float.IsFinite(Density) ? Density : 0f, 0f, 1f);
        public int EffectiveMaxNodes => Mathf.Clamp(MaxNodes, 0, 4096);
        public int EffectiveAmountPerGather => Mathf.Max(1, AmountPerGather);
        public float EffectiveGatherSeconds => Mathf.Max(0.01f, float.IsFinite(GatherSeconds) ? GatherSeconds : 1.5f);
        public string EffectiveResourceId => string.IsNullOrWhiteSpace(ResourceId) ? "resource" : ResourceId.Trim();
        public string EffectiveGatherJobKind => string.IsNullOrWhiteSpace(GatherJobKind) ? "gather" : GatherJobKind.Trim();

        public override void _Ready()
        {
            ResolveReferences();
            if (!Engine.IsEditorHint() && GenerateOnReady)
                RebuildScatter();
            UpdateConfigurationWarnings();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (GridPath.IsEmpty)
                return new[] { "GridPath should point to a GridProjectionComponent." };
            if (BoundsSize.X <= 0 || BoundsSize.Y <= 0)
                return new[] { "BoundsSize must be greater than zero." };
            if (string.IsNullOrWhiteSpace(ResourceId))
                return new[] { "ResourceId must not be empty." };
            return Array.Empty<string>();
        }

        public int RebuildScatter()
        {
            ResolveReferences();
            if (_grid == null)
                return 0;

            Node root = _resourceRoot ?? this;
            if (ClearPreviousGenerated)
                ClearGenerated(root);

            int count = 0;
            foreach (Vector2I cell in CandidateCells())
            {
                if (count >= EffectiveMaxNodes)
                    break;

                Node2D? node = CreateResourceNode(cell, count);
                if (node == null)
                    continue;

                root.AddChild(node);
                ConfigureResourcePaths(node);
                node.GlobalPosition = _grid.CellToWorld(cell);
                if (SetZIndexFromY && float.IsFinite(node.GlobalPosition.Y))
                    node.ZIndex = ClampZ(Mathf.RoundToInt(node.GlobalPosition.Y));
                node.SetMeta(GeneratedMeta, true);
                ReserveGeneratedCell(node, cell);
                count++;
            }

            EmitSignal(SignalName.ResourceScatterRebuilt, count);
            return count;
        }

        public int ClearGenerated()
        {
            ResolveReferences();
            return ClearGenerated(_resourceRoot ?? this);
        }

        public Godot.Collections.Array<Vector2I> PreviewCells()
        {
            var cells = new Godot.Collections.Array<Vector2I>();
            foreach (Vector2I cell in CandidateCells())
            {
                cells.Add(cell);
                if (cells.Count >= EffectiveMaxNodes)
                    break;
            }
            return cells;
        }

        private IEnumerable<Vector2I> CandidateCells()
        {
            var rng = new RandomNumberGenerator { Seed = (ulong)Mathf.Max(0, Seed) };
            Vector2I boundsSize = EffectiveBoundsSize;
            int maxNodes = EffectiveMaxNodes;
            float density = EffectiveDensity;
            if (boundsSize.X <= 0 || boundsSize.Y <= 0 || maxNodes <= 0 || density <= 0f)
                yield break;

            int maxX = BoundsOrigin.X + boundsSize.X;
            int maxY = BoundsOrigin.Y + boundsSize.Y;
            int yielded = 0;

            for (int y = BoundsOrigin.Y; y < maxY; y++)
            {
                for (int x = BoundsOrigin.X; x < maxX; x++)
                {
                    if (yielded >= maxNodes)
                        yield break;

                    var cell = new Vector2I(x, y);

                    // The map decides, when there is one. A cell holds a deposit
                    // because the generator put a resource there, not because a
                    // second random roll happened to agree. A LIQUID resource
                    // skips the terrain veto: water is blocked terrain for a
                    // land deposit and exactly the right place for a fish node.
                    if (_dataLayers is not null)
                    {
                        if (MapResourceAt(cell, out bool liquid).Length == 0)
                            continue;
                        if (!CanSpawnResourceAt(cell, skipTerrainRules: liquid))
                            continue;
                        yielded++;
                        yield return cell;
                        continue;
                    }

                    if (!CanSpawnResourceAt(cell))
                        continue;

                    if (rng.Randf() <= density)
                    {
                        yielded++;
                        yield return cell;
                    }
                }
            }
        }

        /// <summary>
        /// The map's resource at a cell, once the catalog agrees it is one worth
        /// placing. A catalog that does not list it means the game has no use for
        /// it - shown on the map, not gathered - so no deposit is made.
        /// </summary>
        private string MapResourceAt(Vector2I cell)
            => MapResourceAt(cell, out _);

        private string MapResourceAt(Vector2I cell, out bool liquid)
        {
            liquid = false;
            if (_dataLayers is null)
                return string.Empty;

            string id = _dataLayers.ResourceAt(cell);
            if (id.Length == 0 && IncludeLiquidResources)
            {
                id = _dataLayers.LiquidResourceAt(cell);
                liquid = id.Length > 0;
            }
            if (id.Length == 0)
                return string.Empty;

            return Catalog is null || Catalog.Contains(id) ? id : string.Empty;
        }

        private string ResourceIdFor(Vector2I cell)
        {
            string mapped = MapResourceAt(cell);
            return mapped.Length > 0 ? mapped : EffectiveResourceId;
        }

        private Node2D? CreateResourceNode(Vector2I cell, int index)
        {
            string resourceId = ResourceIdFor(cell);

            // The catalog's own scene for this resource, when it has one - a
            // tree scene for wood, a rock scene for stone - takes precedence
            // over the one blanket ResourceScene every deposit would otherwise
            // share regardless of what it actually is.
            PackedScene? scene = Catalog?.Find(resourceId)?.NodeScene ?? ResourceScene;
            Node2D? node = scene?.Instantiate() as Node2D;
            if (node == null)
            {
                node = new GridResourceNodeComponent
                {
                    Name = $"Resource_{SafeName(EffectiveResourceId)}_{index + 1}"
                };
            }
            else
            {
                node.Name = string.IsNullOrWhiteSpace(node.Name) ? $"Resource_{SafeName(EffectiveResourceId)}_{index + 1}" : $"{node.Name}_{index + 1}";
            }

            GridResourceNodeComponent? resource = node as GridResourceNodeComponent
                ?? EntityComponent.FindComponent<GridResourceNodeComponent>(node, recursive: true);
            if (resource == null)
            {
                resource = new GridResourceNodeComponent { Name = "ResourceNode" };
                node.AddChild(resource);
            }

            resource.UseExplicitCell = true;
            resource.Cell = cell;
            resource.ResourceId = resourceId;
            // ONE owner for what a deposit is worth. When the catalog defines
            // this id, the node's own ApplyCatalogDefinition takes Amount,
            // AmountPerGather, GatherSeconds and GatherJobKind from it on
            // _Ready - so writing this component's Min/MaxAmount and gather
            // exports onto the node first was values being accepted, stored,
            // and silently overwritten one frame later. They are now written
            // only for an id the catalog does not define, which is the case
            // they were always the real answer for.
            resource.Catalog = Catalog;
            if (Catalog?.Find(resourceId) is null)
            {
                resource.Amount = RandomAmount(cell, index);
                resource.AmountPerGather = EffectiveAmountPerGather;
                resource.GatherJobKind = EffectiveGatherJobKind;
                resource.GatherSeconds = EffectiveGatherSeconds;
            }
            resource.GatherPriority = GatherPriority;
            resource.MarkCellOccupiedOnReady = MarkGeneratedCellsOccupied;
            return node;
        }

        private void ConfigureResourcePaths(Node2D node)
        {
            GridResourceNodeComponent? resource = node as GridResourceNodeComponent
                ?? EntityComponent.FindComponent<GridResourceNodeComponent>(node, recursive: true);
            if (resource == null)
                return;

            resource.GridPath = RelativePathTo(resource, _grid);
            resource.PlacementPath = RelativePathTo(resource, _placement);
            resource.ResourceWalletPath = RelativePathTo(resource, NodeFromPath(ResourceWalletPath));
            resource.JobQueuePath = RelativePathTo(resource, NodeFromPath(JobQueuePath));
        }

        private int RandomAmount(Vector2I cell, int index)
        {
            int min = Mathf.Max(1, Mathf.Min(MinAmount, MaxAmount));
            int max = Mathf.Max(min, Mathf.Max(MinAmount, MaxAmount));
            if (min == max)
                return min;

            var rng = new RandomNumberGenerator { Seed = (ulong)(Mathf.Max(0, Seed) + cell.X * 73856093 + cell.Y * 19349663 + index * 83492791) };
            return rng.RandiRange(min, max);
        }

        private int ClearGenerated(Node root)
        {
            var generated = new List<Node>();
            foreach (Node child in root.GetChildren())
                if (child.GetMeta(GeneratedMeta, false).AsBool())
                    generated.Add(child);

            foreach (Node child in generated)
            {
                if (MarkGeneratedCellsOccupied
                    && _placement != null
                    && ResourceFrom(child) is { } resource)
                {
                    _placement.SetOccupied(resource.Cell, false);
                }
                child.QueueFree();
            }

            return generated.Count;
        }

        private void ResolveReferences()
        {
            if (_grid == null || !GodotObject.IsInstanceValid(_grid))
                _grid = !GridPath.IsEmpty
                    ? GetNodeOrNull<GridProjectionComponent>(GridPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridProjectionComponent>(GetTree()?.CurrentScene) : null;

            if (_dataLayers == null || !GodotObject.IsInstanceValid(_dataLayers))
                _dataLayers = !DataLayersPath.IsEmpty
                    ? GetNodeOrNull<TerrainDataLayersComponent>(DataLayersPath)
                    : null;

            if (_resourceRoot == null || !GodotObject.IsInstanceValid(_resourceRoot))
                _resourceRoot = !ResourceRootPath.IsEmpty ? GetNodeOrNull<Node>(ResourceRootPath) : GetParent();

            if (_placement == null || !GodotObject.IsInstanceValid(_placement))
                _placement = !PlacementPath.IsEmpty
                    ? GetNodeOrNull<GridPlacementComponent>(PlacementPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridPlacementComponent>(GetTree()?.CurrentScene) : null;

            if (_cellData == null || !GodotObject.IsInstanceValid(_cellData))
                _cellData = !CellDataPath.IsEmpty
                    ? GetNodeOrNull<GridCellDataComponent>(CellDataPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridCellDataComponent>(GetTree()?.CurrentScene) : null;
        }

        private bool CanSpawnResourceAt(Vector2I cell, bool skipTerrainRules = false)
        {
            if (AvoidOccupiedCells && _placement?.IsOccupied(cell) == true)
                return false;

            if (_cellData == null)
                return true;

            if (AvoidCellDataBlocked && _cellData.HasFlag(cell, GridCellDataComponent.CellFlags.Blocked))
                return false;

            // Skipped for liquid-stratum spawns: the terrain rules exist to
            // keep LAND deposits off water, and a fish node belongs there.
            if (skipTerrainRules)
                return true;

            string terrainKind = GridTerrainRules.Normalize(_cellData.GetTerrainKind(cell));
            if (!GridTerrainRules.IsAllowed(terrainKind, AllowedTerrainKinds))
                return false;

            if (AvoidBlockedTerrainKinds
                && GridTerrainRules.MatchesAny(terrainKind, BlockedTerrainKinds))
                return false;

            return true;
        }

        private void ReserveGeneratedCell(Node2D node, Vector2I cell)
        {
            if (!MarkGeneratedCellsOccupied || _placement == null)
                return;

            _placement.SetOccupied(cell, true);
            if (ResourceFrom(node) is { } resource)
            {
                // Captured locally with a validity check: the deposit can
                // outlive this scatter component's placement reference, and a
                // freed node must not be called back into.
                GridPlacementComponent placement = _placement;
                resource.Depleted += () =>
                {
                    if (GodotObject.IsInstanceValid(placement))
                        placement.SetOccupied(cell, false);
                };
            }
        }

        private Node? NodeFromPath(NodePath path)
            => path.IsEmpty ? null : GetNodeOrNull<Node>(path);

        private static NodePath RelativePathTo(Node from, Node? target)
        {
            if (target == null || !GodotObject.IsInstanceValid(target) || !from.IsInsideTree() || !target.IsInsideTree())
                return new NodePath("");
            return from.GetPathTo(target);
        }

        private static string SafeName(string value)
        {
            string safe = string.IsNullOrWhiteSpace(value) ? "resource" : value.Trim().ToLowerInvariant().Replace(' ', '_');
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                safe = safe.Replace(c, '_');
            return safe.Replace('/', '_').Replace('\\', '_');
        }

        private static int ClampZ(int zIndex)
            => zIndex < (int)RenderingServer.CanvasItemZMin
                ? (int)RenderingServer.CanvasItemZMin
                : zIndex > (int)RenderingServer.CanvasItemZMax
                    ? (int)RenderingServer.CanvasItemZMax
                    : zIndex;

        private static GridResourceNodeComponent? ResourceFrom(Node node)
            => node as GridResourceNodeComponent
                ?? EntityComponent.FindComponent<GridResourceNodeComponent>(node, recursive: true);
    }
}

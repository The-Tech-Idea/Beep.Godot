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
        [Export] public NodePath ResourceWalletPath { get; set; } = new("");
        [Export] public NodePath JobQueuePath { get; set; } = new("");
        [Export] public PackedScene? ResourceScene { get; set; }
        [Export] public bool GenerateOnReady { get; set; } = false;
        [Export] public bool ClearPreviousGenerated { get; set; } = true;
        [Export] public bool AvoidOccupiedCells { get; set; } = true;
        [Export] public bool AvoidCellDataBlocked { get; set; } = true;
        [Export] public bool AvoidBlockedTerrainKinds { get; set; } = true;
        [Export] public bool MarkGeneratedCellsOccupied { get; set; } = false;
        [Export] public Godot.Collections.Array<string> BlockedTerrainKinds { get; set; } = new()
        {
            "water",
            "sea",
            "ocean",
            "deep_water",
            "lava"
        };
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

        private Node2D? CreateResourceNode(Vector2I cell, int index)
        {
            Node2D? node = ResourceScene?.Instantiate() as Node2D;
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
            resource.ResourceId = EffectiveResourceId;
            resource.Amount = RandomAmount(cell, index);
            resource.AmountPerGather = EffectiveAmountPerGather;
            resource.GatherJobKind = EffectiveGatherJobKind;
            resource.GatherSeconds = EffectiveGatherSeconds;
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

        private bool CanSpawnResourceAt(Vector2I cell)
        {
            if (AvoidOccupiedCells && _placement?.IsOccupied(cell) == true)
                return false;

            if (_cellData == null)
                return true;

            if (AvoidCellDataBlocked && _cellData.HasFlag(cell, GridCellDataComponent.CellFlags.Blocked))
                return false;

            string terrainKind = NormalizeTerrainKind(_cellData.GetTerrainKind(cell));
            if (AllowedTerrainKinds.Count > 0)
            {
                bool allowed = false;
                foreach (string allowedKind in AllowedTerrainKinds)
                {
                    if (NormalizeTerrainKind(allowedKind) == terrainKind)
                    {
                        allowed = true;
                        break;
                    }
                }

                if (!allowed)
                    return false;
            }

            if (AvoidBlockedTerrainKinds)
            {
                foreach (string blockedKind in BlockedTerrainKinds)
                    if (NormalizeTerrainKind(blockedKind) == terrainKind)
                        return false;
            }

            return true;
        }

        private void ReserveGeneratedCell(Node2D node, Vector2I cell)
        {
            if (!MarkGeneratedCellsOccupied || _placement == null)
                return;

            _placement.SetOccupied(cell, true);
            if (ResourceFrom(node) is { } resource)
                resource.Depleted += () => _placement?.SetOccupied(cell, false);
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

        private static string NormalizeTerrainKind(string value)
            => string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
    }
}

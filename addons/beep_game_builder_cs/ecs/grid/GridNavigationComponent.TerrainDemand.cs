using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Beep.ECS;

public partial class GridNavigationComponent
{
    [Export] public bool LoadMissingTerrain { get; set; }
    [Export(PropertyHint.Range, "1,4096,1")] public int MaximumTerrainChunksPerRequest { get; set; } = 256;
    [Export(PropertyHint.Range, "0.1,120,0.1")] public double TerrainRequestTimeoutSeconds { get; set; } = 30;
    public int TerrainWaitingRequestCount => _terrainWaiting.Count;
    private readonly LinkedList<PathRequest> _terrainWaiting = new();
    internal GridCellDataComponent? RouteCellData => LoadMissingTerrain ? _cellData : null;

    private sealed class TerrainDemand : IDisposable
    {
        public readonly GridCellDataComponent Cells;
        private readonly Node _lease;
        private readonly int _limit;
        private readonly HashSet<Vector2I> _chunks = new();
        private readonly HashSet<Vector2I> _missing = new();
        private readonly long _started = Stopwatch.GetTimestamp();
        public bool LimitExceeded { get; private set; }
        public bool HasMissing => _missing.Any(chunk => !Cells.IsChunkAvailable(chunk));
        public bool Expired(double seconds) => Stopwatch.GetElapsedTime(_started).TotalSeconds >= seconds;

        public TerrainDemand(GridNavigationComponent owner, GridCellDataComponent cells)
        {
            Cells = cells;
            _limit = Math.Clamp(owner.MaximumTerrainChunksPerRequest, 1, 4096);
            _lease = new Node { Name = "PathTerrainDemand" };
            owner.AddChild(_lease);
        }

        public bool Observe(Vector2I cell)
        {
            var chunk = GridCellDataComponent.ChunkOf(cell);
            if (!_chunks.Contains(chunk))
            {
                if (_chunks.Count >= _limit) { LimitExceeded = true; return false; }
                _chunks.Add(chunk);
                Cells.ReplaceChunkPins(_lease, _chunks);
            }
            bool available = Cells.IsChunkAvailable(chunk);
            if (!available) _missing.Add(chunk);
            return available;
        }

        public void Dispose()
        {
            if (!GodotObject.IsInstanceValid(_lease)) return;
            if (GodotObject.IsInstanceValid(Cells)) Cells.ReleaseChunkPins(_lease);
            _lease.QueueFree();
        }
    }

    private double TerrainTimeout => double.IsFinite(TerrainRequestTimeoutSeconds)
        ? Math.Clamp(TerrainRequestTimeoutSeconds, 0.1, 120) : 30;

    private void PollTerrainRequests()
    {
        int count = Math.Min(64, _terrainWaiting.Count);
        while (count-- > 0 && _terrainWaiting.First is { } node)
        {
            _terrainWaiting.RemoveFirst();
            var request = node.Value;
            var demand = request.Demand!;
            if (!LoadMissingTerrain || !GodotObject.IsInstanceValid(demand.Cells) || demand.Cells != _cellData
                || demand.Expired(TerrainTimeout) || !demand.HasMissing)
                request.Node = _waitingRequests.AddLast(request);
            else request.Node = _terrainWaiting.AddLast(request);
        }
    }

    private void RestartTerrainRequest(PathRequest request, bool wait)
    {
        request.Node?.List?.Remove(request.Node);
        request.Search = null;
        request.Node = wait ? _terrainWaiting.AddLast(request) : _waitingRequests.AddLast(request);
    }
}

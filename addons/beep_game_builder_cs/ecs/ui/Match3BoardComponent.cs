using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Match-3 puzzle board. Maintains an integer grid of gem types, exposes
    /// Swap(a, b) which performs a swap, detects matches (3+ in a row/column),
    /// clears them, cascades gravity + refill, and emits ScoreChanged with the
    /// points earned. The parent should be a Node2D; gem rendering is left to the
    /// game (read Grid via GetGrid, or connect to CellChanged).
    ///
    /// Reads GridWidth/GridHeight from GameInfo if present.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class Match3BoardComponent : UIComponent
    {
        [Export] public int GridWidth { get; set; } = 8;
        [Export] public int GridHeight { get; set; } = 8;
        [Export] public int GemTypeCount { get; set; } = 5;
        [Export] public int PointsPerGem { get; set; } = 10;

        /// <summary>Optional GameFlow node. Empty = auto-find in the current scene. Match points
        /// are forwarded to it via AddScore so the shared HUD (bound to GameFlow.Score) updates
        /// and the target-score LevelComplete can fire — the board kept a private score that
        /// nothing consumed, so the puzzle HUD stayed 0 and a level could never complete.</summary>
        [Export] public NodePath GameFlowPath { get; set; } = new("");

        [Signal] public delegate void ScoreChangedEventHandler(int total);
        [Signal] public delegate void CellChangedEventHandler(int x, int y, int gemType);
        [Signal] public delegate void MatchesClearedEventHandler(int count, int points);

        private int[,]? _grid;
        private int _score;
        private bool _resolving;
        private GameFlowComponent? _flow;
        private bool _flowWarned;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;   // don't build/rebuild the board at edit time
            var info = GameBuilder.GameInfo.Instance;
            if (info != null) { GridWidth = info.GridWidth; GridHeight = info.GridHeight; }

            // Difficulty ramp by the level chosen on the level-map screen (GameApp.CurrentLevel).
            // Each level grows the board + gem variety a little (capped), so level 6 is
            // meaningfully harder than level 1 instead of an identical board.
            int level = GameApp.Instance?.CurrentLevel ?? 1;
            if (level > 1)
            {
                GridWidth = Mathf.Min(GridWidth + (level - 1), 12);
                GridHeight = Mathf.Min(GridHeight + (level - 1), 12);
                GemTypeCount = Mathf.Min(GemTypeCount + (level - 1) / 2, 8);
            }
            InitGrid();
        }

        public void InitGrid()
        {
            _grid = new int[GridWidth, GridHeight];
            var rng = new RandomNumberGenerator();
            rng.Randomize();
            for (int x = 0; x < GridWidth; x++)
                for (int y = 0; y < GridHeight; y++)
                {
                    int g;
                    // Avoid seeding immediate matches.
                    do { g = rng.RandiRange(1, GemTypeCount); }
                    while (FormsMatchAt(x, y, g));
                    _grid[x, y] = g;
                }
        }

        public int GetGem(int x, int y) => _grid?[x, y] ?? 0;
        public int[,] GetGrid() => _grid!;

        /// <summary>Swap two adjacent cells, then resolve matches/cascades.</summary>
        public void Swap(int ax, int ay, int bx, int by)
        {
            if (!IsActive || _grid == null || _resolving) return;
            if (Mathf.Abs(ax - bx) + Mathf.Abs(ay - by) != 1) return;
            ( _grid[ax, ay], _grid[bx, by] ) = ( _grid[bx, by], _grid[ax, ay] );
            EmitCell(ax, ay); EmitCell(bx, by);
            CallDeferred(nameof(ResolveCascades));
        }

        // Runs synchronously (no await) — the whole clear→gravity→refill cascade resolves in one
        // frame. Not `async` (it had no awaits, which was a CS1998 warning).
        private void ResolveCascades()
        {
            _resolving = true;
            int totalCleared = 0;
            int totalPoints = 0;
            // Repeat until no more matches (cascades from refills).
            while (true)
            {
                var matches = FindMatches();
                if (matches.Count == 0) break;
                int cleared = matches.Count;
                int points = cleared * PointsPerGem;
                totalCleared += cleared;
                totalPoints += points;
                ClearCells(matches);
                ApplyGravity();
                Refill();
            }
            if (totalCleared > 0)
            {
                _score += totalPoints;
                EmitSignal(SignalName.MatchesCleared, totalCleared, totalPoints);
                EmitSignal(SignalName.ScoreChanged, _score);
                // Forward to GameFlow so the shared HUD updates and TargetScore can end the level.
                var flow = ResolveGameFlow();
                if (flow != null) flow.AddScore(totalPoints);
                else if (!_flowWarned)
                {
                    _flowWarned = true;
                    GD.PushWarning($"[{Name}] Match3BoardComponent could not resolve a GameFlowComponent (set GameFlowPath, or add one to the scene) — the board's own score advances but the shared HUD and TargetScore win-condition won't update.");
                }
            }
            _resolving = false;
        }

        // Same lookup PickupComponent uses: prefer an explicit path, else find GameFlow in the
        // current scene (it sits on the main scene alongside the board). Cached once resolved.
        private GameFlowComponent? ResolveGameFlow()
        {
            if (_flow != null && GodotObject.IsInstanceValid(_flow)) return _flow;
            if (!GameFlowPath.IsEmpty) _flow = GetNodeOrNull<GameFlowComponent>(GameFlowPath);
            if (_flow == null && GetTree()?.CurrentScene is { } scene)
                _flow = EntityComponent.FindComponent<GameFlowComponent>(scene, true);
            return _flow;
        }

        private System.Collections.Generic.List<Vector2I> FindMatches()
        {
            var result = new System.Collections.Generic.List<Vector2I>();
            if (_grid == null) return result;
            // Horizontal runs.
            for (int y = 0; y < GridHeight; y++)
            {
                int runStart = 0;
                for (int x = 1; x <= GridWidth; x++)
                {
                    if (x == GridWidth || _grid[x, y] != _grid[runStart, y])
                    {
                        if (x - runStart >= 3)
                            for (int i = runStart; i < x; i++) result.Add(new Vector2I(i, y));
                        runStart = x;
                    }
                }
            }
            // Vertical runs.
            for (int x = 0; x < GridWidth; x++)
            {
                int runStart = 0;
                for (int y = 1; y <= GridHeight; y++)
                {
                    if (y == GridHeight || _grid[x, y] != _grid[x, runStart])
                    {
                        if (y - runStart >= 3)
                            for (int i = runStart; i < y; i++) result.Add(new Vector2I(x, i));
                        runStart = y;
                    }
                }
            }
            return result;
        }

        private void ClearCells(System.Collections.Generic.List<Vector2I> cells)
        {
            if (_grid == null) return;
            foreach (var c in cells) { _grid[c.X, c.Y] = 0; EmitCell(c.X, c.Y); }
        }

        private void ApplyGravity()
        {
            if (_grid == null) return;
            for (int x = 0; x < GridWidth; x++)
            {
                int writeY = GridHeight - 1;
                for (int y = GridHeight - 1; y >= 0; y--)
                {
                    if (_grid[x, y] != 0)
                    {
                        int g = _grid[x, y];
                        _grid[x, y] = 0;
                        _grid[x, writeY] = g;
                        EmitCell(x, writeY);
                        writeY--;
                    }
                }
            }
        }

        private void Refill()
        {
            if (_grid == null) return;
            var rng = new RandomNumberGenerator();
            rng.Randomize();
            for (int x = 0; x < GridWidth; x++)
                for (int y = 0; y < GridHeight; y++)
                    if (_grid[x, y] == 0)
                    {
                        _grid[x, y] = rng.RandiRange(1, GemTypeCount);
                        EmitCell(x, y);
                    }
        }

        private bool FormsMatchAt(int x, int y, int g)
        {
            if (_grid == null) return false;
            // Check two to the left and two above.
            if (x >= 2 && _grid[x - 1, y] == g && _grid[x - 2, y] == g) return true;
            if (y >= 2 && _grid[x, y - 1] == g && _grid[x, y - 2] == g) return true;
            return false;
        }

        private void EmitCell(int x, int y)
        {
            if (_grid != null) EmitSignal(SignalName.CellChanged, x, y, _grid[x, y]);
        }
    }
}

using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Moves a Node2D/CharacterBody2D along paths produced by GridNavigationComponent.
    /// Use it for workers, trucks, RTS units, town NPCs, or enemies that need
    /// simple top-down/isometric grid navigation without writing a movement loop.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridPathFollowerComponent : GameplayComponent, ISaveable
    {
        [Signal] public delegate void PathStartedEventHandler(int length);
        [Signal] public delegate void WaypointReachedEventHandler(int index, Vector2 position);
        [Signal] public delegate void DestinationReachedEventHandler(int x, int y);
        [Signal] public delegate void MoveFailedEventHandler(int x, int y, string reason);

        [Export] public NodePath GridPath { get; set; } = new("");
        [Export] public NodePath NavigationPath { get; set; } = new("");
        [Export] public float Speed { get; set; } = 140f;
        [Export] public float StopDistance { get; set; } = 2f;
        [Export] public bool DriveCharacterBody { get; set; } = true;
        [Export] public bool RotateToMovement { get; set; } = false;
        [Export] public bool SetZIndexFromY { get; set; } = true;
        [Export] public int ZIndexOffset { get; set; } = 0;
        [Export] public bool SnapToDestination { get; set; } = true;

        private bool _isMoving, _runtimeReady;
        private bool _autoAdvancePath = true;
        /// <summary>Disable when an external simulation clock calls AdvancePath explicitly.</summary>
        [Export] public bool AutoAdvancePath
        {
            get => _autoAdvancePath;
            set { _autoAdvancePath = value; RefreshPhysicsProcessing(); }
        }

        private void RefreshPhysicsProcessing() => SetPhysicsProcess(_runtimeReady && AutoAdvancePath && IsMoving);
        public bool IsMoving
        {
            get => _isMoving;
            private set
            {
                _isMoving = value;
                RefreshPhysicsProcessing();
            }
        }
        /// <summary>True only after the installed route completed, not after cancellation or failure.</summary>
        public bool HasReachedDestination { get; private set; }
        public Vector2I DestinationCell { get; private set; } = new(int.MinValue, int.MinValue);
        public int CurrentWaypointIndex => _pathIndex;

        private readonly Godot.Collections.Array<Vector2> _worldPath = new();
        private readonly Godot.Collections.Array<Vector2I> _cellPath = new();
        private int _pathVersion;
        private Node2D? _body;
        private CharacterBody2D? _characterBody;
        private GridProjectionComponent? _grid;
        private GridNavigationComponent? _navigation;
        private int _pathIndex;

        public float EffectiveSpeed => Mathf.Max(0f, float.IsFinite(Speed) ? Speed : 140f);
        public float EffectiveStopDistance => Mathf.Max(0f, float.IsFinite(StopDistance) ? StopDistance : 2f);

        public override void _Ready()
        {
            base._Ready();
            ResolveReferences();
            _runtimeReady = !Engine.IsEditorHint();
            RefreshPhysicsProcessing();
            UpdateConfigurationWarnings();
            if (_resumeSearch) MoveToCell(DestinationCell);
            else RefreshRoutePins();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (Speed <= 0f)
                return new[] { "Speed must be greater than zero." };

            if (StopDistance < 0f)
                return new[] { "StopDistance cannot be negative." };

            return System.Array.Empty<string>();
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!_runtimeReady || !IsActive) return;
            AdvancePath(delta);
        }

        /// <summary>Accepts a scheduled move. PathStarted or MoveFailed reports its later search result.</summary>
        public bool MoveToCell(Vector2I goal)
        {
            ResolveReferences();
            if (_pathRequestId != 0 && goal == DestinationCell && _requestNavigation == _navigation && _requestGrid == _grid)
                return true;
            CancelMove();
            DestinationCell = goal;
            if (_body == null || _grid == null || _navigation == null)
            {
                return FailActiveRoute("missing_body_grid_or_navigation");
            }
            _searchRetries = 0;
            return BeginPathRequest();
        }

        public bool MoveToWorld(Vector2 goalWorld)
        {
            ResolveReferences();
            if (_grid == null)
            {
                DestinationCell = new(int.MinValue, int.MinValue);
                return FailActiveRoute("missing_grid");
            }

            return MoveToCell(_grid.WorldToCell(goalWorld));
        }

        internal bool TryProjectFollowDestination(Vector2 requested, out Vector2 position)
        {
            position = default;
            ResolveReferences();
            if (!requested.IsFinite() || _grid is null || _navigation is null) return false;
            Vector2I cell = _grid.WorldToCell(requested);
            if (!_navigation.IsInBounds(cell)) return false;
            // Missing archived terrain must still reach the scheduled search's demand loader.
            if (_navigation.RouteCellData?.IsCellAvailable(cell) != false && _navigation.IsBlocked(cell)) return false;
            position = _grid.CellToWorld(cell);
            return position.IsFinite();
        }

        public bool SetCellPath(Godot.Collections.Array cells)
        {
            ResolveReferences();
            if (_body == null || _grid == null || cells.Count == 0)
                return false;

            var points = new Godot.Collections.Array<Vector2>();
            var route = new Godot.Collections.Array<Vector2I>();
            Vector2I lastCell = new(int.MinValue, int.MinValue);
            foreach (Variant value in cells)
            {
                if (!GridVariantReader.TryReadCell(value, out Vector2I cell))
                    continue;

                Vector2 point = _grid.CellToWorld(cell);
                if (!point.IsFinite()) return false;
                points.Add(point);
                route.Add(cell);
                lastCell = cell;
            }

            if (points.Count == 0)
                return false;

            if (!NavigationPath.IsEmpty && _navigation is null) return false;
            if (_navigation is not null)
            {
                Vector2I start = _grid.WorldToCell(_body.GlobalPosition);
                if (start != route[0])
                {
                    Vector2 startPoint = _grid.CellToWorld(start);
                    if (!startPoint.IsFinite()) return false;
                    route.Insert(0, start);
                    points.Insert(0, startPoint);
                }
                if (!_navigation.CanTraversePath(route)) return false;
            }

            DestinationCell = lastCell;
            return InstallPath(points, route);
        }

        public bool SetCellPath(Godot.Collections.Array<Vector2I> cells)
        {
            var looseCells = new Godot.Collections.Array();
            foreach (Vector2I cell in cells)
                looseCells.Add(cell);

            return SetCellPath(looseCells);
        }

        public bool SetWorldPath(Godot.Collections.Array points)
        {
            ResolveReferences();
            if (_body == null || points.Count == 0)
                return false;

            var parsed = new Godot.Collections.Array<Vector2>();
            foreach (Variant value in points)
            {
                if (!GridVariantReader.TryReadWorldPoint(value, out Vector2 point))
                    continue;

                if (float.IsFinite(point.X) && float.IsFinite(point.Y))
                    parsed.Add(point);
            }

            if (parsed.Count == 0)
                return false;

            DestinationCell = new(int.MinValue, int.MinValue);
            return InstallPath(parsed, null);
        }

        private bool InstallPath(Godot.Collections.Array<Vector2> points, Godot.Collections.Array<Vector2I>? cells)
        {
            CancelPathRequest();
            _resumeSearch = false;
            LastMoveFailure = "";
            HasReachedDestination = false;
            _pathVersion++;
            _worldPath.Clear();
            _cellPath.Clear();
            foreach (var point in points) _worldPath.Add(point);
            if (cells is not null) foreach (var cell in cells) _cellPath.Add(cell);
            _pathIndex = cells is null ? ClosestStartingIndex(_body!.GlobalPosition) : 0;
            IsMoving = true;
            RefreshRoutePins();
            EmitSignal(SignalName.PathStarted, _worldPath.Count);
            return true;
        }

        public bool SetWorldPath(Godot.Collections.Array<Vector2> points)
        {
            var loosePoints = new Godot.Collections.Array();
            foreach (Vector2 point in points)
                loosePoints.Add(point);

            return SetWorldPath(loosePoints);
        }

        public void CancelMove()
        {
            ReleaseRoutePins();
            CancelPathRequest();
            _resumeSearch = false;
            LastMoveFailure = "";
            HasReachedDestination = false;
            _pathVersion++;
            IsMoving = false;
            _worldPath.Clear();
            _cellPath.Clear();
            _pathIndex = 0;
            if (GodotObject.IsInstanceValid(_characterBody))
                _characterBody.Velocity = Vector2.Zero;
        }

        public Godot.Collections.Array<Vector2> GetWorldPath()
        {
            var copy = new Godot.Collections.Array<Vector2>();
            for (int i = 0; i < _worldPath.Count; i++)
                copy.Add(_cellPath.Count > 0 && _grid is not null ? _grid.CellToWorld(_cellPath[i]) : _worldPath[i]);
            return copy;
        }

        public bool AdvancePath(double delta)
        {
            if (!IsActive || !IsMoving)
                return false;
            if (ActorComponent.ForBody(GetParent()) is { } actor && !actor.CanDrive(this)) return false;

            ResolveReferences();
            if (_characterBody is not null && CharacterMotion.HasKnockback(_characterBody))
            {
                if (DriveCharacterBody) CharacterMotion.Move(_characterBody);
                return true;
            }
            if (IsPathPending) return CheckPendingRequest();
            if (_routePinCells is not null)
            {
                if (!GodotObject.IsInstanceValid(_routePinCells) || _navigation?.RouteCellData != _routePinCells)
                    return FailActiveRoute("navigation_source_changed");
                if (!_routePinCells.HasChunkPins(this)) RefreshRoutePins();
            }
            if (_body == null || _worldPath.Count == 0)
            {
                CancelMove();
                return false;
            }

            if (_characterBody == null || !DriveCharacterBody)
                return AdvanceDirectPath(delta);

            float effectiveDelta = delta > 0.0 && double.IsFinite(delta) ? (float)delta : 0f;
            if (!RefreshCellSegment()) return false;
            Vector2 target = _worldPath[Mathf.Clamp(_pathIndex, 0, _worldPath.Count - 1)];
            Vector2 offset = target - _body.GlobalPosition;
            float distance = offset.Length();

            if (distance <= EffectiveStopDistance)
            {
                _body.GlobalPosition = target;
                ActorComponent.ForBody(_body)?.SynchronizePosition();
                int version = _pathVersion;
                if (_cellPath.Count > 0)
                {
                    Vector2I reachedCell = _cellPath[_pathIndex];
                    foreach (Node child in _body.GetChildren())
                    {
                        if (child is GridObjectComponent gridObject && gridObject.Cell != reachedCell)
                            gridObject.SetCell(reachedCell);
                        if (version != _pathVersion || !IsMoving) return false;
                    }
                }
                if (version != _pathVersion || !IsMoving) return false;
                EmitSignal(SignalName.WaypointReached, _pathIndex, target);
                if (version != _pathVersion || !IsMoving) return false;

                if (_pathIndex >= _worldPath.Count - 1)
                {
                    FinishMove(target);
                    return true;
                }

                _pathIndex++;
                if (!RefreshCellSegment()) return false;
                target = _worldPath[_pathIndex];
                offset = target - _body.GlobalPosition;
                distance = offset.Length();
            }

            if (distance <= 0.001f)
                return true;

            Vector2 direction = offset / distance;
            float speed = EffectiveSpeed;
            // MoveAndSlide integrates using the engine timestep, not AdvancePath's argument.
            double motionDelta = Engine.IsInPhysicsFrame()
                ? _characterBody.GetPhysicsProcessDeltaTime()
                : _characterBody.GetProcessDeltaTime();
            float arrivalSpeed = motionDelta > 0 ? (float)(distance / motionDelta) : 0f;
            _characterBody.Velocity = direction * (effectiveDelta > 0 ? Mathf.Min(speed, arrivalSpeed) : 0f);
            CharacterMotion.Move(_characterBody);

            if (RotateToMovement)
                _body.Rotation = direction.Angle();

            if (SetZIndexFromY)
            {
                float y = _body.GlobalPosition.Y;
                if (float.IsFinite(y))
                    _body.ZIndex = ZIndexOffset + Mathf.RoundToInt(y);
            }

            return true;
        }

        /// <summary>
        /// How long an offset across the screen is on the GROUND, in the ground's own pixels.
        /// </summary>
        /// <remarks>
        /// <para>The projection's basis laid flat again: a step on screen is a step in cell space
        /// once it has been solved against the two cell axes, and cell space is square by
        /// definition, so its length in cells times the length of one cell axis is a distance that
        /// does not depend on which way it points.</para>
        ///
        /// <para>Falls back to the screen length when there is no projection to ask — which is
        /// exactly what a square projection answers — so a top-down caller sees no difference.</para>
        /// </remarks>
        private float GroundLength(Vector2 onScreen)
        {
            if (_grid is null)
                return onScreen.Length();

            Vector2 alongX = _grid.CellToWorld(new Vector2I(1, 0)) - _grid.CellToWorld(Vector2I.Zero);
            Vector2 alongY = _grid.CellToWorld(new Vector2I(0, 1)) - _grid.CellToWorld(Vector2I.Zero);

            float determinant = (alongX.X * alongY.Y) - (alongX.Y * alongY.X);

            if (Mathf.Abs(determinant) < 0.000001f)
                return onScreen.Length();

            var cells = new Vector2(
                ((onScreen.X * alongY.Y) - (onScreen.Y * alongY.X)) / determinant,
                ((alongX.X * onScreen.Y) - (alongX.Y * onScreen.X)) / determinant);

            return cells.Length() * alongX.Length();
        }

        private bool AdvanceDirectPath(double delta)
        {
            // GROUND PIXELS, NOT SCREEN PIXELS. A projection makes one cell step a different length
            // on each axis — a 2:1 oblique draws a tile 64 wide and 32 deep — so a speed in screen
            // pixels is a different GROUND speed depending on which way the machine happens to be
            // driving: a vehicle crossing the slope of the map covers twice the ground per second
            // that one crossing the flat does. The step is taken as a FRACTION of the segment
            // instead, measured in the lattice's own units, so a cell step costs the same time in
            // every direction.
            double remaining = delta > 0 && double.IsFinite(delta) ? EffectiveSpeed * delta : 0;
            int version = _pathVersion;
            // Visit every crossed edge: callbacks may change terrain or replace the route.
            while (IsMoving && version == _pathVersion)
            {
                if (!RefreshCellSegment()) return false;
                Vector2 target = _worldPath[_pathIndex];
                Vector2 offset = target - _body!.GlobalPosition;
                float distance = offset.Length();
                if (distance > 0)
                {
                    if (remaining <= 0) return true;
                    Vector2 direction = offset / distance;
                    float ground = GroundLength(offset);
                    float fraction = ground > 0.0f
                        ? (float)System.Math.Min(remaining / ground, 1.0)
                        : 1.0f;
                    float step = fraction * distance;
                    bool reached = fraction >= 1.0f || distance - step <= 0.0001f;
                    _body.GlobalPosition = reached ? target : _body.GlobalPosition + direction * step;
                    ActorComponent.ForBody(_body)?.SynchronizePosition();
                    remaining -= fraction * ground;
                    if (RotateToMovement) _body.Rotation = direction.Angle();
                    if (SetZIndexFromY && float.IsFinite(_body.GlobalPosition.Y))
                        _body.ZIndex = ZIndexOffset + Mathf.RoundToInt(_body.GlobalPosition.Y);
                    if (!reached) return true;
                }

                if (_cellPath.Count > 0)
                {
                    Vector2I reachedCell = _cellPath[_pathIndex];
                    foreach (Node child in _body.GetChildren())
                    {
                        if (child is GridObjectComponent gridObject && gridObject.Cell != reachedCell)
                            gridObject.SetCell(reachedCell);
                        if (version != _pathVersion || !IsMoving) return false;
                    }
                }
                EmitSignal(SignalName.WaypointReached, _pathIndex, target);
                if (version != _pathVersion || !IsMoving) return false;
                if (_pathIndex == _worldPath.Count - 1)
                {
                    FinishMove(target);
                    return true;
                }
                _pathIndex++;
            }
            return true;
        }

        public void Save(GameBuilder.GameStateData state)
        {
            var cells = new Godot.Collections.Array();
            foreach (var cell in _cellPath) cells.Add(new Godot.Collections.Dictionary { ["x"] = cell.X, ["y"] = cell.Y });
            var points = new Godot.Collections.Array();
            foreach (var point in _worldPath) points.Add(new Godot.Collections.Dictionary { ["x"] = point.X, ["y"] = point.Y });
            state.GameData["path_follower"] = new Godot.Collections.Dictionary
            {
                ["cells"] = cells, ["points"] = points, ["index"] = _pathIndex, ["moving"] = IsMoving,
                ["arrived"] = HasReachedDestination, ["destination_x"] = DestinationCell.X, ["destination_y"] = DestinationCell.Y,
                ["pending"] = IsPathPending
            };
        }

        public void Load(GameBuilder.GameStateData state)
        {
            if (!state.GameData.TryGetValue("path_follower", out var saved)) return;
            ResolveReferences();
            CancelMove();
            var record = saved.AsGodotDictionary();
            foreach (var item in record["cells"].AsGodotArray())
            {
                var cell = item.AsGodotDictionary();
                _cellPath.Add(new(cell["x"].AsInt32(), cell["y"].AsInt32()));
            }
            foreach (var item in record["points"].AsGodotArray())
            {
                var point = item.AsGodotDictionary();
                _worldPath.Add(new(point["x"].AsSingle(), point["y"].AsSingle()));
            }
            if (_cellPath.Count > 0 && _cellPath.Count != _worldPath.Count)
                throw new System.InvalidOperationException("Saved actor route has inconsistent cell and point counts.");
            _pathIndex = Mathf.Clamp(record["index"].AsInt32(), 0, Mathf.Max(0, _worldPath.Count - 1));
            DestinationCell = new(record["destination_x"].AsInt32(), record["destination_y"].AsInt32());
            if (record["pending"].AsBool())
            {
                MoveToCell(DestinationCell);
                return;
            }
            HasReachedDestination = record["arrived"].AsBool();
            IsMoving = record["moving"].AsBool() && _worldPath.Count > 0;
            if (IsMoving && _cellPath.Count > 0 && _navigation?.LoadMissingTerrain == true)
                MoveToCell(DestinationCell);
            else RefreshRoutePins();
        }

        private void FinishMove(Vector2 target)
        {
            if (_body != null && SnapToDestination)
                _body.GlobalPosition = target;
            ActorComponent.ForBody(_body)?.SynchronizePosition();

            if (_characterBody != null)
                _characterBody.Velocity = Vector2.Zero;

            IsMoving = false;
            HasReachedDestination = true;
            ReleaseRoutePins();
            _worldPath.Clear();
            _cellPath.Clear();
            _pathIndex = 0;
            EmitSignal(SignalName.DestinationReached, DestinationCell.X, DestinationCell.Y);
        }

        private void ResolveReferences()
        {
            if (_body == null || !GodotObject.IsInstanceValid(_body))
            {
                _body = GetParent() as Node2D;
                _characterBody = _body as CharacterBody2D;
            }

            // Explicit-ONLY, through the one owner of the fresh-resolve rule: an unwired path
            // means this follower has no grid/navigation, never "adopt whichever one the scene
            // holds". Live re-pointing still takes effect on the next query.
            EntityComponent.ResolveLive(this, GridPath, ref _grid, fallbackWhenEmpty: false);
            EntityComponent.ResolveLive(this, NavigationPath, ref _navigation, fallbackWhenEmpty: false);
        }

        private bool RefreshCellSegment()
        {
            RetirePassedRouteChunks();
            if (_cellPath.Count == 0) return true;
            if (_grid is null) return FailActiveRoute("missing_grid");
            if (!NavigationPath.IsEmpty && _navigation is null) return FailActiveRoute("missing_navigation");
            int previous = Mathf.Max(0, _pathIndex - 1);
            if (_navigation is not null && previous != _pathIndex
                && !_navigation.CanTraverse(_cellPath[previous], _cellPath[_pathIndex]))
                return FailActiveRoute("terrain_route_changed");
            Vector2 from = _grid.CellToWorld(_cellPath[previous]);
            Vector2 to = _grid.CellToWorld(_cellPath[_pathIndex]);
            if (!from.IsFinite() || !to.IsFinite()) return FailActiveRoute("missing_surface");
            Vector2 oldFrom = _worldPath[previous], oldTo = _worldPath[_pathIndex];
            if (from != oldFrom || to != oldTo)
            {
                // Preserve progress along the logical edge when its projection changes.
                Vector2 edge = oldTo - oldFrom;
                float progress = edge.LengthSquared() > 0.0001f
                    ? Mathf.Clamp((_body!.GlobalPosition - oldFrom).Dot(edge) / edge.LengthSquared(), 0, 1) : 0;
                _body!.GlobalPosition = previous == _pathIndex
                    ? _body.GlobalPosition + (to - oldTo) : from.Lerp(to, progress);
                ActorComponent.ForBody(_body)?.SynchronizePosition();
                _worldPath[previous] = from;
                _worldPath[_pathIndex] = to;
            }
            return true;
        }

        private bool FailActiveRoute(string reason)
        {
            Vector2I destination = DestinationCell;
            CancelMove();
            LastMoveFailure = reason;
            EmitSignal(SignalName.MoveFailed, destination.X, destination.Y, reason);
            return false;
        }

        private int ClosestStartingIndex(Vector2 from)
        {
            int index = 0;
            float best = float.MaxValue;
            for (int i = 0; i < _worldPath.Count; i++)
            {
                float d = from.DistanceSquaredTo(_worldPath[i]);
                if (d < best)
                {
                    best = d;
                    index = i;
                }
            }

            return index;
        }

        // Cell and point parsing is delegated to GridVariantReader.TryReadCell
        // and TryReadWorldPoint - the shared readers this file used to carry
        // its own copies of.
    }
}

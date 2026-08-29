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
    public partial class GridPathFollowerComponent : GameplayComponent
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

        public bool IsMoving { get; private set; }
        public Vector2I DestinationCell { get; private set; } = new(int.MinValue, int.MinValue);
        public int CurrentWaypointIndex => _pathIndex;

        private readonly Godot.Collections.Array<Vector2> _worldPath = new();
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
            SetPhysicsProcess(!Engine.IsEditorHint());
            UpdateConfigurationWarnings();
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
            if (Engine.IsEditorHint() || !IsActive) return;
            AdvancePath(delta);
        }

        public bool MoveToCell(Vector2I goal)
        {
            ResolveReferences();
            if (_body == null || _grid == null || _navigation == null)
            {
                EmitSignal(SignalName.MoveFailed, goal.X, goal.Y, "missing_body_grid_or_navigation");
                return false;
            }

            Vector2I start = _grid.WorldToCell(_body.GlobalPosition);
            var cells = _navigation.FindCellPath(start, goal);
            if (cells.Count == 0)
            {
                EmitSignal(SignalName.MoveFailed, goal.X, goal.Y, "no_path");
                return false;
            }

            return SetCellPath(cells);
        }

        public bool MoveToWorld(Vector2 goalWorld)
        {
            ResolveReferences();
            if (_grid == null)
            {
                EmitSignal(SignalName.MoveFailed, int.MinValue, int.MinValue, "missing_grid");
                return false;
            }

            return MoveToCell(_grid.WorldToCell(goalWorld));
        }

        public bool SetCellPath(Godot.Collections.Array cells)
        {
            ResolveReferences();
            if (_grid == null || cells.Count == 0)
                return false;

            var points = new Godot.Collections.Array<Vector2>();
            Vector2I lastCell = new(int.MinValue, int.MinValue);
            foreach (Variant value in cells)
            {
                if (!TryReadCell(value, out Vector2I cell))
                    continue;

                points.Add(_grid.CellToWorld(cell));
                lastCell = cell;
            }

            if (points.Count == 0)
                return false;

            DestinationCell = lastCell;
            return SetWorldPath(points);
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

            _worldPath.Clear();
            foreach (Variant value in points)
            {
                if (!TryReadWorldPoint(value, out Vector2 point))
                    continue;

                if (float.IsFinite(point.X) && float.IsFinite(point.Y))
                    _worldPath.Add(point);
            }

            if (_worldPath.Count == 0)
                return false;

            _pathIndex = ClosestStartingIndex(_body.GlobalPosition);
            IsMoving = true;
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
            IsMoving = false;
            _worldPath.Clear();
            _pathIndex = 0;
            if (_characterBody != null)
                _characterBody.Velocity = Vector2.Zero;
        }

        public Godot.Collections.Array<Vector2> GetWorldPath()
        {
            var copy = new Godot.Collections.Array<Vector2>();
            foreach (Vector2 point in _worldPath)
                copy.Add(point);
            return copy;
        }

        public bool AdvancePath(double delta)
        {
            if (!IsActive || !IsMoving)
                return false;

            ResolveReferences();
            if (_body == null || _worldPath.Count == 0)
            {
                CancelMove();
                return false;
            }

            float effectiveDelta = delta > 0.0 && double.IsFinite(delta) ? (float)delta : 0f;
            Vector2 target = _worldPath[Mathf.Clamp(_pathIndex, 0, _worldPath.Count - 1)];
            Vector2 offset = target - _body.GlobalPosition;
            float distance = offset.Length();

            if (distance <= EffectiveStopDistance)
            {
                _body.GlobalPosition = target;
                EmitSignal(SignalName.WaypointReached, _pathIndex, target);

                if (_pathIndex >= _worldPath.Count - 1)
                {
                    FinishMove(target);
                    return true;
                }

                _pathIndex++;
                target = _worldPath[_pathIndex];
                offset = target - _body.GlobalPosition;
                distance = offset.Length();
            }

            if (distance <= 0.001f)
                return true;

            Vector2 direction = offset / distance;
            float speed = EffectiveSpeed;
            float step = speed * effectiveDelta;

            if (_characterBody != null && DriveCharacterBody)
            {
                _characterBody.Velocity = direction * speed;
                _characterBody.MoveAndSlide();
            }
            else
            {
                _body.GlobalPosition += direction * Mathf.Min(step, distance);
            }

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

        private void FinishMove(Vector2 target)
        {
            if (_body != null && SnapToDestination)
                _body.GlobalPosition = target;

            if (_characterBody != null)
                _characterBody.Velocity = Vector2.Zero;

            IsMoving = false;
            _worldPath.Clear();
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

            if (_grid == null || !GodotObject.IsInstanceValid(_grid))
                _grid = !GridPath.IsEmpty
                    ? GetNodeOrNull<GridProjectionComponent>(GridPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridProjectionComponent>(GetTree()?.CurrentScene) : null;

            if (_navigation == null || !GodotObject.IsInstanceValid(_navigation))
                _navigation = !NavigationPath.IsEmpty
                    ? GetNodeOrNull<GridNavigationComponent>(NavigationPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridNavigationComponent>(GetTree()?.CurrentScene) : null;
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

        private static bool TryReadCell(Variant value, out Vector2I cell)
        {
            cell = default;
            if (value.VariantType == Variant.Type.Vector2I)
            {
                cell = value.AsVector2I();
                return cell.X != int.MinValue && cell.Y != int.MinValue;
            }

            if (value.VariantType == Variant.Type.Vector2)
            {
                Vector2 point = value.AsVector2();
                if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
                    return false;

                cell = new Vector2I(Mathf.RoundToInt(point.X), Mathf.RoundToInt(point.Y));
                return cell.X != int.MinValue && cell.Y != int.MinValue;
            }

            if (value.VariantType == Variant.Type.Dictionary)
            {
                var data = value.AsGodotDictionary();
                if (TryReadCellField(data, "cell", out cell))
                    return true;

                Variant x = ReadVariant(data, "x", "X");
                Variant y = ReadVariant(data, "y", "Y");
                if (TryReadInt(x, out int ix) && TryReadInt(y, out int iy))
                {
                    cell = new Vector2I(ix, iy);
                    return cell.X != int.MinValue && cell.Y != int.MinValue;
                }
            }

            return false;
        }

        private static bool TryReadWorldPoint(Variant value, out Vector2 point)
        {
            point = default;
            if (value.VariantType == Variant.Type.Vector2)
            {
                point = value.AsVector2();
                return float.IsFinite(point.X) && float.IsFinite(point.Y);
            }

            if (value.VariantType == Variant.Type.Vector2I)
            {
                Vector2I cell = value.AsVector2I();
                point = new Vector2(cell.X, cell.Y);
                return true;
            }

            if (value.VariantType == Variant.Type.Dictionary)
            {
                var data = value.AsGodotDictionary();
                Variant x = ReadVariant(data, "x", "X");
                Variant y = ReadVariant(data, "y", "Y");
                if (TryReadFloat(x, out float fx) && TryReadFloat(y, out float fy))
                {
                    point = new Vector2(fx, fy);
                    return float.IsFinite(point.X) && float.IsFinite(point.Y);
                }
            }

            return false;
        }

        private static bool TryReadCellField(Godot.Collections.Dictionary data, string key, out Vector2I cell)
        {
            cell = default;
            if (!data.ContainsKey(key))
                return false;

            return TryReadCell(data[key], out cell);
        }

        private static Variant ReadVariant(Godot.Collections.Dictionary data, string lower, string upper)
        {
            if (data.ContainsKey(lower)) return data[lower];
            if (data.ContainsKey(upper)) return data[upper];
            return default;
        }

        private static bool TryReadInt(Variant value, out int result)
        {
            result = 0;
            if (value.VariantType == Variant.Type.Int)
            {
                result = value.AsInt32();
                return true;
            }
            if (value.VariantType == Variant.Type.Float)
            {
                double raw = value.AsDouble();
                if (!double.IsFinite(raw))
                    return false;
                result = Mathf.RoundToInt((float)raw);
                return true;
            }
            if (value.VariantType == Variant.Type.String)
                return int.TryParse(value.AsString(), out result);
            return false;
        }

        private static bool TryReadFloat(Variant value, out float result)
        {
            result = 0f;
            if (value.VariantType == Variant.Type.Float || value.VariantType == Variant.Type.Int)
            {
                double raw = value.AsDouble();
                if (!double.IsFinite(raw))
                    return false;
                result = (float)raw;
                return true;
            }
            if (value.VariantType == Variant.Type.String)
                return float.TryParse(value.AsString(), out result) && float.IsFinite(result);
            return false;
        }
    }
}

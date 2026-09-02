using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Runs a set of dispatchable tasks: a button asks for one, a vehicle drives
    /// out, the world changes when it arrives, and the vehicle returns.
    ///
    /// This is the settlers-style work loop with the tasks taken out of it. The
    /// controller it replaces hardcoded eight of them as a switch over button
    /// names, with screen coordinates as literals in the cases and one private
    /// method per outcome; the tasks are GridDispatchTaskDefinition resources
    /// now, so a game configures its own set without touching this file.
    ///
    /// One task at a time on purpose: the vehicle is a single shared prop, and
    /// two overlapping tweens on one node fight rather than queue.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridDispatchBoardComponent : Node
    {
        /// <summary>Raised when a task finishes, so a scene can react to it.</summary>
        [Signal] public delegate void TaskCompletedEventHandler(string action);

        [Export] public NodePath SpawnerPath { get; set; } = new("");
        [Export] public NodePath ResourceWalletPath { get; set; } = new("");
        [Export] public NodePath StatusLabelPath { get; set; } = new("");

        /// <summary>Pulses at the work site while a task runs.</summary>
        [Export] public NodePath WorkMarkerPath { get; set; } = new("");

        /// <summary>
        /// Buttons that request work. A button's NAME selects the task whose
        /// Action matches it, so the panel and the task list are joined by data
        /// rather than by a switch.
        /// </summary>
        [Export] public Godot.Collections.Array<NodePath> ToolButtonPaths { get; set; } = new();

        [Export] public Godot.Collections.Array<GridDispatchTaskDefinition> Tasks { get; set; } = new();

        /// <summary>Hidden until a task shows them.</summary>
        [Export] public Godot.Collections.Array<NodePath> HiddenAtStart { get; set; } = new();

        [ExportGroup("Timing")]
        [Export(PropertyHint.Range, "0.05,5,0.05")] public float TravelSeconds { get; set; } = 0.85f;
        [Export(PropertyHint.Range, "0,5,0.05")] public float WorkSeconds { get; set; } = 0.45f;

        [Export] public string IdlePrompt { get; set; } = "Choose a task. A truck will leave the depot and complete it.";
        [Export] public string BusyPrompt { get; set; } = "A truck is already working. Wait for it to return.";

        private GridWorkerSpawnerComponent? _spawner;
        private GridResourceWalletComponent? _wallet;
        private Label? _status;
        private Node2D? _workMarker;
        private bool _isWorking;

        public override void _Ready()
        {
            if (Engine.IsEditorHint())
                return;

            _spawner = SpawnerPath.IsEmpty ? null : GetNodeOrNull<GridWorkerSpawnerComponent>(SpawnerPath);
            _wallet = ResourceWalletPath.IsEmpty ? null : GetNodeOrNull<GridResourceWalletComponent>(ResourceWalletPath);
            _status = StatusLabelPath.IsEmpty ? null : GetNodeOrNull<Label>(StatusLabelPath);
            _workMarker = WorkMarkerPath.IsEmpty ? null : GetNodeOrNull<Node2D>(WorkMarkerPath);

            if (_workMarker is not null)
                _workMarker.Visible = false;

            foreach (NodePath path in HiddenAtStart)
            {
                if (GetNodeOrNull(path) is CanvasItem item)
                    item.Visible = false;
            }

            if (_spawner is not null)
            {
                _spawner.UnitSpawned += OnUnitSpawned;
                _spawner.SpawnRejected += OnSpawnRejected;
            }

            foreach (NodePath path in ToolButtonPaths)
            {
                if (GetNodeOrNull<Button>(path) is not { } button)
                    continue;

                string action = button.Name;
                button.Pressed += () => Request(action);
            }

            SetStatus(IdlePrompt);
        }

        public override void _ExitTree()
        {
            if (_spawner is not null && GodotObject.IsInstanceValid(_spawner))
            {
                _spawner.UnitSpawned -= OnUnitSpawned;
                _spawner.SpawnRejected -= OnSpawnRejected;
            }
        }

        public override string[] _GetConfigurationWarnings()
            => Tasks.Count == 0
                ? new[] { "Add at least one GridDispatchTaskDefinition, or no button can do anything." }
                : System.Array.Empty<string>();

        /// <summary>Starts the task whose Action matches, if nothing is running.</summary>
        public void Request(string action)
        {
            if (_isWorking)
            {
                SetStatus(BusyPrompt);
                return;
            }

            GridDispatchTaskDefinition? task = FindTask(action);
            if (task is null)
            {
                // Named rather than ignored: a button whose name no task matches
                // is a typo, and silently doing nothing looks like a broken loop.
                SetStatus($"No task is configured for {action}.");
                return;
            }

            Dispatch(task);
        }

        private GridDispatchTaskDefinition? FindTask(string action)
        {
            foreach (GridDispatchTaskDefinition? task in Tasks)
            {
                if (task is not null && task.Action == action)
                    return task;
            }
            return null;
        }

        private void Dispatch(GridDispatchTaskDefinition task)
        {
            if (GetNodeOrNull<Node2D>(task.VehiclePath) is not { } vehicle)
            {
                SetStatus("No truck is available for that task.");
                return;
            }

            _isWorking = true;
            Vector2 origin = vehicle.Position;
            SetStatus($"{task.Label}...");
            ShowWorkMarker(task.Target);

            Tween tween = CreateTween();
            tween.SetTrans(Tween.TransitionType.Sine);
            tween.SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(vehicle, "position", task.Target, Mathf.Max(0.05f, TravelSeconds));
            tween.TweenInterval(Mathf.Max(0.0f, WorkSeconds));
            tween.TweenCallback(Callable.From(() => Apply(task)));
            tween.TweenProperty(vehicle, "position", origin, Mathf.Max(0.05f, TravelSeconds));
            tween.TweenCallback(Callable.From(() => Finish(task)));
        }

        /// <summary>What arriving actually does. Every effect is data on the task.</summary>
        private void Apply(GridDispatchTaskDefinition task)
        {
            foreach (NodePath path in task.Hide)
            {
                if (GetNodeOrNull(path) is CanvasItem item)
                    item.Visible = false;
            }

            foreach (NodePath path in task.Show)
            {
                if (GetNodeOrNull(path) is CanvasItem item)
                    item.Visible = true;
            }

            if (!task.RecolourTarget.IsEmpty && GetNodeOrNull(task.RecolourTarget) is CanvasItem target)
            {
                // A Polygon2D carries its fill in Color; anything else takes the
                // tint through Modulate.
                if (target is Polygon2D polygon)
                    polygon.Color = task.Recolour;
                else
                    target.Modulate = task.Recolour;
            }

            if (!string.IsNullOrWhiteSpace(task.RewardResourceId) && task.RewardAmount != 0)
                _wallet?.AddAmount(task.RewardResourceId, task.RewardAmount);
        }

        private void Finish(GridDispatchTaskDefinition task)
        {
            if (_workMarker is not null)
                _workMarker.Visible = false;

            _isWorking = false;
            SetStatus($"{task.Label} complete. Choose the next task.");
            EmitSignal(SignalName.TaskCompleted, task.Action);
        }

        private void ShowWorkMarker(Vector2 target)
        {
            if (_workMarker is null)
                return;

            _workMarker.Position = target;
            _workMarker.Scale = Vector2.One * 0.65f;
            _workMarker.Visible = true;

            Tween tween = CreateTween();
            tween.TweenProperty(_workMarker, "scale", Vector2.One, 0.22f);
            tween.TweenProperty(_workMarker, "scale", Vector2.One * 0.7f, 0.22f);
            tween.SetLoops(3);
        }

        private void OnUnitSpawned(Node unit, string workerId, int x, int y)
        {
            AnimateArrival(unit);
            SetStatus($"Truck {workerId} dispatched from the depot.");
        }

        private void OnSpawnRejected(string reason)
            => SetStatus($"Truck request blocked: {reason}.");

        /// <summary>A short nudge so a newly spawned vehicle reads as arriving.</summary>
        private void AnimateArrival(Node unit)
        {
            if (unit is not Node2D vehicle)
                return;

            Vector2 origin = vehicle.Position;
            Tween tween = CreateTween();
            tween.SetTrans(Tween.TransitionType.Sine);
            tween.SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(vehicle, "position", origin + new Vector2(84, 42), 0.65f);
            tween.TweenProperty(vehicle, "position", origin, 0.65f);
        }

        private void SetStatus(string text)
        {
            if (_status is not null)
                _status.Text = text;
        }
    }
}

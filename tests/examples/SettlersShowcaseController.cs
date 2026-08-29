using Godot;
using System;
using Beep.ECS;

namespace Beep.Tests.Examples;

[GlobalClass]
public partial class SettlersShowcaseController : Node
{
    [Export] public NodePath SpawnerPath { get; set; } = new("");
    [Export] public NodePath ResourceWalletPath { get; set; } = new("");
    [Export] public NodePath StatusLabelPath { get; set; } = new("");
    [Export] public NodePath TruckClearPath { get; set; } = new("");
    [Export] public NodePath TruckRoadPath { get; set; } = new("");
    [Export] public NodePath TreePatchPath { get; set; } = new("");
    [Export] public NodePath ClearedYardPath { get; set; } = new("");
    [Export] public NodePath PreparedPlotsPath { get; set; } = new("");
    [Export] public NodePath CropPatchPath { get; set; } = new("");
    [Export] public NodePath RoadExtensionPath { get; set; } = new("");
    [Export] public NodePath WorkMarkerPath { get; set; } = new("");
    [Export] public NodePath[] ToolButtonPaths { get; set; } = Array.Empty<NodePath>();

    private GridWorkerSpawnerComponent? _spawner;
    private GridResourceWalletComponent? _wallet;
    private Label? _status;
    private Node2D? _treePatch;
    private Polygon2D? _clearedYard;
    private Node2D? _preparedPlots;
    private Node2D? _cropPatch;
    private CanvasItem? _roadExtension;
    private Node2D? _workMarker;
    private Node2D? _clearTruck;
    private Node2D? _roadTruck;
    private bool _isWorking;

    public override void _Ready()
    {
        _spawner = GetNodeOrNull<GridWorkerSpawnerComponent>(SpawnerPath);
        _wallet = GetNodeOrNull<GridResourceWalletComponent>(ResourceWalletPath);
        _status = GetNodeOrNull<Label>(StatusLabelPath);
        _treePatch = GetNodeOrNull<Node2D>(TreePatchPath);
        _clearedYard = GetNodeOrNull<Polygon2D>(ClearedYardPath);
        _preparedPlots = GetNodeOrNull<Node2D>(PreparedPlotsPath);
        _cropPatch = GetNodeOrNull<Node2D>(CropPatchPath);
        _roadExtension = GetNodeOrNull<CanvasItem>(RoadExtensionPath);
        _workMarker = GetNodeOrNull<Node2D>(WorkMarkerPath);
        _clearTruck = GetNodeOrNull<Node2D>(TruckClearPath);
        _roadTruck = GetNodeOrNull<Node2D>(TruckRoadPath);

        if (_preparedPlots != null)
            _preparedPlots.Visible = false;
        if (_cropPatch != null)
            _cropPatch.Visible = false;
        if (_roadExtension != null)
            _roadExtension.Visible = false;
        if (_workMarker != null)
            _workMarker.Visible = false;

        if (_spawner != null)
        {
            _spawner.UnitSpawned += OnUnitSpawned;
            _spawner.SpawnRejected += OnSpawnRejected;
        }

        foreach (NodePath path in ToolButtonPaths)
        {
            Button? button = GetNodeOrNull<Button>(path);
            if (button == null)
                continue;

            string action = button.Name;
            button.Pressed += () => RequestWork(action);
        }

        SetStatus("Choose a task. A truck will leave the depot and complete it.");
    }

    public override void _ExitTree()
    {
        if (_spawner != null && GodotObject.IsInstanceValid(_spawner))
        {
            _spawner.UnitSpawned -= OnUnitSpawned;
            _spawner.SpawnRejected -= OnSpawnRejected;
        }
    }

    private void RequestWork(string action)
    {
        if (_isWorking)
        {
            SetStatus("A truck is already working. Wait for it to return.");
            return;
        }

        switch (action)
        {
            case "Clear":
                Dispatch(_clearTruck, new Vector2(930, 235), "Clearing brush", CompleteClear);
                break;
            case "Hoe":
                Dispatch(_clearTruck, new Vector2(620, 385), "Preparing plots", CompletePreparation);
                break;
            case "Water":
                Dispatch(_clearTruck, new Vector2(620, 385), "Watering plots", CompleteWater);
                break;
            case "Plant":
                Dispatch(_clearTruck, new Vector2(620, 385), "Planting cover crop", CompletePlanting);
                break;
            case "Harvest":
                Dispatch(_clearTruck, new Vector2(620, 385), "Collecting supplies", CompleteHarvest);
                break;
            case "Job":
                Dispatch(_clearTruck, new Vector2(930, 235), "Dispatching clear order", CompleteClear);
                break;
            case "Road":
                Dispatch(_roadTruck, new Vector2(1015, 310), "Extending dirt road", CompleteRoad);
                break;
            case "NoRoad":
                Dispatch(_roadTruck, new Vector2(1015, 310), "Removing road section", RemoveRoad);
                break;
        }
    }

    private void Dispatch(Node2D? truck, Vector2 target, string action, Action complete)
    {
        if (truck == null)
        {
            SetStatus("No truck is available for that task.");
            return;
        }

        _isWorking = true;
        Vector2 origin = truck.Position;
        SetStatus($"{action}...");
        ShowWorkMarker(target);

        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Sine);
        tween.SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(truck, "position", target, 0.85f);
        tween.TweenInterval(0.45f);
        tween.TweenCallback(Callable.From(() => complete()));
        tween.TweenProperty(truck, "position", origin, 0.85f);
        tween.TweenCallback(Callable.From(() => FinishWork(action)));
    }

    private void ShowWorkMarker(Vector2 target)
    {
        if (_workMarker == null)
            return;

        _workMarker.Position = target;
        _workMarker.Scale = Vector2.One * 0.65f;
        _workMarker.Visible = true;
        var tween = CreateTween();
        tween.TweenProperty(_workMarker, "scale", Vector2.One, 0.22f);
        tween.TweenProperty(_workMarker, "scale", Vector2.One * 0.7f, 0.22f);
        tween.SetLoops(3);
    }

    private void FinishWork(string action)
    {
        if (_workMarker != null)
            _workMarker.Visible = false;
        _isWorking = false;
        SetStatus($"{action} complete. Choose the next task.");
    }

    private void CompleteClear()
    {
        if (_treePatch != null)
            _treePatch.Visible = false;
        if (_clearedYard != null)
            _clearedYard.Color = new Color(0.70f, 0.59f, 0.39f, 1f);
        _wallet?.AddAmount("wood", 20);
    }

    private void CompletePreparation()
    {
        if (_preparedPlots != null)
            _preparedPlots.Visible = true;
    }

    private void CompleteWater()
    {
        if (_preparedPlots != null)
            _preparedPlots.Modulate = new Color(0.72f, 0.88f, 1f, 1f);
    }

    private void CompletePlanting()
    {
        if (_preparedPlots != null)
            _preparedPlots.Visible = true;
        if (_cropPatch != null)
            _cropPatch.Visible = true;
    }

    private void CompleteHarvest()
    {
        if (_cropPatch != null)
            _cropPatch.Visible = false;
        _wallet?.AddAmount("coins", 35);
    }

    private void CompleteRoad()
    {
        if (_roadExtension != null)
            _roadExtension.Visible = true;
        _wallet?.AddAmount("stone", -4);
    }

    private void RemoveRoad()
    {
        if (_roadExtension != null)
            _roadExtension.Visible = false;
    }

    private void OnUnitSpawned(Node unit, string workerId, int x, int y)
    {
        AnimateTruck(unit);
        SetStatus($"Truck {workerId} dispatched from the depot.");
    }

    private void OnSpawnRejected(string reason)
        => SetStatus($"Truck request blocked: {reason}.");

    private void AnimateTruck(Node unit)
    {
        if (unit is not Node2D truck)
            return;

        Vector2 origin = truck.Position;
        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Sine);
        tween.SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(truck, "position", origin + new Vector2(84, 42), 0.65f);
        tween.TweenProperty(truck, "position", origin, 0.65f);
    }

    private void SetStatus(string text)
    {
        if (_status != null)
            _status.Text = text;
    }
}

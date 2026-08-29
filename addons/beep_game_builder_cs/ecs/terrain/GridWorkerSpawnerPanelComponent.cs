using Godot;
using System;
using Beep.ECS.UI.Kit;

namespace Beep.ECS
{
    /// <summary>
    /// Simple HUD panel for a base, depot, garage, or barracks that spawns
    /// worker/truck/NPC units through GridWorkerSpawnerComponent.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridWorkerSpawnerPanelComponent : Control
    {
        [Signal] public delegate void SpawnButtonPressedEventHandler();

        [Export] public NodePath SpawnerPath { get; set; } = new("");
        [Export] public NodePath TitleLabelPath { get; set; } = new("");
        [Export] public NodePath CountLabelPath { get; set; } = new("");
        [Export] public NodePath SpawnButtonPath { get; set; } = new("");
        [Export] public bool BuildInEditor { get; set; } = true;
        [Export] public bool GenerateControlsWhenPathsEmpty { get; set; } = false;
        [Export] public bool HideWhenMissingSpawner { get; set; } = false;
        [Export] public string TitleText { get; set; } = "Base";
        [Export] public string SpawnButtonText { get; set; } = "Spawn Worker";
        [Export] public string CountTextFormat { get; set; } = "Workers: {0}/{1}";
        [Export] public Vector2 PanelMinimumSize { get; set; } = new(176, 78);

        private GridWorkerSpawnerComponent? _spawner;
        private Label? _title;
        private Label? _count;
        private Button? _spawnButton;
        private Button? _connectedSpawnButton;

        public override void _Ready()
        {
            ResolveReferences();
            if (!Engine.IsEditorHint() || BuildInEditor)
                CallDeferred(nameof(RebuildPanel));

            if (!Engine.IsEditorHint() && _spawner != null)
            {
                _spawner.UnitSpawned += OnUnitSpawned;
                _spawner.SpawnRejected += OnSpawnRejected;
            }

            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            if (_spawner != null && GodotObject.IsInstanceValid(_spawner))
            {
                _spawner.UnitSpawned -= OnUnitSpawned;
                _spawner.SpawnRejected -= OnSpawnRejected;
            }

            DisconnectSpawnButton();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (SpawnerPath.IsEmpty)
                return new[] { "SpawnerPath should point to a GridWorkerSpawnerComponent." };
            if (!GenerateControlsWhenPathsEmpty && !HasAuthoredControls())
                return new[] { "Set TitleLabelPath, CountLabelPath, and SpawnButtonPath, add scene-authored Title/Count/SpawnButton children, or enable GenerateControlsWhenPathsEmpty." };
            return Array.Empty<string>();
        }

        public void RebuildPanel()
        {
            ResolveReferences();
            if (BindExistingControls())
            {
                RefreshPanel();
                return;
            }

            if (!GenerateControlsWhenPathsEmpty)
                return;

            ClearChildren();

            var panel = new PanelContainer
            {
                Name = "GeneratedWorkerSpawnerPanel",
                CustomMinimumSize = PanelMinimumSize,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            AddChild(panel);
            SetEditedOwner(panel);

            var layout = new VBoxContainer
            {
                Name = "Content",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            KitChrome.SetConstantOverrideIfChanged(layout, "separation", 4);
            panel.AddChild(layout);
            SetEditedOwner(layout);

            _title = new Label
            {
                Name = "Title",
                Text = TitleText,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
            };
            KitChrome.SetColorOverrideIfChanged(_title, "font_color", Colors.White);
            layout.AddChild(_title);
            SetEditedOwner(_title);

            _count = new Label
            {
                Name = "Count",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            KitChrome.SetColorOverrideIfChanged(_count, "font_color", new Color(0.86f, 0.89f, 0.92f));
            layout.AddChild(_count);
            SetEditedOwner(_count);

            _spawnButton = new Button
            {
                Name = "SpawnButton",
                Text = SpawnButtonText,
                CustomMinimumSize = new Vector2(132, 32)
            };
            ConnectSpawnButton();
            layout.AddChild(_spawnButton);
            SetEditedOwner(_spawnButton);

            RefreshPanel();
        }

        public bool RequestSpawn()
        {
            ResolveReferences();
            EmitSignal(SignalName.SpawnButtonPressed);
            Node2D? unit = _spawner?.SpawnWorker();
            RefreshPanel();
            return unit != null;
        }

        public void RefreshPanel()
        {
            ResolveReferences();

            bool hasSpawner = _spawner != null;
            Visible = hasSpawner || !HideWhenMissingSpawner;

            int count = _spawner?.SpawnedCount ?? 0;
            int max = _spawner?.MaxWorkers ?? 0;
            if (_title != null)
                _title.Text = TitleText;
            if (_count != null)
                _count.Text = hasSpawner ? FormatCount(count, max) : "Spawner missing";
            if (_spawnButton != null)
            {
                _spawnButton.Text = SpawnButtonText;
                _spawnButton.Disabled = !hasSpawner || count >= max;
            }
        }

        public string CountText()
        {
            RefreshPanel();
            return _count?.Text ?? "";
        }

        public bool UsesSceneControls()
            => !TitleLabelPath.IsEmpty || !CountLabelPath.IsEmpty || !SpawnButtonPath.IsEmpty
            || FindTitleLabel() != null || FindCountLabel() != null || FindSpawnButton() != null;

        private void OnUnitSpawned(Node unit, string workerId, int x, int y) => RefreshPanel();
        private void OnSpawnRejected(string reason) => RefreshPanel();

        private string FormatCount(int count, int max)
        {
            try
            {
                return string.Format(CountTextFormat, count, max);
            }
            catch (FormatException)
            {
                return $"Workers: {count}/{max}";
            }
        }

        private void ResolveReferences()
        {
            if (_spawner != null && GodotObject.IsInstanceValid(_spawner))
                return;

            if (!SpawnerPath.IsEmpty)
                _spawner = GetNodeOrNull<GridWorkerSpawnerComponent>(SpawnerPath);
            else if (IsInsideTree())
                _spawner = EntityComponent.FindComponent<GridWorkerSpawnerComponent>(GetTree()?.CurrentScene);
        }

        private bool BindExistingControls()
        {
            bool wantsExisting = UsesSceneControls();
            if (!wantsExisting)
                return false;

            Label? title = FindTitleLabel();
            Label? count = FindCountLabel();
            Button? spawnButton = FindSpawnButton();

            if (title == null || count == null || spawnButton == null)
                return false;

            _title = title;
            _count = count;
            _spawnButton = spawnButton;
            ConnectSpawnButton();
            return true;
        }

        private bool HasAuthoredControls()
            => FindTitleLabel() != null && FindCountLabel() != null && FindSpawnButton() != null;

        private Label? FindTitleLabel()
        {
            if (!TitleLabelPath.IsEmpty && GetNodeOrNull<Label>(TitleLabelPath) is { } pathLabel)
                return pathLabel;

            if (FindChild("Title", recursive: true, owned: false) is Label childLabel)
                return childLabel;

            return GetParent()?.FindChild("Title", recursive: true, owned: false) as Label;
        }

        private Label? FindCountLabel()
        {
            if (!CountLabelPath.IsEmpty && GetNodeOrNull<Label>(CountLabelPath) is { } pathLabel)
                return pathLabel;

            if (FindChild("Count", recursive: true, owned: false) is Label childLabel)
                return childLabel;

            return GetParent()?.FindChild("Count", recursive: true, owned: false) as Label;
        }

        private Button? FindSpawnButton()
        {
            if (!SpawnButtonPath.IsEmpty && GetNodeOrNull<Button>(SpawnButtonPath) is { } pathButton)
                return pathButton;

            if (FindChild("SpawnButton", recursive: true, owned: false) is Button childButton)
                return childButton;

            return GetParent()?.FindChild("SpawnButton", recursive: true, owned: false) as Button;
        }

        private void ConnectSpawnButton()
        {
            if (_spawnButton == null)
                return;

            if (_connectedSpawnButton == _spawnButton)
                return;

            DisconnectSpawnButton();
            _spawnButton.Pressed += OnSpawnButtonPressed;
            _connectedSpawnButton = _spawnButton;
        }

        private void DisconnectSpawnButton()
        {
            if (_connectedSpawnButton != null && GodotObject.IsInstanceValid(_connectedSpawnButton))
                _connectedSpawnButton.Pressed -= OnSpawnButtonPressed;

            _connectedSpawnButton = null;
        }

        private void OnSpawnButtonPressed() => RequestSpawn();

        private void ClearChildren()
        {
            DisconnectSpawnButton();
            foreach (Node child in GetChildren())
                child.QueueFree();
            _title = null;
            _count = null;
            _spawnButton = null;
            _connectedSpawnButton = null;
        }

        private void SetEditedOwner(Node node)
        {
            if (!Engine.IsEditorHint())
                return;

            node.Owner = GetTree()?.EditedSceneRoot;
        }
    }
}

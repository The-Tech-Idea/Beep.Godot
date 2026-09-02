using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Optional discovery: the underground stratum starts hidden and survey
    /// work reveals it, cell by cell - prospecting, seismic lines, licence
    /// surveys.
    ///
    /// OFF by default: RevealAll makes IsDiscovered answer true everywhere, so
    /// a game that does not want the mechanic never notices this component
    /// exists. Turn RevealAll off and only surveyed cells answer - the survey
    /// overlay and any game logic that consults IsDiscovered then hide the
    /// rest.
    ///
    /// Surveys arrive as ordinary jobs: queue a job of SurveyJobKind on a
    /// cell ("survey" - the tool palette or a scripted crew can add it), and
    /// when a worker completes it the area around the cell is discovered. The
    /// discovered set is the ONE fact this component owns; what lies under
    /// the cells stays with the data layers.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridProspectingComponent : Node, ISaveable
    {
        [Signal] public delegate void CellsSurveyedEventHandler(int x, int y, int discoveredCount);
        [Signal] public delegate void DepositDiscoveredEventHandler(int x, int y, string resourceId);

        [Export] public bool ParticipatesInSave { get; set; } = true;
        [Export] public string SaveKey { get; set; } = "grid_prospecting.state";
        [Export] public NodePath JobQueuePath { get; set; } = new("");
        [Export] public NodePath DataLayersPath { get; set; } = new("");

        /// <summary>True means everything is visible and surveys are moot.</summary>
        [Export] public bool RevealAll { get; set; } = true;

        [Export] public string SurveyJobKind { get; set; } = "survey";

        /// <summary>Cells around the surveyed cell revealed with it (a square).</summary>
        [Export(PropertyHint.Range, "0,8,1")] public int SurveyRadius { get; set; } = 1;

        [Export] public bool AutoConnect { get; set; } = true;

        private readonly HashSet<Vector2I> _discovered = new();
        private GridJobQueueComponent? _queue;
        private GridJobQueueComponent? _connectedQueue;
        private TerrainDataLayersComponent? _dataLayers;

        public override void _Ready()
        {
            ResolveReferences();
            if (!Engine.IsEditorHint())
            {
                if (AutoConnect)
                    ConnectQueue();
                if (ParticipatesInSave)
                    AddToGroup(SaveableHelper.Group);
            }
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            DisconnectQueue();
            if (ParticipatesInSave)
                RemoveFromGroup(SaveableHelper.Group);
        }

        public void ConnectQueue()
        {
            ResolveReferences();
            if (_queue == null || _connectedQueue == _queue)
                return;

            DisconnectQueue();
            _queue.JobCompleted += OnJobCompleted;
            _connectedQueue = _queue;
        }

        public void DisconnectQueue()
        {
            if (_connectedQueue != null && GodotObject.IsInstanceValid(_connectedQueue))
                _connectedQueue.JobCompleted -= OnJobCompleted;
            _connectedQueue = null;
        }

        /// <summary>Whether the underground beneath a cell may be shown.</summary>
        public bool IsDiscovered(Vector2I cell)
            => RevealAll || _discovered.Contains(cell);

        public int DiscoveredCount => _discovered.Count;

        /// <summary>
        /// Reveals the square around a cell and reports what turned up. The
        /// return is how many cells were newly discovered.
        /// </summary>
        public int Survey(Vector2I cell)
        {
            ResolveReferences();
            int radius = Mathf.Max(0, SurveyRadius);
            int discovered = 0;
            var found = new HashSet<string>();

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new Vector2I(cell.X + dx, cell.Y + dy);
                    if (!_discovered.Add(at))
                        continue;
                    discovered++;

                    string id = _dataLayers?.UndergroundResourceAt(at) ?? "";
                    if (id.Length > 0 && found.Add(id))
                        EmitSignal(SignalName.DepositDiscovered, at.X, at.Y, id);
                }
            }

            EmitSignal(SignalName.CellsSurveyed, cell.X, cell.Y, discovered);
            return discovered;
        }

        private void OnJobCompleted(string jobId, string workerId)
        {
            if (_connectedQueue == null)
                return;

            string kind = _connectedQueue.GetJobKind(jobId);
            if (!string.Equals(kind, NormalizeKind(SurveyJobKind), System.StringComparison.Ordinal))
                return;

            Vector2I cell = _connectedQueue.GetJobCell(jobId);
            if (cell.X != int.MinValue && cell.Y != int.MinValue)
                Survey(cell);
        }

        public Godot.Collections.Dictionary CaptureState()
        {
            var cells = new Godot.Collections.Array<Vector2I>();
            foreach (Vector2I cell in _discovered)
                cells.Add(cell);
            return new Godot.Collections.Dictionary
            {
                ["reveal_all"] = RevealAll,
                ["cells"] = cells
            };
        }

        public void RestoreState(Godot.Collections.Dictionary state)
        {
            RevealAll = GridVariantReader.Bool(state, "reveal_all", RevealAll);
            _discovered.Clear();
            foreach (Variant value in GridVariantReader.Array(state, "cells"))
            {
                Vector2I cell = GridVariantReader.Vector2I(value, new Vector2I(int.MinValue, int.MinValue));
                if (cell.X != int.MinValue && cell.Y != int.MinValue)
                    _discovered.Add(cell);
            }
        }

        public void Save(GameBuilder.GameStateData state)
        {
            if (!string.IsNullOrWhiteSpace(SaveKey))
                state.GameData[SaveKey] = CaptureState();
        }

        public void Load(GameBuilder.GameStateData state)
        {
            if (string.IsNullOrWhiteSpace(SaveKey))
                return;

            if (state.GameData.TryGetValue(SaveKey, out Variant value)
                && GridVariantReader.TryDictionary(value, out Godot.Collections.Dictionary saved))
                RestoreState(saved);
        }

        private static string NormalizeKind(string kind)
            => string.IsNullOrWhiteSpace(kind) ? "survey" : kind.Trim().ToLowerInvariant().Replace(' ', '_');

        private void ResolveReferences()
        {
            if (_queue == null || !GodotObject.IsInstanceValid(_queue))
                _queue = !JobQueuePath.IsEmpty
                    ? GetNodeOrNull<GridJobQueueComponent>(JobQueuePath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridJobQueueComponent>(GetTree()?.CurrentScene) : null;

            // Explicit wire only, like every other DataLayersPath.
            if (_dataLayers == null || !GodotObject.IsInstanceValid(_dataLayers))
                _dataLayers = !DataLayersPath.IsEmpty
                    ? GetNodeOrNull<TerrainDataLayersComponent>(DataLayersPath)
                    : null;
        }
    }
}

using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// The player start areas of a generated map, answered for gameplay: which start this
    /// player has, where it begins, whether a cell is inside its reserved area, and where
    /// its first units can stand.
    ///
    /// Owns no fact. The reservation is the live cells' <c>terrain_start_area</c>
    /// (GridCellDataComponent.GetStartArea: 0 none, k+1 start k), which the generator hands
    /// over once and the grid save carries; the origin is a <c>Spawns</c> marker where the map
    /// has one (TerrainSpawnMarkers - an authored or published map), otherwise the data layers'
    /// StartCells, in the generator's start order. The origin IS the headquarters anchor: the generator
    /// grew the area from GridFootprint.Cells(origin, kit footprint) and checked that
    /// footprint level, so a game places its headquarters at OriginOf(ActiveStartIndex).
    ///
    /// Every engine consumer that used to read "start 0" asks this instead - placement
    /// (RestrictBuildToStartArea), the worker spawner (SpawnAtStartArea) and the camera
    /// (TerrainWorldCameraComponent.StartAreaPath) - so which start a player has is decided
    /// in one place.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridStartAreaComponent : Node, ISaveable
    {
        /// <summary>
        /// The faction-to-start table changed. Anything that draws a start in its faction's colour
        /// - the minimap's tint, the overlay's palette - repaints on this rather than polling.
        /// </summary>
        [Signal] public delegate void AssignmentChangedEventHandler();

        /// <summary>Returned by <see cref="OriginOf"/> for an index that is not a start.</summary>
        public static readonly Vector2I NoCell = new(int.MinValue, int.MinValue);

        [Export] public NodePath CellDataPath { get; set; } = new("");
        [Export] public NodePath DataLayersPath { get; set; } = new("");
        [Export] public NodePath NavigationPath { get; set; } = new("");

        /// <summary>
        /// The map's <c>Spawns</c> node of Start_&lt;k&gt; markers (TerrainSpawnMarkers). Set, it is
        /// where the origins come from and the data layers are not consulted for them - a marker is
        /// the one thing a native map, saved as a plain .tscn without a generator, still carries.
        /// A designer's authored map is read exactly the same way.
        /// </summary>
        [Export] public NodePath SpawnsRootPath { get; set; } = new("");

        /// <summary>The projection a marker's position is read as a cell through. Needed with SpawnsRootPath.</summary>
        [Export] public NodePath GridPath { get; set; } = new("");

        /// <summary>
        /// The start this machine's player was given, as its index in the generator's start order.
        /// Used when no faction catalog is wired; with one, the assignment decides (FEAT-10).
        /// </summary>
        [Export(PropertyHint.Range, "0,254,1")] public int LocalStartIndex { get; set; }

        [ExportGroup("Factions")]
        /// <summary>Who the factions are. Without one, this component answers for one local player.</summary>
        [Export] public GridFactionCatalog? FactionCatalog { get; set; }

        /// <summary>The faction the player at this machine plays, as its id in the catalog.</summary>
        [Export] public string LocalFaction { get; set; } = "";

        /// <summary>
        /// Whether the catalog is dealt onto the map's starts as soon as a world is built. Only
        /// meaningful once a catalog is wired, so a sandbox with none is unchanged.
        /// </summary>
        [Export] public bool AutoAssignOnBuild { get; set; } = true;

        /// <summary>
        /// The world whose WorldBuilt triggers AutoAssignOnBuild. Re-pointing it moves the
        /// subscription: a path edited after _Ready would otherwise keep listening to the old world
        /// while reading the new one's starts.
        /// </summary>
        [Export] public NodePath WorldPath
        {
            get => _worldPath;
            set
            {
                _worldPath = value;
                if (IsInsideTree() && !Engine.IsEditorHint()) BindWorld();
            }
        }
        private NodePath _worldPath = new("");

        [ExportGroup("Save")]
        [Export] public bool ParticipatesInSave { get; set; } = true;
        [Export] public string SaveKey { get; set; } = "grid_world.start_assignment";

        private GridCellDataComponent? _cells;
        private TerrainDataLayersComponent? _dataLayers;
        private GridNavigationComponent? _navigation;
        private GridProjectionComponent? _grid;
        private Node? _spawns;
        private TerrainWorldComponent? _world;

        /// <summary>faction index -> start index. The assignment, and the only owner of it.</summary>
        private readonly Dictionary<int, int> _assignments = new();

        /// <summary>
        /// The start the player acting here has: the one their faction was assigned when a catalog
        /// is wired, else the authored LocalStartIndex. Every consumer asks this - placement, the
        /// spawner, the camera - so which start a player has is decided in one place.
        /// </summary>
        public int ActiveStartIndex
        {
            get
            {
                if (FactionCatalog is not null && LocalFaction.Length > 0)
                {
                    int assigned = StartIndexOf(LocalFaction);
                    if (assigned >= 0) return assigned;
                }
                return Mathf.Clamp(LocalStartIndex, 0, 254);
            }
        }

        public override void _Ready()
        {
            if (!Engine.IsEditorHint())
            {
                if (ParticipatesInSave) AddToGroup(SaveableHelper.Group);
                BindWorld();
                // A world built before this component was ready still has starts to deal out:
                // WorldBuilt has already passed, so ask the world what it built.
                if (_world?.BuiltSize.X > 0) OnWorldBuilt(_world.BuiltSize);
            }
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            if (_world is not null && GodotObject.IsInstanceValid(_world)) _world.WorldBuilt -= OnWorldBuilt;
            _world = null;
            if (ParticipatesInSave) RemoveFromGroup(SaveableHelper.Group);
        }

        private void BindWorld()
        {
            var next = _worldPath.IsEmpty ? null : GetNodeOrNull<TerrainWorldComponent>(_worldPath);
            if (next == _world) return;
            if (_world is not null && GodotObject.IsInstanceValid(_world)) _world.WorldBuilt -= OnWorldBuilt;
            _world = next;
            if (_world is not null) _world.WorldBuilt += OnWorldBuilt;
        }

        /// <summary>
        /// Deals the catalog onto a newly built world - but only when the table this component
        /// holds cannot be that world's.
        ///
        /// Assigning unconditionally here would be wrong three ways, and all three are silent: a
        /// Redraw re-emits WorldBuilt and would discard a lobby's choices; a load whose Load ran
        /// before the world was restored would have its saved assignment overwritten by a fresh
        /// deal; and neither leaves any trace. So an assignment that already fits this map is left
        /// alone, whoever made it, and only a table that does not fit is replaced - with a warning,
        /// because replacing it throws somebody's choices away.
        /// </summary>
        private void OnWorldBuilt(Vector2I size)
        {
            _ = size;
            if (!AutoAssignOnBuild || FactionCatalog is null) return;
            if (_assignments.Count == 0) { AutoAssign(); return; }

            int starts = StartCount;
            foreach (int start in _assignments.Values)
            {
                if (start < starts) continue;
                GD.PushWarning($"[{Name}] the assignment held a start this world does not have ({start} of {starts}); it was dealt again.");
                AutoAssign();
                return;
            }
        }

        /// <summary>
        /// How many starts the map has, from whichever record of them this component reads.
        /// </summary>
        public int StartCount
        {
            get
            {
                if (!SpawnsRootPath.IsEmpty)
                {
                    EntityComponent.ResolveLive(this, SpawnsRootPath, ref _spawns, fallbackWhenEmpty: false);
                    if (_spawns is null) return 0;
                    int count = 0;
                    while (TerrainSpawnMarkers.Find(_spawns, count) is not null) count++;
                    return count;
                }
                EntityComponent.ResolveLive(this, DataLayersPath, ref _dataLayers);
                return _dataLayers?.StartCells().Count ?? 0;
            }
        }

        /// <summary>
        /// Gives a faction a start, or says why it cannot have one. The reasons are returned rather
        /// than pushed: an assignment refused in a lobby is a thing the caller has to show someone.
        ///
        /// Empty on success. <c>unknown_faction</c>, <c>start_out_of_range</c>,
        /// <c>start_taken:&lt;faction&gt;</c>, or <c>start_locked:&lt;n&gt;</c> when the catalog pins
        /// that faction elsewhere.
        /// </summary>
        public string Assign(string factionId, int startIndex)
        {
            if (FactionCatalog is null) return "no_faction_catalog";
            int faction = FactionCatalog.IndexOf(factionId);
            if (faction == 0) return "unknown_faction";
            int starts = StartCount;
            if (startIndex < 0 || (starts > 0 && startIndex >= starts)) return "start_out_of_range";

            int locked = FactionCatalog.LockedStartOf(faction);
            if (locked >= 0 && locked != startIndex) return $"start_locked:{locked}";

            foreach ((int other, int taken) in _assignments)
                if (taken == startIndex && other != faction)
                    return $"start_taken:{FactionCatalog.IdOf(other)}";

            if (_assignments.TryGetValue(faction, out int held) && held == startIndex) return "";
            _assignments[faction] = startIndex;
            EmitSignal(SignalName.AssignmentChanged);
            return "";
        }

        /// <summary>
        /// Deals the catalog's playable factions onto the map's starts: locked factions first, so a
        /// scenario's pinned side cannot be displaced by catalog order, then the rest in catalog
        /// order onto the starts still free. Deterministic - the same catalog and the same map give
        /// the same table every run, which is what lets two machines agree without talking.
        /// </summary>
        public int AutoAssign()
        {
            bool had = _assignments.Count > 0;
            _assignments.Clear();
            if (FactionCatalog is null)
            {
                if (had) EmitSignal(SignalName.AssignmentChanged);
                return 0;
            }
            int starts = StartCount;
            if (starts == 0)
            {
                if (had) EmitSignal(SignalName.AssignmentChanged);
                return 0;
            }

            var taken = new HashSet<int>();
            for (int faction = 1; faction <= FactionCatalog.Count; faction++)
            {
                if (!FactionCatalog.PlayableAt(faction)) continue;
                int locked = FactionCatalog.LockedStartOf(faction);
                if (locked < 0) continue;
                if (locked >= starts || !taken.Add(locked))
                {
                    GD.PushWarning($"[{Name}] faction '{FactionCatalog.IdOf(faction)}' is locked to start {locked}, which this map does not have free; it was left unassigned.");
                    continue;
                }
                _assignments[faction] = locked;
            }

            int next = 0;
            for (int faction = 1; faction <= FactionCatalog.Count; faction++)
            {
                if (!FactionCatalog.PlayableAt(faction) || _assignments.ContainsKey(faction)) continue;
                while (next < starts && taken.Contains(next)) next++;
                if (next >= starts)
                {
                    GD.PushWarning($"[{Name}] this map has {starts} starts, too few for the catalog's playable factions; '{FactionCatalog.IdOf(faction)}' was left unassigned.");
                    break;
                }
                taken.Add(next);
                _assignments[faction] = next;
            }
            EmitSignal(SignalName.AssignmentChanged);
            return _assignments.Count;
        }

        /// <summary>The start a faction was assigned, or -1 when it has none.</summary>
        public int StartIndexOf(string factionId)
        {
            if (FactionCatalog is null) return -1;
            int faction = FactionCatalog.IndexOf(factionId);
            return faction != 0 && _assignments.TryGetValue(faction, out int start) ? start : -1;
        }

        /// <summary>The faction holding a start, as its catalog index, or 0 when nobody does.</summary>
        public int FactionAtStart(int startIndex)
        {
            foreach ((int faction, int start) in _assignments)
                if (start == startIndex) return faction;
            return 0;
        }

        /// <summary>
        /// The colour start k is drawn in: its faction's, when one is assigned and a catalog is
        /// wired. Transparent otherwise, so a caller tints nothing rather than inventing a colour.
        /// </summary>
        public Color ColourOfStart(int startIndex)
            => FactionCatalog is null ? new Color(0f, 0f, 0f, 0f) : FactionCatalog.ColourOf(FactionAtStart(startIndex));

        /// <summary>The assignment, as a game reads it: faction id to start index.</summary>
        public Godot.Collections.Dictionary GetAssignments()
        {
            var result = new Godot.Collections.Dictionary();
            if (FactionCatalog is null) return result;
            foreach ((int faction, int start) in _assignments)
                result[FactionCatalog.IdOf(faction)] = start;
            return result;
        }

        private const int SnapshotVersion = 1;

        public Godot.Collections.Dictionary CaptureState()
            => new() { ["version"] = SnapshotVersion, ["assignments"] = GetAssignments() };

        /// <summary>
        /// Restores the saved assignment. Ids rather than indices on purpose: a catalog edited
        /// between save and load moves every index, and a table keyed by index would then hand a
        /// player someone else's start without anything reporting it.
        /// </summary>
        public void RestoreState(Godot.Collections.Dictionary state)
        {
            int version = GridVariantReader.Int(state, "version");
            if (version != SnapshotVersion)
                throw new System.FormatException($"Start assignment snapshot version {version} cannot be read by this build (expected {SnapshotVersion}).");
            if (FactionCatalog is null)
            {
                // Nothing to restore INTO: without a catalog there are no faction ids, and silently
                // dropping a saved assignment would hand every player start 0 on the next load.
                if (!GridVariantReader.TryDictionary(state.GetValueOrDefault("assignments"), out Godot.Collections.Dictionary orphaned)
                    || orphaned.Count == 0) return;
                throw new System.FormatException($"[{Name}] the save holds {orphaned.Count} start assignments and no FactionCatalog is wired to read them against.");
            }
            _assignments.Clear();
            if (GridVariantReader.TryDictionary(state.GetValueOrDefault("assignments"), out Godot.Collections.Dictionary saved))
            {
                foreach (var pair in saved)
                {
                    int faction = FactionCatalog.IndexOf(pair.Key.AsString());
                    if (faction == 0)
                    {
                        GD.PushWarning($"[{Name}] the save assigns a start to '{pair.Key.AsString()}', which this catalog does not hold; that assignment was dropped.");
                        continue;
                    }
                    _assignments[faction] = GridVariantReader.Int(pair.Value);
                }
            }
            EmitSignal(SignalName.AssignmentChanged);
        }

        public void Save(GameBuilder.GameStateData state)
        {
            if (!string.IsNullOrWhiteSpace(SaveKey)) state.GameData[SaveKey] = CaptureState();
        }

        public void Load(GameBuilder.GameStateData state)
        {
            if (string.IsNullOrWhiteSpace(SaveKey)) return;
            if (state.GameData.TryGetValue(SaveKey, out Variant value)
                && GridVariantReader.TryDictionary(value, out Godot.Collections.Dictionary saved))
                RestoreState(saved);
        }

        public override string[] _GetConfigurationWarnings()
        {
            var warnings = new List<string>();
            if (CellDataPath.IsEmpty) warnings.Add("CellDataPath should point to the GridCellDataComponent that holds the generated start areas.");
            if (DataLayersPath.IsEmpty && SpawnsRootPath.IsEmpty)
                warnings.Add("Set DataLayersPath (a generated map's start order) or SpawnsRootPath (a map's Start_<k> markers); without one there are no start origins.");
            if (!SpawnsRootPath.IsEmpty && GridPath.IsEmpty)
                warnings.Add("SpawnsRootPath needs GridPath: a marker's position is read as a cell through the grid.");
            if (NavigationPath.IsEmpty) warnings.Add("NavigationPath should point to the GridNavigationComponent spawn cells are walked on.");
            if (ParticipatesInSave && string.IsNullOrWhiteSpace(SaveKey))
                warnings.Add("SaveKey cannot be empty while ParticipatesInSave is on: the assignment would be saved under no key and silently lost.");
            if (FactionCatalog is not null && LocalFaction.Length > 0 && FactionCatalog.IndexOf(LocalFaction) == 0)
                warnings.Add($"LocalFaction '{LocalFaction}' is not in the FactionCatalog, so this machine's player falls back to LocalStartIndex.");
            return warnings.ToArray();
        }

        /// <summary>Whether the reservation can be read at all. An unwired cell store has no answer, and "outside" would be a made-up one.</summary>
        internal bool HasCells
        {
            get
            {
                EntityComponent.ResolveLive(this, CellDataPath, ref _cells);
                return _cells is not null;
            }
        }

        /// <summary>
        /// Whether this map reserves ground for its starts at all. False on a map generated
        /// without start areas and on a native map published with markers but no gameplay
        /// baseline: those maps have starts, and nothing to be inside or outside of. A build
        /// restriction asks this before refusing anything, because on such a map every cell
        /// would be "outside".
        /// </summary>
        public bool HasAreas => HasCells && _cells!.HasStartAreas;

        /// <summary>Whether a cell is in start <paramref name="index"/>'s reserved area.</summary>
        public bool IsInArea(Vector2I cell, int index)
        {
            EntityComponent.ResolveLive(this, CellDataPath, ref _cells);
            return _cells is not null && index >= 0 && _cells.GetStartArea(cell) == index + 1;
        }

        /// <summary>
        /// Start <paramref name="index"/>'s origin - its headquarters anchor - in absolute cells,
        /// or <see cref="NoCell"/> when the map has no such start or the data layers are not wired.
        /// </summary>
        public Vector2I OriginOf(int index)
        {
            if (index < 0) return NoCell;
            if (!SpawnsRootPath.IsEmpty) return MarkerOrigin(index);

            EntityComponent.ResolveLive(this, DataLayersPath, ref _dataLayers);
            if (_dataLayers is null) return NoCell;
            Godot.Collections.Array<Vector2I> starts = _dataLayers.StartCells();
            return index < starts.Count ? starts[index] : NoCell;
        }

        /// <summary>
        /// Start <paramref name="index"/>'s marker, as a cell. Markers WIN over the data layers
        /// rather than filling in for them: a map that carries markers is authored or published,
        /// and its own record of where a start stands is the answer, whatever a regenerated
        /// field would say.
        /// </summary>
        private Vector2I MarkerOrigin(int index)
        {
            EntityComponent.ResolveLive(this, SpawnsRootPath, ref _spawns, fallbackWhenEmpty: false);
            EntityComponent.ResolveLive(this, GridPath, ref _grid, fallbackWhenEmpty: false);
            if (_spawns is null || _grid is null)
            {
                // Wired to something that is not there: no answer, rather than quietly reading
                // the data layers, which is how a published map and its regenerated field come
                // to disagree about where a player begins.
                GD.PushWarning($"[{Name}] SpawnsRootPath/GridPath do not resolve; start {index} has no origin.");
                return NoCell;
            }
            return TerrainSpawnMarkers.Find(_spawns, index) is { } marker
                ? _grid.WorldToCell(marker.GlobalPosition) : NoCell;
        }

        /// <summary>
        /// Up to <paramref name="count"/> walkable cells of start <paramref name="index"/>'s area,
        /// nearest the origin first, ordered by (distance², y, x) so the answer is the same on
        /// every machine. Walked by a 4-neighbour flood from the origin over the area - which the
        /// generator grew 4-connected - and filtered by the navigation's bounds and blocks, so a
        /// building placed since generation takes its cells out. Empty when the start does not
        /// exist, the origin is outside its own area (a map generated without start areas), or
        /// the cells or navigation are not wired.
        /// </summary>
        public Godot.Collections.Array<Vector2I> SpawnCellsFor(int index, int count)
        {
            var result = new Godot.Collections.Array<Vector2I>();
            EntityComponent.ResolveLive(this, NavigationPath, ref _navigation);
            Vector2I origin = OriginOf(index);
            if (count <= 0 || _navigation is null || origin == NoCell || !IsInArea(origin, index))
                return result;

            var area = new List<Vector2I>();
            var seen = new HashSet<Vector2I> { origin };
            var frontier = new Queue<Vector2I>();
            frontier.Enqueue(origin);
            while (frontier.Count > 0)
            {
                Vector2I at = frontier.Dequeue();
                area.Add(at);
                foreach (Vector2I step in Steps)
                {
                    Vector2I next = at + step;
                    if (seen.Add(next) && IsInArea(next, index))
                        frontier.Enqueue(next);
                }
            }

            area.Sort((left, right) =>
            {
                int byDistance = (left - origin).LengthSquared().CompareTo((right - origin).LengthSquared());
                if (byDistance != 0) return byDistance;
                int byRow = left.Y.CompareTo(right.Y);
                return byRow != 0 ? byRow : left.X.CompareTo(right.X);
            });

            foreach (Vector2I cell in area)
            {
                if (!_navigation.IsInBounds(cell) || _navigation.IsBlocked(cell)) continue;
                result.Add(cell);
                if (result.Count == count) break;
            }
            return result;
        }

        private static readonly Vector2I[] Steps = { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down };
    }
}

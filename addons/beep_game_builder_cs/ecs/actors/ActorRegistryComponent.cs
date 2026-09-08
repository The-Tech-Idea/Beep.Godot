using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Beep.ECS;

/// <summary>Scene-local actor/player lookup. Does not generate terrain or own a second world.</summary>
[Tool, GlobalClass]
public partial class ActorRegistryComponent : Node, ISaveable
{
    [Export] public string SaveKey { get; set; } = "actors";
    [Export] public NodePath ActorsRootPath { get; set; } = new("");
    [Export] public Godot.Collections.Array<ActorDefinition> Definitions { get; set; } = new();
    private float _spatialCellSize = 256;
    [Export] public float SpatialCellSize
    {
        get => _spatialCellSize;
        set
        {
            float size = float.IsFinite(value) ? Mathf.Max(1, value) : 256;
            if (size == _spatialCellSize) return;
            _spatialCellSize = size;
            _spatial.Clear();
            _actorCells.Clear();
            foreach (var actor in _actors.Values) UpdatePosition(actor);
            foreach (var (id, record) in _dormant)
                UpdateSpatialPosition(id, new(record["x"].AsSingle(), record["y"].AsSingle()));
        }
    }
    [Signal] public delegate void ActorRegisteredEventHandler(string actorId);
    [Signal] public delegate void ActorRemovedEventHandler(string actorId);
    [Signal] public delegate void CommandRejectedEventHandler(string actorId, string reason);
    [Signal] public delegate void CommandAcceptedEventHandler(string actorId, int action);

    private readonly Dictionary<string, ActorComponent> _actors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PlayerContextComponent> _players = new(StringComparer.Ordinal);
    private readonly Dictionary<Vector2I, HashSet<string>> _spatial = new();
    private readonly Dictionary<string, Vector2I> _actorCells = new(StringComparer.Ordinal);
    private readonly HashSet<string> _hostilities = new(StringComparer.Ordinal);
    private long _nextId = 1;
    public int ActorCount => _actors.Count + _dormant.Count;

    public override void _Ready()
    {
        if (!Engine.IsEditorHint()) AddToGroup(SaveableHelper.Group);
        RefreshChunkPins();
    }

    public bool RegisterActor(ActorComponent actor)
    {
        if (actor.Body is null || !actor.IsInsideTree()) return false;
        if (string.IsNullOrWhiteSpace(actor.ActorId))
        {
            do { actor.ActorId = $"actor_{_nextId++}"; } while (_actors.ContainsKey(actor.ActorId) || _dormant.ContainsKey(actor.ActorId));
        }
        if (_dormant.ContainsKey(actor.ActorId)) return false;
        if (_actors.TryGetValue(actor.ActorId, out var existing)) return existing == actor;
        _actors.Add(actor.ActorId, actor);
        UpdatePosition(actor);
        EmitSignal(SignalName.ActorRegistered, actor.ActorId);
        return true;
    }

    public void UnregisterActor(ActorComponent actor)
    {
        if (!_actors.TryGetValue(actor.ActorId, out var existing) || existing != actor) return;
        ReleaseActorChunkPins(actor);
        ReleaseFollowRest(actor);
        _actors.Remove(actor.ActorId);
        if (_dormant.ContainsKey(actor.ActorId)) return;
        RemoveSpatialEntry(actor.ActorId);
        foreach (var player in _players.Values) player.ForgetActor(actor.ActorId);
        EmitSignal(SignalName.ActorRemoved, actor.ActorId);
    }

    internal bool RegisterPlayer(PlayerContextComponent player)
    {
        if (string.IsNullOrWhiteSpace(player.PlayerId) || _players.ContainsKey(player.PlayerId)) return false;
        _players.Add(player.PlayerId, player);
        return true;
    }

    internal void UnregisterPlayer(PlayerContextComponent player)
    {
        if (_players.GetValueOrDefault(player.PlayerId) == player) _players.Remove(player.PlayerId);
    }

    public ActorComponent? FindActor(string id)
        => _actors.TryGetValue(id, out var actor) && GodotObject.IsInstanceValid(actor)
            && !actor.IsQueuedForDeletion() && actor.Body is { } body && !body.IsQueuedForDeletion() ? actor : null;

    public PlayerContextComponent? FindPlayer(string id) => _players.GetValueOrDefault(id);

    public Godot.Collections.Array<string> GetOwnedActors(string ownerId)
        => new(_actors.Values.Where(a => a.OwnerId == ownerId).Select(a => a.ActorId)
            .Concat(_dormant.Where(p => p.Value["owner"].AsString() == ownerId).Select(p => p.Key)).Order(StringComparer.Ordinal));

    public bool TransferOwnership(string actorId, string ownerId)
    {
        var actor = FindActor(actorId);
        if ((actor is null && !_dormant.ContainsKey(actorId)) || (ownerId.Length > 0 && !_players.ContainsKey(ownerId))) return false;
        foreach (var player in _players.Values) player.ForgetActor(actorId);
        if (actor is not null) actor.ChangeOwner(ownerId); else _dormant[actorId]["owner"] = ownerId;
        return true;
    }

    private static string RelationKey(string a, string b)
        => string.CompareOrdinal(a, b) < 0 ? $"{a.Length}:{a}{b}" : $"{b.Length}:{b}{a}";

    public void SetHostile(string factionA, string factionB, bool hostile)
    {
        string key = RelationKey(factionA, factionB);
        if (hostile && factionA != factionB) _hostilities.Add(key); else _hostilities.Remove(key);
    }

    public bool AreHostile(string ownerA, string ownerB)
        => ownerA != ownerB && FindPlayer(ownerA) is { } a && FindPlayer(ownerB) is { } b
            && _hostilities.Contains(RelationKey(a.FactionId, b.FactionId));

    /// <summary>Validate the entire submission before replacing any recipient's existing orders.</summary>
    public int Submit(ActorCommand command)
        => SubmitTracked(command, null);

    internal int SubmitTracked(ActorCommand command, Action<ActorComponent, ulong>? accepted)
    {
        if (GetTree().Paused || !_players.ContainsKey(command.IssuerId) || !Enum.IsDefined(command.Action))
            return Reject("", "invalid_issuer_action_or_paused");
        var recipients = new List<ActorComponent>();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in command.Recipients)
        {
            if (!_actors.ContainsKey(id) && !_dormant.ContainsKey(id)) return Reject(id, "actor_missing");
            if (GetActorOwner(id) != command.IssuerId) return Reject(id, "not_owner");
        }
        foreach (string id in command.Recipients)
        {
            if (!unique.Add(id)) continue;
            var actor = WakeActor(id);
            if (actor is null) return Reject(id, "actor_missing");
            if (actor.OwnerId != command.IssuerId) return Reject(id, "not_owner");
            string error = actor.Validate(command);
            if (error.Length > 0) return Reject(id, error);
            recipients.Add(actor);
        }
        if (recipients.Count == 0) return Reject("", "no_recipients");
        foreach (var actor in recipients)
        {
            actor.Enqueue(command);
            accepted?.Invoke(actor, actor.CommandRevision);
            EmitSignal(SignalName.CommandAccepted, actor.ActorId, (int)command.Action);
        }
        return recipients.Count;
    }

    private int Reject(string actorId, string reason)
    {
        EmitSignal(SignalName.CommandRejected, actorId, reason);
        return 0;
    }

    public ActorComponent? SpawnActor(string definitionId, string ownerId, Vector2 worldPosition, string actorId = "")
    {
        if (!worldPosition.IsFinite() || (ownerId.Length > 0 && !_players.ContainsKey(ownerId))) return null;
        ActorDefinition? definition = Definitions.FirstOrDefault(d => d is not null && d.Id == definitionId);
        var root = GetNodeOrNull<Node2D>(ActorsRootPath);
        if (definition?.Scene is null || root is null || (actorId.Length > 0 && (_actors.ContainsKey(actorId) || _dormant.ContainsKey(actorId)))) return null;
        var instance = definition.Scene.Instantiate();
        var actor = ActorComponent.ForBody(instance);
        if (instance is not Node2D body || actor is null) { instance.Free(); return null; }
        actor.ActorId = actorId;
        actor.OwnerId = ownerId;
        actor.Definition = definition;
        // Absolute registry paths work before parenting; all identity is ready before _Ready runs.
        actor.RegistryPath = GetPath();
        body.Position = root.ToLocal(worldPosition);
        root.AddChild(body);
        if (actor.Registry != this) { body.QueueFree(); return null; }
        return actor;
    }

    internal void UpdatePosition(ActorComponent actor)
    {
        Vector2? position = actor.Body?.GlobalPosition;
        UpdateActorChunkPins(actor, position);
        if (position is { } current && current.IsFinite())
            UpdateSpatialPosition(actor.ActorId, current);
    }

    private void UpdateSpatialPosition(string actorId, Vector2 position)
    {
        float size = float.IsFinite(SpatialCellSize) ? Mathf.Max(1, SpatialCellSize) : 256;
        var cell = new Vector2I(Mathf.FloorToInt(position.X / size), Mathf.FloorToInt(position.Y / size));
        if (_actorCells.TryGetValue(actorId, out var old))
        {
            if (old == cell) return;
            if (_spatial.TryGetValue(old, out var previous))
            {
                previous.Remove(actorId);
                if (previous.Count == 0) _spatial.Remove(old);
            }
        }
        _actorCells[actorId] = cell;
        if (!_spatial.TryGetValue(cell, out var bucket)) _spatial[cell] = bucket = new();
        bucket.Add(actorId);
    }

    public int LastQueryBucketVisits { get; private set; }
    public int LastQueryCandidateCount { get; private set; }

    public Godot.Collections.Array<string> QueryActors(Rect2 area, string ownerId = "", bool includeDormant = false)
    {
        LastQueryBucketVisits = LastQueryCandidateCount = 0;
        var result = new List<string>();
        if (!area.Position.IsFinite() || !area.Size.IsFinite()) return new();
        area = area.Abs();
        if (!area.End.IsFinite()) return new();
        float size = float.IsFinite(SpatialCellSize) ? Mathf.Max(1, SpatialCellSize) : 256;
        int left = Mathf.FloorToInt(area.Position.X / size), right = Mathf.FloorToInt(area.End.X / size);
        int top = Mathf.FloorToInt(area.Position.Y / size), bottom = Mathf.FloorToInt(area.End.Y / size);
        void Collect(HashSet<string> ids)
        {
            foreach (string id in ids)
            {
                LastQueryCandidateCount++;
                if ((includeDormant || FindActor(id) is not null) && (ownerId.Length == 0 || GetActorOwner(id) == ownerId)
                    && area.HasPoint(GetActorPosition(id))) result.Add(id);
            }
        }
        // Small local queries use direct bucket lookup; overview queries visit only occupied buckets.
        double columns = (long)right - left + 1, rows = (long)bottom - top + 1;
        if (columns > 0 && rows > 0 && columns * rows <= _spatial.Count)
        {
            for (long y = top; y <= bottom; y++)
                for (long x = left; x <= right; x++)
                {
                    LastQueryBucketVisits++;
                    if (_spatial.TryGetValue(new((int)x, (int)y), out var ids)) Collect(ids);
                }
        }
        else
        {
            foreach (var (cell, ids) in _spatial)
            {
                LastQueryBucketVisits++;
                if (cell.X >= left && cell.X <= right && cell.Y >= top && cell.Y <= bottom) Collect(ids);
            }
        }
        result.Sort(StringComparer.Ordinal);
        return new(result);
    }

    public Godot.Collections.Dictionary CaptureState()
    {
        var actors = new Godot.Collections.Array();
        foreach (string id in GetActorIds()) actors.Add(GetActorRecord(id));
        var players = new Godot.Collections.Dictionary();
        foreach (var player in _players.Values) players[player.PlayerId] = player.CaptureState();
        return new() { ["actors"] = actors, ["players"] = players, ["relations"] = new Godot.Collections.Array<string>(_hostilities), ["next_id"] = _nextId };
    }

    public void RestoreState(Godot.Collections.Dictionary snapshot)
    {
        var actors = snapshot["actors"].AsGodotArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        // Check the whole manifest before spawning or deleting any live instance.
        foreach (var item in actors)
        {
            var record = item.AsGodotDictionary();
            string id = record["id"].AsString();
            if (id.Length == 0 || !seen.Add(id)) throw new InvalidOperationException("Duplicate or empty saved actor ID.");
            if (FindActor(id) is null && !Definitions.Any(d => d is not null && d.Id == record["definition"].AsString() && d.Scene is not null))
                throw new InvalidOperationException($"No scene definition for saved actor {id}.");
        }
        foreach (string id in GetActorIds())
            if (!seen.Contains(id)) RemoveActorCore(id, permanent: false);
        foreach (var item in actors)
        {
            var record = item.AsGodotDictionary();
            string id = record["id"].AsString();
            if (!record["resident"].AsBool())
            {
                var existing = FindActor(id);
                _dormant[id] = record.Duplicate(true);
                if (existing?.Body is { } body)
                {
                    body.GetParent().RemoveChild(body);
                    body.QueueFree();
                }
                UpdateSpatialPosition(id, new(record["x"].AsSingle(), record["y"].AsSingle()));
                continue;
            }
            _dormant.Remove(id);
            if (FindActor(id) is null && SpawnActor(record["definition"].AsString(), record["owner"].AsString(),
                new(record["x"].AsSingle(), record["y"].AsSingle()), id) is null)
                throw new InvalidOperationException($"Cannot restore actor {id}.");
        }
        foreach (var item in actors)
        {
            var record = item.AsGodotDictionary();
            if (record["resident"].AsBool()) FindActor(record["id"].AsString())!.RestoreActor(record);
        }
        _hostilities.Clear();
        foreach (var relation in snapshot["relations"].AsGodotArray()) _hostilities.Add(relation.AsString());
        _nextId = Math.Max(1, snapshot["next_id"].AsInt64());
        foreach (var (key, value) in snapshot["players"].AsGodotDictionary())
            FindPlayer(key.AsString())?.RestoreState(value.AsGodotDictionary());
    }

    public void Save(GameBuilder.GameStateData state) => state.GameData[SaveKey] = CaptureState();
    public void Load(GameBuilder.GameStateData state)
    {
        if (state.GameData.TryGetValue(SaveKey, out var snapshot)) RestoreState(snapshot.AsGodotDictionary());
    }
}

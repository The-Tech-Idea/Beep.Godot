using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class ActorOrdersControllerComponent
{
    public Godot.Collections.Dictionary CaptureFormations()
    {
        var plans = new Godot.Collections.Array();
        if (IsFormationPending)
        {
            plans.Add(CapturePlan(_formation, _candidates, _append));
            foreach (var plan in _queuedFormations) plans.Add(CapturePlan(plan.Members, plan.Candidates, plan.Append));
        }
        return new()
        {
            ["version"] = 1, ["issuer"] = _player?.PlayerId ?? "", ["plans"] = plans,
            ["assigned"] = CaptureCells(_assigned), ["dispatched"] = _dispatched, ["rejected"] = _rejected
        };
    }

    private Godot.Collections.Dictionary CapturePlan(IEnumerable<FormationMember> members, IEnumerable<Vector2I> candidates, bool append)
    {
        var actors = new Godot.Collections.Array();
        foreach (var member in members)
            actors.Add(new Godot.Collections.Dictionary
            {
                ["id"] = member.Id,
                ["eligible"] = member.Actor is not null && FormationActorValid(member.Actor, member.Revision)
            });
        return new() { ["members"] = actors, ["candidates"] = CaptureCells(candidates), ["append"] = append };
    }

    private static Godot.Collections.Array CaptureCells(IEnumerable<Vector2I> cells)
    {
        var result = new Godot.Collections.Array();
        foreach (var cell in cells) result.Add(new Godot.Collections.Array { cell.X, cell.Y });
        return result;
    }

    /// <summary>Restore after the actor registry. Validate before replacing live plans; restart searches, never runtime IDs.</summary>
    public bool RestoreFormations(Godot.Collections.Dictionary snapshot)
    {
        if (!IsInsideTree() || !GodotObject.IsInstanceValid(_player?.Registry)
            || !GodotObject.IsInstanceValid(_navigation) || !GodotObject.IsInstanceValid(_grid)) return false;
        try
        {
            if (ReadInt(snapshot, "version", 1, 1) != 1 || ReadString(snapshot, "issuer") != _player!.PlayerId) return false;
            var saved = ReadArray(snapshot, "plans", 65);
            var assigned = ReadCells(snapshot, "assigned");
            int dispatched = ReadInt(snapshot, "dispatched", 0, 65536);
            int rejected = ReadInt(snapshot, "rejected", 0, 65536);
            if (saved.Count == 0 && (assigned.Count > 0 || dispatched != 0 || rejected != 0)) return false;
            if (assigned.Count != dispatched) return false;
            var plans = new Queue<FormationPlan>();
            foreach (var entry in saved)
            {
                if (entry.VariantType != Variant.Type.Dictionary) return false;
                var data = entry.AsGodotDictionary();
                bool append = ReadBool(data, "append");
                if (plans.Count > 0 && !append) return false;
                var candidates = ReadCells(data, "candidates");
                if (candidates.Count == 0) return false;
                var members = new Queue<FormationMember>();
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in ReadArray(data, "members", 65536))
                {
                    if (item.VariantType != Variant.Type.Dictionary) return false;
                    var member = item.AsGodotDictionary();
                    string id = ReadString(member, "id");
                    if (id.Length == 0 || !ids.Add(id)) return false;
                    bool eligible = ReadBool(member, "eligible");
                    var actor = eligible ? _player.Registry!.FindActor(id) : null;
                    if (actor is not null && actor.OwnerId != _player.PlayerId) actor = null;
                    members.Enqueue(new(actor, actor?.CommandRevision ?? 0, id));
                }
                if (members.Count == 0) return false;
                plans.Enqueue(new(members, candidates, append));
            }
            if (plans.TryPeek(out var first))
                foreach (var cell in assigned)
                    if (!first.Candidates.Contains(cell)) return false;
            CancelFormation();
            _issuer = _player.PlayerId;
            if (plans.TryDequeue(out var current))
            {
                StartFormation(current);
                foreach (var plan in plans) _queuedFormations.Enqueue(plan);
                foreach (var cell in assigned) _assigned.Add(cell);
                _dispatched = dispatched; _rejected = rejected;
            }
            return true;
        }
        catch (FormatException) { return false; }
    }

    private static Variant Read(Godot.Collections.Dictionary data, string key)
        => data.TryGetValue(key, out var value) ? value : throw new FormatException($"Missing {key}");
    private static string ReadString(Godot.Collections.Dictionary data, string key)
    {
        var value = Read(data, key);
        return value.VariantType == Variant.Type.String ? value.AsString() : throw new FormatException(key);
    }
    private static bool ReadBool(Godot.Collections.Dictionary data, string key)
    {
        var value = Read(data, key);
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : throw new FormatException(key);
    }
    private static int ReadInt(Godot.Collections.Dictionary data, string key, int min, int max)
        => ReadInteger(Read(data, key), min, max);
    private static int ReadInteger(Variant value, int min, int max)
    {
        if (value.VariantType is not (Variant.Type.Int or Variant.Type.Float)) throw new FormatException("Expected integer");
        double number = value.AsDouble();
        if (!double.IsFinite(number) || number != Math.Truncate(number) || number < min || number > max) throw new FormatException("Invalid integer");
        return (int)number;
    }
    private static Godot.Collections.Array ReadArray(Godot.Collections.Dictionary data, string key, int max)
    {
        var value = Read(data, key);
        if (value.VariantType != Variant.Type.Array) throw new FormatException(key);
        var array = value.AsGodotArray();
        return array.Count <= max ? array : throw new FormatException("Array exceeds limit");
    }
    private static List<Vector2I> ReadCells(Godot.Collections.Dictionary data, string key)
    {
        var result = new List<Vector2I>();
        var seen = new HashSet<Vector2I>();
        foreach (var item in ReadArray(data, key, 4225))
        {
            if (item.VariantType != Variant.Type.Array) throw new FormatException("Invalid cell");
            var pair = item.AsGodotArray();
            if (pair.Count != 2) throw new FormatException("Invalid coordinate pair");
            var cell = new Vector2I(ReadInteger(pair[0], int.MinValue, int.MaxValue), ReadInteger(pair[1], int.MinValue, int.MaxValue));
            if (!seen.Add(cell)) throw new FormatException("Duplicate cell");
            result.Add(cell);
        }
        return result;
    }
}

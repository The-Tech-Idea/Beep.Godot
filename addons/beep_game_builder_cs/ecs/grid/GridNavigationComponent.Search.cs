using Godot;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class GridNavigationComponent
{
    private sealed class PathSearch
    {
        private readonly GridNavigationComponent _owner;
        public readonly Search Rules;
        private readonly Vector2I _goal;
        private readonly PriorityQueue<Vector2I, float> _open = new();
        private readonly Dictionary<Vector2I, Vector2I> _cameFrom = new();
        private readonly Dictionary<Vector2I, float> _bestCost = new();
        private readonly HashSet<Vector2I> _closed = new();
        private int _visited;
        public bool Complete { get; private set; }
        public string Reason { get; private set; } = "";
        public Godot.Collections.Array<Vector2I> Path { get; private set; } = new();

        public PathSearch(GridNavigationComponent owner, Search rules, Vector2I start, Vector2I goal)
        {
            _owner = owner; Rules = rules; _goal = goal;
            if (!owner.IsCellAllowed(rules, start, owner.AllowBlockedStart)) Finish("start_blocked_or_out_of_bounds");
            else if (!owner.IsCellAllowed(rules, goal, owner.AllowBlockedGoal)) Finish("goal_blocked_or_out_of_bounds");
            else
            {
                _bestCost[start] = 0;
                _open.Enqueue(start, owner.Heuristic(rules, start, goal));
            }
        }

        // Count heap removals, including stale entries, so duplicate candidates also consume the budget.
        public int Step(int budget)
        {
            int used = 0;
            while (!Complete && _open.Count > 0 && used < budget)
            {
                Vector2I current = _open.Dequeue();
                used++;
                if (!_closed.Add(current)) continue;
                if (++_visited > _owner.MaxVisitedCells) { Finish("max_visited_cells"); break; }
                if (current == _goal)
                {
                    Path = _owner.Reconstruct(_cameFrom, current);
                    Finish("");
                    break;
                }
                foreach (Vector2I next in _owner.Neighbors(Rules, current, _goal))
                {
                    if (_closed.Contains(next)) continue;
                    float cost = _bestCost[current] + StepCost(Rules, current, next);
                    if (_bestCost.TryGetValue(next, out float old) && cost >= old) continue;
                    _bestCost[next] = cost;
                    _cameFrom[next] = current;
                    _open.Enqueue(next, cost + _owner.Heuristic(Rules, next, _goal));
                }
            }
            if (!Complete && _open.Count == 0) Finish("no_path");
            return used;
        }

        private void Finish(string reason) { Reason = reason; Complete = true; }
    }
}

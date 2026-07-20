using Godot;
using System;
using System.Collections.Generic;

namespace Beep.GameBuilder;

/// <summary>Simple service locator / DI container. Register and resolve services by type.</summary>
public static class BeepServiceLocator
{
    private static Dictionary<Type, object> _services = new();
    private static Dictionary<Type, Func<object>> _factories = new();

    public static void Register<T>(T instance) { _services[typeof(T)] = instance; }
    public static void Register<T>(Func<T> factory) { _factories[typeof(T)] = () => factory(); }
    public static T Resolve<T>() where T : class
    {
        var t = typeof(T);
        if (_services.TryGetValue(t, out var svc)) return svc as T;
        if (_factories.TryGetValue(t, out var fac)) { var inst = fac() as T; _services[t] = inst; return inst; }
        return null;
    }
    public static bool Has<T>() => _services.ContainsKey(typeof(T)) || _factories.ContainsKey(typeof(T));
    public static void Clear() { _services.Clear(); _factories.Clear(); }
}

/// <summary>Grid-based menu navigation for keyboard/controller. Wrap a GridContainer and navigate with arrows.</summary>
public class BeepGridNavigator
{
    private Godot.Control[,] _grid;
    private int _cols, _rows;
    private int _cx, _cy;

    public int CurrentIndex => _cy * _cols + _cx;
    public Godot.Control CurrentItem => _grid[_cy, _cx];
    public Action<Godot.Control, int, int> ItemSelected;

    public void Setup(Godot.Collections.Array<Node> children, int columns)
    {
        _cols = columns;
        _rows = Mathf.CeilToInt((float)children.Count / columns);
        _grid = new Godot.Control[_rows, _cols];
        for (int i = 0; i < children.Count; i++)
        {
            int r = i / columns, c = i % columns;
            if (children[i] is Godot.Control ctrl) _grid[r, c] = ctrl;
        }
        HighlightCurrent();
    }

    public void ProcessInput(InputEvent e)
    {
        if (e is InputEventKey k && k.Pressed)
        {
            switch (k.Keycode)
            {
                case Key.Left: Move(-1, 0); break;
                case Key.Right: Move(1, 0); break;
                case Key.Up: Move(0, -1); break;
                case Key.Down: Move(0, 1); break;
                case Key.Enter: case Key.Space: Confirm(); break;
            }
        }
    }

    private void Move(int dx, int dy)
    {
        UnhighlightCurrent();
        int nx = Mathf.Clamp(_cx + dx, 0, _cols - 1);
        int ny = Mathf.Clamp(_cy + dy, 0, _rows - 1);

        // Skip empty cells in the horizontal direction, but never loop past the grid edge. On a ragged
        // last row, Clamp pins _cx at the final column while that cell is null, so the old
        // `while (grid==null) _cx = Clamp(_cx+dx)` spun forever. Advance only while the index changes.
        if (dx != 0)
        {
            int scan = nx;
            while (_grid[ny, scan] == null)
            {
                int next = Mathf.Clamp(scan + dx, 0, _cols - 1);
                if (next == scan) break; // reached the edge with no non-null cell this way
                scan = next;
            }
            if (_grid[ny, scan] == null) { HighlightCurrent(); return; } // nothing to land on — stay put
            nx = scan;
        }
        else if (_grid[ny, nx] == null)
        {
            HighlightCurrent(); return; // vertical move into an empty (ragged) cell — stay put
        }

        _cx = nx; _cy = ny;
        HighlightCurrent();
    }

    public void GoTo(int index)
    {
        UnhighlightCurrent(); _cy = index / _cols; _cx = index % _cols; HighlightCurrent();
    }

    private void HighlightCurrent()
    {
        var item = _grid[_cy, _cx];
        if (item is Button b) b.GrabFocus();
        else if (item != null) item.Modulate = new Color(1, 1, 1, 1);
    }

    private void UnhighlightCurrent()
    {
        var item = _grid[_cy, _cx];
        if (item != null && item is not Button) item.Modulate = new Color(0.8f, 0.8f, 0.8f, 1);
    }

    private void Confirm() => ItemSelected?.Invoke(CurrentItem, _cx, _cy);
}

/// <summary>Fluent tween chain builder. Chain multiple tweens on one or more targets.</summary>
public static class BeepTweenChain
{
    public class Builder
    {
        private Node _host;
        private Tween _tween;
        public Builder(Node host) { _host = host; _tween = host.CreateTween(); }

        public Builder ThenProp(Node target, string property, Variant finalVal, float duration)
        {
            _tween.TweenProperty(target, property, finalVal, duration);
            return this;
        }
        public Builder ThenCall(Action action)
        {
            _tween.TweenCallback(Callable.From(action));
            return this;
        }
        public Builder ThenInterval(float seconds)
        {
            _tween.TweenInterval(seconds);
            return this;
        }
        public Builder WithEase(Tween.EaseType ease, Tween.TransitionType trans = Tween.TransitionType.Linear)
        {
            _tween.SetEase(ease); _tween.SetTrans(trans);
            return this;
        }
        public Builder Parallel(Action<ParallelBuilder> build)
        {
            var pb = new ParallelBuilder(_tween);
            _tween.Parallel();
            build(pb);
            return this;
        }
        public Tween Done() => _tween;
    }

    public class ParallelBuilder
    {
        private Tween _tween;
        public ParallelBuilder(Tween t) => _tween = t;
        public ParallelBuilder Prop(Node t, string p, Variant v, float d) { _tween.TweenProperty(t, p, v, d); return this; }
        public ParallelBuilder Call(Action a) { _tween.TweenCallback(Callable.From(a)); return this; }
    }

    public static Builder On(Node host) => new(host);
}

/// <summary>Common math utilities missing from Godot Mathf.</summary>
public static class BeepMathHelper
{
    public static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
        => toMin + (value - fromMin) / (fromMax - fromMin) * (toMax - toMin);

    public static float Approach(float current, float target, float delta)
        => Mathf.Abs(target - current) <= delta ? target : current + Mathf.Sign(target - current) * delta;

    public static float Wrap(float value, float min, float max) => ((value - min) % (max - min) + (max - min)) % (max - min) + min;

    public static float SmoothDamp(float current, float target, ref float velocity, float smoothTime, float delta, float maxSpeed = float.MaxValue)
    {
        smoothTime = Mathf.Max(0.0001f, smoothTime);
        float omega = 2f / smoothTime;
        float x = omega * delta;
        float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
        float change = current - target;
        float maxChange = maxSpeed * smoothTime;
        change = Mathf.Clamp(change, -maxChange, maxChange);
        float temp = (velocity + omega * change) * delta;
        velocity = (velocity - omega * temp) * exp;
        float result = target + (change + temp) * exp;
        if (target - current > 0 == result > target) { result = target; velocity = (result - target) / delta; }
        return result;
    }

    public static bool Chance(float percent) => GD.Randf() < percent / 100f;

    public static float AngleBetween(Vector2 a, Vector2 b) => Mathf.RadToDeg((b - a).Angle());

    public static Vector2 RandomInCircle(float radius)
    {
        float angle = GD.Randf() * Mathf.Tau;
        float r = radius * Mathf.Sqrt(GD.Randf());
        return new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
    }

    public static Vector2 RandomOnCircle(float radius)
    {
        float angle = GD.Randf() * Mathf.Tau;
        return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
    }
}

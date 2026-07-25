using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Base for the per-genre HUD components. The HUD LAYOUT is authored as a static block in the
    /// genre's main <c>.tscn</c> (labels, bars, minimap, resource bar, …). Each genre ships its OWN
    /// concrete subclass here (PlatformerHudComponent, RacingHudComponent, …) that is attached in
    /// that block and DRIVES those static labels in C#:
    ///  • the readouts the framework owns bind live — score/lives from <c>GameFlowComponent</c>,
    ///    level from <c>GameApp</c>, health from the player's <c>HealthComponent</c>;
    ///  • the genre-specific readouts (speed, mana, hunger, resources, deck size) are registered as
    ///    <see cref="Placeholder"/>s — they keep their authored text and warn once, and the game
    ///    drives them through <see cref="SetStat"/>. Never silent dead text.
    ///
    /// Attach the genre component as a child of the HUD's content Control (the "Root" node): label
    /// NodePaths resolve relative to that parent, exactly like <c>HudComponent</c>.
    /// </summary>
    [Tool]
    public abstract partial class GenreHudComponent : UIComponent
    {
        /// <summary>Skin genre key, for warnings. (Theming itself is the scene's ThemePresetComponent.)</summary>
        protected abstract string Genre { get; }

        /// <summary>Bind the genre's labels here via BindScore/BindLives/BindLevel/BindHealth/Placeholder.</summary>
        protected abstract void Wire();

        private Node? _host;
        private GameFlowComponent? _flow;
        private GameApp? _app;
        private HealthComponent? _health;

        private Label? _score, _lives, _level, _healthLabel;
        private string _levelFormat = "Level {0}";
        private readonly Dictionary<string, Label> _placeholders = new();

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            CallDeferred(nameof(Setup));
        }

        private void Setup()
        {
            _host = GetParent();
            if (_host == null)
            {
                GD.PushWarning($"[{Name}] {Genre} HUD component has no parent to resolve labels against.");
                return;
            }
            _flow = FindInScene<GameFlowComponent>();
            _app = GameApp.Instance;
            _health = FindInScene<HealthComponent>();
            Wire();
        }

        // ── Binding API used by the per-genre subclasses ───────────────

        protected Label? Resolve(NodePath path)
            => (path is null || path.IsEmpty) ? null : _host?.GetNodeOrNull<Label>(path);

        protected void BindScore(NodePath path)
        {
            var l = Resolve(path); if (l == null) { MissingLabel(path, "score"); return; }
            _score = l;
            if (_flow != null) { _flow.ScoreChanged += OnScore; OnScore(_flow.Score); }
            else NoFlow("score");
        }

        protected void BindLives(NodePath path)
        {
            var l = Resolve(path); if (l == null) { MissingLabel(path, "lives"); return; }
            _lives = l;
            if (_flow != null) { _flow.LivesChanged += OnLives; OnLives(_flow.Lives); }
            else NoFlow("lives");
        }

        protected void BindLevel(NodePath path, string format = "Level {0}")
        {
            var l = Resolve(path); if (l == null) { MissingLabel(path, "level"); return; }
            _level = l; _levelFormat = format;
            if (_app != null) { _app.LevelChanged += OnLevel; OnLevel(_app.CurrentLevel); }
            else GD.PushWarning($"[{Name}] {Genre} HUD: no GameApp autoload — the level readout will not update.");
        }

        protected void BindHealth(NodePath path)
        {
            var l = Resolve(path); if (l == null) { MissingLabel(path, "health"); return; }
            _healthLabel = l;
            if (_health != null) { _health.HealthChanged += OnHealth; OnHealth(_health.CurrentHealth, _health.MaxHealth); }
            else GD.PushWarning($"[{Name}] {Genre} HUD: no HealthComponent in the scene (no player yet) — the health readout stays at its authored text; drive it with SetStat(\"health\", ...).");
        }

        /// <summary>Register a developer-owned readout: keeps its authored text, warns once, and is
        /// driven by <see cref="SetStat"/>. Use for values the framework has no source for.</summary>
        protected void Placeholder(NodePath path, string statName)
        {
            var l = Resolve(path); if (l == null) { MissingLabel(path, statName); return; }
            _placeholders[statName] = l;
            GD.PushWarning($"[{Name}] {Genre} HUD: '{statName}' ({l.Text}) has no framework data source — it shows placeholder text until your game calls SetStat(\"{statName}\", ...). Expected for genre-specific stats.");
        }

        /// <summary>Game code sets a placeholder readout's text. Unknown names warn (typo guard).</summary>
        public void SetStat(string statName, string text)
        {
            if (_placeholders.TryGetValue(statName, out var l) && GodotObject.IsInstanceValid(l)) l.Text = text;
            else GD.PushWarning($"[{Name}] {Genre} HUD: SetStat(\"{statName}\") — no such placeholder readout in this HUD.");
        }

        // ── Signal handlers ────────────────────────────────────────────

        private void OnScore(int v) { if (_score != null) _score.Text = v.ToString(); }
        private void OnLives(int v) { if (_lives != null) _lives.Text = $"× {v}"; }
        private void OnHealth(float cur, float max) { if (_healthLabel != null) _healthLabel.Text = $"{(int)cur} / {(int)max}"; }
        private void OnLevel(int level) { if (_level != null) _level.Text = string.Format(_levelFormat, System.Math.Max(0, level) + 1); }

        public override void _ExitTree()
        {
            base._ExitTree();
            // GameFlow / GameApp / Health outlive this HUD (scene change frees the HUD first) — undo the +=.
            if (_flow != null && GodotObject.IsInstanceValid(_flow)) { _flow.ScoreChanged -= OnScore; _flow.LivesChanged -= OnLives; }
            if (_app != null && GodotObject.IsInstanceValid(_app)) _app.LevelChanged -= OnLevel;
            if (_health != null && GodotObject.IsInstanceValid(_health)) _health.HealthChanged -= OnHealth;
            _flow = null; _app = null; _health = null;
        }

        // ── Warnings + scene search ────────────────────────────────────

        private void MissingLabel(NodePath p, string what)
            => GD.PushWarning($"[{Name}] {Genre} HUD: no Label at '{p}' for the {what} readout (relative to '{_host?.Name}'). Fix the NodePath in the scene.");

        private void NoFlow(string what)
            => GD.PushWarning($"[{Name}] {Genre} HUD: no GameFlowComponent in the scene — the {what} readout will not update.");

        private T? FindInScene<T>() where T : Node
        {
            Node? scene = Owner ?? GetTree()?.CurrentScene ?? GetTree()?.Root;
            return scene == null ? null : FindDescendant<T>(scene);
        }

        private static T? FindDescendant<T>(Node node) where T : Node
        {
            foreach (var child in node.GetChildren())
            {
                if (child is T t) return t;
                var found = FindDescendant<T>(child);
                if (found != null) return found;
            }
            return null;
        }
    }
}

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

        private Godot.Control? _score, _lives, _level, _healthReadout;
        private string _levelFormat = "Level {0}";
        private readonly Dictionary<string, Godot.Control> _placeholders = new();

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

        /// <summary>Resolve any node type against the HUD host, not just a Label.
        ///
        /// The genre HUDs are moving from stacks of Labels to real widgets — meters, demand
        /// meters, toolbars, toast hosts — and the Label-only <see cref="Resolve"/> could not
        /// reach them. An empty path is an intentional "this scene has no such widget" and
        /// stays silent; a path that is set but wrong warns, which is the case worth catching.</summary>
        /// <summary>Silent lookup — no warning when the node is absent or the wrong type.
        ///
        /// Needed because ResolveReadout PROBES for a badge before falling back to a Label. Going
        /// through the warning variant made every Label-based readout log "no
        /// ResourceBadgeComponent at ..." even though the fallback worked perfectly, which is
        /// four bogus warnings per genre and exactly how a log stops being read.</summary>
        protected T? TryResolveNode<T>(NodePath path) where T : Node
            => path is null || path.IsEmpty ? null : _host?.GetNodeOrNull<T>(path);

        protected T? ResolveNode<T>(NodePath path) where T : Node
        {
            if (path is null || path.IsEmpty) return null;
            var n = _host?.GetNodeOrNull<T>(path);
            if (n == null)
                GD.PushWarning($"[{Name}] {Genre} HUD: no {typeof(T).Name} at '{path}' (relative to '{_host?.Name}'). Fix the NodePath in the scene.");
            return n;
        }

        protected void BindScore(NodePath path)
        {
            _score = ResolveCoreReadout(path, "score", UiSurface.TextRole.Value, HorizontalAlignment.Right);
            if (_score == null) return;
            if (_flow != null) { _flow.ScoreChanged += OnScore; OnScore(_flow.Score); }
            else NoFlow("score");
        }

        protected void BindLives(NodePath path)
        {
            _lives = ResolveCoreReadout(path, "lives", UiSurface.TextRole.Value, HorizontalAlignment.Right);
            if (_lives == null) return;
            if (_flow != null) { _flow.LivesChanged += OnLives; OnLives(_flow.Lives); }
            else NoFlow("lives");
        }

        protected void BindLevel(NodePath path, string format = "Level {0}")
        {
            _level = ResolveCoreReadout(path, "level", UiSurface.TextRole.Caption, HorizontalAlignment.Left);
            if (_level == null) return;
            _levelFormat = format;
            if (_app != null) { _app.LevelChanged += OnLevel; OnLevel(_app.CurrentLevel); }
            else GD.PushWarning($"[{Name}] {Genre} HUD: no GameApp autoload — the level readout will not update.");
        }

        protected void BindHealth(NodePath path)
        {
            _healthReadout = ResolveCoreReadout(path, "health", UiSurface.TextRole.Value, HorizontalAlignment.Right);
            if (_healthReadout == null) return;
            if (_health != null) { _health.HealthChanged += OnHealth; OnHealth(_health.CurrentHealth, _health.MaxHealth); }
            else GD.PushWarning($"[{Name}] {Genre} HUD: no HealthComponent in the scene (no player yet) — the health readout stays at its authored text; drive it with SetStat(\"health\", ...).");
        }

        /// <summary>Register a developer-owned readout: keeps its authored text, warns once, and is
        /// driven by <see cref="SetStat"/>. Use for values the framework has no source for.</summary>
        // ── Readout helpers, shared by every genre HUD ────────────────────────────────────
        // These lived as identical private copies in the city-builder, survival and rpg HUDs.
        // Three copies of the same twenty lines is how the codebase drifted into styling the
        // same thing several different ways before; a fourth genre would have made it four.

        /// <summary>Resolve a readout as either a badge or a Label.
        ///
        /// A scene may use either, and binding to Label only would silently resolve to null the
        /// moment a scene upgraded to badges — which is exactly the class of breakage that took
        /// the RCI meter offline for several turns without any error.</summary>
        protected Godot.Control? ResolveReadout(NodePath path, string what)
        {
            if (TryResolveNode<ResourceBadgeComponent>(path) is { } badge) return badge;
            // Kit widgets, so a genre HUD can move from a Label stack to real bars and stat
            // pairs one node at a time instead of all ten scenes in lockstep.
            if (TryResolveNode<Kit.KitMeter>(path) is { } meter) return meter;
            if (TryResolveNode<Kit.KitOrbMeter>(path) is { } orb) return orb;
            if (TryResolveNode<Kit.KitRadialMeter>(path) is { } ring) return ring;
            if (TryResolveNode<Kit.KitLabelValue>(path) is { } pair) return pair;
            if (Resolve(path) is { } label)
            {
                StyleHudLabel(label, UiSurface.TextRole.Value, HorizontalAlignment.Right);
                return label;
            }
            GD.PushWarning($"[{Name}] {Genre} HUD: '{path}' is not a Label, ResourceBadge, "
                         + $"KitMeter, KitOrbMeter, KitRadialMeter or KitLabelValue, so {what} has nowhere "
                         + "to display.");
            return null;
        }

        private Godot.Control? ResolveCoreReadout(NodePath path, string what,
                                                  UiSurface.TextRole labelRole,
                                                  HorizontalAlignment labelAlignment)
        {
            var c = ResolveReadout(path, what);
            if (c is Label l) StyleHudLabel(l, labelRole, labelAlignment);
            return c;
        }

        /// <summary>HUD readouts sit over moving gameplay, so every plain Label gets the same
        /// behavior: pass-through mouse, vertically centred text and ellipsis instead of growth
        /// that pushes neighbouring readouts around.</summary>
        protected static void StyleHudLabel(Label label, UiSurface.TextRole role,
                                            HorizontalAlignment alignment = HorizontalAlignment.Left)
        {
            label.MouseFilter = Godot.Control.MouseFilterEnum.Ignore;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.HorizontalAlignment = alignment;
            label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            label.AutowrapMode = TextServer.AutowrapMode.Off;
            label.ClipText = true;
            label.AddThemeFontSizeOverride("font_size", UiSurface.FontSize(label, role));
        }

        /// <summary>Write a value, and a 0..1 fill when the readout is a badge.</summary>
        protected static void SetReadout(Godot.Control? c, string text, float fill = -1f)
        {
            switch (c)
            {
                case ResourceBadgeComponent b: b.Set(text, fill); break;
                // A bar shows the FRACTION; the exact number is the Label's job. A vital that is
                // only a number cannot be read at a glance, which is the whole complaint Stage 30
                // opens with ("a player cannot read health at a glance from 'Health: 72'").
                case Kit.KitMeter m:
                    if (fill >= 0f) m.Value = fill;
                    m.Readout = text;
                    break;
                case Kit.KitOrbMeter om:
                    if (fill >= 0f) om.Value = fill;
                    om.CentreText = text;
                    break;
                case Kit.KitRadialMeter rm:
                    if (fill >= 0f) rm.Value = fill;
                    rm.CentreText = text;
                    break;
                case Kit.KitLabelValue p: p.Value = text; break;
                case Label l: l.Text = text; break;
            }
        }

        /// <summary>Apply a semantic alert role, or null to clear it.
        ///
        /// A badge keeps <c>Alert</c> separate from its declared <c>Accent</c>, so clearing
        /// restores its identity colour instead of leaving it stuck on red. Labels take a
        /// palette-derived override rather than a literal.</summary>
        protected static void Tint(Godot.Control? c, UiSurface.Role? role)
        {
            switch (c)
            {
                case ResourceBadgeComponent b:
                    b.Alert = role;
                    break;
                // A meter recolours its FILL, not its text — the bar is the thing being read,
                // and a warning that only tints a number defeats the point of having a bar.
                case Kit.KitMeter m:
                    m.Fill = role ?? UiSurface.Role.Success;
                    m.QueueRedraw();
                    break;
                case Kit.KitOrbMeter om:
                    om.Fill = role ?? UiSurface.Role.Success;
                    om.QueueRedraw();
                    break;
                case Kit.KitRadialMeter rm:
                    rm.Fill = role ?? UiSurface.Role.Success;
                    rm.QueueRedraw();
                    break;
                case Kit.KitLabelValue p:
                    p.Accent = role ?? UiSurface.Role.Neutral;
                    p.QueueRedraw();
                    break;
                case Label l when role is { } r:
                    l.AddThemeColorOverride("font_color", UiSurface.Semantic(l, r));
                    break;
                case Label l:
                    l.RemoveThemeColorOverride("font_color");
                    break;
            }
        }

        protected void Placeholder(NodePath path, string statName)
        {
            // Resolves widgets as well as Labels. Binding the placeholder path to Label ONLY
            // meant that upgrading a scene's readout to a KitMeter turned a working placeholder
            // into "no such node" — the migration would have been punished for succeeding.
            var c = ResolveReadout(path, statName);
            if (c == null) { MissingLabel(path, statName); return; }
            _placeholders[statName] = c;
            GD.PushWarning($"[{Name}] {Genre} HUD: '{statName}' has no framework data source — it shows placeholder values until your game calls SetStat(\"{statName}\", ...). Expected for genre-specific stats.");
        }

        /// <summary>Game code sets a placeholder readout's text. Unknown names warn (typo guard).</summary>
        public void SetStat(string statName, string text)
        {
            if (_placeholders.TryGetValue(statName, out var l) && GodotObject.IsInstanceValid(l))
                SetReadout(l, text);
            else GD.PushWarning($"[{Name}] {Genre} HUD: SetStat(\"{statName}\") — no such placeholder readout in this HUD.");
        }

        // ── Signal handlers ────────────────────────────────────────────

        private void OnScore(int v) => SetReadout(_score, v.ToString());
        private void OnLives(int v) => SetReadout(_lives, $"× {v}");
        protected virtual string FormatHealthReadout(float cur, float max) => $"{(int)cur} / {(int)max}";

        private void OnHealth(float cur, float max)
            => SetReadout(_healthReadout, FormatHealthReadout(cur, max),
                          max <= 0f ? -1f : Mathf.Clamp(cur / max, 0f, 1f));
        private void OnLevel(int level)
            => SetReadout(_level, string.Format(_levelFormat, System.Math.Max(0, level) + 1));

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

        /// <summary>Find the first component of a type anywhere in the gameplay scene. Used by
        /// the genre subclasses to locate their state component (the economy, the vitals, the
        /// race state) without each re-implementing a tree walk.</summary>
        protected T? FindInScene<T>() where T : Node
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

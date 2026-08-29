using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Impact sound on damage. The audio twin of <see cref="HitSparkComponent"/>: listens to a
    /// sibling <see cref="HealthComponent"/>'s Damaged signal and plays a random hit sound with a
    /// little pitch variation so repeated hits don't sound mechanical.
    ///
    /// Ships audible out of the box — if <see cref="Sounds"/> is left empty it falls back to the
    /// addon's bundled impact clips, so combat has feedback with zero wiring (a shipped feature
    /// shouldn't need an asset nobody supplies). Assign your own clips to override, or clear the
    /// fallback by pointing it at a genre-appropriate set.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class HitSoundComponent : GameplayComponent
    {
        /// <summary>Hit clips, chosen at random per impact. Empty → the bundled default set loads at _Ready.</summary>
        [Export] public AudioStream[] Sounds { get; set; } = System.Array.Empty<AudioStream>();

        /// <summary>Ignore hits weaker than this (matches HitSparkComponent so spark and sound agree).</summary>
        [Export] public float MinDamage { get; set; } = 1f;

        [Export(PropertyHint.Range, "-40,6,0.5")] public float VolumeDb { get; set; } = -4f;

        /// <summary>±range added to pitch each hit, so a run of hits varies instead of machine-gunning one tone.</summary>
        [Export] public float PitchVariation { get; set; } = 0.12f;

        [Export] public string Bus { get; set; } = "Master";
        public float EffectiveMinDamage => NonNegativeFinite(MinDamage);
        public float EffectiveVolumeDb => float.IsFinite(VolumeDb) ? Mathf.Clamp(VolumeDb, -80f, 24f) : -4f;
        public float EffectivePitchVariation => float.IsFinite(PitchVariation) ? Mathf.Clamp(Mathf.Abs(PitchVariation), 0f, 0.99f) : 0f;
        public string EffectiveBus => string.IsNullOrWhiteSpace(Bus) ? "Master" : Bus;

        // The five bundled impact clips. Loaded once if the exported array is left empty.
        private static readonly string[] _defaultPaths =
        {
            "res://addons/beep_game_builder_cs/audio/combat/hit_000.ogg",
            "res://addons/beep_game_builder_cs/audio/combat/hit_001.ogg",
            "res://addons/beep_game_builder_cs/audio/combat/hit_002.ogg",
            "res://addons/beep_game_builder_cs/audio/combat/hit_003.ogg",
            "res://addons/beep_game_builder_cs/audio/combat/hit_004.ogg",
        };

        private AudioStreamPlayer? _player;
        private HealthComponent? _health;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;

            if (Sounds.Length == 0)
                Sounds = LoadDefaults();

            Callable.From(Setup).CallDeferred();
        }

        private void Setup()
        {
            _player = new AudioStreamPlayer { Name = "HitSoundPlayer", VolumeDb = EffectiveVolumeDb, Bus = EffectiveBus };
            AddChild(_player);

            _health = GetSiblingComponent<HealthComponent>();
            if (_health != null)
                _health.Damaged += OnDamaged;
            else
                // Say so — with no Health sibling this component is inert, and it used to be inert
                // in silence. Parent it beside a HealthComponent on the same entity.
                GD.PushWarning($"[{Name}] HitSoundComponent found no sibling HealthComponent — no hit sounds will play. Add it beside a HealthComponent.");
        }

        private void OnDamaged(float amount, float newHealth)
        {
            if (!IsActive || !float.IsFinite(amount) || amount < EffectiveMinDamage) return;
            if (_player == null || Sounds.Length == 0) return;

            var sound = PickSound();
            if (sound == null) return;

            float variation = EffectivePitchVariation;
            _player.VolumeDb = EffectiveVolumeDb;
            _player.Bus = EffectiveBus;
            _player.Stream = sound;
            _player.PitchScale = Mathf.Max(0.01f, 1f + (float)GD.RandRange(-variation, variation));
            _player.Play();
        }

        private AudioStream? PickSound()
        {
            var valid = System.Array.FindAll(Sounds, s => s != null);
            return valid.Length == 0 ? null : valid[GD.RandRange(0, valid.Length - 1)];
        }

        private static AudioStream[] LoadDefaults()
        {
            var list = new Godot.Collections.Array<AudioStream>();
            foreach (var path in _defaultPaths)
                if (ResourceLoader.Exists(path) && ResourceLoader.Load<AudioStream>(path) is { } s)
                    list.Add(s);
            return System.Linq.Enumerable.ToArray(list);
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (_health != null && GodotObject.IsInstanceValid(_health))
                _health.Damaged -= OnDamaged;
        }

        private static float NonNegativeFinite(float value)
            => float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;
    }
}

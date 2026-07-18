using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Zone-based ambient audio. Attach to an Area2D. When a body enters the zone,
    /// crossfades to the ambient track. On exit (once the LAST body leaves), crossfades
    /// back to silence. Integrates with WeatherSystemComponent — storm weather can trigger
    /// a thunder ambient override.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class AmbientAudioComponent : WorldComponent
    {
        [Export] public AudioStream? AmbientTrack { get; set; }
        [Export] public AudioStream? CombatTrack { get; set; }
        [Export] public AudioStream? ThunderTrack { get; set; }
        [Export] public float CrossfadeDuration { get; set; } = 1.5f;
        [Export] public string Bus { get; set; } = "Master";
        /// <summary>Loop the ambient/combat tracks while the zone is occupied / in combat. Applied
        /// via a replay-on-finished handler so it works for any stream type (it used to be exported
        /// and never read, so unchecking it did nothing).</summary>
        [Export] public bool Loop { get; set; } = true;
        [Export] public NodePath? WeatherSystemPath { get; set; }

        private AudioStreamPlayer? _ambientPlayer;
        private AudioStreamPlayer? _combatPlayer;
        private AudioStreamPlayer? _thunderPlayer;
        private bool _inCombat;
        private int _occupants;          // bodies currently inside the zone; ambient plays only while > 0
        private Area2D? _area;
        private WeatherSystemComponent? _weather;
        // One tween PER player: a single shared field killed the previous fade on every call, so
        // EnterCombat's two Crossfades (duck ambient, raise combat) cancelled each other — only the
        // last player ever faded.
        private readonly System.Collections.Generic.Dictionary<AudioStreamPlayer, Tween> _fades = new();

        public override void _Ready()
        {
            base._Ready();
            // Don't spawn players / subscribe signals at edit time — this [Tool] component would
            // otherwise litter any scene it's dropped into with runtime-only children and start
            // audio in the editor (every sibling atmosphere component guards this the same way).
            if (Engine.IsEditorHint()) return;
            Callable.From(Setup).CallDeferred();
        }

        private void Setup()
        {
            _ambientPlayer = new AudioStreamPlayer { Name = "AmbientPlayer", Bus = Bus, VolumeDb = -80f };
            _combatPlayer = new AudioStreamPlayer { Name = "CombatPlayer", Bus = Bus, VolumeDb = -80f };
            _thunderPlayer = new AudioStreamPlayer { Name = "ThunderPlayer", Bus = Bus, VolumeDb = 0f };
            AddChild(_ambientPlayer);
            AddChild(_combatPlayer);
            AddChild(_thunderPlayer);

            // Replay while the relevant state holds, so the tracks loop for any stream type without
            // needing per-format loop flags in the .import.
            _ambientPlayer.Finished += () => { if (Loop && IsActive && _occupants > 0) _ambientPlayer?.Play(); };
            _combatPlayer.Finished += () => { if (Loop && IsActive && _inCombat) _combatPlayer?.Play(); };

            // Wire to Area2D parent for zone detection.
            _area = GetParent() as Area2D;
            if (_area != null)
            {
                _area.BodyEntered += OnBodyEntered;
                _area.BodyExited += OnBodyExited;
            }
            else
                // Zone detection is how ambient ever starts — a non-Area2D parent means the players
                // are built but nothing ever triggers them, silently. Parent under an Area2D.
                GD.PushWarning($"[{Name}] AmbientAudioComponent's parent is {GetParent()?.GetType().Name ?? "null"}, not an Area2D — the zone can't fire, so ambient will never play. Parent it under an Area2D.");

            // Wire to WeatherSystemComponent for thunder on lightning strikes.
            if (WeatherSystemPath != null) _weather = GetNodeOrNull<WeatherSystemComponent>(WeatherSystemPath);
            if (_weather == null)
            {
                foreach (var n in GetTree().GetNodesInGroup("weather_system"))
                    if (n is WeatherSystemComponent w) { _weather = w; break; }
            }
            if (_weather != null) _weather.LightningStruck += OnLightningStruck;

            if (AmbientTrack == null && CombatTrack == null && ThunderTrack == null)
                // Shipped silent (the addon ships no audio). Say so once rather than build three
                // players to mix silence, mirroring WeatherAudioController's warning.
                GD.PushWarning($"[{Name}] AmbientAudioComponent has no AmbientTrack/CombatTrack/ThunderTrack assigned — it will be silent. These are yours to supply.");
        }

        private void OnBodyEntered(Node body)
        {
            if (!IsActive) return;
            // Refcount occupants: play on the 0→1 transition only, so a second body entering doesn't
            // restart the track from zero.
            if (++_occupants > 1) return;
            if (AmbientTrack != null && _ambientPlayer != null)
            {
                _ambientPlayer.Stream = AmbientTrack;
                _ambientPlayer.Play();
                Crossfade(_ambientPlayer, _inCombat ? -20f : 0f);
            }
        }

        private void OnBodyExited(Node body)
        {
            if (!IsActive) return;
            // Fade out only when the LAST body leaves — the old code faded on any exit, cutting
            // ambient while other bodies were still inside.
            _occupants = Mathf.Max(0, _occupants - 1);
            if (_occupants > 0) return;
            Crossfade(_ambientPlayer, -80f);
        }

        /// <summary>Switch to combat music (called when enemies are near).</summary>
        public void EnterCombat()
        {
            if (_inCombat || CombatTrack == null) return;
            _inCombat = true;
            _combatPlayer!.Stream = CombatTrack;
            _combatPlayer!.Play();
            // Duck ambient only if it's actually playing (a body is in the zone); otherwise keep it silent.
            Crossfade(_ambientPlayer, _occupants > 0 ? -20f : -80f);
            Crossfade(_combatPlayer, 0f);
        }

        /// <summary>Return to ambient music.</summary>
        public void ExitCombat()
        {
            if (!_inCombat) return;
            _inCombat = false;
            Crossfade(_combatPlayer, -80f);
            // Only raise ambient back if the zone is still occupied — otherwise it should stay silent.
            Crossfade(_ambientPlayer, _occupants > 0 ? 0f : -80f);
        }

        private void Crossfade(AudioStreamPlayer? player, float targetDb)
        {
            if (player == null) return;
            if (_fades.TryGetValue(player, out var old) && GodotObject.IsInstanceValid(old)) old.Kill();
            var tw = CreateTween();
            tw.TweenProperty(player, "volume_db", targetDb, CrossfadeDuration);
            _fades[player] = tw;
        }

        private void OnLightningStruck()
        {
            if (!IsActive || ThunderTrack == null || _thunderPlayer == null) return;
            _thunderPlayer.Stream = ThunderTrack;
            _thunderPlayer.Play();
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            foreach (var t in _fades.Values) if (GodotObject.IsInstanceValid(t)) t.Kill();
            if (_ambientPlayer != null && _ambientPlayer.Playing) _ambientPlayer.Stop();
            if (_combatPlayer != null && _combatPlayer.Playing) _combatPlayer.Stop();
            if (_thunderPlayer != null && _thunderPlayer.Playing) _thunderPlayer.Stop();
            if (_weather != null) _weather.LightningStruck -= OnLightningStruck;
            // Detach the Area2D handlers too (Godot auto-disconnects on free, but not on reparent).
            if (_area != null && GodotObject.IsInstanceValid(_area))
            {
                _area.BodyEntered -= OnBodyEntered;
                _area.BodyExited -= OnBodyExited;
            }
        }
    }
}

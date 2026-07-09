using Godot;
using System.Collections.Generic;

/// <summary>Simple audio manager with volume control, fade, and pooled AudioStreamPlayers.</summary>
public static class BeepAudioManager
{
    private static Node _root;
    private static float _masterVol = 1f, _sfxVol = 1f, _musicVol = 1f;
    private static AudioStreamPlayer _musicPlayer;
    private static Queue<AudioStreamPlayer> _sfxPool = new();
    private const int MaxSfxPlayers = 8;

    public static float MasterVolume { get => _masterVol; set { _masterVol = value; ApplyVolumes(); } }
    public static float SfxVolume { get => _sfxVol; set { _sfxVol = value; ApplyVolumes(); } }
    public static float MusicVolume { get => _musicVol; set { _musicVol = value; ApplyVolumes(); } }

    public static void Initialize(Node root)
    {
        _root = root;
        _musicPlayer = new AudioStreamPlayer { Bus = "Music" };
        _root.AddChild(_musicPlayer);
        for (int i = 0; i < MaxSfxPlayers; i++)
        {
            var p = new AudioStreamPlayer { Bus = "SFX" };
            _root.AddChild(p); _sfxPool.Enqueue(p);
        }
        ApplyVolumes();
    }

    public static void PlayMusic(AudioStream stream, float fadeIn = 0.5f, bool loop = true)
    {
        if (_musicPlayer == null) return;
        _musicPlayer.Stream = stream;
        _musicPlayer.Play();
        if (fadeIn > 0) { _musicPlayer.VolumeDb = -40; FadeTo(_musicPlayer, 0, fadeIn); }
    }

    public static void StopMusic(float fadeOut = 0.5f)
    {
        if (_musicPlayer == null || !_musicPlayer.Playing) return;
        if (fadeOut > 0) FadeTo(_musicPlayer, -40, fadeOut, true);
        else _musicPlayer.Stop();
    }

    public static void PlaySfx(AudioStream stream, float pitchVariation = 0.1f)
    {
        if (_sfxPool.Count == 0) return;
        var p = _sfxPool.Dequeue();
        p.Stream = stream;
        p.PitchScale = 1f + (float)GD.RandRange(-pitchVariation, pitchVariation);
        p.Play();
        // Return to pool when finished
        p.Finished += () => { _sfxPool.Enqueue(p); };
    }

    public static void PlaySfx(string path) => PlaySfx(ResourceLoader.Load<AudioStream>(path));

    public static async void FadeTo(AudioStreamPlayer player, float targetDb, float duration, bool stopAfter = false)
    {
        float startDb = player.VolumeDb;
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += 0.05f;
            player.VolumeDb = Mathf.Lerp(startDb, targetDb, elapsed / duration);
            await _root.ToSignal(_root.GetTree(), "process_frame");
        }
        if (stopAfter) player.Stop();
    }

    private static void ApplyVolumes()
    {
        float db = _masterVol > 0 ? Mathf.LinearToDb(_masterVol) : -80;
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), db);
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("SFX"), _sfxVol > 0 ? Mathf.LinearToDb(_sfxVol) + db : -80);
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Music"), _musicVol > 0 ? Mathf.LinearToDb(_musicVol) + db : -80);
    }
}

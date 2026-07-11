using Godot;
using System;
using System.Collections.Generic;

/// <summary>Spring-based procedural animation for UI elements. Smooth, bouncy physics-driven motion.</summary>
public class BeepProceduralAnim
{
    private float _position, _velocity, _target;
    private float _stiffness = 100f, _damping = 10f;

    public float Value => _position;
    public float Target { get => _target; set => _target = value; }
    public float Velocity => _velocity;
    public bool IsResting => Mathf.Abs(_target - _position) < 0.01f && Mathf.Abs(_velocity) < 0.1f;

    public BeepProceduralAnim(float stiffness = 100f, float damping = 10f)
    { _stiffness = stiffness; _damping = damping; }

    public void Set(float value, bool instant = false)
    {
        _target = value;
        if (instant) { _position = value; _velocity = 0; }
    }

    public float Update(float delta)
    {
        float force = (_target - _position) * _stiffness;
        _velocity += force * delta;
        _velocity *= Mathf.Exp(-_damping * delta);
        _position += _velocity * delta;
        return _position;
    }

    // Presets
    public static BeepProceduralAnim Smooth() => new(120f, 15f);
    public static BeepProceduralAnim Bouncy() => new(200f, 5f);
    public static BeepProceduralAnim Snappy() => new(300f, 20f);
}

/// <summary>Procedural animation for 2D vectors. Spring physics per axis.</summary>
public class BeepProceduralAnim2D
{
    private BeepProceduralAnim _x, _y;
    public Vector2 Value => new(_x.Value, _y.Value);
    public Vector2 Target { get => new(_x.Target, _y.Target); set { _x.Target = value.X; _y.Target = value.Y; } }

    public BeepProceduralAnim2D(float stiffness = 100f, float damping = 10f)
    { _x = new(stiffness, damping); _y = new(stiffness, damping); }

    public void Set(Vector2 value, bool instant = false) { _x.Set(value.X, instant); _y.Set(value.Y, instant); }
    public Vector2 Update(float delta) => new(_x.Update(delta), _y.Update(delta));

    public static BeepProceduralAnim2D Smooth() => new(120f, 15f);
    public static BeepProceduralAnim2D Bouncy() => new(200f, 5f);
}

/// <summary>Perlin/Simplex noise utility for procedural generation, terrain, wobble effects.</summary>
public static class BeepNoiseGenerator
{
    private static FastNoiseLite _noise = new();

    static BeepNoiseGenerator()
    {
        _noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        _noise.Frequency = 0.01f;
    }

    public static void SetSeed(int seed) => _noise.Seed = seed;
    public static void SetFrequency(float freq) => _noise.Frequency = freq;

    public static float Noise(float x) => (_noise.GetNoise1D(x) + 1f) / 2f;
    public static float Noise(float x, float y) => (_noise.GetNoise2D(x, y) + 1f) / 2f;
    public static float Noise(float x, float y, float z) => (_noise.GetNoise3D(x, y, z) + 1f) / 2f;

    /// <summary>Wobble effect for UI elements. Smooth oscillation using 1D noise.</summary>
    public static float Wobble(float time, float speed = 2f, float amplitude = 5f) => Noise(time * speed) * amplitude;

    /// <summary>Generate a heightmap-style 2D noise array.</summary>
    public static float[,] GenerateHeightmap(int width, int height, float scale = 0.05f, float offsetX = 0, float offsetY = 0)
    {
        var map = new float[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                map[x, y] = Noise((x + offsetX) * scale, (y + offsetY) * scale);
        return map;
    }
}

/// <summary>Pre-made color palettes for gradients, themes, and visual effects.</summary>
public static class BeepGradientPresets
{
    public static Color[] Sunset => new[] { new Color(1,0.3f,0), new Color(1,0.1f,0.3f), new Color(0.5f,0,0.3f) };
    public static Color[] Ocean => new[] { new Color(0,0.5f,1), new Color(0,0.3f,0.7f), new Color(0,0.1f,0.3f) };
    public static Color[] Forest => new[] { new Color(0.2f,0.8f,0.2f), new Color(0.1f,0.5f,0.1f), new Color(0,0.2f,0) };
    public static Color[] Lava => new[] { new Color(1,1,0), new Color(1,0.4f,0), new Color(0.7f,0,0) };
    public static Color[] Neon => new[] { new Color(1,0,1), new Color(0,1,1), new Color(0,1,0) };
    public static Color[] Candy => new[] { new Color(1,0.4f,0.7f), new Color(0.7f,0.4f,1), new Color(0.4f,0.7f,1) };
    public static Color[] Fire => new[] { new Color(1,1,0.8f), new Color(1,0.6f,0), new Color(0.8f,0,0), new Color(0.2f,0,0.05f) };
    public static Color[] Ice => new[] { new Color(0.8f,0.9f,1), new Color(0.4f,0.7f,1), new Color(0.1f,0.3f,0.7f) };
    public static Color[] Cyberpunk => new[] { new Color(1,0,1), new Color(0,1,1), new Color(1,1,0), new Color(0.1f,0,0.2f) };
    public static Color[] Retro => new[] { new Color(0.9f,0.5f,0.2f), new Color(0.2f,0.6f,0.2f), new Color(0.1f,0.2f,0.5f), new Color(0.3f,0.1f,0.3f) };

    public static Color LerpGradient(Color[] gradient, float t)
    {
        if (gradient.Length == 0) return Colors.White;
        if (gradient.Length == 1) return gradient[0];
        float scaledT = t * (gradient.Length - 1);
        int idx = Mathf.FloorToInt(scaledT);
        float frac = scaledT - idx;
        return gradient[idx].Lerp(gradient[Mathf.Min(idx + 1, gradient.Length - 1)], frac);
    }
}

using Godot;
using System.Collections.Generic;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Beep.GameBuilder;

/// <summary>Simple AES encryption for save files. Protect player data from tampering.</summary>
public static class BeepEncryptionHelper
{
    private static readonly byte[] Salt = { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 };

    public static string Encrypt(string plainText, string password)
    {
        using var aes = Aes.Create();
        var key = new Rfc2898DeriveBytes(password, Salt, 1000, HashAlgorithmName.SHA256);
        aes.Key = key.GetBytes(32); aes.IV = key.GetBytes(16);
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        { var data = Encoding.UTF8.GetBytes(plainText); cs.Write(data, 0, data.Length); }
        return Convert.ToBase64String(ms.ToArray());
    }

    public static string Decrypt(string cipherText, string password)
    {
        try
        {
            using var aes = Aes.Create();
            var key = new Rfc2898DeriveBytes(password, Salt, 1000, HashAlgorithmName.SHA256);
            aes.Key = key.GetBytes(32); aes.IV = key.GetBytes(16);
            using var ms = new MemoryStream(Convert.FromBase64String(cipherText));
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }
        catch { return null; }
    }

    public static void SaveEncrypted<T>(string path, T data, string password)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        var encrypted = Encrypt(json, password);
        using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
        f?.StoreString(encrypted);
    }

    public static T LoadEncrypted<T>(string path, string password) where T : class
    {
        if (!Godot.FileAccess.FileExists(path)) return null;
        using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        var encrypted = f?.GetAsText();
        if (string.IsNullOrEmpty(encrypted)) return null;
        var json = Decrypt(encrypted, password);
        return json != null ? System.Text.Json.JsonSerializer.Deserialize<T>(json) : null;
    }

    public static string HashString(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }
}

/// <summary>A* pathfinding for grid-based UI. Find shortest path through a 2D grid with obstacles.</summary>
public class BeepPathfindingGrid
{
    private bool[,] _walkable;
    private int _width, _height;

    public BeepPathfindingGrid(int width, int height)
    {
        _width = width; _height = height;
        _walkable = new bool[width, height];
        for (int x = 0; x < width; x++) for (int y = 0; y < height; y++) _walkable[x, y] = true;
    }

    public void SetObstacle(int x, int y, bool blocked) { if (InBounds(x, y)) _walkable[x, y] = !blocked; }

    public System.Collections.Generic.List<Vector2I> FindPath(Vector2I start, Vector2I end)
    {
        var openSet = new System.Collections.Generic.PriorityQueue<Vector2I, float>();
        var cameFrom = new System.Collections.Generic.Dictionary<Vector2I, Vector2I>();
        var gScore = new System.Collections.Generic.Dictionary<Vector2I, float>();
        gScore[start] = 0;
        openSet.Enqueue(start, Heuristic(start, end));

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();
            if (current == end) return ReconstructPath(cameFrom, current);

            foreach (var neighbor in GetNeighbors(current))
            {
                if (!InBounds(neighbor.X, neighbor.Y) || !_walkable[neighbor.X, neighbor.Y]) continue;
                float tentativeG = gScore[current] + 1;
                if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    float f = tentativeG + Heuristic(neighbor, end);
                    openSet.Enqueue(neighbor, f);
                }
            }
        }
        return null; // no path
    }

    private bool InBounds(int x, int y) => x >= 0 && x < _width && y >= 0 && y < _height;
    private static float Heuristic(Vector2I a, Vector2I b) => Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);

    private System.Collections.Generic.IEnumerable<Vector2I> GetNeighbors(Vector2I p)
    {
        yield return new Vector2I(p.X + 1, p.Y);
        yield return new Vector2I(p.X - 1, p.Y);
        yield return new Vector2I(p.X, p.Y + 1);
        yield return new Vector2I(p.X, p.Y - 1);
        // Diagonals
        yield return new Vector2I(p.X + 1, p.Y + 1);
        yield return new Vector2I(p.X - 1, p.Y + 1);
        yield return new Vector2I(p.X + 1, p.Y - 1);
        yield return new Vector2I(p.X - 1, p.Y - 1);
    }

    private static System.Collections.Generic.List<Vector2I> ReconstructPath(System.Collections.Generic.Dictionary<Vector2I, Vector2I> cameFrom, Vector2I current)
    {
        var path = new System.Collections.Generic.List<Vector2I> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }
        return path;
    }
}

/// <summary>Fluent API for building BBCode-rich text for RichTextLabel.</summary>
public static class BeepRichTextBuilder
{
    public static string Color(string text, string hexColor) => $"[color=#{hexColor}]{text}[/color]";
    public static string Color(string text, Color color) => $"[color=#{color.ToHtml()}]{text}[/color]";
    public static string Bold(string text) => $"[b]{text}[/b]";
    public static string Italic(string text) => $"[i]{text}[/i]";
    public static string Size(string text, int size) => $"[font_size={size}]{text}[/font_size]";
    public static string Wave(string text, float amp = 5f, float freq = 5f) => $"[wave amp={amp} freq={freq}]{text}[/wave]";
    public static string Shake(string text, float rate = 5f, int level = 10) => $"[shake rate={rate} level={level}]{text}[/shake]";
    public static string Rainbow(string text, float freq = 0.2f, float sat = 0.8f, float val = 0.8f) => $"[rainbow freq={freq} sat={sat} val={val}]{text}[/rainbow]";
    public static string Tornado(string text, float freq = 1f, float radius = 10f) => $"[tornado radius={radius} freq={freq}]{text}[/tornado]";
    public static string Fade(string text, int start, int length) => $"[fade start={start} length={length}]{text}[/fade]";

    public static string Build(Action<System.Text.StringBuilder> build)
    {
        var sb = new System.Text.StringBuilder();
        build(sb);
        return sb.ToString();
    }
}

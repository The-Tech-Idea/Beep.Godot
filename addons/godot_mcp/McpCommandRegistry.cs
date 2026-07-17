using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace GodotMcp;

/// <summary>
/// Process-wide registry of project-specific MCP commands and state providers.
///
/// Why this exists alongside <see cref="McpGameAdapter"/>: the adapter is an autoload,
/// so it only exists in the running game. Editor-side tools (anything touching
/// EditorInterface) have no autoload to register with. This registry is static, so it
/// works in both the editor and the running game, and lets any addon contribute
/// commands without this addon depending on it.
///
/// Register at plugin load / autoload _Ready:
/// <code>
///   McpCommandRegistry.RegisterCommand("mygame.spawn", args => ...);
///   McpCommandRegistry.RegisterState("mygame.score", () => ...);
/// </code>
/// The bridge consults this registry first, then falls back to the McpGameAdapter
/// autoload, so existing adapter-based commands keep working unchanged.
/// </summary>
public static class McpCommandRegistry
{
    private static readonly object _lock = new();
    private static readonly Dictionary<string, Func<JsonObject, JsonNode?>> _commands = new();
    private static readonly Dictionary<string, Func<JsonNode?>> _states = new();

    /// <summary>Register a command. Re-registering the same name replaces it, so a
    /// plugin reload doesn't throw or leave a stale handler behind.</summary>
    public static void RegisterCommand(string name, Func<JsonObject, JsonNode?> handler)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Command name is required.", nameof(name));
        if (handler is null)
            throw new ArgumentNullException(nameof(handler));
        lock (_lock) { _commands[name] = handler; }
    }

    /// <summary>Register a state provider. Re-registering the same name replaces it.</summary>
    public static void RegisterState(string name, Func<JsonNode?> provider)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("State name is required.", nameof(name));
        if (provider is null)
            throw new ArgumentNullException(nameof(provider));
        lock (_lock) { _states[name] = provider; }
    }

    /// <summary>Drop every handler whose name starts with the prefix. Call this from an
    /// addon's _ExitTree so disabling it doesn't leave dead commands advertised.</summary>
    public static void UnregisterPrefix(string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return;
        lock (_lock)
        {
            foreach (var key in new List<string>(_commands.Keys))
                if (key.StartsWith(prefix, StringComparison.Ordinal)) _commands.Remove(key);
            foreach (var key in new List<string>(_states.Keys))
                if (key.StartsWith(prefix, StringComparison.Ordinal)) _states.Remove(key);
        }
    }

    public static bool TryExecute(string name, JsonObject args, out JsonNode? result)
    {
        Func<JsonObject, JsonNode?>? handler;
        lock (_lock) { _commands.TryGetValue(name, out handler); }
        if (handler is null) { result = null; return false; }
        result = handler(args);
        return true;
    }

    public static bool TryReadState(string name, out JsonNode? result)
    {
        Func<JsonNode?>? provider;
        lock (_lock) { _states.TryGetValue(name, out provider); }
        if (provider is null) { result = null; return false; }
        result = provider();
        return true;
    }

    /// <summary>Names of everything registered — surfaced through the bridge so an agent
    /// can discover project commands instead of guessing.</summary>
    public static List<string> CommandNames()
    {
        lock (_lock) { return new List<string>(_commands.Keys); }
    }

    public static List<string> StateNames()
    {
        lock (_lock) { return new List<string>(_states.Keys); }
    }
}

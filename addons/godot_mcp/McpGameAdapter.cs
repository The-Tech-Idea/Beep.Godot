using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Godot;

namespace GodotMcp;

/// <summary>
/// Optional explicit game adapter. Add this as an autoload named McpGameAdapter
/// when you want MCP to call game-specific commands/state. No reflection is used.
/// </summary>
[Tool]
public partial class McpGameAdapter : Node
{
    private readonly Dictionary<string, Func<JsonObject, JsonNode?>> _commands = new();
    private readonly Dictionary<string, Func<JsonNode?>> _states = new();

    public void RegisterCommand(string name, Func<JsonObject, JsonNode?> handler)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Command name is required.", nameof(name));
        _commands[name] = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public void RegisterState(string name, Func<JsonNode?> provider)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("State name is required.", nameof(name));
        _states[name] = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public JsonNode? ExecuteCommand(string name, JsonObject parameters)
    {
        if (!_commands.TryGetValue(name, out Func<JsonObject, JsonNode?>? handler))
            throw new InvalidOperationException($"Game command is not registered: {name}");
        return handler(parameters);
    }

    public JsonNode? ReadState(string name)
    {
        if (!_states.TryGetValue(name, out Func<JsonNode?>? provider))
            throw new InvalidOperationException($"Game state provider is not registered: {name}");
        return provider();
    }

    public JsonObject ListRegistered()
    {
        JsonArray commands = new();
        foreach (string key in _commands.Keys)
            commands.Add(key);

        JsonArray states = new();
        foreach (string key in _states.Keys)
            states.Add(key);

        return new JsonObject
        {
            ["commands"] = commands,
            ["states"] = states
        };
    }
}

using Godot;
using System;
using System.Collections.Generic;

/// <summary>Simple localization / string table. Load CSV or JSON translations, get strings by key.</summary>
public static class BeepLocalization
{
    private static Dictionary<string, Dictionary<string, string>> _tables = new();
    private static string _currentLang = "en";

    public static string CurrentLanguage { get => _currentLang; set { _currentLang = value; } }
    public static event System.Action LanguageChanged;

    /// <summary>Load translations from a JSON file: { "en": {"hello":"Hello"}, "fr": {"hello":"Bonjour"} }</summary>
    public static void LoadJson(string path)
    {
        using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        if (f == null) return;
        var json = new Json();
        var err = json.Parse(f.GetAsText());
        if (err != Error.Ok) return;
        var data = json.Data.AsGodotDictionary();
        foreach (var lang in data.Keys)
        {
            var langStr = lang.AsString();
            if (!_tables.ContainsKey(langStr)) _tables[langStr] = new();
            var entries = data[lang].AsGodotDictionary();
            foreach (var key in entries.Keys)
                _tables[langStr][key.AsString()] = entries[key].AsString();
        }
    }

    /// <summary>Add a single translation at runtime.</summary>
    public static void Add(string lang, string key, string value)
    {
        if (!_tables.ContainsKey(lang)) _tables[lang] = new();
        _tables[lang][key] = value;
    }

    public static string Tr(string key, string fallback = null)
    {
        if (_tables.TryGetValue(_currentLang, out var dict) && dict.TryGetValue(key, out var val))
            return val;
        return fallback ?? key;
    }

    /// <summary>Format with args: Tr("welcome", "Player1") for "Welcome, Player1!"</summary>
    public static string TrF(string key, params object[] args) => string.Format(Tr(key), args);

    public static void SetLanguage(string lang)
    {
        _currentLang = lang;
        LanguageChanged?.Invoke();
    }
}

/// <summary>Localized label that auto-updates when language changes.</summary>
[Tool]
public partial class BeepLocalizedLabel : Label
{
    private string _key;

    [Export] public string LocalizationKey { get => _key; set { _key = value; Refresh(); } }

    public override void _Ready()
    {
        BeepLocalization.LanguageChanged += Refresh;
        Refresh();
    }

    private void Refresh() { if (!string.IsNullOrEmpty(_key)) Text = BeepLocalization.Tr(_key, Text); }
}

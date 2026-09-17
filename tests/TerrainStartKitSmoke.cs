using Beep.ECS;
using Godot;
using System;

/// <summary>
/// Start-kit invariants a GDScript probe cannot reach, for terrain_start_distance_probe.gd.
///
/// A kit entry whose scope is neither PerPlayer nor Neutral must fail capture loudly. A thrown C#
/// exception reaches a GDScript caller as an engine error line, which the integration runner rightly
/// fails a probe on, so the capture runs here and the error comes back as its message.
///
/// No deposit a start kit stamps may lie under ground its own resource does not support - the rule
/// the subsurface stage lays every deposit by.
/// </summary>
public partial class TerrainStartKitSmoke : Node
{
    /// <summary>The capture error for an entry with scope 7, or empty when capture accepted it.</summary>
    public string UndefinedScopeError()
    {
        var kit = new TerrainStartKit();
        kit.Entries.Add(new TerrainStartKitEntry { ResourceId = "horses", Scope = (TerrainStartKitScope)7 });
        var generator = new TerrainGeneratorComponent { BoundsSize = new Vector2I(16, 16), StartAreaRadius = 4, StartKit = kit };
        try
        {
            TerrainGenerationSettings settings = generator.CaptureGenerationSettings();
            try
            {
                TerrainStartKitRules.Capture(settings);
            }
            catch (InvalidOperationException error)
            {
                // The outcome under test: handed back to the probe, which checks it names the entry.
                return error.Message;
            }
            return "";
        }
        finally { generator.Free(); }
    }

    /// <summary>
    /// Over a map with start areas, a per-player iron entry and a Neutral iron entry: how many
    /// underground cells lie under terrain their own catalog entry does not support
    /// ("unsupported"), and how many iron deposits each scope stamped ("player_iron",
    /// "neutral_iron"), so a zero cannot come from a kit that placed nothing. Iron, because it lies
    /// under dry grass as well as rock, and a temperate start area is dry grass.
    /// </summary>
    public Godot.Collections.Dictionary DepositSupport(Vector2I size, int seed)
    {
        var kit = new TerrainStartKit { AreaGap = 1 };
        kit.Entries.Add(new TerrainStartKitEntry { ResourceId = "iron", Count = 1, MinDistance = 2, MaxDistance = 8 });
        kit.Entries.Add(new TerrainStartKitEntry { ResourceId = "iron", Count = 2, Scope = TerrainStartKitScope.Neutral });
        var generator = new TerrainGeneratorComponent
        {
            BoundsSize = size, Seed = seed, UseClimateBiomeMaps = true, UseScaleRules = true,
            StartAreaRadius = 10, StartKit = kit,
        };
        try
        {
            generator.ApplyMapSetup(0, 1, 1, 1, 1, 2);
            TerrainGenerationSettings settings = generator.CaptureGenerationSettings();
            TerrainResourceRules rules = TerrainResourceRules.Capture(settings);
            GeneratedTerrainField field = TerrainFieldBuilder.Build(settings);

            int unsupported = 0;
            for (int y = 0; y < size.Y; y++)
            for (int x = 0; x < size.X; x++)
            {
                var cell = new Vector2I(x, y);
                string id = field.UndergroundResourceAtCell(cell);
                if (id.Length == 0) continue;
                if (rules.Find(id) is not { } definition || !definition.Supports(field.TerrainAtCell(cell), field.ReliefAtCell(cell)))
                    unsupported++;
            }

            int playerIron = 0;
            foreach (TerrainStartAreaReport report in field.StartAreas)
                foreach (TerrainStartAreaPlacement placement in report.Placements)
                    if (placement.ResourceId == "iron") playerIron++;
            int neutralIron = 0;
            foreach (TerrainStartAreaPlacement placement in field.NeutralSites.Placements)
                if (placement.ResourceId == "iron") neutralIron++;

            return new Godot.Collections.Dictionary
            {
                ["unsupported"] = unsupported,
                ["player_iron"] = playerIron,
                ["neutral_iron"] = neutralIron,
            };
        }
        finally { generator.Free(); }
    }
}

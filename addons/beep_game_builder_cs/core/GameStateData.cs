using Godot;
using System;
using System.Collections.Generic;

namespace Beep.GameBuilder
{
	/// <summary>
	/// Conversion helpers between System.Collections.Generic and Godot.Collections.
	/// Godot's Dictionary/Array expose no constructor taking a BCL collection, so every
	/// crossing has to be built up entry by entry.
	/// </summary>
	internal static class GodotConv
	{
		public static Godot.Collections.Array ToArray(IEnumerable<string> src)
		{
			var a = new Godot.Collections.Array();
			foreach (var s in src) a.Add(s);
			return a;
		}

		public static Godot.Collections.Array ToArray(IEnumerable<Godot.Collections.Dictionary> src)
		{
			var a = new Godot.Collections.Array();
			foreach (var d in src) a.Add(d);
			return a;
		}

		public static Godot.Collections.Array ToArray(IEnumerable<int> src)
		{
			var a = new Godot.Collections.Array();
			foreach (var i in src) a.Add(i);
			return a;
		}

		public static Godot.Collections.Dictionary ToDict(IDictionary<string, float> src)
		{
			var d = new Godot.Collections.Dictionary();
			foreach (var kv in src) d[kv.Key] = kv.Value;
			return d;
		}

		public static Godot.Collections.Dictionary ToDict(IDictionary<string, int> src)
		{
			var d = new Godot.Collections.Dictionary();
			foreach (var kv in src) d[kv.Key] = kv.Value;
			return d;
		}

		public static Godot.Collections.Dictionary ToDict(IDictionary<string, bool> src)
		{
			var d = new Godot.Collections.Dictionary();
			foreach (var kv in src) d[kv.Key] = kv.Value;
			return d;
		}

		public static Godot.Collections.Dictionary ToDict(IDictionary<string, Variant> src)
		{
			var d = new Godot.Collections.Dictionary();
			foreach (var kv in src) d[kv.Key] = kv.Value;
			return d;
		}

		public static Dictionary<string, float> ToFloatDict(Godot.Collections.Dictionary src)
		{
			var r = new Dictionary<string, float>();
			if (src == null) return r;
			foreach (var k in src.Keys) r[k.AsString()] = (float)src[k];
			return r;
		}

		public static Dictionary<string, int> ToIntDict(Godot.Collections.Dictionary src)
		{
			var r = new Dictionary<string, int>();
			if (src == null) return r;
			foreach (var k in src.Keys) r[k.AsString()] = (int)src[k];
			return r;
		}

		public static Dictionary<string, bool> ToBoolDict(Godot.Collections.Dictionary src)
		{
			var r = new Dictionary<string, bool>();
			if (src == null) return r;
			foreach (var k in src.Keys) r[k.AsString()] = src[k].AsBool();
			return r;
		}

		public static Dictionary<string, Variant> ToVariantDict(Godot.Collections.Dictionary src)
		{
			var r = new Dictionary<string, Variant>();
			if (src == null) return r;
			foreach (var k in src.Keys) r[k.AsString()] = src[k];
			return r;
		}
	}

	/// <summary>
	/// Aggregated game state container using Feature-Based Architecture.
	/// Each feature (movement, combat, inventory, progression) has its own state class.
	/// The root GameState aggregates them + allows game-specific features via the Features dict.
	///
	/// This pattern allows different game types (platformer, RPG, shooter) to include
	/// only the features they need, making it extensible without waste.
	/// </summary>
	[GlobalClass]
	public partial class GameStateData : Resource
	{
		/// <summary>Save file metadata (timestamp, playtime, level, screenshot).</summary>
		public SaveMetadata Metadata { get; set; } = new();

		/// <summary>Player movement state (position, velocity, facing).</summary>
		public PlayerMovementStateData Movement { get; set; } = new();

		/// <summary>Player combat/health state (health, stamina, equipped weapons).</summary>
		public PlayerCombatStateData Combat { get; set; } = new();

		/// <summary>Player inventory state (items, slots, weight).</summary>
		public InventoryStateData Inventory { get; set; } = new();

		/// <summary>Game progression state (quests, achievements, unlocks, level).</summary>
		public ProgressionStateData Progression { get; set; } = new();

		/// <summary>World/level state (entities, switches, environmental data).</summary>
		public WorldStateData World { get; set; } = new();

		/// <summary>Game-specific features (for extensibility by genre).</summary>
		public Dictionary<string, Variant> Features { get; set; } = new();

		/// <summary>
		/// Free-form data bag for component state that has no dedicated feature class.
		/// Components write custom keys here from ISaveable.Save — see SaveLoadImplementationGuide.md.
		/// </summary>
		public Dictionary<string, Variant> GameData { get; set; } = new();

		/// <summary>Bumped when the on-disk shape changes in a way FromJsonString can't
		/// infer. Missing keys already degrade gracefully; a key whose *meaning* changed
		/// cannot be detected without this, and it can't be retrofitted once saves exist.</summary>
		public const int SaveFormatVersion = 1;

		public virtual string ToJson()
		{
			var dict = new Godot.Collections.Dictionary
			{
				{ "version", SaveFormatVersion },
				{ "metadata", Metadata.ToDict() },
				{ "movement", Movement.ToDict() },
				{ "combat", Combat.ToDict() },
				{ "inventory", Inventory.ToDict() },
				{ "progression", Progression.ToDict() },
				{ "world", World.ToDict() },
				{ "features", GodotConv.ToDict(Features) },
				{ "game_data", GodotConv.ToDict(GameData) }
			};
			return Json.Stringify(dict, "  ");
		}

		/// <summary>Populate from JSON. Returns false on a corrupt/unparseable file so the
		/// caller can refuse the load — silently returning a default-constructed state made
		/// a corrupt save look like a successful one, dropping the player into a fresh game
		/// that the next save then wrote over the still-recoverable file.</summary>
		public virtual bool FromJsonString(string json)
		{
			var j = new Json();
			if (j.Parse(json) != Error.Ok)
			{
				GD.PushError($"[GameStateData] Save file is not valid JSON (line {j.GetErrorLine()}): {j.GetErrorMessage()}");
				return false;
			}

			if (j.Data.VariantType != Variant.Type.Dictionary)
			{
				GD.PushError("[GameStateData] Save file root is not a JSON object.");
				return false;
			}

			var root = j.Data.AsGodotDictionary();
			if (root.ContainsKey("metadata")) Metadata = SaveMetadata.FromDict(root["metadata"].AsGodotDictionary());
			if (root.ContainsKey("movement")) Movement = PlayerMovementStateData.FromDict(root["movement"].AsGodotDictionary());
			if (root.ContainsKey("combat")) Combat = PlayerCombatStateData.FromDict(root["combat"].AsGodotDictionary());
			if (root.ContainsKey("inventory")) Inventory = InventoryStateData.FromDict(root["inventory"].AsGodotDictionary());
			if (root.ContainsKey("progression")) Progression = ProgressionStateData.FromDict(root["progression"].AsGodotDictionary());
			if (root.ContainsKey("world")) World = WorldStateData.FromDict(root["world"].AsGodotDictionary());
			if (root.ContainsKey("features")) Features = GodotConv.ToVariantDict(root["features"].AsGodotDictionary());
			if (root.ContainsKey("game_data")) GameData = GodotConv.ToVariantDict(root["game_data"].AsGodotDictionary());
			return true;
		}
	}

	/// <summary>Save file metadata (timestamp, playtime, level, custom screenshot).</summary>
	public partial class SaveMetadata
	{
		public string SaveName { get; set; } = "Save";
		public long Timestamp { get; set; } = 0;
		public double PlaytimeSeconds { get; set; } = 0;
		public string CurrentLevel { get; set; } = "";
		public int PlayCount { get; set; } = 0;
		public string Description { get; set; } = "";

		public Godot.Collections.Dictionary ToDict() => new()
		{
			{ "save_name", SaveName },
			{ "timestamp", Timestamp },
			{ "playtime_seconds", PlaytimeSeconds },
			{ "current_level", CurrentLevel },
			{ "play_count", PlayCount },
			{ "description", Description }
		};

		public static SaveMetadata FromDict(Godot.Collections.Dictionary d) => new()
		{
			SaveName = d.TryGetValue("save_name", out var sn) ? sn.AsString() : "Save",
			Timestamp = d.TryGetValue("timestamp", out var ts) ? ts.AsInt64() : 0,
			PlaytimeSeconds = d.TryGetValue("playtime_seconds", out var ps) ? ps.AsDouble() : 0,
			CurrentLevel = d.TryGetValue("current_level", out var cl) ? cl.AsString() : "",
			PlayCount = d.TryGetValue("play_count", out var pc) ? (int)pc : 0,
			Description = d.TryGetValue("description", out var desc) ? desc.AsString() : ""
		};
	}

	/// <summary>Player movement state: position, velocity, facing direction.</summary>
	public partial class PlayerMovementStateData
	{
		public float PositionX { get; set; } = 0;
		public float PositionY { get; set; } = 0;
		public float VelocityX { get; set; } = 0;
		public float VelocityY { get; set; } = 0;
		public float FacingDirection { get; set; } = 1; // 1 or -1 for left/right
		public float Rotation { get; set; } = 0;

		public Godot.Collections.Dictionary ToDict() => new()
		{
			{ "position_x", PositionX },
			{ "position_y", PositionY },
			{ "velocity_x", VelocityX },
			{ "velocity_y", VelocityY },
			{ "facing_direction", FacingDirection },
			{ "rotation", Rotation }
		};

		public static PlayerMovementStateData FromDict(Godot.Collections.Dictionary d) => new()
		{
			PositionX = d.TryGetValue("position_x", out var px) ? (float)px : 0,
			PositionY = d.TryGetValue("position_y", out var py) ? (float)py : 0,
			VelocityX = d.TryGetValue("velocity_x", out var vx) ? (float)vx : 0,
			VelocityY = d.TryGetValue("velocity_y", out var vy) ? (float)vy : 0,
			FacingDirection = d.TryGetValue("facing_direction", out var fd) ? (float)fd : 1,
			Rotation = d.TryGetValue("rotation", out var r) ? (float)r : 0
		};
	}

	/// <summary>Player combat state: health, stamina, equipped weapons, stats.</summary>
	public partial class PlayerCombatStateData
	{
		public float Health { get; set; } = 100;
		public float MaxHealth { get; set; } = 100;
		public float Stamina { get; set; } = 100;
		public float MaxStamina { get; set; } = 100;
		public int Lives { get; set; } = 3;
		public List<string> EquippedWeapons { get; set; } = new();
		public Dictionary<string, float> Stats { get; set; } = new(); // damage, defense, speed, etc.
		public List<string> ActiveBuffs { get; set; } = new();

		public Godot.Collections.Dictionary ToDict() => new()
		{
			{ "health", Health },
			{ "max_health", MaxHealth },
			{ "stamina", Stamina },
			{ "max_stamina", MaxStamina },
			{ "lives", Lives },
			{ "equipped_weapons", GodotConv.ToArray(EquippedWeapons) },
			{ "stats", GodotConv.ToDict(Stats) },
			{ "active_buffs", GodotConv.ToArray(ActiveBuffs) }
		};

		public static PlayerCombatStateData FromDict(Godot.Collections.Dictionary d) => new()
		{
			Health = d.TryGetValue("health", out var h) ? (float)h : 100,
			MaxHealth = d.TryGetValue("max_health", out var mh) ? (float)mh : 100,
			Stamina = d.TryGetValue("stamina", out var s) ? (float)s : 100,
			MaxStamina = d.TryGetValue("max_stamina", out var ms) ? (float)ms : 100,
			Lives = d.TryGetValue("lives", out var l) ? (int)l : 3,
			EquippedWeapons = new List<string>(d.TryGetValue("equipped_weapons", out var ew) ? ew.AsStringArray() : Array.Empty<string>()),
			Stats = GodotConv.ToFloatDict(d.TryGetValue("stats", out var st) ? st.AsGodotDictionary() : new()),
			ActiveBuffs = new List<string>(d.TryGetValue("active_buffs", out var ab) ? ab.AsStringArray() : Array.Empty<string>())
		};
	}

	/// <summary>Player state: position, health, inventory, stats, abilities. DEPRECATED: use feature-specific classes instead.</summary>
	public partial class PlayerStateData
	{
		public Vector2 Position { get; set; } = Vector2.Zero;
		public float Health { get; set; } = 100;
		public float MaxHealth { get; set; } = 100;
		public int Lives { get; set; } = 3;
		public int Score { get; set; } = 0;

		public InventoryStateData Inventory { get; set; } = new();
		public Dictionary<string, float> Stats { get; set; } = new();
		public List<string> UnlockedAbilities { get; set; } = new();
		public Dictionary<string, Variant> Custom { get; set; } = new();

		public Godot.Collections.Dictionary ToDict() => new()
		{
			{ "position", Position },
			{ "health", Health },
			{ "max_health", MaxHealth },
			{ "lives", Lives },
			{ "score", Score },
			{ "inventory", Inventory.ToDict() },
			{ "stats", GodotConv.ToDict(Stats) },
			{ "unlocked_abilities", GodotConv.ToArray(UnlockedAbilities) },
			{ "custom", GodotConv.ToDict(Custom) }
		};

		public static PlayerStateData FromDict(Godot.Collections.Dictionary d) => new()
		{
			Position = d.TryGetValue("position", out var pos) ? pos.AsVector2() : Vector2.Zero,
			Health = d.TryGetValue("health", out var h) ? (float)h : 100,
			MaxHealth = d.TryGetValue("max_health", out var mh) ? (float)mh : 100,
			Lives = d.TryGetValue("lives", out var l) ? (int)l : 3,
			Score = d.TryGetValue("score", out var s) ? (int)s : 0,
			Inventory = d.TryGetValue("inventory", out var inv) ? InventoryStateData.FromDict(inv.AsGodotDictionary()) : new(),
			Stats = GodotConv.ToFloatDict(d.TryGetValue("stats", out var st) ? st.AsGodotDictionary() : new()),
			UnlockedAbilities = new List<string>(d.TryGetValue("unlocked_abilities", out var ua) ? ua.AsStringArray() : Array.Empty<string>()),
			Custom = GodotConv.ToVariantDict(d.TryGetValue("custom", out var c) ? c.AsGodotDictionary() : new())
		};
	}

	/// <summary>Player inventory state: items with quantities.</summary>
	public partial class InventoryStateData
	{
		public Dictionary<string, int> Items { get; set; } = new();
		public int MaxSlots { get; set; } = 20;

		public Godot.Collections.Dictionary ToDict() => new()
		{
			{ "items", GodotConv.ToDict(Items) },
			{ "max_slots", MaxSlots }
		};

		public static InventoryStateData FromDict(Godot.Collections.Dictionary d) => new()
		{
			Items = GodotConv.ToIntDict(d.TryGetValue("items", out var i) ? i.AsGodotDictionary() : new()),
			MaxSlots = d.TryGetValue("max_slots", out var ms) ? (int)ms : 20
		};
	}

	/// <summary>World/level state: entities (enemies, pickups, NPCs) and world data.</summary>
	public partial class WorldStateData
	{
		public List<EntityStateData> Entities { get; set; } = new();
		public Dictionary<string, bool> Switches { get; set; } = new();
		public Dictionary<string, Variant> WorldData { get; set; } = new();

		public Godot.Collections.Dictionary ToDict() => new()
		{
			{ "entities", GodotConv.ToArray(Entities.ConvertAll(e => e.ToDict())) },
			{ "switches", GodotConv.ToDict(Switches) },
			{ "world_data", GodotConv.ToDict(WorldData) }
		};

		public static WorldStateData FromDict(Godot.Collections.Dictionary d)
		{
			var state = new WorldStateData
			{
				Switches = GodotConv.ToBoolDict(d.TryGetValue("switches", out var sw) ? sw.AsGodotDictionary() : new()),
				WorldData = GodotConv.ToVariantDict(d.TryGetValue("world_data", out var wd) ? wd.AsGodotDictionary() : new())
			};

			if (d.TryGetValue("entities", out var ents))
				foreach (var e in ents.AsGodotArray())
					state.Entities.Add(EntityStateData.FromDict(e.AsGodotDictionary()));

			return state;
		}
	}

	/// <summary>Entity state: type, position, health, custom properties.</summary>
	public partial class EntityStateData
	{
		public string Id { get; set; } = "";
		public string Type { get; set; } = "";
		public Vector2 Position { get; set; } = Vector2.Zero;
		public float Rotation { get; set; } = 0;
		public float Health { get; set; } = -1;
		public bool IsActive { get; set; } = true;
		public Dictionary<string, Variant> Properties { get; set; } = new();

		public Godot.Collections.Dictionary ToDict() => new()
		{
			{ "id", Id },
			{ "type", Type },
			{ "position", Position },
			{ "rotation", Rotation },
			{ "health", Health },
			{ "is_active", IsActive },
			{ "properties", GodotConv.ToDict(Properties) }
		};

		public static EntityStateData FromDict(Godot.Collections.Dictionary d) => new()
		{
			Id = d.TryGetValue("id", out var id) ? id.AsString() : "",
			Type = d.TryGetValue("type", out var t) ? t.AsString() : "",
			Position = d.TryGetValue("position", out var pos) ? pos.AsVector2() : Vector2.Zero,
			Rotation = d.TryGetValue("rotation", out var r) ? (float)r : 0,
			Health = d.TryGetValue("health", out var h) ? (float)h : -1,
			IsActive = d.TryGetValue("is_active", out var ia) ? ia.AsBool() : true,
			Properties = GodotConv.ToVariantDict(d.TryGetValue("properties", out var p) ? p.AsGodotDictionary() : new())
		};
	}

	/// <summary>Game progression state: quests, achievements, unlocks.</summary>
	public partial class ProgressionStateData
	{
		public List<string> CompletedQuests { get; set; } = new();
		public List<string> UnlockedAchievements { get; set; } = new();
		public Dictionary<string, bool> Unlocks { get; set; } = new();
		public int Level { get; set; } = 1;
		public int Experience { get; set; } = 0;

		/// <summary>The level the player is on. Distinct from Level, which is the player's
		/// experience level. Nothing recorded this, so GameApp.CurrentLevel came back as -1
		/// after a load and LevelLoaderComponent clamped it to FirstLevelIndex — every save
		/// reopened on level 1.</summary>
		public int CurrentLevel { get; set; } = -1;

		/// <summary>Levels actually beaten. Was a plain in-memory HashSet on GameApp that no
		/// ISaveable persisted, so all progression was lost on quit.</summary>
		public List<int> CompletedLevels { get; set; } = new();

		public int MaxLevelReached { get; set; } = 0;

		// ── Lifetime stats (GameApp tracked these and nothing wrote them to disk) ──
		public int GamesPlayedTotal { get; set; } = 0;
		public int GamesWonTotal { get; set; } = 0;
		public int GamesLostTotal { get; set; } = 0;
		public int BestScore { get; set; } = 0;
		public int TotalPlaytimeMinutes { get; set; } = 0;

		public Godot.Collections.Dictionary ToDict() => new()
		{
			{ "completed_quests", GodotConv.ToArray(CompletedQuests) },
			{ "unlocked_achievements", GodotConv.ToArray(UnlockedAchievements) },
			{ "unlocks", GodotConv.ToDict(Unlocks) },
			{ "level", Level },
			{ "experience", Experience },
			{ "current_level", CurrentLevel },
			{ "completed_levels", GodotConv.ToArray(CompletedLevels) },
			{ "max_level_reached", MaxLevelReached },
			{ "games_played_total", GamesPlayedTotal },
			{ "games_won_total", GamesWonTotal },
			{ "games_lost_total", GamesLostTotal },
			{ "best_score", BestScore },
			{ "total_playtime_minutes", TotalPlaytimeMinutes }
		};

		public static ProgressionStateData FromDict(Godot.Collections.Dictionary d) => new()
		{
			CompletedQuests = new List<string>(d.TryGetValue("completed_quests", out var cq) ? cq.AsStringArray() : Array.Empty<string>()),
			UnlockedAchievements = new List<string>(d.TryGetValue("unlocked_achievements", out var ua) ? ua.AsStringArray() : Array.Empty<string>()),
			Unlocks = GodotConv.ToBoolDict(d.TryGetValue("unlocks", out var u) ? u.AsGodotDictionary() : new()),
			Level = d.TryGetValue("level", out var l) ? (int)l : 1,
			Experience = d.TryGetValue("experience", out var e) ? (int)e : 0,
			CurrentLevel = d.TryGetValue("current_level", out var cl) ? (int)cl : -1,
			CompletedLevels = new List<int>(d.TryGetValue("completed_levels", out var cls) ? cls.AsInt32Array() : Array.Empty<int>()),
			MaxLevelReached = d.TryGetValue("max_level_reached", out var mlr) ? (int)mlr : 0,
			GamesPlayedTotal = d.TryGetValue("games_played_total", out var gp) ? (int)gp : 0,
			GamesWonTotal = d.TryGetValue("games_won_total", out var gw) ? (int)gw : 0,
			GamesLostTotal = d.TryGetValue("games_lost_total", out var gl) ? (int)gl : 0,
			BestScore = d.TryGetValue("best_score", out var bs) ? (int)bs : 0,
			TotalPlaytimeMinutes = d.TryGetValue("total_playtime_minutes", out var tp) ? (int)tp : 0
		};
	}
}

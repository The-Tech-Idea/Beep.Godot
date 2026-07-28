using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// The city-builder simulation: the single source of truth for every number the
    /// City Builder HUD shows. Owns the treasury, population, utilities, happiness, RCI
    /// demand and the calendar, advances them on a monthly tick, and persists through
    /// <see cref="ISaveable"/>.
    ///
    /// This exists because the HUD had no data source at all — all five City Builder
    /// readouts were registered as <c>Placeholder(...)</c> and showed numbers typed into
    /// the scene. A readout with no owner cannot be correct, cannot be saved, and cannot
    /// be tested; this component is that owner.
    ///
    /// The city is modelled as BUILDING COUNTS rather than placed objects, so the whole
    /// economic loop (buy -> upkeep -> population -> tax -> demand) is real and verifiable
    /// without a world to place them in. Placement, when a project adds a world, only has
    /// to call <see cref="TryPurchase"/> at the point of placement — the economy does not
    /// change shape.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class CityEconomyComponent : GameplayComponent, ISaveable
    {
        // ── Save wiring ────────────────────────────────────────────────
        // Default ON, unlike HealthComponent: city economy is global and single-slot, so the
        // multi-writer hazard documented on ISaveable (every enemy writing state.Combat) does
        // not apply here. There is exactly one city.
        [Export] public bool ParticipatesInSave { get; set; } = true;

        // GameStateData.GameData is a free-form Dictionary<string, Variant> that IS serialised
        // both ways (game_data in ToDict AND FromDict), so genre state needs no schema change
        // and no SaveFormatVersion bump. Keys are namespaced and live next to Save/Load below
        // so the two halves cannot drift.
        private const string KTreasury = "citybuilder.treasury";
        private const string KPopulation = "citybuilder.population";
        private const string KHappiness = "citybuilder.happiness";
        private const string KDay = "citybuilder.day";
        private const string KSpeed = "citybuilder.speed";
        private const string KBuildings = "citybuilder.buildings";

        // ── Tuning ─────────────────────────────────────────────────────
        [Export] public int StartingTreasury { get; set; } = 50_000;
        /// <summary>Real seconds per in-game month at 1x speed.</summary>
        [Export] public float SecondsPerMonth { get; set; } = 6f;
        /// <summary>Tax collected per resident per month.</summary>
        [Export] public float TaxPerResident { get; set; } = 9f;

        // ── Live state ─────────────────────────────────────────────────
        public int Treasury { get; private set; }
        public int MonthlyDelta { get; private set; }
        public int Population { get; private set; }
        public int PowerUsed { get; private set; }
        public int PowerCapacity { get; private set; }
        public int WaterUsed { get; private set; }
        public int WaterCapacity { get; private set; }
        /// <summary>0..100.</summary>
        public int Happiness { get; private set; } = 70;
        /// <summary>Demand for each zone, -1..+1. Negative = oversupplied.</summary>
        public float DemandResidential { get; private set; }
        public float DemandCommercial { get; private set; }
        public float DemandIndustrial { get; private set; }
        /// <summary>Elapsed in-game months.</summary>
        public int Month { get; private set; }
        public int Year => 1 + Month / 12;
        public string Season => SeasonNames[(Month / 3) % 4];
        private static readonly string[] SeasonNames = { "Spring", "Summer", "Autumn", "Winter" };

        /// <summary>0 = paused, 1..3 = speed multipliers. Drives the tick; nothing else.</summary>
        public int Speed
        {
            get => _speed;
            set
            {
                int v = Mathf.Clamp(value, 0, 3);
                if (_speed == v) return;
                _speed = v;
                EmitSignal(SignalName.SpeedChanged, v);
            }
        }
        private int _speed = 1;

        [Signal] public delegate void StatsChangedEventHandler();
        [Signal] public delegate void SpeedChangedEventHandler(int speed);
        [Signal] public delegate void AlertRaisedEventHandler(string severity, string text);
        /// <summary>A building was bought or refunded — the toolbar re-checks affordability.</summary>
        [Signal] public delegate void BuildingsChangedEventHandler();

        private readonly Dictionary<string, int> _owned = new();
        private float _tickClock;

        // ── Building catalogue ─────────────────────────────────────────
        /// <summary>One buildable type. Real game data, not sample data: these values drive
        /// the simulation above and the affordability state in the build toolbar.</summary>
        public sealed record BuildingDef(
            string Id, string Category, string Display, int Cost, int Upkeep,
            int Residents, int Jobs, int Power, int Water, int Happiness);

        public static readonly BuildingDef[] Catalogue =
        {
            //          id            category      display          cost  upkeep res jobs pwr wtr happy
            new("house",        "Zones",     "House",         1_200,   40,  12,   0,   4,   3,   0),
            new("apartment",    "Zones",     "Apartment",     4_800,  140,  56,   0,  16,  14,  -2),
            new("shop",         "Zones",     "Shop",          2_400,   80,   0,  14,   7,   4,   1),
            new("factory",      "Zones",     "Factory",       6_500,  260,   0,  48,  32,  18,  -6),
            new("road",         "Roads",     "Road",            180,    6,   0,   0,   0,   0,   0),
            new("school",       "Services",  "School",        5_200,  220,   0,  18,  10,   8,   6),
            new("clinic",       "Services",  "Clinic",        6_800,  300,   0,  22,  14,  12,   8),
            new("park",         "Services",  "Park",            800,   30,   0,   2,   0,   6,   7),
            new("power_plant",  "Utilities", "Power Plant",  14_000,  520,   0,  30,-140,  20,  -8),
            new("water_tower",  "Utilities", "Water Tower",   4_400,  160,   0,   4,   6,-120,   0),
        };

        public static BuildingDef? Find(string id)
        {
            foreach (var b in Catalogue) if (b.Id == id) return b;
            return null;
        }

        public int CountOf(string id) => _owned.TryGetValue(id, out int n) ? n : 0;
        public bool CanAfford(string id) => Find(id) is { } b && Treasury >= b.Cost;

        // ── Lifecycle ──────────────────────────────────────────────────
        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            if (ParticipatesInSave) AddToGroup(SaveableHelper.Group);
            if (Treasury == 0) Treasury = StartingTreasury;
            Recalculate();
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint() || !IsActive || Speed <= 0) return;
            _tickClock += (float)delta * Speed;
            if (_tickClock < SecondsPerMonth) return;
            _tickClock -= SecondsPerMonth;
            AdvanceMonth();
        }

        // ── Simulation ─────────────────────────────────────────────────

        /// <summary>Buy one building. Returns false (and does nothing) when unaffordable —
        /// the toolbar greys the item, this is the authoritative second check.</summary>
        public bool TryPurchase(string id)
        {
            var b = Find(id);
            if (b == null) { GD.PushWarning($"[{Name}] TryPurchase('{id}') — no such building in the catalogue."); return false; }
            if (Treasury < b.Cost)
            {
                EmitSignal(SignalName.AlertRaised, "warning", $"Cannot afford {b.Display} ({b.Cost:N0})");
                return false;
            }
            Treasury -= b.Cost;
            _owned[id] = CountOf(id) + 1;
            Recalculate();
            EmitSignal(SignalName.BuildingsChanged);
            return true;
        }

        /// <summary>Sell one building back at half cost. Kept symmetric with TryPurchase so a
        /// demolish action has a real economic effect rather than only removing a node.</summary>
        public bool TrySell(string id)
        {
            if (CountOf(id) <= 0) return false;
            var b = Find(id);
            if (b == null) return false;
            _owned[id] = CountOf(id) - 1;
            Treasury += b.Cost / 2;
            Recalculate();
            EmitSignal(SignalName.BuildingsChanged);
            return true;
        }

        private void AdvanceMonth()
        {
            Month++;

            // Population drifts toward housing capacity, gated by happiness and by whether the
            // utilities actually cover demand. An unpowered city stops growing.
            int capacity = Sum(b => b.Residents);
            bool utilitiesOk = PowerUsed <= PowerCapacity && WaterUsed <= WaterCapacity;
            float pull = (Happiness / 100f) * (utilitiesOk ? 1f : 0.25f);
            int target = Mathf.RoundToInt(capacity * pull);
            Population += Mathf.RoundToInt((target - Population) * 0.25f);
            if (Population < 0) Population = 0;

            Treasury += MonthlyDelta;
            Recalculate();

            if (Treasury < 0)
                EmitSignal(SignalName.AlertRaised, "danger", "Treasury is in debt");
            if (PowerCapacity > 0 && PowerUsed > PowerCapacity)
                EmitSignal(SignalName.AlertRaised, "danger", "Power demand exceeds capacity");
            if (WaterCapacity > 0 && WaterUsed > WaterCapacity)
                EmitSignal(SignalName.AlertRaised, "warning", "Water demand exceeds capacity");
        }

        /// <summary>Recompute every derived figure from the owned buildings. Single place, so
        /// the HUD can never disagree with the simulation.</summary>
        private void Recalculate()
        {
            int upkeep = Sum(b => b.Upkeep);
            int jobs = Sum(b => b.Jobs);
            int housing = Sum(b => b.Residents);

            // Utilities: negative Power/Water in the catalogue means the building SUPPLIES it.
            int pUse = 0, pCap = 0, wUse = 0, wCap = 0;
            foreach (var (id, n) in _owned)
            {
                if (Find(id) is not { } b || n <= 0) continue;
                if (b.Power >= 0) pUse += b.Power * n; else pCap += -b.Power * n;
                if (b.Water >= 0) wUse += b.Water * n; else wCap += -b.Water * n;
            }
            PowerUsed = pUse; PowerCapacity = pCap;
            WaterUsed = wUse; WaterCapacity = wCap;

            int amenity = Sum(b => b.Happiness);
            int shortfall = (pUse > pCap ? 12 : 0) + (wUse > wCap ? 8 : 0);
            Happiness = Mathf.Clamp(70 + amenity - shortfall, 0, 100);

            int income = Mathf.RoundToInt(Population * TaxPerResident);
            MonthlyDelta = income - upkeep;

            // RCI: residents want jobs, businesses want customers, industry wants demand.
            DemandResidential = Norm(jobs - Population);
            DemandCommercial = Norm(Population - jobs * 2);
            DemandIndustrial = Norm(housing - jobs);

            EmitSignal(SignalName.StatsChanged);
        }

        private static float Norm(float v) => Mathf.Clamp(v / 120f, -1f, 1f);

        private int Sum(System.Func<BuildingDef, int> pick)
        {
            int total = 0;
            foreach (var (id, n) in _owned)
                if (Find(id) is { } b && n > 0) total += pick(b) * n;
            return total;
        }

        // ── ISaveable ──────────────────────────────────────────────────

        public void Save(GameBuilder.GameStateData state)
        {
            state.GameData[KTreasury] = Treasury;
            state.GameData[KPopulation] = Population;
            state.GameData[KHappiness] = Happiness;
            state.GameData[KDay] = Month;
            state.GameData[KSpeed] = Speed;

            // Building counts as one nested dictionary, so adding a building type later does not
            // add a key to the top-level save and cannot collide with another genre's namespace.
            var owned = new Godot.Collections.Dictionary();
            foreach (var (id, n) in _owned) owned[id] = n;
            state.GameData[KBuildings] = owned;
        }

        public void Load(GameBuilder.GameStateData state)
        {
            var d = state.GameData;
            if (d.TryGetValue(KTreasury, out var t)) Treasury = t.AsInt32();
            if (d.TryGetValue(KPopulation, out var p)) Population = p.AsInt32();
            if (d.TryGetValue(KHappiness, out var h)) Happiness = h.AsInt32();
            if (d.TryGetValue(KDay, out var m)) Month = m.AsInt32();
            if (d.TryGetValue(KSpeed, out var s)) Speed = s.AsInt32();

            _owned.Clear();
            if (d.TryGetValue(KBuildings, out var b))
                foreach (var kv in b.AsGodotDictionary())
                    _owned[kv.Key.AsString()] = kv.Value.AsInt32();

            // Derived values are never saved — they are recomputed, so a save can never carry a
            // treasury that disagrees with the buildings that produced it.
            Recalculate();
            EmitSignal(SignalName.BuildingsChanged);
        }
    }
}

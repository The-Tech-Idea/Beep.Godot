using Godot;
using System.Collections.Generic;
using System.Globalization;

namespace Beep.ECS
{
    /// <summary>
    /// The strategy genre's empire state: turn-based gold, food and wood, plus the unit roster
    /// they support.
    ///
    /// <c>StrategyHudComponent</c> registered all five readouts as <c>Placeholder(...)</c>, so
    /// every number shown was typed into the scene. Seventh genre to get a real one.
    ///
    /// ASSUMPTIONS, since the specific game is not yet defined — all are exported so a project
    /// can retune without touching this file:
    ///  - turn-based, one player empire, resources banked per turn (4X convention)
    ///  - units cost upkeep in gold and eat food each turn; food is the binding constraint
    ///  - a food deficit starves units rather than going negative, because a resource that can
    ///    go negative has no failure state and the player never has to react
    ///
    /// What makes it an economy rather than four counters: income is DERIVED from what the
    /// empire holds, so a number on the HUD can never disagree with the units producing it.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class StrategyEmpireComponent : GameplayComponent, ISaveable
    {
        /// <summary>Join the save walk. Declared per-component, not inherited.</summary>
        [Export] public bool ParticipatesInSave { get; set; } = true;

        // ── Tuning ────────────────────────────────────────────────────────────────────────
        [Export] public int StartingGold { get; set; } = 200;
        [Export] public int StartingFood { get; set; } = 150;
        [Export] public int StartingWood { get; set; } = 100;

        /// <summary>Base per-turn yield before unit costs — the empire's territory.</summary>
        [Export] public int GoldPerTurn { get; set; } = 25;
        [Export] public int FoodPerTurn { get; set; } = 20;
        [Export] public int WoodPerTurn { get; set; } = 12;

        [Export] public int GoldUpkeepPerUnit { get; set; } = 3;
        [Export] public int FoodPerUnit { get; set; } = 2;

        /// <summary>Units lost per turn when food runs out. Starvation has to bite, or a food
        /// shortage is just a number that stops going up.</summary>
        [Export] public int StarvationLossPerTurn { get; set; } = 1;
        public int EffectiveStartingGold => Mathf.Max(0, StartingGold);
        public int EffectiveStartingFood => Mathf.Max(0, StartingFood);
        public int EffectiveStartingWood => Mathf.Max(0, StartingWood);
        public int EffectiveGoldPerTurn => Mathf.Max(0, GoldPerTurn);
        public int EffectiveFoodPerTurn => Mathf.Max(0, FoodPerTurn);
        public int EffectiveWoodPerTurn => Mathf.Max(0, WoodPerTurn);
        public int EffectiveGoldUpkeepPerUnit => Mathf.Max(0, GoldUpkeepPerUnit);
        public int EffectiveFoodPerUnit => Mathf.Max(0, FoodPerUnit);
        public int EffectiveStarvationLossPerTurn => Mathf.Max(0, StarvationLossPerTurn);

        // ── State ─────────────────────────────────────────────────────────────────────────
        public int Turn { get; private set; } = 1;
        public int Gold { get; private set; }
        public int Food { get; private set; }
        public int Wood { get; private set; }
        public int Units { get; private set; }

        /// <summary>Net per-turn change, derived from the roster. Shown as the delta beside each
        /// resource — a stockpile alone does not tell a player whether it is sustainable.</summary>
        public int GoldDelta => EffectiveGoldPerTurn - Units * EffectiveGoldUpkeepPerUnit;
        public int FoodDelta => EffectiveFoodPerTurn - Units * EffectiveFoodPerUnit;
        public int WoodDelta => EffectiveWoodPerTurn;

        public bool IsStarving => FoodDelta < 0 && Food <= 0;
        public bool IsBankrupt => GoldDelta < 0 && Gold <= 0;

        /// <summary>Units the current food yield can sustain indefinitely.</summary>
        public int SustainableUnits => EffectiveFoodPerUnit <= 0 ? int.MaxValue : EffectiveFoodPerTurn / EffectiveFoodPerUnit;

        [Signal] public delegate void EmpireChangedEventHandler();
        [Signal] public delegate void TurnAdvancedEventHandler(int turn);
        [Signal] public delegate void EmpireAlertEventHandler(string severity, string text);

        public override void _Ready()
        {
            base._Ready();
            Gold = EffectiveStartingGold;
            Food = EffectiveStartingFood;
            Wood = EffectiveStartingWood;
            if (ParticipatesInSave) AddToGroup(SaveableHelper.Group);
        }

        // ── Turn cycle ────────────────────────────────────────────────────────────────────

        /// <summary>Advance one turn: collect yields, pay upkeep, then resolve starvation.</summary>
        public void EndTurn()
        {
            Turn++;

            Gold = Mathf.Max(0, Gold + GoldDelta);
            Wood = Mathf.Max(0, Wood + WoodDelta);

            int food = Food + FoodDelta;
            if (food < 0)
            {
                // Starve rather than bank a negative. Units are lost until the deficit is
                // covered, so the shortage resolves itself the way a player would expect.
                Food = 0;
                int foodPerUnit = EffectiveFoodPerUnit;
                int lost = Mathf.Max(EffectiveStarvationLossPerTurn,
                                     foodPerUnit <= 0 ? 0 : Mathf.CeilToInt(-food / (float)foodPerUnit));
                lost = Mathf.Min(lost, Units);
                if (lost > 0)
                {
                    Units -= lost;
                    EmitSignal(SignalName.EmpireAlert, "danger",
                               lost == 1 ? "A unit starved" : $"{lost} units starved");
                }
            }
            else
            {
                Food = food;
                if (FoodDelta < 0)
                    EmitSignal(SignalName.EmpireAlert, "warning", "Food is running out");
            }

            if (GoldDelta < 0 && Gold <= 0)
                EmitSignal(SignalName.EmpireAlert, "warning", "Treasury is empty");

            EmitSignal(SignalName.TurnAdvanced, Turn);
            EmitSignal(SignalName.EmpireChanged);
        }

        // ── Empire API ────────────────────────────────────────────────────────────────────

        /// <summary>Can the empire pay this cost right now?</summary>
        public bool CanAfford(int gold, int food = 0, int wood = 0)
            => Gold >= Cost(gold) && Food >= Cost(food) && Wood >= Cost(wood);

        /// <summary>Spend resources. Returns false and changes NOTHING when short, so a caller
        /// cannot half-pay for a unit it does not get.</summary>
        public bool Spend(int gold, int food = 0, int wood = 0)
        {
            int goldCost = Cost(gold);
            int foodCost = Cost(food);
            int woodCost = Cost(wood);
            if (!CanAfford(goldCost, foodCost, woodCost)) return false;
            Gold -= goldCost; Food -= foodCost; Wood -= woodCost;
            EmitSignal(SignalName.EmpireChanged);
            return true;
        }

        public void Grant(int gold, int food = 0, int wood = 0)
        {
            Gold = Mathf.Max(0, Gold + Cost(gold));
            Food = Mathf.Max(0, Food + Cost(food));
            Wood = Mathf.Max(0, Wood + Cost(wood));
            EmitSignal(SignalName.EmpireChanged);
        }

        /// <summary>Recruit units, paying up front. Warns when the roster passes what the food
        /// yield can sustain — the moment growth becomes a liability.</summary>
        public bool Recruit(int count, int goldCost, int foodCost = 0, int woodCost = 0)
        {
            int recruitCount = Mathf.Max(0, count);
            if (recruitCount <= 0 || !Spend(goldCost, foodCost, woodCost)) return false;
            Units += recruitCount;
            if (Units > SustainableUnits)
                EmitSignal(SignalName.EmpireAlert, "warning",
                           $"{Units} units exceed what {EffectiveFoodPerTurn} food/turn sustains");
            EmitSignal(SignalName.EmpireChanged);
            return true;
        }

        /// <summary>Lose units to combat or disband.</summary>
        public void LoseUnits(int count)
        {
            if (count <= 0 || Units <= 0) return;
            Units = Mathf.Max(0, Units - count);
            EmitSignal(SignalName.EmpireChanged);
        }

        public void RestartEmpire()
        {
            Turn = 1; Units = 0;
            Gold = EffectiveStartingGold; Food = EffectiveStartingFood; Wood = EffectiveStartingWood;
            EmitSignal(SignalName.TurnAdvanced, Turn);
            EmitSignal(SignalName.EmpireChanged);
        }

        // ── Persistence ───────────────────────────────────────────────────────────────────
        private const string KTurn = "strategy.turn";
        private const string KGold = "strategy.gold";
        private const string KFood = "strategy.food";
        private const string KWood = "strategy.wood";
        private const string KUnits = "strategy.units";

        public void Save(GameBuilder.GameStateData state)
        {
            state.GameData[KTurn] = Turn;
            state.GameData[KGold] = Gold;
            state.GameData[KFood] = Food;
            state.GameData[KWood] = Wood;
            state.GameData[KUnits] = Units;
            // The deltas are NOT saved — they are derived from the roster, so a save can never
            // carry an income that disagrees with the units producing it.
        }

        public void Load(GameBuilder.GameStateData state)
        {
            var d = state.GameData;
            if (d.TryGetValue(KTurn, out var t)) Turn = Mathf.Max(1, ReadInt(t, Turn));
            if (d.TryGetValue(KGold, out var g)) Gold = Mathf.Max(0, ReadInt(g, Gold));
            if (d.TryGetValue(KFood, out var f)) Food = Mathf.Max(0, ReadInt(f, Food));
            if (d.TryGetValue(KWood, out var w)) Wood = Mathf.Max(0, ReadInt(w, Wood));
            if (d.TryGetValue(KUnits, out var u)) Units = Mathf.Max(0, ReadInt(u, Units));
            EmitSignal(SignalName.TurnAdvanced, Turn);
            EmitSignal(SignalName.EmpireChanged);
        }

        private static int Cost(int value) => Mathf.Max(0, value);

        private static int ReadInt(Variant value, int fallback)
        {
            switch (value.VariantType)
            {
                case Variant.Type.Int:
                    return value.AsInt32();
                case Variant.Type.Float:
                {
                    double raw = value.AsDouble();
                    return double.IsFinite(raw) ? Mathf.RoundToInt((float)raw) : fallback;
                }
                case Variant.Type.String:
                    return int.TryParse(value.AsString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                        ? parsed
                        : fallback;
                default:
                    return fallback;
            }
        }
    }
}

using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// The card genre's run state: the draw/discard cycle, the per-turn energy budget, and the
    /// player's health and gold.
    ///
    /// <c>CardGameHudComponent</c> registered all five readouts as <c>Placeholder(...)</c>.
    /// Eighth and last genre to get a real one.
    ///
    /// The shape is a DECKBUILDER, inferred from the HUD's own bindings rather than assumed: it
    /// asks for energy, a deck count and a discard count, which is the deckbuilder loop and not
    /// a trick-taker or a TCG. Every number is exported so a project can retune.
    ///
    /// The defining mechanic — and the one thing here that is easy to get wrong — is that the
    /// deck does not simply run out. When a draw empties it, the discard pile is shuffled back
    /// in and the draw continues. A deck that stops at zero ends the run on turn three.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class CardDeckComponent : GameplayComponent, ISaveable
    {
        /// <summary>Join the save walk. Declared per-component, not inherited.</summary>
        [Export] public bool ParticipatesInSave { get; set; } = true;

        // ── Tuning ────────────────────────────────────────────────────────────────────────
        [Export] public int MaxHealth { get; set; } = 70;
        [Export] public int StartingGold { get; set; } = 99;
        [Export] public int EnergyPerTurn { get; set; } = 3;
        [Export] public int HandSize { get; set; } = 5;

        /// <summary>Card ids the run starts with. Duplicates are the point — a starting deck is
        /// mostly copies of two or three cards.</summary>
        [Export] public string[] StartingDeck { get; set; } =
            { "strike", "strike", "strike", "strike", "strike",
              "defend", "defend", "defend", "defend", "block" };

        // ── State ─────────────────────────────────────────────────────────────────────────
        public int Health { get; private set; }
        public int Gold { get; private set; }
        public int Energy { get; private set; }
        public int Turn { get; private set; } = 1;

        private readonly List<string> _deck = new();
        private readonly List<string> _hand = new();
        private readonly List<string> _discard = new();

        public IReadOnlyList<string> Deck => _deck;
        public IReadOnlyList<string> Hand => _hand;
        public IReadOnlyList<string> Discard => _discard;

        public int DeckCount => _deck.Count;
        public int HandCount => _hand.Count;
        public int DiscardCount => _discard.Count;
        /// <summary>Every card in the run, wherever it currently sits. Constant except when
        /// cards are added or removed — a useful invariant to assert against.</summary>
        public int TotalCards => _deck.Count + _hand.Count + _discard.Count;

        public bool IsDead => Health <= 0;
        public float HealthFraction => MaxHealth <= 0 ? 0f : (float)Health / MaxHealth;
        public float EnergyFraction => EnergyPerTurn <= 0 ? 0f
            : Mathf.Clamp((float)Energy / EnergyPerTurn, 0f, 1f);

        [Signal] public delegate void DeckChangedEventHandler();
        [Signal] public delegate void HandDrawnEventHandler(int count);
        [Signal] public delegate void ReshuffledEventHandler(int cards);
        [Signal] public delegate void TurnStartedEventHandler(int turn);
        [Signal] public delegate void DiedEventHandler();

        private readonly RandomNumberGenerator _rng = new();

        public override void _Ready()
        {
            base._Ready();
            _rng.Randomize();
            Health = MaxHealth;
            Gold = StartingGold;
            StartRun();
            if (ParticipatesInSave) AddToGroup(SaveableHelper.Group);
        }

        /// <summary>Reset to the starting deck and open turn 1.</summary>
        public void StartRun()
        {
            _deck.Clear(); _hand.Clear(); _discard.Clear();
            if (StartingDeck != null) _deck.AddRange(StartingDeck);
            Shuffle(_deck);
            Turn = 1;
            Energy = EnergyPerTurn;
            DrawHand();
            EmitSignal(SignalName.TurnStarted, Turn);
            EmitSignal(SignalName.DeckChanged);
        }

        private void Shuffle(List<string> cards)
        {
            // Fisher-Yates. Godot's RNG rather than System.Random so a project can seed it
            // through _rng for reproducible runs.
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = (int)(_rng.Randi() % (uint)(i + 1));
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
        }

        // ── Draw cycle ────────────────────────────────────────────────────────────────────

        /// <summary>Draw one card, reshuffling the discard pile in when the deck runs dry.
        /// Returns the card id, or null when the run genuinely holds no cards at all.</summary>
        public string? DrawOne()
        {
            if (_deck.Count == 0)
            {
                if (_discard.Count == 0) return null;   // truly out of cards, not just out of deck
                _deck.AddRange(_discard);
                _discard.Clear();
                Shuffle(_deck);
                EmitSignal(SignalName.Reshuffled, _deck.Count);
            }
            string card = _deck[^1];
            _deck.RemoveAt(_deck.Count - 1);
            _hand.Add(card);
            return card;
        }

        /// <summary>Draw up to <see cref="HandSize"/>. Stops early only when the run is out of
        /// cards entirely, which is a real state rather than an error.</summary>
        public int DrawHand()
        {
            int drawn = 0;
            while (_hand.Count < HandSize && DrawOne() != null) drawn++;
            EmitSignal(SignalName.HandDrawn, drawn);
            EmitSignal(SignalName.DeckChanged);
            return drawn;
        }

        // ── Turn cycle ────────────────────────────────────────────────────────────────────

        /// <summary>Play a card from hand. Returns false — changing nothing — when the card is
        /// not held or the energy is not there, so a caller cannot half-play a card.</summary>
        public bool PlayCard(string cardId, int energyCost)
        {
            if (IsDead || energyCost > Energy) return false;
            int at = _hand.IndexOf(cardId);
            if (at < 0) return false;

            Energy -= energyCost;
            _hand.RemoveAt(at);
            _discard.Add(cardId);
            EmitSignal(SignalName.DeckChanged);
            return true;
        }

        /// <summary>End the turn: the hand is discarded, energy refills, a new hand is drawn.
        /// Discarding the hand is what makes energy a per-turn budget rather than a pool.</summary>
        public void EndTurn()
        {
            if (IsDead) return;
            _discard.AddRange(_hand);
            _hand.Clear();
            Turn++;
            Energy = EnergyPerTurn;
            DrawHand();
            EmitSignal(SignalName.TurnStarted, Turn);
            EmitSignal(SignalName.DeckChanged);
        }

        // ── Player state ──────────────────────────────────────────────────────────────────
        public bool Damage(int amount)
        {
            if (amount <= 0 || IsDead) return false;
            Health = Mathf.Max(0, Health - amount);
            EmitSignal(SignalName.DeckChanged);
            if (IsDead) EmitSignal(SignalName.Died);
            return IsDead;
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || IsDead) return;
            Health = Mathf.Min(MaxHealth, Health + amount);
            EmitSignal(SignalName.DeckChanged);
        }

        public void AddGold(int amount)
        {
            Gold = Mathf.Max(0, Gold + amount);
            EmitSignal(SignalName.DeckChanged);
        }

        public bool SpendGold(int amount)
        {
            if (amount <= 0) return true;
            if (Gold < amount) return false;
            Gold -= amount;
            EmitSignal(SignalName.DeckChanged);
            return true;
        }

        /// <summary>Add a card to the DISCARD pile, which is where a reward lands so it appears
        /// in the next reshuffle rather than jumping the current deck order.</summary>
        public void AddCard(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return;
            _discard.Add(cardId);
            EmitSignal(SignalName.DeckChanged);
        }

        /// <summary>Remove one copy from wherever it sits — deck, hand or discard.</summary>
        public bool RemoveCard(string cardId)
        {
            foreach (var pile in new[] { _deck, _hand, _discard })
            {
                int at = pile.IndexOf(cardId);
                if (at < 0) continue;
                pile.RemoveAt(at);
                EmitSignal(SignalName.DeckChanged);
                return true;
            }
            return false;
        }

        // ── Persistence ───────────────────────────────────────────────────────────────────
        private const string KHealth = "cardgame.health";
        private const string KGold = "cardgame.gold";
        private const string KEnergy = "cardgame.energy";
        private const string KTurn = "cardgame.turn";
        private const string KDeck = "cardgame.deck";
        private const string KHand = "cardgame.hand";
        private const string KDiscard = "cardgame.discard";

        private static Godot.Collections.Array Pack(List<string> pile)
        {
            var a = new Godot.Collections.Array();
            foreach (string c in pile) a.Add(c);
            return a;
        }

        private static void Unpack(Variant v, List<string> into)
        {
            into.Clear();
            foreach (var e in v.AsGodotArray()) into.Add(e.AsString());
        }

        public void Save(GameBuilder.GameStateData state)
        {
            state.GameData[KHealth] = Health;
            state.GameData[KGold] = Gold;
            state.GameData[KEnergy] = Energy;
            state.GameData[KTurn] = Turn;
            // All three piles, in order: the deck's ORDER is game state. Saving only the counts
            // would reshuffle the run on load and hand the player a different next draw.
            state.GameData[KDeck] = Pack(_deck);
            state.GameData[KHand] = Pack(_hand);
            state.GameData[KDiscard] = Pack(_discard);
        }

        public void Load(GameBuilder.GameStateData state)
        {
            var d = state.GameData;
            if (d.TryGetValue(KHealth, out var h)) Health = Mathf.Clamp(h.AsInt32(), 0, MaxHealth);
            if (d.TryGetValue(KGold, out var g)) Gold = Mathf.Max(0, g.AsInt32());
            if (d.TryGetValue(KEnergy, out var e)) Energy = Mathf.Max(0, e.AsInt32());
            if (d.TryGetValue(KTurn, out var t)) Turn = Mathf.Max(1, t.AsInt32());
            if (d.TryGetValue(KDeck, out var dk)) Unpack(dk, _deck);
            if (d.TryGetValue(KHand, out var hd)) Unpack(hd, _hand);
            if (d.TryGetValue(KDiscard, out var ds)) Unpack(ds, _discard);
            EmitSignal(SignalName.TurnStarted, Turn);
            EmitSignal(SignalName.DeckChanged);
        }
    }
}

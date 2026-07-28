using Godot;

namespace Beep.ECS.UI
{
    /// <summary>Card-game HUD: player HP, Gold, Energy, and the Deck/Discard counts.
    ///
    /// Driven by <see cref="CardDeckComponent"/>. All five were previously registered as
    /// <c>Placeholder(...)</c>, so every number shown was typed into the scene. Placeholder is
    /// now the FALLBACK for a scene with no deck component, not the normal path.</summary>
    [Tool]
    [GlobalClass]
    public partial class CardGameHudComponent : GenreHudComponent
    {
        [Export] public NodePath HealthPath { get; set; } = "TopLeft/StatsVBox/HealthLabel";
        [Export] public NodePath GoldPath { get; set; } = "TopRight/GoldLabel";
        [Export] public NodePath EnergyPath { get; set; } = "EnergyBox/EnergyLabel";
        [Export] public NodePath DeckPath { get; set; } = "BottomRight/DeckLabel";
        [Export] public NodePath DiscardPath { get; set; } = "BottomRight/DiscardLabel";

        /// <summary>Optional toast host for reshuffle and death alerts.</summary>
        [Export] public NodePath AlertHostPath { get; set; } = new("");

        protected override string Genre => "cardgame";

        private CardDeckComponent? _deck;
        private ToastNotificationComponent? _alerts;
        private Godot.Control? _health, _gold, _energy, _deckCount, _discardCount;

        protected override void Wire()
        {
            _deck = FindInScene<CardDeckComponent>();

            if (_deck == null)
            {
                // No simulation in this scene: fall back to developer-driven readouts so the HUD
                // still functions, and say so once.
                Placeholder(HealthPath, "health");
                Placeholder(GoldPath, "gold");
                Placeholder(EnergyPath, "energy");
                Placeholder(DeckPath, "deck");
                Placeholder(DiscardPath, "discard");
                return;
            }

            _health = ResolveReadout(HealthPath, "health");
            _gold = ResolveReadout(GoldPath, "gold");
            _energy = ResolveReadout(EnergyPath, "energy");
            _deckCount = ResolveReadout(DeckPath, "deck");
            _discardCount = ResolveReadout(DiscardPath, "discard");
            _alerts = ResolveNode<ToastNotificationComponent>(AlertHostPath);

            _deck.DeckChanged += OnDeck;
            _deck.Reshuffled += OnReshuffled;
            _deck.Died += OnDied;
            OnDeck();
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (_deck != null && GodotObject.IsInstanceValid(_deck))
            {
                _deck.DeckChanged -= OnDeck;
                _deck.Reshuffled -= OnReshuffled;
                _deck.Died -= OnDied;
            }
            _deck = null;
        }

        private void OnDeck()
        {
            if (_deck == null) return;

            SetReadout(_health, $"{_deck.Health} / {_deck.MaxHealth}", _deck.HealthFraction);
            Tint(_health, _deck.IsDead ? UiSurface.Role.Danger
                 : _deck.HealthFraction <= 0.3f ? UiSurface.Role.Warning
                 : null);

            SetReadout(_gold, _deck.Gold.ToString("N0"));

            SetReadout(_energy, $"{_deck.Energy} / {_deck.EnergyPerTurn}", _deck.EnergyFraction);
            // Out of energy is the end of what you can do this turn, not a failure — a warning
            // rather than danger.
            Tint(_energy, _deck.Energy <= 0 ? UiSurface.Role.Warning : null);

            SetReadout(_deckCount, _deck.DeckCount.ToString());
            SetReadout(_discardCount, _deck.DiscardCount.ToString());
        }

        private void OnReshuffled(int cards)
            => _alerts?.ShowToast($"Reshuffled {cards} cards",
                                  ToastNotificationComponent.ToastType.Info);

        private void OnDied()
            => _alerts?.ShowToast("Defeated", ToastNotificationComponent.ToastType.Error);
    }
}

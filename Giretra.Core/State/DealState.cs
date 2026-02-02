using System.Collections.Immutable;
using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Negotiation;
using Giretra.Core.Players;
using Giretra.Core.Scoring;

namespace Giretra.Core.State;

/// <summary>
/// Represents the complete state of a single deal.
/// </summary>
public sealed class DealState
{
    /// <summary>
    /// Gets the current phase of the deal.
    /// </summary>
    public DealPhase Phase { get; }

    /// <summary>
    /// Gets the dealer's position.
    /// </summary>
    public PlayerPosition Dealer { get; }

    /// <summary>
    /// Gets the four players indexed by position.
    /// </summary>
    public ImmutableDictionary<PlayerPosition, Player> Players { get; }

    /// <summary>
    /// Gets the current deck state.
    /// </summary>
    public Deck Deck { get; }

    /// <summary>
    /// Gets the negotiation state (available during and after Negotiation phase).
    /// </summary>
    public NegotiationState? Negotiation { get; }

    /// <summary>
    /// Gets the hand state (available during and after Playing phase).
    /// </summary>
    public HandState? Hand { get; }

    /// <summary>
    /// Gets the resolved game mode (set after negotiation completes).
    /// </summary>
    public GameMode? ResolvedMode { get; }

    /// <summary>
    /// Gets the announcer team (set after negotiation completes).
    /// </summary>
    public Team? AnnouncerTeam { get; }

    /// <summary>
    /// Gets the multiplier state (set after negotiation completes).
    /// </summary>
    public MultiplierState? Multiplier { get; }

    /// <summary>
    /// Gets the deal result (available when phase is Completed).
    /// </summary>
    public DealResult? Result { get; }

    private DealState(
        DealPhase phase,
        PlayerPosition dealer,
        ImmutableDictionary<PlayerPosition, Player> players,
        Deck deck,
        NegotiationState? negotiation,
        HandState? hand,
        GameMode? resolvedMode,
        Team? announcerTeam,
        MultiplierState? multiplier,
        DealResult? result)
    {
        Phase = phase;
        Dealer = dealer;
        Players = players;
        Deck = deck;
        Negotiation = negotiation;
        Hand = hand;
        ResolvedMode = resolvedMode;
        AnnouncerTeam = announcerTeam;
        Multiplier = multiplier;
        Result = result;
    }

    /// <summary>
    /// Creates a new deal with a fresh deck awaiting cut.
    /// </summary>
    public static DealState Create(PlayerPosition dealer, Deck deck)
    {
        var players = ImmutableDictionary<PlayerPosition, Player>.Empty
            .Add(PlayerPosition.Bottom, Player.Create(PlayerPosition.Bottom))
            .Add(PlayerPosition.Left, Player.Create(PlayerPosition.Left))
            .Add(PlayerPosition.Top, Player.Create(PlayerPosition.Top))
            .Add(PlayerPosition.Right, Player.Create(PlayerPosition.Right));

        return new DealState(
            DealPhase.AwaitingCut,
            dealer,
            players,
            deck,
            null,
            null,
            null,
            null,
            null,
            null);
    }

    /// <summary>
    /// Cuts the deck and advances to initial distribution.
    /// </summary>
    public DealState CutDeck(int position, bool fromTop = true)
    {
        if (Phase != DealPhase.AwaitingCut)
        {
            throw new InvalidOperationException($"Cannot cut in phase {Phase}.");
        }

        var cutDeck = Deck.Cut(position, fromTop);

        return new DealState(
            DealPhase.InitialDistribution,
            Dealer,
            Players,
            cutDeck,
            Negotiation,
            Hand,
            ResolvedMode,
            AnnouncerTeam,
            Multiplier,
            Result);
    }

    /// <summary>
    /// Performs the initial distribution (5 cards each: 3+2) and advances to negotiation.
    /// </summary>
    public DealState PerformInitialDistribution()
    {
        if (Phase != DealPhase.InitialDistribution)
        {
            throw new InvalidOperationException($"Cannot distribute in phase {Phase}.");
        }

        var currentDeck = Deck;
        var newPlayers = Players;

        // Deal 3 cards to each player (clockwise from dealer's left)
        foreach (var position in Dealer.GetPlayOrder())
        {
            var (cards, remaining) = currentDeck.Deal(3);
            newPlayers = newPlayers.SetItem(position, newPlayers[position].AddCards(cards));
            currentDeck = remaining;
        }

        // Deal 2 cards to each player
        foreach (var position in Dealer.GetPlayOrder())
        {
            var (cards, remaining) = currentDeck.Deal(2);
            newPlayers = newPlayers.SetItem(position, newPlayers[position].AddCards(cards));
            currentDeck = remaining;
        }

        var negotiation = NegotiationState.Create(Dealer);

        return new DealState(
            DealPhase.Negotiation,
            Dealer,
            newPlayers,
            currentDeck,
            negotiation,
            Hand,
            ResolvedMode,
            AnnouncerTeam,
            Multiplier,
            Result);
    }

    /// <summary>
    /// Applies a negotiation action.
    /// </summary>
    public DealState ApplyNegotiationAction(NegotiationAction action)
    {
        if (Phase != DealPhase.Negotiation)
        {
            throw new InvalidOperationException($"Cannot negotiate in phase {Phase}.");
        }

        if (Negotiation is null)
        {
            throw new InvalidOperationException("Negotiation state not initialized.");
        }

        var newNegotiation = Negotiation.Apply(action);

        var newPhase = newNegotiation.IsComplete
            ? DealPhase.FinalDistribution
            : DealPhase.Negotiation;

        GameMode? resolvedMode = null;
        Team? announcerTeam = null;
        MultiplierState? multiplier = null;

        if (newNegotiation.IsComplete)
        {
            var (mode, team, mult) = newNegotiation.ResolveFinalMode();
            resolvedMode = mode;
            announcerTeam = team;
            multiplier = mult;
        }

        return new DealState(
            newPhase,
            Dealer,
            Players,
            Deck,
            newNegotiation,
            Hand,
            resolvedMode,
            announcerTeam,
            multiplier,
            Result);
    }

    /// <summary>
    /// Performs the final distribution (3 cards each) and advances to playing.
    /// </summary>
    public DealState PerformFinalDistribution()
    {
        if (Phase != DealPhase.FinalDistribution)
        {
            throw new InvalidOperationException($"Cannot distribute in phase {Phase}.");
        }

        var currentDeck = Deck;
        var newPlayers = Players;

        // Deal 3 cards to each player
        foreach (var position in Dealer.GetPlayOrder())
        {
            var (cards, remaining) = currentDeck.Deal(3);
            newPlayers = newPlayers.SetItem(position, newPlayers[position].AddCards(cards));
            currentDeck = remaining;
        }

        // First player to lead is to dealer's left
        var firstLeader = Dealer.Next();
        var hand = HandState.Create(ResolvedMode!.Value, firstLeader);

        return new DealState(
            DealPhase.Playing,
            Dealer,
            newPlayers,
            currentDeck,
            Negotiation,
            hand,
            ResolvedMode,
            AnnouncerTeam,
            Multiplier,
            Result);
    }

    /// <summary>
    /// Plays a card in the current trick.
    /// </summary>
    public DealState PlayCard(PlayerPosition player, Card card)
    {
        if (Phase != DealPhase.Playing)
        {
            throw new InvalidOperationException($"Cannot play cards in phase {Phase}.");
        }

        if (Hand is null)
        {
            throw new InvalidOperationException("Hand state not initialized.");
        }

        // Verify it's this player's turn
        if (Hand.CurrentTrick?.CurrentPlayer != player)
        {
            throw new InvalidOperationException($"It is not {player}'s turn.");
        }

        // Verify player has the card
        if (!Players[player].HasCard(card))
        {
            throw new InvalidOperationException($"{player} does not have {card}.");
        }

        // Remove card from player's hand
        var newPlayers = Players.SetItem(player, Players[player].RemoveCard(card));

        // Play card in hand
        var newHand = Hand.PlayCard(card);

        var newPhase = newHand.IsComplete ? DealPhase.Completed : DealPhase.Playing;

        DealResult? result = null;
        if (newHand.IsComplete)
        {
            result = CalculateResult(newHand);
        }

        return new DealState(
            newPhase,
            Dealer,
            newPlayers,
            Deck,
            Negotiation,
            newHand,
            ResolvedMode,
            AnnouncerTeam,
            Multiplier,
            result);
    }

    private DealResult CalculateResult(HandState hand)
    {
        var calculator = new ScoringCalculator();
        return calculator.Calculate(
            ResolvedMode!.Value,
            Multiplier!.Value,
            AnnouncerTeam!.Value,
            hand.Team1CardPoints,
            hand.Team2CardPoints,
            hand.SweepingTeam);
    }

    /// <summary>
    /// Gets the player at the specified position.
    /// </summary>
    public Player GetPlayer(PlayerPosition position) => Players[position];
}

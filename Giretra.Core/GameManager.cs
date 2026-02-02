using Giretra.Core.Cards;
using Giretra.Core.Negotiation;
using Giretra.Core.Play;
using Giretra.Core.Players;
using Giretra.Core.Scoring;
using Giretra.Core.State;

namespace Giretra.Core;

/// <summary>
/// Orchestrates a full Belote match from start to finish,
/// coordinating four players through all game phases.
/// </summary>
public sealed class GameManager
{
    private readonly IReadOnlyDictionary<PlayerPosition, IPlayerAgent> _players;
    private readonly Func<Deck> _deckProvider;

    private MatchState _matchState;

    /// <summary>
    /// Gets the current match state.
    /// </summary>
    public MatchState MatchState => _matchState;

    /// <summary>
    /// Creates a new game manager with the specified players.
    /// </summary>
    /// <param name="players">Dictionary mapping positions to player implementations.</param>
    /// <param name="firstDealer">The position of the first dealer.</param>
    /// <param name="deckProvider">Optional function to provide the deck for each deal. Defaults to standard deck.</param>
    public GameManager(
        IReadOnlyDictionary<PlayerPosition, IPlayerAgent> players,
        PlayerPosition firstDealer,
        Func<Deck>? deckProvider = null)
    {
        if (players.Count != 4)
        {
            throw new ArgumentException("Exactly 4 players are required.", nameof(players));
        }

        foreach (PlayerPosition position in Enum.GetValues<PlayerPosition>())
        {
            if (!players.ContainsKey(position))
            {
                throw new ArgumentException($"Missing player for position {position}.", nameof(players));
            }

            if (players[position].Position != position)
            {
                throw new ArgumentException(
                    $"Player at position {position} has mismatched Position property: {players[position].Position}.",
                    nameof(players));
            }
        }

        _players = players;
        _deckProvider = deckProvider ?? Deck.CreateStandard;
        _matchState = MatchState.Create(firstDealer);
    }

    /// <summary>
    /// Creates a new game manager with the specified players.
    /// </summary>
    /// <param name="bottom">Player at the Bottom position.</param>
    /// <param name="left">Player at the Left position.</param>
    /// <param name="top">Player at the Top position.</param>
    /// <param name="right">Player at the Right position.</param>
    /// <param name="firstDealer">The position of the first dealer.</param>
    /// <param name="deckProvider">Optional function to provide the deck for each deal.</param>
    public GameManager(
        IPlayerAgent bottom,
        IPlayerAgent left,
        IPlayerAgent top,
        IPlayerAgent right,
        PlayerPosition firstDealer,
        Func<Deck>? deckProvider = null)
        : this(
            new Dictionary<PlayerPosition, IPlayerAgent>
            {
                [PlayerPosition.Bottom] = bottom,
                [PlayerPosition.Left] = left,
                [PlayerPosition.Top] = top,
                [PlayerPosition.Right] = right
            },
            firstDealer,
            deckProvider)
    {
    }

    /// <summary>
    /// Plays a complete match until a winner is determined.
    /// </summary>
    /// <returns>The final match state with the winner.</returns>
    public async Task<MatchState> PlayMatchAsync()
    {
        while (!_matchState.IsComplete)
        {
            await PlayDealAsync();
        }

        // Notify all players that the match has ended
        await NotifyAllPlayersAsync(p => p.OnMatchEndedAsync(_matchState));

        return _matchState;
    }

    /// <summary>
    /// Plays a single deal.
    /// </summary>
    private async Task PlayDealAsync()
    {
        // Start the deal
        var deck = _deckProvider();
        _matchState = _matchState.StartDeal(deck);

        // Notify players that deal has started
        await NotifyAllPlayersAsync(p => p.OnDealStartedAsync(_matchState));

        // Cut phase - player to dealer's right cuts
        await PerformCutAsync();

        // Initial distribution (5 cards each)
        var deal = _matchState.CurrentDeal!.PerformInitialDistribution();
        _matchState = _matchState.WithDeal(deal);

        // Negotiation phase
        await PerformNegotiationAsync();

        // Final distribution (3 more cards each)
        deal = _matchState.CurrentDeal!.PerformFinalDistribution();
        _matchState = _matchState.WithDeal(deal);

        // Playing phase (8 tricks)
        await PerformPlayingAsync();

        // Get the result and notify players
        var result = _matchState.CompletedDeals[^1];
        await NotifyAllPlayersAsync(p => p.OnDealEndedAsync(result, _matchState));
    }

    /// <summary>
    /// Performs the cut phase.
    /// </summary>
    private async Task PerformCutAsync()
    {
        var deal = _matchState.CurrentDeal!;

        // Player to the right of dealer cuts
        var cutter = deal.Dealer.Previous();
        var player = _players[cutter];

        var (position, fromTop) = await player.ChooseCutAsync(deal.Deck.Count, _matchState);

        // Validate cut position
        if (position < 6 || position > 26)
        {
            throw new InvalidOperationException(
                $"Invalid cut position {position}. Must be between 6 and 26.");
        }

        deal = deal.CutDeck(position, fromTop);
        _matchState = _matchState.WithDeal(deal);
    }

    /// <summary>
    /// Performs the negotiation phase.
    /// </summary>
    private async Task PerformNegotiationAsync()
    {
        var deal = _matchState.CurrentDeal!;

        while (!deal.Negotiation!.IsComplete)
        {
            var currentPosition = deal.Negotiation.CurrentPlayer;
            var player = _players[currentPosition];
            var hand = deal.Players[currentPosition].Hand;

            // Compute valid actions to pass to the agent
            var validActions = NegotiationEngine.GetValidActions(deal.Negotiation);

            var action = await player.ChooseNegotiationActionAsync(
                hand,
                deal.Negotiation,
                _matchState,
                validActions);

            // Validate action is from correct player
            if (action.Player != currentPosition)
            {
                throw new InvalidOperationException(
                    $"Player {currentPosition} returned action for {action.Player}.");
            }

            deal = deal.ApplyNegotiationAction(action);
            _matchState = _matchState.WithDeal(deal);
        }
    }

    /// <summary>
    /// Performs the playing phase (8 tricks).
    /// </summary>
    private async Task PerformPlayingAsync()
    {
        var deal = _matchState.CurrentDeal!;

        while (deal.Phase == DealPhase.Playing)
        {
            var currentPosition = deal.Hand!.CurrentTrick!.CurrentPlayer!.Value;
            var player = _players[currentPosition];
            var playerState = deal.Players[currentPosition];
            var hand = playerState.Hand;

            // Compute valid plays to pass to the agent
            var validPlays = PlayValidator.GetValidPlays(
                Player.Create(currentPosition, hand),
                deal.Hand.CurrentTrick,
                deal.Hand.GameMode);

            var card = await player.ChooseCardAsync(
                hand,
                deal.Hand,
                _matchState,
                validPlays);

            // Validate player has the card
            if (!playerState.HasCard(card))
            {
                throw new InvalidOperationException(
                    $"Player {currentPosition} tried to play {card} which is not in their hand.");
            }

            deal = deal.PlayCard(currentPosition, card);
            _matchState = _matchState.WithDeal(deal);
        }
    }

    /// <summary>
    /// Notifies all players with the given action.
    /// </summary>
    private async Task NotifyAllPlayersAsync(Func<IPlayerAgent, Task> action)
    {
        foreach (var player in _players.Values)
        {
            await action(player);
        }
    }
}

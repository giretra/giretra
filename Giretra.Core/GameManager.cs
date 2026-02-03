using Giretra.Core.Cards;
using Giretra.Core.GameModes;
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
    /// <param name="targetScore">The target score to win the match (default 150).</param>
    public GameManager(
        IReadOnlyDictionary<PlayerPosition, IPlayerAgent> players,
        PlayerPosition firstDealer,
        Func<Deck>? deckProvider = null,
        int targetScore = 150)
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
        _matchState = MatchState.Create(firstDealer, targetScore);
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
    /// <param name="targetScore">The target score to win the match (default 150).</param>
    public GameManager(
        IPlayerAgent bottom,
        IPlayerAgent left,
        IPlayerAgent top,
        IPlayerAgent right,
        PlayerPosition firstDealer,
        Func<Deck>? deckProvider = null,
        int targetScore = 150)
        : this(
            new Dictionary<PlayerPosition, IPlayerAgent>
            {
                [PlayerPosition.Bottom] = bottom,
                [PlayerPosition.Left] = left,
                [PlayerPosition.Top] = top,
                [PlayerPosition.Right] = right
            },
            firstDealer,
            deckProvider,
            targetScore)
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

            // Track completed tricks count before playing
            var trickCountBefore = deal.Hand.CompletedTricks.Count;

            deal = deal.PlayCard(currentPosition, card);
            _matchState = _matchState.WithDeal(deal);

            // Notify all players that a card was played
            await NotifyAllPlayersAsync(p => p.OnCardPlayedAsync(currentPosition, card, deal.Hand!, _matchState));

            // Check if a trick was just completed
            if (deal.Hand != null && deal.Hand.CompletedTricks.Count > trickCountBefore)
            {
                var completedTrick = deal.Hand.CompletedTricks[^1];
                // The winner is the leader of the next trick, or we can get it from completed trick
                var winner = deal.Hand.CurrentTrick?.Leader ?? completedTrick.PlayedCards[0].Player;
                // Actually, the next trick's leader IS the winner of the previous trick
                if (deal.Hand.CurrentTrick != null)
                {
                    winner = deal.Hand.CurrentTrick.Leader;
                }
                else
                {
                    // Last trick - determine winner from the completed trick
                    // The winner leads next, but there's no next trick. We need to find who won.
                    // Look at who would have led - it's stored implicitly
                    // Actually we can check Team1TricksWon vs previous to determine winner team
                    // But simpler: iterate through completed trick to find winner
                    winner = DetermineWinner(completedTrick, deal.Hand.GameMode);
                }

                await NotifyAllPlayersAsync(p => p.OnTrickCompletedAsync(completedTrick, winner, deal.Hand, _matchState));
            }
        }
    }

    /// <summary>
    /// Determines the winner of a completed trick.
    /// </summary>
    private static PlayerPosition DetermineWinner(State.TrickState trick, GameModes.GameMode gameMode)
    {
        var trumpSuit = gameMode.GetTrumpSuit();
        var leadSuit = trick.LeadSuit!.Value;

        var winningCard = trick.PlayedCards[0];

        foreach (var playedCard in trick.PlayedCards.Skip(1))
        {
            if (IsBetter(playedCard, winningCard, leadSuit, trumpSuit, gameMode))
            {
                winningCard = playedCard;
            }
        }

        return winningCard.Player;
    }

    private static bool IsBetter(Play.PlayedCard challenger, Play.PlayedCard current, Cards.CardSuit leadSuit, Cards.CardSuit? trumpSuit, GameModes.GameMode gameMode)
    {
        var challengerSuit = challenger.Card.Suit;
        var currentSuit = current.Card.Suit;

        // Trump beats non-trump
        if (trumpSuit.HasValue)
        {
            if (challengerSuit == trumpSuit && currentSuit != trumpSuit)
                return true;
            if (currentSuit == trumpSuit && challengerSuit != trumpSuit)
                return false;
        }

        // If different suits (and neither is trump, or no trump mode), lead suit wins
        if (challengerSuit != currentSuit)
        {
            if (currentSuit == leadSuit && challengerSuit != leadSuit)
                return false;
            if (challengerSuit == leadSuit && currentSuit != leadSuit)
                return true;
            return false;
        }

        // Same suit: compare strength
        return challenger.Card.GetStrength(gameMode) > current.Card.GetStrength(gameMode);
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

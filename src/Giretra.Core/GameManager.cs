using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Negotiation;
using Giretra.Core.Play;
using Giretra.Core.Players;
using Giretra.Core.Scoring;
using Giretra.Core.State;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Giretra.Core;

/// <summary>
/// Orchestrates a full Belote match from start to finish,
/// coordinating four players through all game phases.
/// </summary>
public sealed class GameManager
{
    private readonly IReadOnlyDictionary<PlayerPosition, IPlayerAgent> _players;
    private readonly Func<Deck> _deckProvider;
    private readonly ILogger<GameManager> _logger;

    private MatchState _matchState;
    private Deck? _nextDeck;
    private CancellationToken _cancellationToken;

    /// <summary>
    /// Gets the current match state.
    /// </summary>
    public MatchState MatchState => _matchState;

    /// <summary>
    /// Gets the deck as it sits on the table: rebuilt from the tricks of the last completed
    /// deal, dealer team's tricks on top. Null until a deal has completed. After the match
    /// ends, pass this as the next match's deck provider to keep the same physical deck in
    /// play from one game to the next.
    /// </summary>
    public Deck? FinalDeck => _nextDeck;

    /// <summary>
    /// Creates a new game manager with the specified players.
    /// </summary>
    /// <param name="players">Dictionary mapping positions to player implementations.</param>
    /// <param name="firstDealer">The position of the first dealer.</param>
    /// <param name="deckProvider">Optional function to provide the first deal's deck. Defaults to a shuffled deck.
    /// Subsequent deals reuse the deck collected from the previous hand's tricks (never reshuffled).</param>
    /// <param name="targetScore">The target score to win the match (default 150).</param>
    /// <param name="colourSweepMatchPoints">
    /// When set, Colour sweeps award this many base match points (× multiplier)
    /// instead of triggering an instant match win. Null = normal rules.
    /// </param>
    /// <param name="logger">Optional logger for game flow diagnostics.</param>
    public GameManager(
        IReadOnlyDictionary<PlayerPosition, IPlayerAgent> players,
        PlayerPosition firstDealer,
        Func<Deck>? deckProvider = null,
        int targetScore = 150,
        int? colourSweepMatchPoints = null,
        ILogger<GameManager>? logger = null)
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
        _deckProvider = deckProvider ?? Deck.CreateShuffled;
        _matchState = MatchState.Create(firstDealer, targetScore, colourSweepMatchPoints);
        _logger = logger ?? NullLogger<GameManager>.Instance;
    }

    /// <summary>
    /// Creates a new game manager with the specified players.
    /// </summary>
    /// <param name="bottom">Player at the Bottom position.</param>
    /// <param name="left">Player at the Left position.</param>
    /// <param name="top">Player at the Top position.</param>
    /// <param name="right">Player at the Right position.</param>
    /// <param name="firstDealer">The position of the first dealer.</param>
    /// <param name="deckProvider">Optional function to provide the first deal's deck. Defaults to a shuffled deck.
    /// Subsequent deals reuse the deck collected from the previous hand's tricks (never reshuffled).</param>
    /// <param name="targetScore">The target score to win the match (default 150).</param>
    /// <param name="colourSweepMatchPoints">
    /// When set, Colour sweeps award this many base match points (× multiplier)
    /// instead of triggering an instant match win. Null = normal rules.
    /// </param>
    /// <param name="logger">Optional logger for game flow diagnostics.</param>
    public GameManager(
        IPlayerAgent bottom,
        IPlayerAgent left,
        IPlayerAgent top,
        IPlayerAgent right,
        PlayerPosition firstDealer,
        Func<Deck>? deckProvider = null,
        int targetScore = 150,
        int? colourSweepMatchPoints = null,
        ILogger<GameManager>? logger = null)
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
            targetScore,
            colourSweepMatchPoints,
            logger)
    {
    }

    /// <summary>
    /// Plays a complete match until a winner is determined.
    /// </summary>
    /// <param name="cancellationToken">Optional token to cancel the match between deals.</param>
    /// <returns>The final match state with the winner.</returns>
    public async Task<MatchState> PlayMatchAsync(CancellationToken cancellationToken = default)
    {
        _cancellationToken = cancellationToken;

        _logger.LogDebug("Match started with target score {TargetScore}, first dealer: {Dealer}",
            _matchState.TargetScore, _matchState.CurrentDealer);

        while (!_matchState.IsComplete)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await PlayDealAsync();
        }

        _logger.LogDebug("Match completed. Winner: {Winner}. Final score: Team1={Team1Score}, Team2={Team2Score}",
            _matchState.Winner, _matchState.Team1MatchPoints, _matchState.Team2MatchPoints);

        // Notify all players that the match has ended
        await NotifyAllPlayersAsync(p => p.OnMatchEndedAsync(_matchState));

        // Wait for players to confirm before returning from match
        _logger.LogDebug("Waiting for player confirmation after match ends");
        await ConfirmAllPlayersAsync(p => p.ConfirmContinueMatchAsync(_matchState));

        return _matchState;
    }

    /// <summary>
    /// Plays a single deal.
    /// </summary>
    private async Task PlayDealAsync()
    {
        var dealNumber = _matchState.CompletedDeals.Count + 1;
        var dealer = _matchState.CurrentDealer;
        _logger.LogDebug("Deal {DealNumber} started. Dealer: {Dealer}. Score: Team1={Team1Score}, Team2={Team2Score}",
            dealNumber, dealer, _matchState.Team1MatchPoints, _matchState.Team2MatchPoints);

        // Start the deal. The deck is never shuffled during a match: only the first
        // deal comes from the provider, subsequent deals reuse the deck collected
        // from the previous hand's tricks.
        var deck = _nextDeck ?? _deckProvider();
        _matchState = _matchState.StartDeal(deck);

        // Notify players that deal has started
        await NotifyAllPlayersAsync(p => p.OnDealStartedAsync(_matchState));

        // Cut phase - player to dealer's right cuts
        await PerformCutAsync();

        // Initial distribution (5 cards each)
        var deal = _matchState.CurrentDeal!.PerformInitialDistribution();
        _matchState = _matchState.WithDeal(deal);
        _logger.LogDebug("Initial distribution complete (5 cards each)");

        // Negotiation phase
        await PerformNegotiationAsync();

        // Notify all players that negotiation completed
        await NotifyAllPlayersAsync(p => p.OnNegotiationCompletedAsync(
            _matchState.CurrentDeal!.Negotiation!, _matchState));

        // Final distribution (3 more cards each)
        deal = _matchState.CurrentDeal!.PerformFinalDistribution();
        _matchState = _matchState.WithDeal(deal);
        _logger.LogDebug("Final distribution complete (8 cards each). Game mode: {GameMode}, Announcer: {AnnouncerTeam}, Multiplier: {Multiplier}",
            deal.ResolvedMode, deal.AnnouncerTeam, deal.Multiplier);

        // Playing phase (8 tricks)
        var finalHandState = await PerformPlayingAsync();

        // Rebuild the deck for the next deal from the tricks just played,
        // dealer team's tricks on top of the other team's.
        _nextDeck = finalHandState.CollectDeck(dealer.GetTeam());

        // Get the result and notify players
        var result = _matchState.CompletedDeals[^1];
        _logger.LogDebug("Deal {DealNumber} completed. Card points: Team1={Team1CardPoints}, Team2={Team2CardPoints}. Match points: Team1={Team1MatchPoints}, Team2={Team2MatchPoints}{SweepInfo}",
            dealNumber, result.Team1CardPoints, result.Team2CardPoints, result.Team1MatchPoints, result.Team2MatchPoints,
            result.WasSweep ? $" (Sweep by {result.SweepingTeam})" : "");
        await NotifyAllPlayersAsync(p => p.OnDealEndedAsync(result, finalHandState, _matchState));

        // Wait for players to confirm before starting next deal (only if match is not complete)
        if (!_matchState.IsComplete)
        {
            _logger.LogDebug("Waiting for player confirmation before starting next deal");
            await ConfirmAllPlayersAsync(p => p.ConfirmContinueDealAsync(_matchState));
        }
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

        _logger.LogDebug("Deck cut by {Cutter}: {Position} cards from {Direction}",
            cutter, position, fromTop ? "top" : "bottom");

        deal = deal.CutDeck(position, fromTop);
        _matchState = _matchState.WithDeal(deal);
    }

    /// <summary>
    /// Performs the negotiation phase.
    /// </summary>
    private async Task PerformNegotiationAsync()
    {
        _logger.LogDebug("Negotiation phase started");
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

            _logger.LogDebug("Negotiation: {Player} - {Action}", currentPosition, FormatNegotiationAction(action));

            deal = deal.ApplyNegotiationAction(action);
            _matchState = _matchState.WithDeal(deal);
        }

        _logger.LogDebug("Negotiation complete. Resolved mode: {Mode}, Announcer: {AnnouncerTeam}, Multiplier: {Multiplier}",
            deal.ResolvedMode, deal.AnnouncerTeam, deal.Multiplier);
    }

    private static string FormatNegotiationAction(NegotiationAction action)
    {
        return action switch
        {
            AnnouncementAction a => $"Announce {a.Mode}",
            AcceptAction => "Accept",
            DoubleAction d => $"Double ({d.TargetMode})",
            RedoubleAction r => $"Redouble ({r.TargetMode})",
            _ => action.GetType().Name
        };
    }

    /// <summary>
    /// Performs the playing phase (8 tricks).
    /// </summary>
    /// <returns>The final hand state with all completed tricks.</returns>
    private async Task<HandState> PerformPlayingAsync()
    {
        _logger.LogDebug("Playing phase started. Game mode: {GameMode}", _matchState.CurrentDeal!.ResolvedMode);
        var deal = _matchState.CurrentDeal!;

        while (deal.Phase == DealPhase.Playing)
        {
            _cancellationToken.ThrowIfCancellationRequested();
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
            var trickNumber = deal.Hand.CurrentTrick.TrickNumber;
            var isLead = deal.Hand.CurrentTrick.PlayedCards.Count == 0;

            deal = deal.PlayCard(currentPosition, card);
            _matchState = _matchState.WithDeal(deal);

            _logger.LogDebug("Trick {TrickNumber}: {Player} plays {Card}{LeadMarker}",
                trickNumber, currentPosition, card, isLead ? " (lead)" : "");

            // Notify all players that a card was played
            await NotifyAllPlayersAsync(p => p.OnCardPlayedAsync(currentPosition, card, deal.Hand!, _matchState));

            // Check if a trick was just completed
            if (deal.Hand != null && deal.Hand.CompletedTricks.Count > trickCountBefore)
            {
                var completedTrick = deal.Hand.CompletedTricks[^1];
                var winner = deal.Hand.TrickWinners[^1];

                _logger.LogDebug("Trick {TrickNumber} won by {Winner} ({Team}). Points: Team1={Team1Points}, Team2={Team2Points}",
                    trickNumber, winner, winner.GetTeam(), deal.Hand.Team1CardPoints, deal.Hand.Team2CardPoints);

                await NotifyAllPlayersAsync(p => p.OnTrickCompletedAsync(completedTrick, winner, deal.Hand, _matchState));
            }
        }

        _logger.LogDebug("Playing phase complete. Final tricks: Team1={Team1Tricks}, Team2={Team2Tricks}",
            deal.Hand!.Team1TricksWon, deal.Hand.Team2TricksWon);

        return deal.Hand;
    }

    /// <summary>
    /// Runs an action on all players concurrently via Task.WhenAll.
    /// Used for confirmations (ContinueDeal/ContinueMatch) where all players
    /// need to respond independently — sequential calls would deadlock with
    /// multiple human players sharing a single PendingAction slot.
    /// </summary>
    private async Task ConfirmAllPlayersAsync(Func<IPlayerAgent, Task> action)
    {
        var tasks = _players.Values.Select(async player =>
        {
            try
            {
                await action(player);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Confirmation failed for player at {Position}, continuing with remaining players",
                    player.Position);
            }
        });

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Notifies all players with the given action.
    /// Sequential execution is required — AI agents track cards in order.
    /// Individual failures are caught so one player's error doesn't skip the rest.
    /// </summary>
    private async Task NotifyAllPlayersAsync(Func<IPlayerAgent, Task> action)
    {
        foreach (var player in _players.Values)
        {
            try
            {
                await action(player);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Notification failed for player at {Position}, continuing with remaining players",
                    player.Position);
            }
        }
    }
}

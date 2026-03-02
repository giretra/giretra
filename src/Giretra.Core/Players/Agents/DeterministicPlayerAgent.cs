using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Negotiation;
using Giretra.Core.Play;
using Giretra.Core.Scoring;
using Giretra.Core.State;

namespace Giretra.Core.Players.Agents;

/// <summary>
/// A strong, fully deterministic AI player (no Random) that uses card counting with void inference,
/// positional play awareness (1st/2nd/3rd/4th seat), and match-score-aware negotiation with
/// guaranteed-trick counting.
/// </summary>
public class DeterministicPlayerAgent : IPlayerAgent
{
    // Card tracking (reset each deal)
    private readonly HashSet<Card> _playedCards = [];
    private readonly HashSet<Card> _remainingCards = [];
    private readonly Dictionary<PlayerPosition, HashSet<CardSuit>> _knownVoids = new()
    {
        [PlayerPosition.Bottom] = [],
        [PlayerPosition.Left] = [],
        [PlayerPosition.Top] = [],
        [PlayerPosition.Right] = [],
    };

    // Opponent void tracking (populated from completed tricks)
    private readonly Dictionary<PlayerPosition, HashSet<CardSuit>> _opponentVoidSuits = new()
    {
        [PlayerPosition.Bottom] = [],
        [PlayerPosition.Left] = [],
        [PlayerPosition.Top] = [],
        [PlayerPosition.Right] = [],
    };
    private readonly HashSet<PlayerPosition> _opponentNoTrump = [];

    // Partner observations
    private readonly HashSet<CardSuit> _partnerPreferredSuits = [];
    private readonly HashSet<CardSuit> _partnerDislikedSuits = [];
    private readonly HashSet<CardSuit> _partnerPrioritySuits = [];

    // Current trick state for partner observation
    private PlayerPosition? _currentTrickLeader;
    private CardSuit? _currentTrickLeadSuit;
    private PlayerPosition? _currentTrickWinner;

    // Deal context
    private GameMode? _currentGameMode;
    private readonly Team _myTeam;

    public PlayerPosition Position { get; }

    public DeterministicPlayerAgent(PlayerPosition position)
    {
        Position = position;
        _myTeam = position.GetTeam();
    }

    #region IPlayerAgent Implementation

    public Task<(int position, bool fromTop)> ChooseCutAsync(int deckSize, MatchState matchState)
    {
        return Task.FromResult((position: 16, fromTop: true));
    }

    public Task<NegotiationAction> ChooseNegotiationActionAsync(
        IReadOnlyList<Card> hand,
        NegotiationState negotiationState,
        MatchState matchState,
        IReadOnlyList<NegotiationAction> validActions)
    {
        return Task.FromResult(ChooseNegotiationAction(hand, negotiationState, matchState, validActions));
    }

    public Task<Card> ChooseCardAsync(
        IReadOnlyList<Card> hand,
        HandState handState,
        MatchState matchState,
        IReadOnlyList<Card> validPlays)
    {
        if (validPlays.Count == 1)
            return Task.FromResult(validPlays[0]);

        var trick = handState.CurrentTrick!;

        if (trick.PlayedCards.Count == 0)
            return Task.FromResult(ChooseLeadCard(hand, validPlays, handState, matchState));

        return Task.FromResult(ChooseFollowCard(hand, validPlays, handState, matchState));
    }

    public Task OnDealStartedAsync(MatchState matchState)
    {
        ResetDealState();
        return Task.CompletedTask;
    }

    public Task OnDealEndedAsync(DealResult result, HandState handState, MatchState matchState)
        => Task.CompletedTask;

    public Task OnCardPlayedAsync(PlayerPosition player, Card card, HandState handState, MatchState matchState)
    {
        _playedCards.Add(card);
        _remainingCards.Remove(card);

        var trick = handState.CurrentTrick;
        if (trick == null) return Task.CompletedTask;

        _currentGameMode = handState.GameMode;

        if (trick.PlayedCards.Count == 1)
        {
            _currentTrickLeader = player;
            _currentTrickLeadSuit = card.Suit;
            _currentTrickWinner = player;
        }
        else
        {
            var (winner, _) = PlayerAgentHelper.DetermineCurrentWinner(trick, handState.GameMode);
            _currentTrickWinner = winner;

            InferVoidsFromPlay(player, card, trick, handState.GameMode);

            if (player == Position.Teammate() && player != _currentTrickLeader)
                ObservePartnerPlay(card, handState.GameMode);
        }

        return Task.CompletedTask;
    }

    public Task OnTrickCompletedAsync(TrickState completedTrick, PlayerPosition winner, HandState handState, MatchState matchState)
    {
        InferOpponentVoids(completedTrick, handState.GameMode);
        AnalyzePartnerBehavior(completedTrick, handState);
        InferPrioritySuitsFromOpponentVoids(handState.GameMode);

        _currentTrickLeader = null;
        _currentTrickLeadSuit = null;
        _currentTrickWinner = null;
        return Task.CompletedTask;
    }

    public Task OnMatchEndedAsync(MatchState matchState) => Task.CompletedTask;
    public Task ConfirmContinueDealAsync(MatchState matchState) => Task.CompletedTask;
    public Task ConfirmContinueMatchAsync(MatchState matchState) => Task.CompletedTask;

    #endregion

    #region State Management

    private void ResetDealState()
    {
        _playedCards.Clear();
        _remainingCards.Clear();
        foreach (var card in PlayerAgentHelper.AllCards)
            _remainingCards.Add(card);

        foreach (var key in _knownVoids.Keys)
            _knownVoids[key].Clear();

        foreach (var key in _opponentVoidSuits.Keys)
            _opponentVoidSuits[key].Clear();
        _opponentNoTrump.Clear();

        _partnerPreferredSuits.Clear();
        _partnerDislikedSuits.Clear();
        _partnerPrioritySuits.Clear();
        _currentTrickLeader = null;
        _currentTrickLeadSuit = null;
        _currentTrickWinner = null;
        _currentGameMode = null;
    }

    /// <summary>
    /// Removes our hand from _remainingCards so it only tracks opponent unknowns.
    /// Called before any decision-making.
    /// </summary>
    private void SyncRemainingCards(IReadOnlyList<Card> hand)
    {
        foreach (var card in hand)
            _remainingCards.Remove(card);
    }

    #endregion

    #region Observation & Inference

    /// <summary>
    /// Infers void information when a player doesn't follow suit during a trick.
    /// </summary>
    private void InferVoidsFromPlay(PlayerPosition player, Card card, TrickState trick, GameMode mode)
    {
        if (player == _currentTrickLeader || !_currentTrickLeadSuit.HasValue)
            return;

        var leadSuit = _currentTrickLeadSuit.Value;
        if (card.Suit == leadSuit)
            return;

        // Player is void in lead suit
        _knownVoids[player].Add(leadSuit);

        // In Colour mode, infer trump void
        var trumpSuit = mode.GetTrumpSuit();
        if (!trumpSuit.HasValue || card.Suit == trumpSuit.Value)
            return;

        // Player didn't play trump either — check teammate exception
        if (IsTeammateWinningWithNonTrump(player, trick, mode, trumpSuit.Value))
            return;

        _knownVoids[player].Add(trumpSuit.Value);
    }

    /// <summary>
    /// Returns true when the player's teammate is currently winning with a non-trump card,
    /// meaning the player is not obligated to trump.
    /// </summary>
    private bool IsTeammateWinningWithNonTrump(PlayerPosition player, TrickState trick, GameMode mode, CardSuit trumpSuit)
    {
        if (trick.PlayedCards.Count < 2)
            return false;

        var currentWinnerTeam = _currentTrickWinner?.GetTeam();
        if (currentWinnerTeam != player.GetTeam())
            return false;

        var winningCard = PlayerAgentHelper.GetCurrentWinningCard(trick, mode);
        return winningCard.HasValue && winningCard.Value.Suit != trumpSuit;
    }

    /// <summary>
    /// Replays a completed trick to detect opponent voids from the full trick perspective.
    /// </summary>
    private void InferOpponentVoids(TrickState trick, GameMode mode)
    {
        if (trick.PlayedCards.Count < 2) return;

        var leadSuit = trick.LeadSuit!.Value;
        var trumpSuit = mode.GetTrumpSuit();

        for (int i = 1; i < trick.PlayedCards.Count; i++)
        {
            var played = trick.PlayedCards[i];
            var player = played.Player;

            if (player.GetTeam() == _myTeam) continue;
            if (played.Card.Suit == leadSuit) continue;

            _opponentVoidSuits[player].Add(leadSuit);

            if (!trumpSuit.HasValue || played.Card.Suit == trumpSuit.Value)
                continue;

            // Check teammate exception at the moment this opponent played
            var currentWinner = trick.PlayedCards[0];
            for (int j = 1; j < i; j++)
            {
                if (CardComparer.Beats(trick.PlayedCards[j].Card, currentWinner.Card, leadSuit, mode))
                    currentWinner = trick.PlayedCards[j];
            }

            if (currentWinner.Player.GetTeam() == player.GetTeam()
                && currentWinner.Card.Suit != trumpSuit.Value)
                continue;

            _opponentNoTrump.Add(player);
        }
    }

    /// <summary>
    /// Observes partner's non-leading play to infer suit preferences.
    /// High-value discards when team is losing signal a disliked suit.
    /// </summary>
    private void ObservePartnerPlay(Card card, GameMode mode)
    {
        int cardPoints = card.GetPointValue(mode);
        if (cardPoints < 8) return;

        bool teamWinning = _currentTrickWinner?.GetTeam() == _myTeam;
        if (teamWinning) return;

        _partnerDislikedSuits.Add(card.Suit);
        _partnerPreferredSuits.Remove(card.Suit);
    }

    /// <summary>
    /// Analyzes completed trick patterns to infer partner's suit priorities.
    /// </summary>
    private void AnalyzePartnerBehavior(TrickState completedTrick, HandState handState)
    {
        var mode = handState.GameMode;

        // When we led and won, check if partner signaled a priority suit
        if (completedTrick.Leader == Position
            && PlayerAgentHelper.DetermineCurrentWinner(completedTrick, mode).winner == Position)
        {
            var partnerCard = completedTrick.PlayedCards.First(f => f.Player == Position.Teammate());
            if (partnerCard.Card.Suit != completedTrick.LeadSuit
                && PlayerAgentHelper.IsMasterCard(partnerCard.Card, mode, [], _playedCards))
            {
                _partnerPrioritySuits.Add(partnerCard.Card.Suit);
            }
        }

        // Track suits where teammate led but lost (they still prefer those suits)
        foreach (var suit in handState.CompletedTricks
                     .Where(r => r.Leader == Position.Teammate()
                                 && PlayerAgentHelper.GetCurrentWinningCard(r, mode)
                                 != r.PlayedCards.First(f => f.Player == Position.Teammate()).Card)
                     .Select(s => s.LeadSuit)
                     .Where(s => s != null)
                     .Select(s => s!.Value)
                     .Distinct())
        {
            _partnerPreferredSuits.Add(suit);
        }

        // Analyze tricks where we led with master and teammate couldn't follow
        AnalyzeTeammateCannotFollow(handState, mode);
    }

    /// <summary>
    /// When we led and won with master, observe what teammate played (ascending = priority, descending = disliked).
    /// </summary>
    private void AnalyzeTeammateCannotFollow(HandState handState, GameMode mode)
    {
        var cannotFollowTricks = handState.CompletedTricks
            .Where(r => r.Leader == Position
                        && PlayerAgentHelper.GetCurrentWinningCard(r, mode) == r.LeadCard
                        && r.LeadSuit != r.PlayedCards.First(f => f.Player == Position.Teammate()).Card.Suit)
            .ToList();

        if (cannotFollowTricks.Count == 0) return;

        var cannotFollowCards = cannotFollowTricks
            .Select(t => t.PlayedCards.First(f => f.Player == Position.Teammate()).Card)
            .ToList();

        foreach (var group in cannotFollowCards.GroupBy(g => g.Suit))
        {
            var played = group.ToList();

            if (played.Count > 1)
            {
                bool ascending = played[0].GetStrength(mode) < played[1].GetStrength(mode);
                if (ascending)
                {
                    _partnerPrioritySuits.Add(group.Key);
                    _partnerPreferredSuits.Add(group.Key);
                    _partnerDislikedSuits.Remove(group.Key);
                }
                else
                {
                    _partnerDislikedSuits.Add(group.Key);
                    _partnerPrioritySuits.Remove(group.Key);
                }
            }
            else if (played[0].GetStrength(mode) >= 10)
            {
                _partnerDislikedSuits.Add(group.Key);
            }
        }
    }

    /// <summary>
    /// If both opponents are void in a non-trump suit, promote it as partner priority.
    /// </summary>
    private void InferPrioritySuitsFromOpponentVoids(GameMode mode)
    {
        foreach (var suit in Enum.GetValues<CardSuit>())
        {
            if (suit == mode.GetTrumpSuit()) continue;

            if (IsPlayerVoidIn(Position.Next(), suit) &&
                IsPlayerVoidIn(Position.Teammate().Next(), suit))
            {
                _partnerPrioritySuits.Add(suit);
            }
        }
    }

    #endregion

    #region Query Helpers

    private int CountRemainingTrumps(GameMode mode)
    {
        var trumpSuit = mode.GetTrumpSuit();
        if (!trumpSuit.HasValue) return 0;
        return _remainingCards.Count(c => c.Suit == trumpSuit.Value);
    }

    private bool IsPlayerVoidIn(PlayerPosition player, CardSuit suit)
        => _knownVoids[player].Contains(suit);

    private bool IsOpponentOutOfTrump(PlayerPosition opponent)
        => _opponentNoTrump.Contains(opponent);

    private List<Card> GetRemainingInSuit(CardSuit suit)
        => _remainingCards.Where(c => c.Suit == suit).ToList();

    /// <summary>
    /// Finds the strongest winning card that is also a master, falling back to the cheapest winner
    /// if it costs less than 10 points. Returns null if no good winning card exists.
    /// </summary>
    private Card? FindMaximumWinningCardMaster(
        IReadOnlyList<Card> validPlays, Card currentWinner, CardSuit leadSuit, GameMode mode)
    {
        var winningCards = validPlays
            .Where(c => CardComparer.Beats(c, currentWinner, leadSuit, mode))
            .OrderByDescending(c => c.GetStrength(mode))
            .ToList();

        if (winningCards.Count > 0
            && PlayerAgentHelper.IsMasterCard(winningCards[0], mode, validPlays, _playedCards))
        {
            return winningCards[0];
        }

        var cheapWinner = PlayerAgentHelper.FindMinimumWinningCard(validPlays, currentWinner, leadSuit, mode);
        if (cheapWinner != null && cheapWinner.Value.GetPointValue(mode) < 10)
            return cheapWinner;

        return null;
    }

    #endregion

    #region Hand Evaluation

    private record struct HandEvaluation(int GuaranteedTricks, int ProbableTricks, double Score);

    private HandEvaluation EvaluateHand(IReadOnlyList<Card> hand, GameMode mode, bool isStarter)
    {
        return mode.GetCategory() switch
        {
            GameModeCategory.Colour => EvaluateColourHand(hand, mode),
            GameModeCategory.NoTrumps => EvaluateNoTrumpsHand(hand, mode, isStarter),
            GameModeCategory.AllTrumps => EvaluateAllTrumpsHand(hand, mode),
            _ => new HandEvaluation(0, 0, 0)
        };
    }

    private HandEvaluation EvaluateColourHand(IReadOnlyList<Card> hand, GameMode mode)
    {
        var trumpSuit = mode.GetTrumpSuit()!.Value;
        var trumpCards = hand.Where(c => c.Suit == trumpSuit).ToList();
        var sideCards = hand.Where(c => c.Suit != trumpSuit).ToList();

        int guaranteed = 0;
        int probable = 0;

        bool hasJ = trumpCards.Any(c => c.Rank == CardRank.Jack);
        bool has9 = trumpCards.Any(c => c.Rank == CardRank.Nine);
        bool hasA = trumpCards.Any(c => c.Rank == CardRank.Ace);

        if (hasJ) guaranteed++;
        if (has9)
        {
            if (hasJ) guaranteed++;
            else probable++;
        }
        if (hasA)
        {
            if (hasJ && has9) guaranteed++;
            else probable++;
        }

        // Side suits
        var sideSuits = sideCards.GroupBy(c => c.Suit).ToList();
        foreach (var suitGroup in sideSuits)
        {
            var cards = suitGroup.ToList();
            bool hasSideAce = cards.Any(c => c.Rank == CardRank.Ace);
            bool hasSideTen = cards.Any(c => c.Rank == CardRank.Ten);

            if (hasSideAce && hasSideTen)
                guaranteed++;
            else if (hasSideAce)
                probable++;
        }

        // Void side suit with >= 2 trumps = ruffing opportunity
        foreach (var suit in Enum.GetValues<CardSuit>())
        {
            if (suit == trumpSuit) continue;
            if (!hand.Any(c => c.Suit == suit) && trumpCards.Count >= 2)
                probable++;
        }

        // Long trump bonus
        if (trumpCards.Count >= 5) guaranteed++;
        else if (trumpCards.Count >= 4) probable++;

        // Composite score
        int handPoints = hand.Sum(c => c.GetPointValue(mode));
        int totalPoints = mode.GetTotalPoints();
        double rawPointPercentage = (double)handPoints / totalPoints * 100;

        double score = guaranteed * 18
                     + probable * 8
                     + rawPointPercentage * 0.30
                     + trumpCards.Count * 5
                     + sideSuits.Count(g => g.Count() == 0) * 4;

        return new HandEvaluation(guaranteed, probable, Math.Clamp(score, 0, 100));
    }

    private HandEvaluation EvaluateNoTrumpsHand(IReadOnlyList<Card> hand, GameMode mode, bool isStarter)
    {
        int guaranteed = 0;
        int probable = 0;

        foreach (var suitGroup in hand.GroupBy(c => c.Suit))
        {
            var cards = suitGroup.OrderByDescending(c => c.GetStrength(mode)).ToList();
            bool hasAce = cards.Any(c => c.Rank == CardRank.Ace);
            bool hasTen = cards.Any(c => c.Rank == CardRank.Ten);
            bool hasKing = cards.Any(c => c.Rank == CardRank.King);

            if (hasAce)
            {
                guaranteed++;
                if (hasTen)
                {
                    guaranteed++;
                    if (isStarter && (hasKing || suitGroup.Count() >= 4))
                        guaranteed++;
                }
            }

            if (cards.Count >= 3)
                probable++;
        }

        int handPoints = hand.Sum(c => c.GetPointValue(mode));
        int totalPoints = mode.GetTotalPoints();
        double rawPointPercentage = (double)handPoints / totalPoints * 100;

        double score = guaranteed * 18
                     + probable * 8
                     + rawPointPercentage * 0.15
                     + hand.Count(c => c.Rank == CardRank.Ace) * 5;

        return new HandEvaluation(guaranteed, probable, Math.Clamp(score, 0, 100));
    }

    private HandEvaluation EvaluateAllTrumpsHand(IReadOnlyList<Card> hand, GameMode mode)
    {
        int guaranteed = 0;
        int probable = 0;

        foreach (var suitGroup in hand.GroupBy(c => c.Suit))
        {
            var cards = suitGroup.ToList();
            bool hasJack = cards.Any(c => c.Rank == CardRank.Jack);
            bool hasNine = cards.Any(c => c.Rank == CardRank.Nine);
            bool hasAce = cards.Any(c => c.Rank == CardRank.Ace);

            if (hasJack)
            {
                guaranteed++;
                if (hasNine) guaranteed++;
                if (hasAce) probable++;
            }
            else if (hasNine)
            {
                probable++;
            }
        }

        int handPoints = hand.Sum(c => c.GetPointValue(mode));
        int totalPoints = mode.GetTotalPoints();
        double rawPointPercentage = (double)handPoints / totalPoints * 100;

        double score = guaranteed * 18
                     + probable * 8
                     + rawPointPercentage * 0.30
                     + hand.Count(c => c.Rank == CardRank.Jack) * 5
                     + hand.Count(c => c.Rank == CardRank.Nine) * 3;

        return new HandEvaluation(guaranteed, probable, Math.Clamp(score, 0, 100));
    }

    #endregion

    #region Negotiation

    private NegotiationAction ChooseNegotiationAction(
        IReadOnlyList<Card> hand,
        NegotiationState negotiationState,
        MatchState matchState,
        IReadOnlyList<NegotiationAction> validActions)
    {
        var isStarter = negotiationState.Dealer.Next() == Position;

        var modeEvals = new Dictionary<GameMode, HandEvaluation>();
        foreach (var mode in Enum.GetValues<GameMode>())
            modeEvals[mode] = EvaluateHand(hand, mode, isStarter);

        var opponentTeam = _myTeam == Team.Team1 ? Team.Team2 : Team.Team1;
        double aggressiveness = PlayerAgentHelper.ComputeAggressiveness(
            matchState.GetMatchPoints(_myTeam),
            matchState.GetMatchPoints(opponentTeam),
            matchState.TargetScore);

        double announceThreshold = 55 - aggressiveness * 15;
        double competeThreshold = 42 - aggressiveness * 7;
        double doubleThreshold = 65 - aggressiveness * 10;
        double redoubleThreshold = 75 - aggressiveness * 10;
        int doubleGuaranteedMin = aggressiveness > 0.5 ? 2 : 3;
        int redoubleGuaranteedMin = aggressiveness > 0.5 ? 3 : 4;

        // Check for redouble
        var redoubleAction = validActions.OfType<RedoubleAction>().FirstOrDefault();
        if (redoubleAction != null)
        {
            var eval = modeEvals[redoubleAction.TargetMode];
            if (eval.GuaranteedTricks >= redoubleGuaranteedMin && eval.Score >= redoubleThreshold)
                return redoubleAction;
        }

        // Check for double
        var doubleAction = validActions.OfType<DoubleAction>().FirstOrDefault();
        if (doubleAction != null)
        {
            var eval = modeEvals[doubleAction.TargetMode];
            if (eval.GuaranteedTricks >= doubleGuaranteedMin && eval.Score >= doubleThreshold)
                return doubleAction;
        }

        // Find best announcement
        var announceActions = validActions.OfType<AnnouncementAction>().ToList();
        bool isFirstSpeaker = negotiationState.CurrentBid == null;

        if (isFirstSpeaker && announceActions.Count > 0)
        {
            return announceActions
                .OrderByDescending(a => modeEvals[a.Mode].Score)
                .First();
        }

        if (announceActions.Count > 0)
        {
            var strongAnnounce = announceActions
                .Where(a => modeEvals[a.Mode].Score >= announceThreshold)
                .OrderByDescending(a => modeEvals[a.Mode].Score)
                .FirstOrDefault();

            if (strongAnnounce != null)
                return strongAnnounce;

            // Competitive: if opponent bid and we have decent hand
            if (negotiationState.CurrentBid.HasValue &&
                negotiationState.CurrentBidder.HasValue &&
                negotiationState.CurrentBidder.Value.GetTeam() != _myTeam)
            {
                var competeAnnounce = announceActions
                    .Where(a => modeEvals[a.Mode].Score >= competeThreshold)
                    .OrderByDescending(a => modeEvals[a.Mode].Score)
                    .FirstOrDefault();

                if (competeAnnounce != null)
                    return competeAnnounce;
            }
        }

        // Accept logic
        var acceptAction = validActions.OfType<AcceptAction>().FirstOrDefault();
        if (acceptAction != null)
        {
            var currentBid = negotiationState.CurrentBid;
            if (currentBid.HasValue)
            {
                var bidMode = currentBid.Value;

                // Accepting Clubs or NoTrumps by opponent triggers auto-double —
                // treat this like voluntarily doubling: require a strong hand
                if (bidMode.AcceptCausesAutoDouble() &&
                    negotiationState.CurrentBidder.HasValue &&
                    negotiationState.CurrentBidder.Value.GetTeam() != _myTeam &&
                    !negotiationState.DoubledModes.ContainsKey(bidMode))
                {
                    var eval = modeEvals[bidMode];
                    double autoDoubleThreshold = doubleThreshold - 5;

                    if (eval.Score >= autoDoubleThreshold && eval.GuaranteedTricks >= doubleGuaranteedMin - 1)
                        return acceptAction;

                    // Hand not strong enough — try to escape-announce into a better mode
                    var escapeAnnounce = announceActions
                        .Where(a => modeEvals[a.Mode].Score >= competeThreshold - 10)
                        .OrderByDescending(a => modeEvals[a.Mode].Score)
                        .FirstOrDefault();

                    if (escapeAnnounce != null)
                        return escapeAnnounce;
                }
            }

            return acceptAction;
        }

        return validActions[0];
    }

    #endregion

    #region Lead Strategy

    private Card ChooseLeadCard(
        IReadOnlyList<Card> hand,
        IReadOnlyList<Card> validPlays,
        HandState handState,
        MatchState matchState)
    {
        SyncRemainingCards(hand);
        var mode = handState.GameMode;
        var trumpSuit = mode.GetTrumpSuit();
        int trickNumber = handState.CompletedTricks.Count + 1;

        // Endgame: trick 8 — if we need last-trick bonus, play strongest card
        if (trickNumber == 8)
            return ChooseLastTrickLead(validPlays, handState, matchState);

        // 1. Cash master cards (guaranteed winners)
        var masterCards = PlayerAgentHelper.GetMasterCards(hand, mode, _playedCards)
            .Where(c => validPlays.Contains(c)).ToList();

        if (masterCards.Count > 0 && !ShouldHoldBackMasters(masterCards, trickNumber, mode))
        {
            var preferredMasters = masterCards
                .Where(c => !_partnerDislikedSuits.Contains(c.Suit))
                .OrderByDescending(c => _partnerPrioritySuits.Contains(c.Suit) || _partnerPreferredSuits.Contains(c.Suit))
                .ThenByDescending(c => c.GetPointValue(mode))
                .ToList();

            if (preferredMasters.Count > 0)
                return preferredMasters[0];

            return masterCards.OrderByDescending(c => c.GetPointValue(mode)).First();
        }

        // 1b. Partner priority suit (both opponents void)
        if (_partnerPrioritySuits.Count > 0)
        {
            var priorityCards = validPlays
                .Where(c => _partnerPrioritySuits.Contains(c.Suit))
                .OrderByDescending(c => c.GetStrength(mode))
                .ToList();

            if (priorityCards.Count > 0)
                return priorityCards[0];
        }

        // 2. Trump exhaustion (Colour mode)
        if (trumpSuit.HasValue)
        {
            var leadTrump = TryTrumpExhaustionLead(hand, validPlays, mode, trumpSuit.Value);
            if (leadTrump.HasValue)
                return leadTrump.Value;
        }

        // 3. Partner's preferred suit
        if (_partnerPreferredSuits.Count > 0)
        {
            var preferredSuitCards = validPlays
                .Where(c => _partnerPreferredSuits.Contains(c.Suit) || _partnerPrioritySuits.Contains(c.Suit))
                .Where(c => !trumpSuit.HasValue || c.Suit != trumpSuit.Value)
                .ToList();

            if (preferredSuitCards.Count > 0)
                return preferredSuitCards.OrderByDescending(c => c.GetStrength(mode)).First();
        }

        // 4. Exploit opponent voids (NoTrumps / AllTrumps)
        if (!mode.IsColourMode())
        {
            var exploitCard = TryLeadIntoOpponentVoid(validPlays, mode);
            if (exploitCard.HasValue)
                return exploitCard.Value;
        }

        // 5. Long suit — lead from longest non-trump suit
        return ChooseDefaultLead(validPlays, mode, trumpSuit);
    }

    /// <summary>
    /// In early NoTrumps, avoid cashing masters if they're spread across many suits
    /// (better to hold them for later when opponents run out of options).
    /// </summary>
    private bool ShouldHoldBackMasters(List<Card> masterCards, int trickNumber, GameMode mode)
    {
        if (trickNumber > 3 || mode != GameMode.NoTrumps)
            return false;

        int suitCount = masterCards.Select(c => c.Suit).Distinct().Count();
        double masterRatio = masterCards.Count / (8.0 - (trickNumber - 1));

        return suitCount >= 3 && masterRatio < 0.4;
    }

    /// <summary>
    /// Leads trump if we hold more trumps than opponents to drain them.
    /// </summary>
    private Card? TryTrumpExhaustionLead(
        IReadOnlyList<Card> hand, IReadOnlyList<Card> validPlays, GameMode mode, CardSuit trumpSuit)
    {
        var myTrumps = validPlays.Where(c => c.Suit == trumpSuit).ToList();
        if (myTrumps.Count == 0) return null;

        int remainingOpponentTrumps = CountRemainingTrumps(mode);
        if (myTrumps.Count <= remainingOpponentTrumps || remainingOpponentTrumps <= 0)
            return null;

        var strongestTrump = myTrumps.OrderByDescending(c => c.GetStrength(mode)).First();
        if (PlayerAgentHelper.IsMasterCard(strongestTrump, mode, hand, _playedCards))
            return strongestTrump;

        return null;
    }

    /// <summary>
    /// In non-colour modes, lead into suits where opponents are void.
    /// </summary>
    private Card? TryLeadIntoOpponentVoid(IReadOnlyList<Card> validPlays, GameMode mode)
    {
        var opponentVoidSuits = new HashSet<CardSuit>();
        var opponents = new[] { Position.Next(), Position.Previous() };

        foreach (var opponent in opponents)
        foreach (var suit in Enum.GetValues<CardSuit>())
        {
            if (IsPlayerVoidIn(opponent, suit))
                opponentVoidSuits.Add(suit);
        }

        var candidateCards = validPlays.Where(c => opponentVoidSuits.Contains(c.Suit)).ToList();
        if (candidateCards.Count == 0) return null;

        var bestGroup = candidateCards.GroupBy(g => g.Suit)
            .OrderBy(r => r.Count())
            .First();

        // From the shortest suit, lead second-strongest if available (preserve strongest)
        var ordered = bestGroup.OrderByDescending(c => c.GetStrength(mode)).ToList();
        return ordered.Count > 1 ? ordered[1] : ordered[0];
    }

    private Card ChooseDefaultLead(IReadOnlyList<Card> validPlays, GameMode mode, CardSuit? trumpSuit)
    {
        var nonDislikedPlays = validPlays
            .Where(c => !_partnerDislikedSuits.Contains(c.Suit))
            .ToList();

        var playsToConsider = nonDislikedPlays.Count > 0 ? nonDislikedPlays : validPlays.ToList();

        var nonTrumpPlays = trumpSuit.HasValue
            ? playsToConsider.Where(c => c.Suit != trumpSuit.Value).ToList()
            : playsToConsider;

        if (nonTrumpPlays.Count > 0)
        {
            var longestGroup = nonTrumpPlays.GroupBy(c => c.Suit)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Max(c => c.GetStrength(mode)))
                .First();

            var candidates = longestGroup.OrderByDescending(c => c.GetStrength(mode)).ToList();
            return candidates.Count > 1 ? candidates[1] : candidates[0];
        }

        return ChooseLeastValuableCard(validPlays, mode, validPlays);
    }

    private Card ChooseLastTrickLead(
        IReadOnlyList<Card> validPlays,
        HandState handState,
        MatchState matchState)
    {
        var mode = handState.GameMode;
        int myPoints = handState.GetCardPoints(_myTeam);
        int threshold = mode.GetWinThreshold();

        var deal = matchState.CurrentDeal;
        bool weAreAnnouncer = deal?.AnnouncerTeam == _myTeam;

        if (weAreAnnouncer && myPoints >= threshold)
            return ChooseLeastValuableCard(validPlays, mode, validPlays);

        // Need last-trick bonus or just trying to win — play strongest
        return validPlays.OrderByDescending(c => c.GetStrength(mode)).First();
    }

    #endregion

    #region Following Strategy

    private Card ChooseFollowCard(
        IReadOnlyList<Card> hand,
        IReadOnlyList<Card> validPlays,
        HandState handState,
        MatchState matchState)
    {
        SyncRemainingCards(hand);
        var trick = handState.CurrentTrick!;
        var mode = handState.GameMode;
        var (currentWinner, winningCard) = PlayerAgentHelper.DetermineCurrentWinner(trick, mode);
        bool teammateWinning = currentWinner?.GetTeam() == _myTeam;
        int seatPosition = trick.PlayedCards.Count; // 1=2nd, 2=3rd, 3=4th

        var leadSuit = trick.LeadSuit!.Value;
        var trumpSuit = mode.GetTrumpSuit();

        bool followingSuit = validPlays.Any(c => c.Suit == leadSuit);
        bool playingTrump = !followingSuit && trumpSuit.HasValue && validPlays.Any(c => c.Suit == trumpSuit.Value);
        bool discarding = !followingSuit && !playingTrump;

        bool isEndgame = handState.CompletedTricks.Count + 1 >= 7;

        bool opponentHasMaster = trick.PlayedCards
            .Where(s => s.Team != _myTeam)
            .Any(c => PlayerAgentHelper.IsMasterCard(c.Card, mode, [], _playedCards));

        if (discarding)
        {
            if (winningCard.HasValue && PlayerAgentHelper.IsMasterCard(winningCard.Value, mode, [], _playedCards))
            {
                return teammateWinning
                    ? ChooseMostValuableUselessCard(validPlays, mode, hand, leadSuit)
                    : ChooseLeastValuableCard(validPlays, mode, hand);
            }

            return ChooseSmartDiscard(hand, validPlays, mode, opponentHasMaster);
        }

        if (playingTrump)
            return ChooseSmartTrump(validPlays, trick, mode, teammateWinning, winningCard);

        // Following suit — use positional play
        return seatPosition switch
        {
            1 => ChooseSecondSeat(hand, validPlays, trick, mode, winningCard!.Value),
            2 => ChooseThirdSeat(hand, validPlays, trick, mode, teammateWinning, winningCard),
            3 => ChooseFourthSeat(hand, validPlays, trick, mode, teammateWinning, winningCard),
            _ => throw new InvalidOperationException("Unexpected seat position")
        };
    }

    /// <summary>
    /// 2nd seat: win cheap tricks, skip expensive ones for partner (4th seat).
    /// </summary>
    private Card ChooseSecondSeat(
        IReadOnlyList<Card> hand,
        IReadOnlyList<Card> validPlays,
        TrickState trick,
        GameMode mode,
        Card winningCard)
    {
        var leadSuit = trick.LeadSuit!.Value;
        int trickPoints = PlayerAgentHelper.GetTrickPointsSoFar(trick, mode);
        var minWinner = PlayerAgentHelper.FindMinimumWinningCard(validPlays, winningCard, leadSuit, mode);

        var colourTrumpSuit = mode.GetTrumpSuit();

        // In colour mode for a side suit that hasn't seen much play, try to grab with sub-master
        if (colourTrumpSuit != null && colourTrumpSuit.Value != leadSuit && minWinner != null)
        {
            bool leadSuitFresh = _playedCards.Count(c => c.Suit == leadSuit) < 3;
            if (leadSuitFresh)
            {
                var subMaster = validPlays
                    .Where(c => PlayerAgentHelper.IsMasterCardExcludeTrump(c, mode, hand, _playedCards))
                    .OrderByDescending(c => c.GetStrength(mode))
                    .FirstOrDefault();

                if (subMaster != default)
                    return subMaster;
            }
        }

        if (minWinner.HasValue)
        {
            int winnerCost = minWinner.Value.GetPointValue(mode);
            if (trickPoints >= 10 || winnerCost <= 4)
                return minWinner.Value;

            return ChooseLeastValuableCard(validPlays, mode, hand);
        }

        return ChooseLeastValuableCard(validPlays, mode, hand);
    }

    /// <summary>
    /// 3rd seat: complex logic — 4th player may be opponent or teammate.
    /// </summary>
    private Card ChooseThirdSeat(
        IReadOnlyList<Card> hand,
        IReadOnlyList<Card> validPlays,
        TrickState trick,
        GameMode mode,
        bool teammateWinning,
        Card? winningCard)
    {
        var leadSuit = trick.LeadSuit!.Value;
        var fourthPlayer = Position.Next();

        if (teammateWinning && winningCard.HasValue)
        {
            if (PlayerAgentHelper.IsMasterCard(winningCard.Value, mode, hand, _playedCards))
                return ChooseMostValuableUselessCard(validPlays, mode, hand, leadSuit);

            if (IsPlayerVoidIn(fourthPlayer, leadSuit))
            {
                if (!mode.IsColourMode() || IsOpponentOutOfTrump(fourthPlayer))
                    return ChooseMostValuableUselessCard(validPlays, mode, hand, leadSuit);
            }

            return ChooseLeastValuableCard(validPlays, mode);
        }

        // Opponent winning — try to win with master or cheap card
        if (winningCard.HasValue)
        {
            var winner = FindMaximumWinningCardMaster(validPlays, winningCard.Value, leadSuit, mode);
            if (winner.HasValue)
                return winner.Value;

            return ChooseLeastValuableCard(validPlays, mode, hand);
        }

        return ChooseLeastValuableCard(validPlays, mode, hand);
    }

    /// <summary>
    /// 4th seat (last to play): complete information — maximize or minimize.
    /// </summary>
    private Card ChooseFourthSeat(
        IReadOnlyList<Card> hand,
        IReadOnlyList<Card> validPlays,
        TrickState trick,
        GameMode mode,
        bool teammateWinning,
        Card? winningCard)
    {
        if (teammateWinning)
            return ChooseMostValuableUselessCard(validPlays, mode, hand, trick.LeadSuit!.Value);

        if (winningCard.HasValue)
        {
            var minWinner = PlayerAgentHelper.FindMinimumWinningCard(
                validPlays, winningCard.Value, trick.LeadSuit!.Value, mode);
            if (minWinner.HasValue)
                return minWinner.Value;
        }

        return ChooseLeastValuableCard(validPlays, mode, hand);
    }

    #endregion

    #region Trump & Discard Strategy

    private Card ChooseSmartTrump(
        IReadOnlyList<Card> validPlays,
        TrickState trick,
        GameMode mode,
        bool teammateWinning,
        Card? winningCard)
    {
        var trumpSuit = mode.GetTrumpSuit();
        var trumpPlays = trumpSuit.HasValue
            ? validPlays.Where(c => c.Suit == trumpSuit.Value).ToList()
            : validPlays.ToList();
        var nonTrumpPlays = trumpSuit.HasValue
            ? validPlays.Where(c => c.Suit != trumpSuit.Value).ToList()
            : new List<Card>();

        bool opponentHasMaster = trick.PlayedCards
            .Where(s => s.Team != _myTeam)
            .Any(c => PlayerAgentHelper.IsMasterCard(c.Card, mode, [], _playedCards));

        // Teammate winning with non-trump — discard rather than trump
        if (teammateWinning && winningCard.HasValue &&
            trumpSuit.HasValue && winningCard.Value.Suit != trumpSuit.Value)
        {
            if (nonTrumpPlays.Count > 0)
                return ChooseSmartDiscard(validPlays, nonTrumpPlays, mode, opponentHasMaster);
        }

        if (trumpPlays.Count == 0)
            return ChooseSmartDiscard(validPlays, validPlays, mode, opponentHasMaster);

        // Overtrump if needed
        if (winningCard.HasValue && trumpSuit.HasValue && winningCard.Value.Suit == trumpSuit.Value)
        {
            var leadSuit = trick.LeadSuit!.Value;
            var overtrumps = trumpPlays
                .Where(c => CardComparer.Beats(c, winningCard.Value, leadSuit, mode))
                .OrderBy(c => c.GetStrength(mode))
                .ToList();

            if (overtrumps.Count > 0)
                return overtrumps[0]; // Minimum overtrump

            // Can't overtrump — play lowest trump (undertrump)
            return trumpPlays.OrderBy(c => c.GetStrength(mode)).First();
        }

        // No trump in trick yet — play lowest trump to win cheaply
        return trumpPlays.OrderBy(c => c.GetStrength(mode)).First();
    }

    private Card ChooseSmartDiscard(
        IReadOnlyList<Card> hand,
        IReadOnlyList<Card> validPlays,
        GameMode mode, bool lostTrick)
    {
        var trumpSuit = mode.GetTrumpSuit();

        var suitGroups = validPlays
            .Where(c => !trumpSuit.HasValue || c.Suit != trumpSuit.Value)
            .GroupBy(c => c.Suit)
            .ToList();

        if (suitGroups.Count == 0 || lostTrick)
            return ChooseLeastValuableCard(validPlays, mode, hand);

        // Prefer discarding from short side suits to create voids for future ruffing
        var shortSuits = suitGroups
            .OrderBy(g => g.Count())
            .ThenBy(g => _partnerPreferredSuits.Contains(g.Key) || _partnerPrioritySuits.Contains(g.Key))
            .ToList();

        // Among equal-length suits, prefer suits where opponents hold masters
        var bestSuitToDiscard = shortSuits[0];
        foreach (var group in shortSuits)
        {
            if (group.Count() > bestSuitToDiscard.Count()) break;

            bool opponentHasMaster = GetRemainingInSuit(group.Key)
                .Any(c => c.GetStrength(mode) > group.Max(g => g.GetStrength(mode)));

            if (opponentHasMaster && !_partnerPreferredSuits.Contains(group.Key))
            {
                bestSuitToDiscard = group;
                break;
            }
        }

        return bestSuitToDiscard
            .OrderBy(c => c.GetPointValue(mode))
            .ThenBy(c => c.GetStrength(mode))
            .First();
    }

    #endregion

    #region Card Selection Helpers

    /// <summary>
    /// Chooses the best card to load onto a trick the team is winning.
    /// Maximizes points loaded now while minimizing future tactical cost.
    /// </summary>
    private Card ChooseMostValuableUselessCard(
        IReadOnlyList<Card> validPlays, GameMode mode,
        IReadOnlyList<Card> hand, CardSuit leadSuit)
    {
        if (validPlays.Count == 1)
            return validPlays[0];

        bool canFollow = validPlays.Any(c => c.Suit == leadSuit);

        if (canFollow)
        {
            var strongest = validPlays.OrderByDescending(r => r.GetStrength(mode)).First();

            // Don't dump a master trump — keep it for leading
            if (mode.GetTrumpSuit() == strongest.Suit
                && PlayerAgentHelper.IsMasterCard(strongest, mode, hand, _playedCards))
            {
                return validPlays.OrderBy(r => r.GetStrength(mode)).First();
            }

            return strongest;
        }

        var masterCardsInHand = hand
            .Where(c => PlayerAgentHelper.IsMasterCard(c, mode, hand, _playedCards))
            .ToList();
        var masterSuits = masterCardsInHand.Select(t => t.Suit).Distinct().ToHashSet();

        var validBySuit = validPlays.GroupBy(c => c.Suit)
            .ToDictionary(g => g.Key, g => g.ToList());

        // If all valid plays are masters, dump from longest non-trump suit
        if (validPlays.All(v => PlayerAgentHelper.IsMasterCard(v, mode, hand, _playedCards)))
        {
            return validPlays
                .OrderBy(r => r.Suit == mode.GetTrumpSuit())
                .ThenByDescending(r => validBySuit[r.Suit].Count)
                .ThenByDescending(c => c.GetPointValue(mode))
                .ThenByDescending(c => c.GetStrength(mode))
                .First();
        }

        // Prefer dumping high-value non-master, non-trump cards from suits without masters
        var highValueDumps = validPlays
            .Where(c => !masterSuits.Contains(c.Suit))
            .Where(c => c.GetPointValue(mode) >= 10)
            .Where(c => mode.GetTrumpSuit() != c.Suit)
            .Where(c => !PlayerAgentHelper.IsMasterCard(c, mode, hand, _playedCards))
            .OrderBy(r => validBySuit[r.Suit].Count)
            .ThenByDescending(r => r.GetPointValue(mode))
            .ThenByDescending(r => r.GetStrength(mode))
            .ToList();

        if (highValueDumps.Count > 0)
            return highValueDumps[0];

        // Any non-master, non-trump card from suits without masters
        var mediumDumps = validPlays
            .Where(c => !masterSuits.Contains(c.Suit))
            .Where(c => mode.GetTrumpSuit() != c.Suit)
            .OrderBy(c => PlayerAgentHelper.IsMasterCard(c, mode, hand, _playedCards))
            .ThenBy(r => validBySuit[r.Suit].Count)
            .ThenByDescending(r => r.GetPointValue(mode))
            .ToList();

        if (mediumDumps.Count > 0)
            return mediumDumps[0];

        return validPlays
            .OrderBy(c => c.GetStrength(mode))
            .ThenByDescending(r => validBySuit[r.Suit].Count)
            .First();
    }

    private Card ChooseLeastValuableCard(IReadOnlyList<Card> validPlays, GameMode mode,
        IReadOnlyList<Card>? hand = null)
    {
        var validBySuit = validPlays.GroupBy(c => c.Suit)
            .ToDictionary(g => g.Key, g => g.ToList());

        return validPlays
            .OrderBy(r => r.Suit == mode.GetTrumpSuit())
            .ThenBy(c => PlayerAgentHelper.IsMasterCard(c, mode, hand ?? [], _playedCards))
            .ThenBy(c => c.GetPointValue(mode))
            .ThenBy(c => validBySuit[c.Suit].Count)
            .ThenBy(c => c.GetStrength(mode))
            .First();
    }

    #endregion
}

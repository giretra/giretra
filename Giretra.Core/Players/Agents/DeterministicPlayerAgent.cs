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
    // All 32 cards in the deck
    private static readonly Card[] AllCards = BuildAllCards();

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

    // Partner observation
    private readonly HashSet<CardSuit> _partnerPreferredSuits = [];
    private readonly HashSet<CardSuit> _partnerDislikedSuits = [];

    // Current trick state for partner observation
    private PlayerPosition? _currentTrickLeader;
    private CardSuit? _currentTrickLeadSuit;
    private PlayerPosition? _currentTrickWinner;

    // Deal context
    private GameMode? _currentGameMode;
    private Team _myTeam;

    public PlayerPosition Position { get; }

    public DeterministicPlayerAgent(PlayerPosition position)
    {
        Position = position;
        _myTeam = position.GetTeam();
    }

    private static Card[] BuildAllCards()
    {
        var cards = new Card[32];
        int i = 0;
        foreach (var suit in Enum.GetValues<CardSuit>())
        {
            foreach (var rank in Enum.GetValues<CardRank>())
            {
                cards[i++] = new Card(rank, suit);
            }
        }
        return cards;
    }

    #region IPlayerAgent Implementation

    public Task<(int position, bool fromTop)> ChooseCutAsync(int deckSize, MatchState matchState)
    {
        // Deterministic cut: always cut at 16 from top
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
        var mode = handState.GameMode;

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
            // First card of trick
            _currentTrickLeader = player;
            _currentTrickLeadSuit = card.Suit;
            _currentTrickWinner = player;

            if (player == Position.Teammate())
                _partnerPreferredSuits.Add(card.Suit);
        }
        else
        {
            // Update current winner
            var (winner, _) = DetermineCurrentWinner(trick, handState.GameMode);
            _currentTrickWinner = winner;

            // Void inference
            if (player != _currentTrickLeader && _currentTrickLeadSuit.HasValue)
            {
                var leadSuit = _currentTrickLeadSuit.Value;
                if (card.Suit != leadSuit)
                {
                    // Player is void in lead suit
                    _knownVoids[player].Add(leadSuit);

                    // In Colour mode, infer trump void
                    var trumpSuit = handState.GameMode.GetTrumpSuit();
                    if (trumpSuit.HasValue && card.Suit != trumpSuit.Value)
                    {
                        // Player didn't play trump either - check if teammate exception applies
                        bool teammateException = false;
                        if (trick.PlayedCards.Count >= 2)
                        {
                            var currentWinnerTeam = _currentTrickWinner?.GetTeam();
                            var playerTeam = player.GetTeam();
                            if (currentWinnerTeam == playerTeam)
                            {
                                // Teammate is winning - check if winning with non-trump
                                var winningCard = GetCurrentWinningCard(trick, handState.GameMode);
                                if (winningCard.HasValue && winningCard.Value.Suit != trumpSuit.Value)
                                    teammateException = true;
                            }
                        }

                        if (!teammateException)
                            _knownVoids[player].Add(trumpSuit.Value);
                    }
                }
            }

            // Partner signals
            if (player == Position.Teammate() && player != _currentTrickLeader)
                ObservePartnerPlay(player, card, handState.GameMode);
        }

        // Remove our own hand cards from _remainingCards so it only reflects unknown cards
        // (We do this every time since hand changes aren't directly tracked)
        if (handState.CurrentTrick?.CurrentPlayer == Position || player == Position)
        {
            // Will be cleaned up on next play; the key is _remainingCards never contains our hand
        }

        return Task.CompletedTask;
    }

    public Task OnTrickCompletedAsync(TrickState completedTrick, PlayerPosition winner, HandState handState, MatchState matchState)
    {
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
        foreach (var card in AllCards)
            _remainingCards.Add(card);

        foreach (var key in _knownVoids.Keys)
            _knownVoids[key].Clear();

        _partnerPreferredSuits.Clear();
        _partnerDislikedSuits.Clear();
        _currentTrickLeader = null;
        _currentTrickLeadSuit = null;
        _currentTrickWinner = null;
        _currentGameMode = null;
    }

    private void ObservePartnerPlay(PlayerPosition partner, Card card, GameMode mode)
    {
        int cardPoints = card.GetPointValue(mode);
        bool isHighValue = cardPoints >= 8;

        if (!isHighValue) return;

        bool teamWinning = _currentTrickWinner?.GetTeam() == _myTeam;

        if (!teamWinning)
        {
            _partnerDislikedSuits.Add(card.Suit);
            _partnerPreferredSuits.Remove(card.Suit);
        }
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

    #region Query Helpers

    private int CountRemainingTrumps(GameMode mode)
    {
        var trumpSuit = mode.GetTrumpSuit();
        if (!trumpSuit.HasValue) return 0;
        return _remainingCards.Count(c => c.Suit == trumpSuit.Value);
    }

    private bool IsPlayerVoidIn(PlayerPosition player, CardSuit suit)
    {
        return _knownVoids[player].Contains(suit);
    }

    private List<Card> GetRemainingInSuit(CardSuit suit)
    {
        return _remainingCards.Where(c => c.Suit == suit).ToList();
    }

    private (PlayerPosition? winner, Card? card) DetermineCurrentWinner(TrickState trick, GameMode mode)
    {
        if (trick.PlayedCards.Count == 0)
            return (null, null);

        var leadSuit = trick.LeadSuit!.Value;
        PlayedCard best = trick.PlayedCards[0];
        foreach (var played in trick.PlayedCards.Skip(1))
        {
            if (CardComparer.Beats(played.Card, best.Card, leadSuit, mode))
                best = played;
        }
        return (best.Player, best.Card);
    }

    private Card? GetCurrentWinningCard(TrickState trick, GameMode mode)
    {
        var (_, card) = DetermineCurrentWinner(trick, mode);
        return card;
    }

    private Card? FindMinimumWinningCard(IReadOnlyList<Card> validPlays, Card currentWinner, CardSuit leadSuit, GameMode mode)
    {
        var winningCards = validPlays
            .Where(c => CardComparer.Beats(c, currentWinner, leadSuit, mode))
            .OrderBy(c => c.GetStrength(mode))
            .ToList();

        return winningCards.Count > 0 ? winningCards[0] : null;
    }

    private int GetTrickPointsSoFar(TrickState trick, GameMode mode)
    {
        return trick.PlayedCards.Sum(pc => pc.Card.GetPointValue(mode));
    }

    #endregion

    #region Hand Evaluation

    private record struct HandEvaluation(int GuaranteedTricks, int ProbableTricks, double Score);

    private HandEvaluation EvaluateHand(IReadOnlyList<Card> hand, GameMode mode)
    {
        var category = mode.GetCategory();
        return category switch
        {
            GameModeCategory.Colour => EvaluateColourHand(hand, mode),
            GameModeCategory.SansAs => EvaluateSansAsHand(hand, mode),
            GameModeCategory.ToutAs => EvaluateToutAsHand(hand, mode),
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

        // Trump J is always guaranteed
        if (hasJ) guaranteed++;

        // Trump 9: guaranteed if have J, else probable
        if (has9)
        {
            if (hasJ) guaranteed++;
            else probable++;
        }

        // Trump A: guaranteed if have J+9, else probable
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
                guaranteed++; // A+10 combo
            else if (hasSideAce)
                probable++; // Ace alone can be trumped
        }

        // Void side suit with ≥2 trumps = ruffing opportunity
        var allSuits = Enum.GetValues<CardSuit>();
        foreach (var suit in allSuits)
        {
            if (suit == trumpSuit) continue;
            bool isVoid = !hand.Any(c => c.Suit == suit);
            if (isVoid && trumpCards.Count >= 2)
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
                     + sideSuits.Count(g => g.Count() == 0) * 4; // void bonuses already counted, this adds for existing voids in hand context

        return new HandEvaluation(guaranteed, probable, Math.Min(100, Math.Max(0, score)));
    }

    private HandEvaluation EvaluateSansAsHand(IReadOnlyList<Card> hand, GameMode mode)
    {
        int guaranteed = 0;
        int probable = 0;

        var suitGroups = hand.GroupBy(c => c.Suit).ToList();
        foreach (var suitGroup in suitGroups)
        {
            var cards = suitGroup.OrderByDescending(c => c.GetStrength(mode)).ToList();
            bool hasAce = cards.Any(c => c.Rank == CardRank.Ace);
            bool hasTen = cards.Any(c => c.Rank == CardRank.Ten);
            bool hasKing = cards.Any(c => c.Rank == CardRank.King);

            if (hasAce)
            {
                guaranteed++; // Ace is master in SansAs
                if (hasTen)
                {
                    guaranteed++; // A+10 guaranteed
                    if (hasKing)
                        guaranteed++; // A+10+K
                }
            }

            // Long suit bonus
            if (cards.Count >= 3)
                probable++;
        }

        int handPoints = hand.Sum(c => c.GetPointValue(mode));
        int totalPoints = mode.GetTotalPoints();
        double rawPointPercentage = (double)handPoints / totalPoints * 100;

        double score = guaranteed * 18
                     + probable * 8
                     + rawPointPercentage * 0.30
                     + hand.Count(c => c.Rank == CardRank.Ace) * 5;

        return new HandEvaluation(guaranteed, probable, Math.Min(100, Math.Max(0, score)));
    }

    private HandEvaluation EvaluateToutAsHand(IReadOnlyList<Card> hand, GameMode mode)
    {
        int guaranteed = 0;
        int probable = 0;

        var suitGroups = hand.GroupBy(c => c.Suit).ToList();
        foreach (var suitGroup in suitGroups)
        {
            var cards = suitGroup.ToList();
            bool hasJack = cards.Any(c => c.Rank == CardRank.Jack);
            bool hasNine = cards.Any(c => c.Rank == CardRank.Nine);
            bool hasAce = cards.Any(c => c.Rank == CardRank.Ace);

            if (hasJack)
            {
                guaranteed++; // Jack is master in ToutAs
                if (hasNine)
                    guaranteed++; // J+9 guaranteed
                if (hasAce)
                    probable++; // A with J
            }
            else
            {
                if (hasNine)
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

        return new HandEvaluation(guaranteed, probable, Math.Min(100, Math.Max(0, score)));
    }

    #endregion

    #region Negotiation

    private NegotiationAction ChooseNegotiationAction(
        IReadOnlyList<Card> hand,
        NegotiationState negotiationState,
        MatchState matchState,
        IReadOnlyList<NegotiationAction> validActions)
    {
        // Evaluate hand for all modes
        var modeEvals = new Dictionary<GameMode, HandEvaluation>();
        foreach (var mode in Enum.GetValues<GameMode>())
            modeEvals[mode] = EvaluateHand(hand, mode);

        // Match context aggressiveness
        double aggressiveness = ComputeAggressiveness(matchState);

        // Adjust thresholds
        double announceThreshold = 55 - aggressiveness * 15; // 55 normal, ~40 desperate
        double competeThreshold = 42 - aggressiveness * 7;   // 42 normal, ~35 desperate
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
            // Must announce - pick the best mode, or least bad
            var best = announceActions
                .OrderByDescending(a => modeEvals[a.Mode].Score)
                .First();
            return best;
        }

        // Can announce higher with strong hand
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

                // Accepting Clubs or SansAs by opponent triggers auto-double —
                // treat this like voluntarily doubling: require a strong hand
                if (bidMode.AcceptCausesAutoDouble() &&
                    negotiationState.CurrentBidder.HasValue &&
                    negotiationState.CurrentBidder.Value.GetTeam() != _myTeam &&
                    !negotiationState.DoubledModes.ContainsKey(bidMode))
                {
                    var eval = modeEvals[bidMode];
                    double autoDoubleThreshold = doubleThreshold - 5; // ~60, nearly as strict as a deliberate double

                    if (eval.Score >= autoDoubleThreshold && eval.GuaranteedTricks >= doubleGuaranteedMin - 1)
                    {
                        // Strong enough to welcome the auto-double
                        return acceptAction;
                    }

                    // Hand not strong enough — try to escape-announce into a better mode
                    var escapeAnnounce = announceActions
                        .Where(a => modeEvals[a.Mode].Score >= competeThreshold - 10)
                        .OrderByDescending(a => modeEvals[a.Mode].Score)
                        .FirstOrDefault();

                    if (escapeAnnounce != null)
                        return escapeAnnounce;

                    // No escape possible — accept as last resort
                }
            }

            return acceptAction;
        }

        // Fallback
        return validActions[0];
    }

    /// <summary>
    /// Returns 0.0 (conservative) to 1.0 (desperate/aggressive) based on match score.
    /// </summary>
    private double ComputeAggressiveness(MatchState matchState)
    {
        int ourPoints = matchState.GetMatchPoints(_myTeam);
        var opponentTeam = _myTeam == Team.Team1 ? Team.Team2 : Team.Team1;
        int theirPoints = matchState.GetMatchPoints(opponentTeam);
        int target = matchState.TargetScore;

        double ourProgress = (double)ourPoints / target;
        double theirProgress = (double)theirPoints / target;

        // If opponent is close to winning, be more aggressive
        if (theirProgress >= 0.8 && ourProgress < 0.5)
            return 0.9;
        if (theirProgress >= 0.6 && ourProgress < 0.3)
            return 0.7;

        // If we're close to winning, be conservative
        if (ourProgress >= 0.8)
            return 0.0;
        if (ourProgress >= 0.6)
            return 0.1;

        // Neutral
        return 0.3;
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

        // Endgame: trick 8 - if we need last-trick bonus, play strongest card
        if (trickNumber == 8)
        {
            return ChooseLastTrickLead(hand, validPlays, handState, matchState);
        }

        // 1. Cash master cards (guaranteed winners)
        var masterCards = PlayerAgentHelper.GetMasterCards(hand, mode, _playedCards)
            .Where(c => validPlays.Contains(c)).ToList();
        if (masterCards.Count > 0)
        {
            // Prefer high-point masters in partner's preferred suit
            var preferredMasters = masterCards
                .Where(c => !_partnerDislikedSuits.Contains(c.Suit))
                .OrderByDescending(c => _partnerPreferredSuits.Contains(c.Suit))
                .ThenByDescending(c => c.GetPointValue(mode))
                .ToList();

            if (preferredMasters.Count > 0)
                return preferredMasters[0];

            return masterCards.OrderByDescending(c => c.GetPointValue(mode)).First();
        }

        // 2. Trump exhaustion (Colour mode)
        if (trumpSuit.HasValue)
        {
            var myTrumps = validPlays.Where(c => c.Suit == trumpSuit.Value).ToList();
            int remainingOpponentTrumps = CountRemainingTrumps(mode);

            if (myTrumps.Count > 0)
            {
                // If we hold more trumps than opponents, lead trump to drain them
                if (myTrumps.Count > remainingOpponentTrumps && remainingOpponentTrumps > 0)
                {
                    return myTrumps.OrderByDescending(c => c.GetStrength(mode)).First();
                }

                // Lead trump if we have J or 9 to force out opponents' big trumps
                bool hasJorNine = myTrumps.Any(c => c.Rank == CardRank.Jack || c.Rank == CardRank.Nine);
                if (hasJorNine && remainingOpponentTrumps > 0)
                {
                    return myTrumps.OrderByDescending(c => c.GetStrength(mode)).First();
                }
            }
        }

        // 3. Partner's preferred suit
        if (_partnerPreferredSuits.Count > 0)
        {
            var preferredSuitCards = validPlays
                .Where(c => _partnerPreferredSuits.Contains(c.Suit))
                .Where(c => !trumpSuit.HasValue || c.Suit != trumpSuit.Value)
                .ToList();

            if (preferredSuitCards.Count > 0)
                return preferredSuitCards.OrderByDescending(c => c.GetStrength(mode)).First();
        }

        // 4. Long suit - lead from longest non-trump suit
        var nonDislikedPlays = validPlays
            .Where(c => !_partnerDislikedSuits.Contains(c.Suit))
            .ToList();
        var playsToConsider = nonDislikedPlays.Count > 0 ? nonDislikedPlays : validPlays.ToList();

        var nonTrumpPlays = trumpSuit.HasValue
            ? playsToConsider.Where(c => c.Suit != trumpSuit.Value).ToList()
            : playsToConsider;

        if (nonTrumpPlays.Count > 0)
        {
            var suitGroups = nonTrumpPlays.GroupBy(c => c.Suit)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Max(c => c.GetStrength(mode)))
                .ToList();

            if (suitGroups.Count > 0)
            {
                var longestGroup = suitGroups[0];
                return longestGroup.OrderByDescending(c => c.GetStrength(mode)).First();
            }
        }

        // 5. Default - lowest value, lowest strength
        return ChooseLeastValuableCard(validPlays, mode);
    }

    private Card ChooseLastTrickLead(
        IReadOnlyList<Card> hand,
        IReadOnlyList<Card> validPlays,
        HandState handState,
        MatchState matchState)
    {
        var mode = handState.GameMode;
        int myPoints = handState.GetCardPoints(_myTeam);
        int threshold = mode.GetWinThreshold();

        // Check if we're the announcing team
        var deal = matchState.CurrentDeal;
        bool weAreAnnouncer = deal?.AnnouncerTeam == _myTeam;

        // If we already have enough points to win, play safe
        if (weAreAnnouncer && myPoints >= threshold)
            return ChooseLeastValuableCard(validPlays, mode);

        // If we need the last trick bonus, play strongest card to win
        if (weAreAnnouncer && myPoints + 10 >= threshold && myPoints < threshold)
            return validPlays.OrderByDescending(c => c.GetStrength(mode)).First();

        // Default: play strongest to try to win last trick
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
        var (currentWinner, winningCard) = DetermineCurrentWinner(trick, mode);
        bool teammateWinning = currentWinner?.GetTeam() == _myTeam;
        int seatPosition = trick.PlayedCards.Count; // 1=2nd, 2=3rd, 3=4th (0-indexed in cards but count is post-play)

        var leadSuit = trick.LeadSuit!.Value;
        var trumpSuit = mode.GetTrumpSuit();

        // Determine if we're following suit or not
        bool followingSuit = validPlays.Any(c => c.Suit == leadSuit);
        bool playingTrump = !followingSuit && trumpSuit.HasValue && validPlays.Any(c => c.Suit == trumpSuit.Value);
        bool discarding = !followingSuit && !playingTrump;

        // Endgame awareness
        int trickNumber = handState.CompletedTricks.Count + 1;
        bool isEndgame = trickNumber >= 7;

        if (discarding)
            return ChooseSmartDiscard(hand, validPlays, mode);

        if (playingTrump)
            return ChooseSmartTrump(validPlays, trick, mode, teammateWinning, winningCard);

        // Following suit - use positional play
        return seatPosition switch
        {
            1 => ChooseSecondSeat(hand, validPlays, trick, mode, teammateWinning, winningCard, isEndgame),
            2 => ChooseThirdSeat(hand, validPlays, trick, mode, teammateWinning, winningCard, isEndgame),
            3 => ChooseFourthSeat(validPlays, trick, mode, teammateWinning, winningCard, isEndgame),
            _ => ChooseFourthSeat(validPlays, trick, mode, teammateWinning, winningCard, isEndgame)
        };
    }

    /// <summary>
    /// 2nd seat strategy.
    /// </summary>
    private Card ChooseSecondSeat(
        IReadOnlyList<Card> hand,
        IReadOnlyList<Card> validPlays,
        TrickState trick,
        GameMode mode,
        bool teammateWinning,
        Card? winningCard,
        bool isEndgame)
    {
        var leadSuit = trick.LeadSuit!.Value;
        var leader = trick.Leader;
        bool leaderIsOpponent = leader.GetTeam() != _myTeam;

        if (leaderIsOpponent && winningCard.HasValue)
        {
            int trickPoints = GetTrickPointsSoFar(trick, mode);
            var minWinner = FindMinimumWinningCard(validPlays, winningCard.Value, leadSuit, mode);

            if (minWinner.HasValue)
            {
                int winnerCost = minWinner.Value.GetPointValue(mode);
                // Win if trick has good value or winning is cheap
                if (trickPoints >= 10 || winnerCost <= 4)
                    return minWinner.Value;

                // Otherwise let teammate (4th seat) handle it - play cheap
                return ChooseLeastValuableCard(validPlays, mode);
            }

            // Can't win, dump cheapest
            return ChooseLeastValuableCard(validPlays, mode);
        }

        // Teammate led - play cheap to save strength
        if (teammateWinning)
        {
            // Exception: load points if we have high-value card teammate can protect
            var highValueCards = validPlays
                .Where(c => c.GetPointValue(mode) >= 10)
                .OrderByDescending(c => c.GetPointValue(mode))
                .ToList();

            if (highValueCards.Count > 0 && winningCard.HasValue && PlayerAgentHelper.IsMasterCard(winningCard.Value, mode, hand, _playedCards))
                return highValueCards[0]; // Load points on teammate's master

            return ChooseLeastValuableCard(validPlays, mode);
        }

        return ChooseLeastValuableCard(validPlays, mode);
    }

    /// <summary>
    /// 3rd seat strategy.
    /// </summary>
    private Card ChooseThirdSeat(
        IReadOnlyList<Card> hand,
        IReadOnlyList<Card> validPlays,
        TrickState trick,
        GameMode mode,
        bool teammateWinning,
        Card? winningCard,
        bool isEndgame)
    {
        var leadSuit = trick.LeadSuit!.Value;
        // 4th player is the one after us
        var fourthPlayer = Position.Next();
        bool fourthIsTeammate = fourthPlayer.GetTeam() == _myTeam;

        if (teammateWinning && winningCard.HasValue)
        {
            if (fourthIsTeammate)
            {
                // Both remaining are teammates - load max points
                return ChooseMostValuableCard(validPlays, mode);
            }

            // 4th is opponent - load points only if teammate's card is master
            if (PlayerAgentHelper.IsMasterCard(winningCard.Value, mode, hand, _playedCards))
                return ChooseMostValuableCard(validPlays, mode);

            // Teammate winning but opponent can still overtake - hedge with medium
            return ChooseMediumCard(validPlays, mode);
        }

        // Opponent winning
        if (winningCard.HasValue)
        {
            if (fourthIsTeammate)
            {
                // Try to beat cheaply, if too expensive let teammate handle it
                var minWinner = FindMinimumWinningCard(validPlays, winningCard.Value, leadSuit, mode);
                if (minWinner.HasValue)
                {
                    int cost = minWinner.Value.GetPointValue(mode);
                    if (cost <= 10)
                        return minWinner.Value;

                    // Expensive to win, trust teammate
                    return ChooseLeastValuableCard(validPlays, mode);
                }

                return ChooseLeastValuableCard(validPlays, mode);
            }

            // 4th is opponent - must try to win
            var winner = FindMinimumWinningCard(validPlays, winningCard.Value, leadSuit, mode);
            if (winner.HasValue)
                return winner.Value;

            return ChooseLeastValuableCard(validPlays, mode);
        }

        return ChooseLeastValuableCard(validPlays, mode);
    }

    /// <summary>
    /// 4th seat strategy (last to play - complete information).
    /// </summary>
    private Card ChooseFourthSeat(
        IReadOnlyList<Card> validPlays,
        TrickState trick,
        GameMode mode,
        bool teammateWinning,
        Card? winningCard,
        bool isEndgame)
    {
        if (teammateWinning)
        {
            // Load maximum points
            return ChooseMostValuableCard(validPlays, mode);
        }

        // Opponent winning - win with minimum card
        if (winningCard.HasValue)
        {
            var leadSuit = trick.LeadSuit!.Value;
            var minWinner = FindMinimumWinningCard(validPlays, winningCard.Value, leadSuit, mode);
            if (minWinner.HasValue)
                return minWinner.Value;
        }

        // Can't win, dump cheapest
        return ChooseLeastValuableCard(validPlays, mode);
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

        // Teammate winning with non-trump - discard rather than trump
        if (teammateWinning && winningCard.HasValue &&
            trumpSuit.HasValue && winningCard.Value.Suit != trumpSuit.Value)
        {
            // Validator may allow discarding; if we have non-trump options, prefer them
            if (nonTrumpPlays.Count > 0)
                return ChooseSmartDiscard(validPlays, nonTrumpPlays, mode);
            // Otherwise must play trump (validator forced)
        }

        if (trumpPlays.Count == 0)
            return ChooseSmartDiscard(validPlays, validPlays, mode);

        // Check if we need to overtrump
        if (winningCard.HasValue && trumpSuit.HasValue && winningCard.Value.Suit == trumpSuit.Value)
        {
            // Must overtrump if possible
            var leadSuit = trick.LeadSuit!.Value;
            var overtrumps = trumpPlays
                .Where(c => CardComparer.Beats(c, winningCard.Value, leadSuit, mode))
                .OrderBy(c => c.GetStrength(mode))
                .ToList();

            if (overtrumps.Count > 0)
                return overtrumps[0]; // Minimum overtrump

            // Can't overtrump - play lowest trump (undertrump)
            return trumpPlays.OrderBy(c => c.GetStrength(mode)).First();
        }

        // No trump in trick yet - play lowest trump to win cheaply
        return trumpPlays.OrderBy(c => c.GetStrength(mode)).First();
    }

    private Card ChooseSmartDiscard(
        IReadOnlyList<Card> hand,
        IReadOnlyList<Card> validPlays,
        GameMode mode)
    {
        var trumpSuit = mode.GetTrumpSuit();

        // Group valid discards by suit
        var suitGroups = validPlays
            .Where(c => !trumpSuit.HasValue || c.Suit != trumpSuit.Value)
            .GroupBy(c => c.Suit)
            .ToList();

        if (suitGroups.Count == 0)
            return ChooseLeastValuableCard(validPlays, mode);

        // Prefer discarding from short side suits to create voids for future ruffing
        var shortSuits = suitGroups
            .OrderBy(g => g.Count())
            .ThenBy(g => _partnerPreferredSuits.Contains(g.Key) ? 1 : 0) // Avoid partner's preferred
            .ToList();

        // Among equal-length suits, prefer suits where opponents hold masters
        var bestSuitToDiscard = shortSuits[0];
        foreach (var group in shortSuits)
        {
            if (group.Count() > bestSuitToDiscard.Count()) break;

            // Check if opponents hold master in this suit
            bool opponentHasMaster = GetRemainingInSuit(group.Key)
                .Any(c => c.GetStrength(mode) > group.Max(g => g.GetStrength(mode)));

            if (opponentHasMaster && !_partnerPreferredSuits.Contains(group.Key))
            {
                bestSuitToDiscard = group;
                break;
            }
        }

        // Pick lowest-value card from chosen suit
        return bestSuitToDiscard
            .OrderBy(c => c.GetPointValue(mode))
            .ThenBy(c => c.GetStrength(mode))
            .First();
    }

    #endregion

    #region Card Selection Helpers

    private Card ChooseMostValuableCard(IReadOnlyList<Card> validPlays, GameMode mode)
    {
        return validPlays
            .OrderByDescending(c => c.GetPointValue(mode))
            .ThenByDescending(c => c.GetStrength(mode))
            .First();
    }

    private Card ChooseLeastValuableCard(IReadOnlyList<Card> validPlays, GameMode mode)
    {
        return validPlays
            .OrderBy(c => c.GetPointValue(mode))
            .ThenBy(c => c.GetStrength(mode))
            .First();
    }

    private Card ChooseMediumCard(IReadOnlyList<Card> validPlays, GameMode mode)
    {
        // Pick a card in the middle of the value range
        var sorted = validPlays.OrderBy(c => c.GetPointValue(mode))
                               .ThenBy(c => c.GetStrength(mode))
                               .ToList();
        return sorted[sorted.Count / 2];
    }

    #endregion
}

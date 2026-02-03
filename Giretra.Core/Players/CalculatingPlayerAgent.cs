using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Negotiation;
using Giretra.Core.Play;
using Giretra.Core.Scoring;
using Giretra.Core.State;

namespace Giretra.Core.Players;

/// <summary>
/// A calculating player that tracks played cards to identify master cards
/// and makes strategic decisions based on card values and trick state.
/// </summary>
/// <remarks>
/// <para><b>Master Card Concept:</b></para>
/// <para>
/// A "master card" is a card guaranteed to win if played, meaning all higher-ranked
/// cards of that suit have already been played. The agent tracks played cards via
/// OnCardPlayedAsync (without reading trick history) to dynamically determine masters.
/// </para>
///
/// <para><b>Announcement Behavior:</b></para>
/// <list type="bullet">
///   <item>Evaluates hand strength as a percentage (0-100%) for each game mode</item>
///   <item>Considers: raw points, high cards, trump count, and mode-specific bonuses</item>
///   <item>Announces at 60%+ strength eagerly, 45%+ if competitive bidding</item>
///   <item>Only doubles/redoubles when holding 3+ potential master cards (risk threshold)</item>
///   <item>Only accepts Clubs/SansAs (which auto-double) with 40%+ hand strength;
///         otherwise tries to announce a higher mode to escape the doubled game</item>
/// </list>
///
/// <para><b>Play Behavior:</b></para>
/// <list type="bullet">
///   <item><b>Leading:</b> Plays master cards first (highest value). Without masters,
///         leads from long suits or medium trumps to draw out opponent masters.</item>
///   <item><b>Teammate winning:</b> Plays the most valuable card to maximize points.</item>
///   <item><b>Opponent winning:</b> Attempts to win with minimum necessary card.
///         If unable to win, discards the least valuable card.</item>
/// </list>
///
/// <para><b>Partner Behavior Observation:</b></para>
/// <para>
/// The agent observes partner's plays (when not leading) to infer suit preferences:
/// </para>
/// <list type="bullet">
///   <item><b>Disliked suits:</b> When partner drops a high-value card (8+ points) on a trick
///         the team isn't winning, they probably want to get rid of that suit.</item>
///   <item><b>Preferred suits:</b> When partner leads a suit, they likely want to continue
///         playing that suit (tracked as recently led).</item>
/// </list>
/// <para>
/// These observations influence lead selection: prefer partner's preferred suits,
/// avoid partner's disliked suits when possible.
/// </para>
/// </remarks>
public class CalculatingPlayerAgent : IPlayerAgent
{
    private readonly HashSet<Card> _playedCards = [];
    private readonly Random _random = new();

    // Partner behavior tracking
    private readonly HashSet<CardSuit> _partnerDislikedSuits = [];
    private readonly HashSet<CardSuit> _partnerPreferredSuits = [];

    // Current trick state for partner observation (reset each trick)
    private PlayerPosition? _currentTrickLeader;
    private CardSuit? _currentTrickLeadSuit;
    private PlayerPosition? _currentTrickWinner;
    private GameMode? _currentGameMode;

    public PlayerPosition Position { get; }

    public CalculatingPlayerAgent(PlayerPosition position)
    {
        Position = position;
    }

    public Task<(int position, bool fromTop)> ChooseCutAsync(int deckSize, MatchState matchState)
    {
        // Simple random cut between 6 and 26
        var position = _random.Next(6, 27);
        var fromTop = _random.Next(2) == 0;
        return Task.FromResult((position, fromTop));
    }

    public Task<NegotiationAction> ChooseNegotiationActionAsync(
        IReadOnlyList<Card> hand,
        NegotiationState negotiationState,
        MatchState matchState,
        IReadOnlyList<NegotiationAction> validActions)
    {
        // Calculate hand strength for each game mode
        var modeScores = new Dictionary<GameMode, double>();
        foreach (GameMode mode in Enum.GetValues<GameMode>())
        {
            modeScores[mode] = CalculateHandStrengthPercentage(hand, mode);
        }

        // Find the best mode for our hand
        var bestMode = modeScores.MaxBy(kv => kv.Value).Key;
        var bestScore = modeScores[bestMode];

        // Count potential master cards for risk assessment
        int masterCardCount = CountPotentialMasterCards(hand, bestMode);

        // Check for double/redouble opportunities
        var doubleAction = validActions.OfType<DoubleAction>().FirstOrDefault();
        var redoubleAction = validActions.OfType<RedoubleAction>().FirstOrDefault();

        // Only take risks if we have 3+ master cards
        if (masterCardCount >= 3)
        {
            if (redoubleAction != null)
            {
                return Task.FromResult<NegotiationAction>(redoubleAction);
            }
            if (doubleAction != null)
            {
                return Task.FromResult<NegotiationAction>(doubleAction);
            }
        }

        // Try to announce if we have a strong hand (>= 60% strength)
        if (bestScore >= 60)
        {
            var announceAction = validActions
                .OfType<AnnouncementAction>()
                .Where(a => a.Mode == bestMode ||
                            (negotiationState.CurrentBid == null && modeScores[a.Mode] >= 50))
                .OrderByDescending(a => modeScores[a.Mode])
                .FirstOrDefault();

            if (announceAction != null)
            {
                return Task.FromResult<NegotiationAction>(announceAction);
            }
        }

        // If we have a decent hand (>= 45%) and can announce something higher
        if (bestScore >= 45)
        {
            var announceAction = validActions
                .OfType<AnnouncementAction>()
                .Where(a => modeScores[a.Mode] >= 45)
                .OrderByDescending(a => modeScores[a.Mode])
                .FirstOrDefault();

            if (announceAction != null)
            {
                return Task.FromResult<NegotiationAction>(announceAction);
            }
        }

        // Default: accept (but be selective about Clubs/SansAs which auto-double)
        var acceptAction = validActions.OfType<AcceptAction>().FirstOrDefault();
        if (acceptAction != null)
        {
            var currentBid = negotiationState.CurrentBid;
            if (currentBid.HasValue)
            {
                var bidMode = currentBid.Value;

                // Accepting Clubs or SansAs triggers auto-double, so be careful
                if (bidMode == GameMode.ColourClubs || bidMode == GameMode.SansAs)
                {
                    // Only accept if we have decent cards for that mode (>= 40%)
                    double ourStrength = modeScores[bidMode];
                    if (ourStrength < 40)
                    {
                        // Try to announce something higher instead of accepting a bad doubled game
                        var escapeAnnounce = validActions
                            .OfType<AnnouncementAction>()
                            .Where(a => modeScores[a.Mode] >= 35) // Lower threshold to escape
                            .OrderByDescending(a => modeScores[a.Mode])
                            .FirstOrDefault();

                        if (escapeAnnounce != null)
                        {
                            return Task.FromResult<NegotiationAction>(escapeAnnounce);
                        }
                    }
                }
            }

            return Task.FromResult<NegotiationAction>(acceptAction);
        }

        // Fallback to first valid action
        return Task.FromResult(validActions[0]);
    }

    public Task<Card> ChooseCardAsync(
        IReadOnlyList<Card> hand,
        HandState handState,
        MatchState matchState,
        IReadOnlyList<Card> validPlays)
    {
        if (validPlays.Count == 1)
        {
            return Task.FromResult(validPlays[0]);
        }

        var trick = handState.CurrentTrick!;
        var gameMode = handState.GameMode;
        var myTeam = Position.GetTeam();

        // Determine if we're leading
        if (trick.PlayedCards.Count == 0)
        {
            return Task.FromResult(ChooseLeadCard(validPlays, gameMode));
        }

        // Analyze current trick state
        var (currentWinner, winningCard) = DetermineCurrentWinner(trick, gameMode);
        bool teammateWinning = currentWinner?.GetTeam() == myTeam;

        // If teammate is winning, play high value card
        if (teammateWinning)
        {
            return Task.FromResult(ChooseMostValuableCard(validPlays, gameMode));
        }

        // At this point, an opponent is winning
        var leadSuit = trick.LeadSuit!.Value;
        var currentWinningCard = winningCard!.Value;

        // If we're last to play or opponent is winning
        if (trick.PlayedCards.Count == 3 || !teammateWinning)
        {
            // Try to win with minimum necessary card, otherwise dump lowest
            var winningPlay = FindMinimumWinningCard(validPlays, currentWinningCard, leadSuit, gameMode);
            if (winningPlay != null)
            {
                return Task.FromResult(winningPlay.Value);
            }

            // Can't win, play least valuable
            return Task.FromResult(ChooseLeastValuableCard(validPlays, gameMode));
        }

        // We're second or third, opponent ahead - try to win or dump
        var canWin = FindMinimumWinningCard(validPlays, currentWinningCard, leadSuit, gameMode);
        if (canWin != null)
        {
            return Task.FromResult(canWin.Value);
        }

        return Task.FromResult(ChooseLeastValuableCard(validPlays, gameMode));
    }

    public Task OnDealStartedAsync(MatchState matchState)
    {
        // Reset tracking for new deal
        _playedCards.Clear();
        _partnerDislikedSuits.Clear();
        _partnerPreferredSuits.Clear();
        _currentTrickLeader = null;
        _currentTrickLeadSuit = null;
        _currentTrickWinner = null;
        _currentGameMode = null;
        return Task.CompletedTask;
    }

    public Task OnDealEndedAsync(DealResult result, MatchState matchState)
    {
        return Task.CompletedTask;
    }

    public Task OnCardPlayedAsync(PlayerPosition player, Card card, HandState handState, MatchState matchState)
    {
        // Track all played cards to determine master cards
        _playedCards.Add(card);

        var trick = handState.CurrentTrick;
        if (trick == null) return Task.CompletedTask;

        _currentGameMode = handState.GameMode;

        // Track trick leader and lead suit
        if (trick.PlayedCards.Count == 1)
        {
            _currentTrickLeader = player;
            _currentTrickLeadSuit = card.Suit;
            _currentTrickWinner = player;

            // If partner leads a suit, they prefer it
            if (player == Position.Teammate())
            {
                _partnerPreferredSuits.Add(card.Suit);
            }
        }
        else
        {
            // Update current winner
            var (winner, _) = DetermineCurrentWinner(trick, handState.GameMode);
            _currentTrickWinner = winner;

            // Observe partner's non-leading plays
            if (player == Position.Teammate() && player != _currentTrickLeader)
            {
                ObservePartnerPlay(player, card, handState.GameMode);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Observes partner's play when they are not leading to infer suit preferences.
    /// </summary>
    private void ObservePartnerPlay(PlayerPosition partner, Card card, GameMode mode)
    {
        // Check if partner dropped a high-value card
        int cardPoints = card.GetPointValue(mode);
        bool isHighValue = cardPoints >= 8; // 10, A, J(trump), 9(trump)

        if (!isHighValue) return;

        // Check if our team is NOT winning the current trick
        bool teamWinning = _currentTrickWinner?.GetTeam() == Position.GetTeam();

        if (!teamWinning)
        {
            // Partner dropped a high-value card when we're losing
            // They probably don't like this suit (trying to get rid of it)
            _partnerDislikedSuits.Add(card.Suit);

            // Remove from preferred if it was there
            _partnerPreferredSuits.Remove(card.Suit);
        }
    }

    public Task OnTrickCompletedAsync(TrickState completedTrick, PlayerPosition winner, HandState handState, MatchState matchState)
    {
        // Reset current trick tracking
        _currentTrickLeader = null;
        _currentTrickLeadSuit = null;
        _currentTrickWinner = null;
        return Task.CompletedTask;
    }

    public Task OnMatchEndedAsync(MatchState matchState)
    {
        return Task.CompletedTask;
    }

    #region Strategic Calculations

    /// <summary>
    /// Calculates hand strength as a percentage (0-100) for a given game mode.
    /// Considers card points, high cards, and suit distribution.
    /// </summary>
    private double CalculateHandStrengthPercentage(IReadOnlyList<Card> hand, GameMode mode)
    {
        var trumpSuit = mode.GetTrumpSuit();
        var category = mode.GetCategory();

        // Calculate raw point percentage
        int handPoints = hand.Sum(c => c.GetPointValue(mode));
        int maxPossiblePoints = mode.GetTotalPoints();
        double pointPercentage = (double)handPoints / maxPossiblePoints * 100;

        // Calculate strength bonus for high cards
        double strengthBonus = 0;
        foreach (var card in hand)
        {
            int strength = card.GetStrength(mode);
            if (strength >= 7) strengthBonus += 5;  // A or J (depending on mode)
            else if (strength >= 6) strengthBonus += 3;
        }

        // Trump suit bonus in Colour mode
        double trumpBonus = 0;
        if (trumpSuit.HasValue)
        {
            int trumpCount = hand.Count(c => c.Suit == trumpSuit.Value);
            trumpBonus = trumpCount * 4; // Each trump adds 4%

            // Extra bonus for trump Jack and 9
            if (hand.Any(c => c.Suit == trumpSuit.Value && c.Rank == CardRank.Jack))
                trumpBonus += 8;
            if (hand.Any(c => c.Suit == trumpSuit.Value && c.Rank == CardRank.Nine))
                trumpBonus += 5;
        }

        // ToutAs bonus: Jacks and 9s are very valuable
        double toutAsBonus = 0;
        if (category == GameModeCategory.ToutAs)
        {
            toutAsBonus += hand.Count(c => c.Rank == CardRank.Jack) * 6;
            toutAsBonus += hand.Count(c => c.Rank == CardRank.Nine) * 4;
        }

        // SansAs bonus: Aces and sequences are valuable
        double sansAsBonus = 0;
        if (category == GameModeCategory.SansAs)
        {
            sansAsBonus += hand.Count(c => c.Rank == CardRank.Ace) * 5;
            sansAsBonus += hand.Count(c => c.Rank == CardRank.Ten) * 2;
        }

        // Combine factors (weighted)
        double score = pointPercentage * 0.4 + strengthBonus + trumpBonus + toutAsBonus + sansAsBonus;

        return Math.Min(100, Math.Max(0, score));
    }

    /// <summary>
    /// Counts cards that are likely master cards (highest remaining in their suit).
    /// During negotiation, assumes all 32 cards are in play.
    /// </summary>
    private int CountPotentialMasterCards(IReadOnlyList<Card> hand, GameMode mode)
    {
        int masterCount = 0;
        var trumpSuit = mode.GetTrumpSuit();
        var category = mode.GetCategory();

        foreach (var suit in Enum.GetValues<CardSuit>())
        {
            var cardsInSuit = hand.Where(c => c.Suit == suit).ToList();
            if (cardsInSuit.Count == 0) continue;

            // Find the strongest card we have in this suit
            var strongestOwned = cardsInSuit.MaxBy(c => c.GetStrength(mode));
            int ourStrength = strongestOwned.GetStrength(mode);

            // Check if any card in the full deck would beat it
            bool isMaster = true;
            foreach (CardRank rank in Enum.GetValues<CardRank>())
            {
                var potentialCard = new Card(rank, suit);
                if (hand.Contains(potentialCard)) continue; // We have it

                int potentialStrength = potentialCard.GetStrength(mode);
                if (potentialStrength > ourStrength)
                {
                    isMaster = false;
                    break;
                }
            }

            if (isMaster) masterCount++;
        }

        return masterCount;
    }

    /// <summary>
    /// Determines if a card is currently a master card based on played cards.
    /// </summary>
    private bool IsMasterCard(Card card, GameMode mode)
    {
        int cardStrength = card.GetStrength(mode);

        // Check if any unplayed card of the same suit is stronger
        foreach (CardRank rank in Enum.GetValues<CardRank>())
        {
            var potentialCard = new Card(rank, card.Suit);
            if (potentialCard.Equals(card)) continue;
            if (_playedCards.Contains(potentialCard)) continue; // Already played

            int potentialStrength = potentialCard.GetStrength(mode);
            if (potentialStrength > cardStrength)
            {
                return false; // A stronger card is still out there
            }
        }

        return true;
    }

    /// <summary>
    /// Gets all current master cards from the hand.
    /// </summary>
    private List<Card> GetMasterCards(IReadOnlyList<Card> hand, GameMode mode)
    {
        return hand.Where(c => IsMasterCard(c, mode)).ToList();
    }

    /// <summary>
    /// Chooses a card to lead the trick.
    /// Prefers master cards, considers partner preferences, or cards that might force out master cards.
    /// </summary>
    private Card ChooseLeadCard(IReadOnlyList<Card> validPlays, GameMode mode)
    {
        var masterCards = GetMasterCards(validPlays, mode);

        // Lead with master cards to guarantee points
        if (masterCards.Count > 0)
        {
            // Prefer master cards in suits partner likes, avoid suits partner dislikes
            var preferredMasters = masterCards
                .Where(c => !_partnerDislikedSuits.Contains(c.Suit))
                .OrderByDescending(c => _partnerPreferredSuits.Contains(c.Suit))
                .ThenByDescending(c => c.GetPointValue(mode))
                .ToList();

            if (preferredMasters.Count > 0)
            {
                return preferredMasters.First();
            }

            // Fall back to any master card
            return masterCards.OrderByDescending(c => c.GetPointValue(mode)).First();
        }

        // No master cards - consider partner preferences first
        var trumpSuit = mode.GetTrumpSuit();

        // Try to lead a suit partner prefers (and we have cards in)
        if (_partnerPreferredSuits.Count > 0)
        {
            var preferredSuitCards = validPlays
                .Where(c => _partnerPreferredSuits.Contains(c.Suit))
                .Where(c => !trumpSuit.HasValue || c.Suit != trumpSuit.Value) // Don't waste trumps
                .ToList();

            if (preferredSuitCards.Count > 0)
            {
                // Lead strongest in partner's preferred suit
                return preferredSuitCards.OrderByDescending(c => c.GetStrength(mode)).First();
            }
        }

        // Filter out suits partner dislikes if possible
        var nonDislikedPlays = validPlays
            .Where(c => !_partnerDislikedSuits.Contains(c.Suit))
            .ToList();

        var playsToConsider = nonDislikedPlays.Count > 0 ? nonDislikedPlays : validPlays.ToList();

        // In Colour mode, leading trump can be strong
        if (trumpSuit.HasValue)
        {
            var trumpCards = playsToConsider.Where(c => c.Suit == trumpSuit.Value).ToList();
            if (trumpCards.Count > 0)
            {
                // Lead with a medium trump to draw out higher trumps
                var sortedTrumps = trumpCards.OrderBy(c => c.GetStrength(mode)).ToList();
                if (sortedTrumps.Count >= 2)
                {
                    return sortedTrumps[sortedTrumps.Count / 2]; // Middle trump
                }
            }
        }

        // Lead with cards from suits we have multiple cards in (sequence play)
        var suitGroups = playsToConsider.GroupBy(c => c.Suit).Where(g => g.Count() >= 2).ToList();
        if (suitGroups.Count > 0)
        {
            var bestGroup = suitGroups.OrderByDescending(g => g.Max(c => c.GetStrength(mode))).First();
            return bestGroup.OrderByDescending(c => c.GetStrength(mode)).First();
        }

        // Default: lead highest strength card from non-disliked suits
        return playsToConsider.OrderByDescending(c => c.GetStrength(mode)).First();
    }

    /// <summary>
    /// Determines the current winning player and card in the trick.
    /// </summary>
    private (PlayerPosition? winner, Card? card) DetermineCurrentWinner(TrickState trick, GameMode mode)
    {
        if (trick.PlayedCards.Count == 0)
            return (null, null);

        var leadSuit = trick.LeadSuit!.Value;
        var trumpSuit = mode.GetTrumpSuit();

        PlayedCard best = trick.PlayedCards[0];
        foreach (var played in trick.PlayedCards.Skip(1))
        {
            if (CardComparer.Beats(played.Card, best.Card, leadSuit, mode))
            {
                best = played;
            }
        }

        return (best.Player, best.Card);
    }

    /// <summary>
    /// Finds the minimum card that would win against the current winner.
    /// </summary>
    private Card? FindMinimumWinningCard(IReadOnlyList<Card> validPlays, Card currentWinner, CardSuit leadSuit, GameMode mode)
    {
        var winningCards = validPlays
            .Where(c => CardComparer.Beats(c, currentWinner, leadSuit, mode))
            .OrderBy(c => c.GetStrength(mode))
            .ToList();

        return winningCards.Count > 0 ? winningCards[0] : null;
    }

    /// <summary>
    /// Chooses the most valuable card from valid plays (for when teammate is winning).
    /// </summary>
    private Card ChooseMostValuableCard(IReadOnlyList<Card> validPlays, GameMode mode)
    {
        return validPlays.OrderByDescending(c => c.GetPointValue(mode))
                        .ThenByDescending(c => c.GetStrength(mode))
                        .First();
    }

    /// <summary>
    /// Chooses the least valuable card from valid plays (for discarding).
    /// </summary>
    private Card ChooseLeastValuableCard(IReadOnlyList<Card> validPlays, GameMode mode)
    {
        return validPlays.OrderBy(c => c.GetPointValue(mode))
                        .ThenBy(c => c.GetStrength(mode))
                        .First();
    }

    #endregion
}

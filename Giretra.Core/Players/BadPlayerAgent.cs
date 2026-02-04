using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Negotiation;
using Giretra.Core.Play;
using Giretra.Core.Scoring;
using Giretra.Core.State;

namespace Giretra.Core.Players;

/// <summary>
/// A deliberately bad player that makes the worst possible decisions.
/// This is the inverse of CalculatingPlayerAgent.
/// </summary>
/// <remarks>
/// <para><b>Announcement Behavior (inverse of CalculatingPlayerAgent):</b></para>
/// <list type="bullet">
///   <item>Announces eagerly with weak hands (below 40% strength)</item>
///   <item>Accepts/passes with strong hands (above 60% strength)</item>
///   <item>Doubles/redoubles when holding few master cards (high risk)</item>
///   <item>Chooses the worst game mode for the hand</item>
/// </list>
///
/// <para><b>Play Behavior (inverse of CalculatingPlayerAgent):</b></para>
/// <list type="bullet">
///   <item><b>Leading:</b> Plays weakest, lowest-value cards first. Avoids master cards.</item>
///   <item><b>Teammate winning:</b> Plays the least valuable card (wastes opportunity).</item>
///   <item><b>Opponent winning:</b> Plays the most valuable card possible (gifts points).
///         Never tries to win when it would be beneficial.</item>
/// </list>
/// </remarks>
public class BadPlayerAgent : IPlayerAgent
{
    private readonly HashSet<Card> _playedCards = [];
    private readonly Random _random = new();

    public PlayerPosition Position { get; }

    public BadPlayerAgent(PlayerPosition position)
    {
        Position = position;
    }

    public Task<(int position, bool fromTop)> ChooseCutAsync(int deckSize, MatchState matchState)
    {
        // Random cut (same as calculating - cutting doesn't matter strategically)
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

        // Find the WORST mode for our hand (inverse of calculating)
        var worstMode = modeScores.MinBy(kv => kv.Value).Key;
        var worstScore = modeScores[worstMode];

        // Count potential master cards for risk assessment
        int masterCardCount = CountPotentialMasterCards(hand, worstMode);

        // Check for double/redouble opportunities
        var doubleAction = validActions.OfType<DoubleAction>().FirstOrDefault();
        var redoubleAction = validActions.OfType<RedoubleAction>().FirstOrDefault();

        if (masterCardCount == 0)
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

        // INVERSE: Announce eagerly with WEAK hands (below 40%)
        if (worstScore < 20)
        {
            var announceAction = validActions
                .OfType<AnnouncementAction>()
                .Where(a => a.Mode == worstMode ||
                            (negotiationState.CurrentBid == null && modeScores[a.Mode] < 50))
                .OrderBy(a => modeScores[a.Mode]) // Pick worst mode
                .FirstOrDefault();

            if (announceAction != null)
            {
                return Task.FromResult<NegotiationAction>(announceAction);
            }
        }

        // INVERSE: With decent hand, try to announce the WORST possible mode
        if (worstScore < 40)
        {
            var announceAction = validActions
                .OfType<AnnouncementAction>()
                .OrderBy(a => modeScores[a.Mode]) // Pick worst mode
                .FirstOrDefault();

            if (announceAction != null)
            {
                return Task.FromResult<NegotiationAction>(announceAction);
            }
        }

        // INVERSE: With strong hand (>60%), just accept and let opponents set the mode
        var acceptAction = validActions.OfType<AcceptAction>().FirstOrDefault();
        if (acceptAction != null)
        {
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
            // INVERSE: Lead with weakest, lowest-value cards
            return Task.FromResult(ChooseWorstLeadCard(validPlays, gameMode));
        }

        // Analyze current trick state
        var (currentWinner, winningCard) = DetermineCurrentWinner(trick, gameMode);
        bool teammateWinning = currentWinner?.GetTeam() == myTeam;

        // INVERSE: If teammate is winning, play LEAST valuable card (waste opportunity)
        if (teammateWinning)
        {
            return Task.FromResult(ChooseLeastValuableCard(validPlays, gameMode));
        }

        // INVERSE: Opponent winning - play MOST valuable card (gift them points)
        return Task.FromResult(ChooseMostValuableCard(validPlays, gameMode));
    }

    public Task OnDealStartedAsync(MatchState matchState)
    {
        _playedCards.Clear();
        return Task.CompletedTask;
    }

    public Task OnDealEndedAsync(DealResult result, HandState handState, MatchState matchState)
    {
        return Task.CompletedTask;
    }

    public Task OnCardPlayedAsync(PlayerPosition player, Card card, HandState handState, MatchState matchState)
    {
        _playedCards.Add(card);
        return Task.CompletedTask;
    }

    public Task OnTrickCompletedAsync(TrickState completedTrick, PlayerPosition winner, HandState handState, MatchState matchState)
    {
        return Task.CompletedTask;
    }

    public Task OnMatchEndedAsync(MatchState matchState)
    {
        return Task.CompletedTask;
    }

    #region Strategic Calculations (copied from CalculatingPlayerAgent for evaluation)

    /// <summary>
    /// Calculates hand strength as a percentage (0-100) for a given game mode.
    /// </summary>
    private double CalculateHandStrengthPercentage(IReadOnlyList<Card> hand, GameMode mode)
    {
        var trumpSuit = mode.GetTrumpSuit();
        var category = mode.GetCategory();

        int handPoints = hand.Sum(c => c.GetPointValue(mode));
        int maxPossiblePoints = mode.GetTotalPoints();
        double pointPercentage = (double)handPoints / maxPossiblePoints * 100;

        double strengthBonus = 0;
        foreach (var card in hand)
        {
            int strength = card.GetStrength(mode);
            if (strength >= 7) strengthBonus += 5;
            else if (strength >= 6) strengthBonus += 3;
        }

        double trumpBonus = 0;
        if (trumpSuit.HasValue)
        {
            int trumpCount = hand.Count(c => c.Suit == trumpSuit.Value);
            trumpBonus = trumpCount * 4;

            if (hand.Any(c => c.Suit == trumpSuit.Value && c.Rank == CardRank.Jack))
                trumpBonus += 8;
            if (hand.Any(c => c.Suit == trumpSuit.Value && c.Rank == CardRank.Nine))
                trumpBonus += 5;
        }

        double toutAsBonus = 0;
        if (category == GameModeCategory.ToutAs)
        {
            toutAsBonus += hand.Count(c => c.Rank == CardRank.Jack) * 6;
            toutAsBonus += hand.Count(c => c.Rank == CardRank.Nine) * 4;
        }

        double sansAsBonus = 0;
        if (category == GameModeCategory.SansAs)
        {
            sansAsBonus += hand.Count(c => c.Rank == CardRank.Ace) * 5;
            sansAsBonus += hand.Count(c => c.Rank == CardRank.Ten) * 2;
        }

        double score = pointPercentage * 0.4 + strengthBonus + trumpBonus + toutAsBonus + sansAsBonus;
        return Math.Min(100, Math.Max(0, score));
    }

    /// <summary>
    /// Counts cards that are likely master cards.
    /// </summary>
    private int CountPotentialMasterCards(IReadOnlyList<Card> hand, GameMode mode)
    {
        int masterCount = 0;

        foreach (var suit in Enum.GetValues<CardSuit>())
        {
            var cardsInSuit = hand.Where(c => c.Suit == suit).ToList();
            if (cardsInSuit.Count == 0) continue;

            var strongestOwned = cardsInSuit.MaxBy(c => c.GetStrength(mode));
            int ourStrength = strongestOwned.GetStrength(mode);

            bool isMaster = true;
            foreach (CardRank rank in Enum.GetValues<CardRank>())
            {
                var potentialCard = new Card(rank, suit);
                if (hand.Contains(potentialCard)) continue;

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
    /// Determines if a card is currently a master card.
    /// </summary>
    private bool IsMasterCard(Card card, GameMode mode)
    {
        int cardStrength = card.GetStrength(mode);

        foreach (CardRank rank in Enum.GetValues<CardRank>())
        {
            var potentialCard = new Card(rank, card.Suit);
            if (potentialCard.Equals(card)) continue;
            if (_playedCards.Contains(potentialCard)) continue;

            int potentialStrength = potentialCard.GetStrength(mode);
            if (potentialStrength > cardStrength)
            {
                return false;
            }
        }

        return true;
    }

    #endregion

    #region Bad Card Selection (inverse of CalculatingPlayerAgent)

    /// <summary>
    /// Chooses the WORST card to lead (inverse of CalculatingPlayerAgent).
    /// Avoids master cards, prefers weakest cards with lowest point value.
    /// </summary>
    private Card ChooseWorstLeadCard(IReadOnlyList<Card> validPlays, GameMode mode)
    {
        // INVERSE: Avoid master cards - they're too good!
        var nonMasterCards = validPlays.Where(c => !IsMasterCard(c, mode)).ToList();
        var cardsToConsider = nonMasterCards.Count > 0 ? nonMasterCards : validPlays.ToList();

        // INVERSE: Lead with the weakest, lowest-value card
        return cardsToConsider
            .OrderBy(c => c.GetPointValue(mode))
            .ThenBy(c => c.GetStrength(mode))
            .First();
    }

    /// <summary>
    /// Determines the current winning player and card in the trick.
    /// </summary>
    private (PlayerPosition? winner, Card? card) DetermineCurrentWinner(TrickState trick, GameMode mode)
    {
        if (trick.PlayedCards.Count == 0)
            return (null, null);

        var leadSuit = trick.LeadSuit!.Value;

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
    /// Chooses the most valuable card (to gift to opponents).
    /// Prioritizes master cards first (wasting them), then falls back to highest point value.
    /// </summary>
    private Card ChooseMostValuableCard(IReadOnlyList<Card> validPlays, GameMode mode)
    {
        // Prefer playing master cards first (wasting them on losing situations)
        var masterCards = validPlays.Where(c => IsMasterCard(c, mode)).ToList();
        if (masterCards.Count > 0)
        {
            return masterCards
                .OrderByDescending(c => c.GetPointValue(mode))
                .ThenByDescending(c => c.GetStrength(mode))
                .First();
        }

        return validPlays
            .OrderByDescending(c => c.GetPointValue(mode))
            .ThenByDescending(c => c.GetStrength(mode))
            .First();
    }

    /// <summary>
    /// Chooses the least valuable card (to waste opportunities).
    /// </summary>
    private Card ChooseLeastValuableCard(IReadOnlyList<Card> validPlays, GameMode mode)
    {
        return validPlays
            .OrderBy(c => c.GetPointValue(mode))
            .ThenBy(c => c.GetStrength(mode))
            .First();
    }

    #endregion
}

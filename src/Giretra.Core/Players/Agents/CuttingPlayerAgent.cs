using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Negotiation;
using Giretra.Core.Scoring;
using Giretra.Core.State;

namespace Giretra.Core.Players.Agents;

/// <summary>
/// Negotiates and plays exactly like <see cref="DeterministicPlayerAgent"/>, which it delegates to,
/// and differs from it in one respect: it cuts the deck deliberately instead of at a fixed position.
/// <para>
/// The deck is never shuffled during a match, so a player who watches the tricks knows the order of
/// the deck the next deal will use (see <see cref="DeckTracker"/>). Only 21 cuts are reachable, and
/// each of them determines every hand at the table, so this agent projects all of them
/// (see <see cref="CutPlanner"/>) and takes the one that leaves its own team with the most
/// announceable hand while leaving the opponents with the least.
/// </para>
/// </summary>
public sealed class CuttingPlayerAgent : IPlayerAgent
{
    /// <summary>Cut used when the deck order is unknown.</summary>
    private const int FallbackCutPosition = 16;

    /// <summary>Weight of the weaker of the two hands of a team.</summary>
    private const double PartnerSupportWeight = 0.35;

    /// <summary>
    /// Share of a team's value that depends on being able to announce the mode at all. The rest
    /// is kept because a strong hand still defends well when the other side takes the contract.
    /// </summary>
    private const double ClaimWeight = 0.45;

    /// <summary>How much the opponents' prospects count against our own.</summary>
    private const double OpponentWeight = 0.85;

    /// <summary>Guaranteed tricks (both hands combined) at which a Colour sweep looks likely.</summary>
    private const int SweepThreshold = 6;

    /// <summary>Score swing applied for a likely Colour sweep, which ends the match instantly.</summary>
    private const double SweepSwing = 30;

    /// <summary>Base match points of a plain Colour, used to normalise stakes.</summary>
    private const double PlainColourStake = 16;

    private readonly DeterministicPlayerAgent _inner;
    private readonly DeckTracker _deckTracker = new();
    private readonly Team _myTeam;

    private IReadOnlyList<Card>? _projectedCutHand;
    private bool _cutProjectionChecked;

    public PlayerPosition Position { get; }

    /// <summary>
    /// Gets how many cuts this agent chose from a tracked deck order rather than falling back
    /// to a fixed position.
    /// </summary>
    public int TrackedCuts { get; private set; }

    /// <summary>
    /// Gets how many tracked cuts were followed by a hand that did not match the projection.
    /// Stays at zero while the tracked deck order is correct.
    /// </summary>
    public int CutProjectionMismatches { get; private set; }

    public CuttingPlayerAgent(PlayerPosition position)
    {
        Position = position;
        _myTeam = position.GetTeam();
        _inner = new DeterministicPlayerAgent(position);
    }

    #region Cut Selection

    public Task<(int position, bool fromTop)> ChooseCutAsync(int deckSize, MatchState matchState)
    {
        var deck = _deckTracker.PredictedDeck;

        // The very first deal at a table comes from a shuffled deck we have never seen: nothing to optimise.
        if (deck is null || deckSize != CutPlanner.DeckSize || deck.Count != deckSize)
            return Task.FromResult((position: FallbackCutPosition, fromTop: true));

        var dealer = matchState.CurrentDeal?.Dealer ?? matchState.CurrentDealer;
        var bestPosition = RankCuts(deck, dealer, matchState)[0].Position;

        TrackedCuts++;
        _projectedCutHand = CutPlanner.ProjectHand(deck, dealer, Position, bestPosition);

        return Task.FromResult((position: bestPosition, fromTop: true));
    }

    private readonly record struct CutTeamValue(double Value, GameMode Mode, int GuaranteedTricks);

    /// <summary>
    /// Ranks every reachable cut of a known deck, best first, from this agent's seat.
    /// <see cref="ChooseCutAsync"/> takes the first entry; the full ranking is exposed so the
    /// choice can be inspected and compared.
    /// </summary>
    /// <param name="deck">The deck about to be cut, in its current order.</param>
    /// <param name="dealer">The dealer of the deal being cut.</param>
    /// <param name="matchState">The current match state, which shapes how much risk is worth taking.</param>
    public IReadOnlyList<(int Position, double Score)> RankCuts(Deck deck, PlayerPosition dealer, MatchState matchState)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(matchState);

        var opponentTeam = _myTeam == Team.Team1 ? Team.Team2 : Team.Team1;
        var aggressiveness = PlayerAgentHelper.ComputeAggressiveness(
            matchState.GetMatchPoints(_myTeam),
            matchState.GetMatchPoints(opponentTeam),
            matchState.TargetScore);

        // Same threshold negotiation will apply to the five cards this cut deals out, so the
        // ranking measures announceability the way the agent will actually judge it.
        var announceThreshold = 55 - aggressiveness * 15;

        // Behind on the scoreboard, the higher-paying modes are worth reaching for.
        var stakeSensitivity = 0.10 + 0.15 * aggressiveness;

        var ranking = new List<(int Position, double Score)>(CutPlanner.CandidateCutPositions.Count);

        foreach (var position in CutPlanner.CandidateCutPositions)
        {
            var ours = EvaluateTeamAfterCut(deck, dealer, Position, position, announceThreshold, stakeSensitivity);
            var theirs = EvaluateTeamAfterCut(deck, dealer, Position.Next(), position, announceThreshold, stakeSensitivity);

            var score = ours.Value - OpponentWeight * theirs.Value;

            // A Colour sweep wins the match outright, so an overwhelming Colour hand is worth
            // more than the strength gap suggests, and handing one over costs more.
            if (matchState.ColourSweepMatchPoints is null)
            {
                if (ours.Mode.IsColourMode() && ours.GuaranteedTricks >= SweepThreshold)
                    score += SweepSwing;

                if (theirs.Mode.IsColourMode() && theirs.GuaranteedTricks >= SweepThreshold)
                    score -= SweepSwing;
            }

            ranking.Add((position, score));
        }

        // Ties keep the lowest position, so the ranking is fully deterministic.
        return [.. ranking.OrderByDescending(entry => entry.Score).ThenBy(entry => entry.Position)];
    }

    /// <summary>
    /// Values the best mode available to the team of <paramref name="first"/> after the cut.
    /// </summary>
    private static CutTeamValue EvaluateTeamAfterCut(
        Deck deck,
        PlayerPosition dealer,
        PlayerPosition first,
        int cutPosition,
        double announceThreshold,
        double stakeSensitivity)
    {
        var second = first.Teammate();

        var firstHand = CutPlanner.ProjectHand(deck, dealer, first, cutPosition);
        var secondHand = CutPlanner.ProjectHand(deck, dealer, second, cutPosition);

        // Negotiation happens on the first five cards, before the last three are dealt.
        var firstOpening = firstHand.Take(CutPlanner.NegotiationHandSize).ToList();
        var secondOpening = secondHand.Take(CutPlanner.NegotiationHandSize).ToList();

        var firstStarts = dealer.Next() == first;
        var secondStarts = dealer.Next() == second;

        var best = new CutTeamValue(double.NegativeInfinity, GameMode.ColourClubs, 0);

        foreach (var mode in Enum.GetValues<GameMode>())
        {
            var firstFull = HandEvaluator.Evaluate(firstHand, mode, firstStarts);
            var secondFull = HandEvaluator.Evaluate(secondHand, mode, secondStarts);

            // The announcer's hand carries the contract; the partner's hand supports it.
            var strength = Math.Max(firstFull.Score, secondFull.Score)
                         + PartnerSupportWeight * Math.Min(firstFull.Score, secondFull.Score);

            // Eight strong cards are worth little in a mode neither player can announce, so
            // discount by how convincing the better of the two five-card hands looks.
            var opening = Math.Max(
                HandEvaluator.Evaluate(firstOpening, mode, firstStarts).Score,
                HandEvaluator.Evaluate(secondOpening, mode, secondStarts).Score);
            var claim = Math.Clamp(opening / announceThreshold, 0, 1);

            var stake = 1 + stakeSensitivity * (mode.GetBaseMatchPoints() / PlainColourStake - 1);
            var value = strength * (1 - ClaimWeight + ClaimWeight * claim) * stake;

            if (value <= best.Value)
                continue;

            best = new CutTeamValue(
                value,
                mode,
                Math.Min(CutPlanner.FullHandSize, firstFull.GuaranteedTricks + secondFull.GuaranteedTricks));
        }

        return best;
    }

    /// <summary>
    /// Checks the cards we were dealt against the cut projection. A mismatch means the tracked
    /// deck order drifted, so it is dropped rather than trusted for the rest of the deal.
    /// </summary>
    private void VerifyCutProjection(IReadOnlyList<Card> hand)
    {
        if (_cutProjectionChecked || _projectedCutHand is null)
            return;

        _cutProjectionChecked = true;

        var projected = _projectedCutHand.Take(CutPlanner.NegotiationHandSize).ToHashSet();
        if (projected.SetEquals(hand))
            return;

        CutProjectionMismatches++;
        _deckTracker.Invalidate();
    }

    #endregion

    #region Delegated Play

    public Task<NegotiationAction> ChooseNegotiationActionAsync(
        IReadOnlyList<Card> hand,
        NegotiationState negotiationState,
        MatchState matchState,
        IReadOnlyList<NegotiationAction> validActions)
    {
        VerifyCutProjection(hand);
        return _inner.ChooseNegotiationActionAsync(hand, negotiationState, matchState, validActions);
    }

    public Task<Card> ChooseCardAsync(
        IReadOnlyList<Card> hand,
        HandState handState,
        MatchState matchState,
        IReadOnlyList<Card> validPlays)
        => _inner.ChooseCardAsync(hand, handState, matchState, validPlays);

    public Task OnDealStartedAsync(MatchState matchState)
    {
        // A projection only exists for deals this agent cuts.
        _projectedCutHand = null;
        _cutProjectionChecked = false;

        _deckTracker.OnDealStarted(matchState);
        return _inner.OnDealStartedAsync(matchState);
    }

    public Task OnNegotiationCompletedAsync(NegotiationState negotiationState, MatchState matchState)
        => _inner.OnNegotiationCompletedAsync(negotiationState, matchState);

    public Task OnDealEndedAsync(DealResult result, HandState handState, MatchState matchState)
    {
        // The next deal is dealt from these tricks, so this is where the deck order is learned.
        _deckTracker.OnDealEnded(handState);
        return _inner.OnDealEndedAsync(result, handState, matchState);
    }

    public Task OnCardPlayedAsync(PlayerPosition player, Card card, HandState handState, MatchState matchState)
        => _inner.OnCardPlayedAsync(player, card, handState, matchState);

    public Task OnTrickCompletedAsync(TrickState completedTrick, PlayerPosition winner, HandState handState, MatchState matchState)
        => _inner.OnTrickCompletedAsync(completedTrick, winner, handState, matchState);

    public Task OnMatchEndedAsync(MatchState matchState)
    {
        // The deck stays as collected from the last hand and hosts carry it into the next
        // match, so the tracked order is kept. If the next match is dealt from a fresh
        // shuffle instead, the cut projection check catches the mismatch and drops it.
        return _inner.OnMatchEndedAsync(matchState);
    }

    public Task ConfirmContinueDealAsync(MatchState matchState) => _inner.ConfirmContinueDealAsync(matchState);

    public Task ConfirmContinueMatchAsync(MatchState matchState) => _inner.ConfirmContinueMatchAsync(matchState);

    #endregion
}

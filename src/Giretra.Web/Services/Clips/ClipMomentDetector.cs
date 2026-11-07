using Giretra.Core.GameModes;
using Giretra.Core.Play;
using Giretra.Core.Players;
using Giretra.Core.Solving;
using Giretra.Core.State;
using Giretra.Web.Achievements;
using Giretra.Web.Domain;
using Giretra.Web.Models.Responses;
using Giretra.Web.Players;

namespace Giretra.Web.Services.Clips;

/// <summary>One completed deal as the detector needs it.</summary>
public sealed record ClipMomentDeal(
    int DealNumber,
    GameMode GameMode,
    Team AnnouncerTeam,
    MultiplierState Multiplier,
    bool WasSweep,
    Team? SweepingTeam,
    IReadOnlyList<PlayedCard> Plays);

/// <summary>
/// Finds the moments of a match worth sharing as a clip: sweeps, achievements, plays the
/// solver rates as the only good card, and tricks where the opponents' mistakes swung a lot
/// of match points. Pure and deterministic; a full match takes well under a second.
/// </summary>
public static class ClipMomentDetector
{
    /// <summary>
    /// Base match points (before the deal's multiplier) the opponents must hand over in one
    /// trick, net of our own mistakes, to count as a swing.
    /// </summary>
    public const int SwingThreshold = 5;

    /// <summary>Base match points every alternative card must have lost for a play to count as brilliant.</summary>
    public const int BrilliantThreshold = 4;

    /// <summary>A deal is worth at most 26 base match points; an instant win counts as that much for ranking.</summary>
    public const int MaxBasePoints = 26;

    public const int MaxMoments = 12;
    private const int TricksPerDeal = 8;
    private const int PlaysPerDeal = 32;

    public static IReadOnlyList<ClipMomentResponse> Detect(
        IReadOnlyList<ClipMomentDeal> deals,
        IReadOnlyList<EarnedAchievementInfo> achievements,
        int? colourSweepMatchPoints = null)
    {
        var moments = new List<ClipMomentResponse>();

        foreach (var deal in deals)
        {
            if (deal.Plays.Count != PlaysPerDeal)
                continue;

            if (deal.WasSweep && deal.SweepingTeam is { } sweeper)
            {
                moments.Add(new ClipMomentResponse
                {
                    Kind = ClipMomentKind.Sweep,
                    DealNumber = deal.DealNumber,
                    FromTrick = 1,
                    ToTrick = TricksPerDeal,
                    Team = sweeper,
                    Score = 100 + 10 * (deal.Multiplier.GetMultiplier() - 1),
                });
            }

            foreach (var earned in achievements.Where(a => a.DealNumber == deal.DealNumber))
            {
                moments.Add(new ClipMomentResponse
                {
                    Kind = ClipMomentKind.Achievement,
                    DealNumber = deal.DealNumber,
                    FromTrick = 1,
                    ToTrick = TricksPerDeal,
                    Team = earned.PlayerPosition.GetTeam(),
                    Player = earned.PlayerPosition,
                    AchievementCode = earned.Code,
                    Score = 80 + 2 * earned.Tier,
                });
            }

            HandState hand;
            HandAnalysis analysis;
            var scoring = new SolverScoring(deal.AnnouncerTeam, deal.Multiplier, colourSweepMatchPoints);
            try
            {
                hand = Replay(deal);
                analysis = HandAnalyzer.Analyze(hand, scoring);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                // An inconsistent play record; the deal is simply not analysed.
                continue;
            }

            moments.AddRange(AnalysedMoments(deal.DealNumber, deal.Multiplier, hand, analysis, scoring));
        }

        // A close deal makes many plays "the only good card" and several tricks a swing:
        // keep one play per player and one swing per team in each deal (the biggest, then the
        // latest, which is the decisive one), so a match yields a short, varied list.
        return moments
            .GroupBy(m => (m.DealNumber, m.Kind, m.Team, m.Player))
            .Select(g => g.OrderByDescending(m => m.Score).ThenByDescending(m => m.FromTrick).First())
            .OrderByDescending(m => m.Score)
            .ThenBy(m => m.DealNumber)
            .ThenBy(m => m.FromTrick)
            .Take(MaxMoments)
            .ToList();
    }

    /// <summary>Ranking value of a match-point amount: per multiplier, capped at a full deal.</summary>
    public static int BasePoints(int matchPoints, MultiplierState multiplier)
        => Math.Min(MaxBasePoints, matchPoints / multiplier.GetMultiplier());

    private static IEnumerable<ClipMomentResponse> AnalysedMoments(int dealNumber, MultiplierState multiplier, HandState hand, HandAnalysis analysis, SolverScoring scoring)
    {
        foreach (var trickPlays in analysis.Plays.GroupBy(p => p.TrickNumber))
        {
            var trick = trickPlays.Key;
            var winner = hand.TrickWinners[trick - 1];

            foreach (var team in new[] { Team.Team1, Team.Team2 })
            {
                var handedOver = trickPlays.Where(p => p.Player.GetTeam() != team).Sum(p => p.MatchPointLoss ?? 0);
                var givenBack = trickPlays.Where(p => p.Player.GetTeam() == team).Sum(p => p.MatchPointLoss ?? 0);
                var swing = handedOver - givenBack;
                if (BasePoints(swing, multiplier) >= SwingThreshold)
                {
                    yield return new ClipMomentResponse
                    {
                        Kind = ClipMomentKind.Swing,
                        DealNumber = dealNumber,
                        FromTrick = trick,
                        ToTrick = trick,
                        Team = team,
                        Player = winner.GetTeam() == team ? winner : null,
                        MatchPoints = swing,
                        Score = 40 + 2 * BasePoints(swing, multiplier),
                    };
                }
            }

            foreach (var play in trickPlays)
            {
                if (!play.IsOptimal || play.IsForced)
                    continue;
                var alternatives = play.Evaluation.Cards.Where(c => !c.IsOptimal).ToList();
                if (alternatives.Count == 0)
                    continue;
                var cheapestMistake = alternatives.Min(c => play.Evaluation.GetMatchPointLoss(c.Card, scoring));
                if (BasePoints(cheapestMistake, multiplier) < BrilliantThreshold)
                    continue;

                yield return new ClipMomentResponse
                {
                    Kind = ClipMomentKind.BrilliantPlay,
                    DealNumber = dealNumber,
                    FromTrick = trick,
                    ToTrick = trick,
                    Team = play.Player.GetTeam(),
                    Player = play.Player,
                    MatchPoints = cheapestMistake,
                    Score = 50 + 2 * BasePoints(cheapestMistake, multiplier),
                };
            }
        }
    }

    private static HandState Replay(ClipMomentDeal deal)
    {
        var state = HandState.Create(deal.GameMode, deal.Plays[0].Player);
        foreach (var play in deal.Plays)
        {
            if (state.CurrentTrick?.CurrentPlayer != play.Player)
                throw new ArgumentException($"Play order is inconsistent in deal {deal.DealNumber}.");
            state = state.PlayCard(play.Card);
        }
        return state;
    }

    /// <summary>The finished session's deals: results from the match state, plays from the action recorder.</summary>
    public static IReadOnlyList<ClipMomentDeal> FromSession(GameSession session)
    {
        var matchState = session.MatchState;
        if (matchState == null)
            return [];

        var recorded = (session.ActionRecorder?.GetDeals() ?? []).ToDictionary(d => d.DealNumber);
        var deals = new List<ClipMomentDeal>();
        for (var i = 0; i < matchState.CompletedDeals.Count; i++)
        {
            var result = matchState.CompletedDeals[i];
            var dealNumber = i + 1;
            var plays = recorded.TryGetValue(dealNumber, out var record)
                ? record.Actions
                    .Where(a => a.ActionType == RecordedActionType.PlayCard && a.CardRank != null && a.CardSuit != null)
                    .OrderBy(a => a.ActionOrder)
                    .Select(a => new PlayedCard(a.PlayerPosition, new Core.Cards.Card(a.CardRank!.Value, a.CardSuit!.Value)))
                    .ToList()
                : [];

            deals.Add(new ClipMomentDeal(
                dealNumber,
                result.GameMode,
                result.AnnouncerTeam,
                result.Multiplier,
                result.WasSweep,
                result.SweepingTeam,
                plays));
        }
        return deals;
    }
}

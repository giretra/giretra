using System.Collections.Immutable;
using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Players;
using Giretra.Core.Scoring;

namespace Giretra.Core.State;

/// <summary>
/// Represents the state of an entire match (multiple deals until 150 points).
/// </summary>
public sealed class MatchState
{
    /// <summary>
    /// Gets the current target score to win (starts at 150, increases if both exceed).
    /// </summary>
    public int TargetScore { get; }

    /// <summary>
    /// Gets Team1's total match points.
    /// </summary>
    public int Team1MatchPoints { get; }

    /// <summary>
    /// Gets Team2's total match points.
    /// </summary>
    public int Team2MatchPoints { get; }

    /// <summary>
    /// Gets the current dealer position.
    /// </summary>
    public PlayerPosition CurrentDealer { get; }

    /// <summary>
    /// Gets the current deal state, or null if between deals.
    /// </summary>
    public DealState? CurrentDeal { get; }

    /// <summary>
    /// Gets the results of completed deals.
    /// </summary>
    public ImmutableList<DealResult> CompletedDeals { get; }

    /// <summary>
    /// Gets whether the match is complete.
    /// </summary>
    public bool IsComplete { get; }

    /// <summary>
    /// Gets the winning team, or null if match is not complete.
    /// </summary>
    public Team? Winner { get; }

    /// <summary>
    /// When set, Colour sweeps award this many base match points (× multiplier)
    /// instead of triggering an instant match win. Null = normal rules (instant win).
    /// </summary>
    public int? ColourSweepMatchPoints { get; }

    private MatchState(
        int targetScore,
        int team1MatchPoints,
        int team2MatchPoints,
        PlayerPosition currentDealer,
        DealState? currentDeal,
        ImmutableList<DealResult> completedDeals,
        bool isComplete,
        Team? winner,
        int? colourSweepMatchPoints = null)
    {
        TargetScore = targetScore;
        Team1MatchPoints = team1MatchPoints;
        Team2MatchPoints = team2MatchPoints;
        CurrentDealer = currentDealer;
        CurrentDeal = currentDeal;
        CompletedDeals = completedDeals;
        IsComplete = isComplete;
        Winner = winner;
        ColourSweepMatchPoints = colourSweepMatchPoints;
    }

    /// <summary>
    /// Creates a new match with the specified first dealer.
    /// </summary>
    /// <param name="firstDealer">The position of the first dealer.</param>
    /// <param name="targetScore">The target score to win (default 150).</param>
    /// <param name="colourSweepMatchPoints">
    /// When set, Colour sweeps award this many base match points (× multiplier)
    /// instead of triggering an instant match win. Null = normal rules.
    /// </param>
    public static MatchState Create(PlayerPosition firstDealer, int targetScore = 150, int? colourSweepMatchPoints = null)
    {
        return new MatchState(
            targetScore,
            0,
            0,
            firstDealer,
            null,
            ImmutableList<DealResult>.Empty,
            false,
            null,
            colourSweepMatchPoints);
    }

    /// <summary>
    /// Starts a new deal with the given deck.
    /// </summary>
    public MatchState StartDeal(Deck deck)
    {
        if (IsComplete)
        {
            throw new InvalidOperationException("Match is already complete.");
        }

        if (CurrentDeal is not null)
        {
            throw new InvalidOperationException("A deal is already in progress.");
        }

        var deal = DealState.Create(CurrentDealer, deck);

        return new MatchState(
            TargetScore,
            Team1MatchPoints,
            Team2MatchPoints,
            CurrentDealer,
            deal,
            CompletedDeals,
            false,
            null,
            ColourSweepMatchPoints);
    }

    /// <summary>
    /// Updates the current deal state.
    /// </summary>
    public MatchState WithDeal(DealState deal)
    {
        if (CurrentDeal is null)
        {
            throw new InvalidOperationException("No deal in progress.");
        }

        // If deal just completed, apply the result
        if (deal.Phase == DealPhase.Completed && deal.Result is not null)
        {
            return ApplyDealResult(deal);
        }

        return new MatchState(
            TargetScore,
            Team1MatchPoints,
            Team2MatchPoints,
            CurrentDealer,
            deal,
            CompletedDeals,
            false,
            null,
            ColourSweepMatchPoints);
    }

    private MatchState ApplyDealResult(DealState completedDeal)
    {
        var result = completedDeal.Result!;

        // Check for instant win (Colour sweep)
        if (result.IsInstantWin)
        {
            if (ColourSweepMatchPoints.HasValue)
            {
                // Override: convert instant win to a point bonus
                var sweepPoints = ColourSweepMatchPoints.Value * result.Multiplier.GetMultiplier();
                var sweepT1 = Team1MatchPoints + (result.SweepingTeam == Team.Team1 ? sweepPoints : 0);
                var sweepT2 = Team2MatchPoints + (result.SweepingTeam == Team.Team2 ? sweepPoints : 0);
                var sweepState = DetermineMatchState(sweepT1, sweepT2);

                return new MatchState(
                    sweepState.NewTarget, sweepT1, sweepT2,
                    CurrentDealer.Next(), null,
                    CompletedDeals.Add(result),
                    sweepState.IsComplete, sweepState.Winner, ColourSweepMatchPoints);
            }

            return new MatchState(
                TargetScore,
                Team1MatchPoints + result.Team1MatchPoints,
                Team2MatchPoints + result.Team2MatchPoints,
                CurrentDealer.Next(),
                null,
                CompletedDeals.Add(result),
                true,
                result.SweepingTeam);
        }

        var newTeam1Points = Team1MatchPoints + result.Team1MatchPoints;
        var newTeam2Points = Team2MatchPoints + result.Team2MatchPoints;

        // Check for match completion
        var (isComplete, winner, newTarget) = DetermineMatchState(newTeam1Points, newTeam2Points);

        return new MatchState(
            newTarget,
            newTeam1Points,
            newTeam2Points,
            CurrentDealer.Next(),
            null,
            CompletedDeals.Add(result),
            isComplete,
            winner,
            ColourSweepMatchPoints);
    }

    private (bool IsComplete, Team? Winner, int NewTarget) DetermineMatchState(int team1Points, int team2Points)
    {
        var team1Reached = team1Points >= TargetScore;
        var team2Reached = team2Points >= TargetScore;

        if (team1Reached && team2Reached)
        {
            // Both exceeded target, increase by 100 and continue
            return (false, null, TargetScore + 100);
        }

        if (team1Reached && team1Points > team2Points)
        {
            return (true, Team.Team1, TargetScore);
        }

        if (team2Reached && team2Points > team1Points)
        {
            return (true, Team.Team2, TargetScore);
        }

        // Exact tie at target or neither reached
        if (team1Reached || team2Reached)
        {
            // Exact tie at target, continue to 250
            return (false, null, TargetScore + 100);
        }

        return (false, null, TargetScore);
    }

    /// <summary>
    /// Gets the match points for a specific team.
    /// </summary>
    public int GetMatchPoints(Team team)
        => team == Team.Team1 ? Team1MatchPoints : Team2MatchPoints;
}

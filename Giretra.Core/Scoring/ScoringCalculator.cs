using Giretra.Core.GameModes;
using Giretra.Core.Players;

namespace Giretra.Core.Scoring;

/// <summary>
/// Calculates match points based on card points and game mode.
/// </summary>
public class ScoringCalculator
{
    /// <summary>
    /// Calculates the deal result based on card points earned.
    /// </summary>
    public DealResult Calculate(
        GameMode gameMode,
        MultiplierState multiplier,
        Team announcerTeam,
        int team1CardPoints,
        int team2CardPoints,
        Team? sweepingTeam)
    {
        var category = gameMode.GetCategory();

        // Handle sweep
        if (sweepingTeam.HasValue)
        {
            return CalculateSweep(gameMode, multiplier, announcerTeam,
                team1CardPoints, team2CardPoints, sweepingTeam.Value);
        }

        // Calculate based on mode category
        return category switch
        {
            GameModeCategory.ToutAs => CalculateToutAs(gameMode, multiplier, announcerTeam,
                team1CardPoints, team2CardPoints),
            GameModeCategory.SansAs => CalculateSansAs(gameMode, multiplier, announcerTeam,
                team1CardPoints, team2CardPoints),
            GameModeCategory.Colour => CalculateColour(gameMode, multiplier, announcerTeam,
                team1CardPoints, team2CardPoints),
            _ => throw new ArgumentOutOfRangeException(nameof(gameMode))
        };
    }

    private DealResult CalculateSweep(
        GameMode gameMode,
        MultiplierState multiplier,
        Team announcerTeam,
        int team1CardPoints,
        int team2CardPoints,
        Team sweepingTeam)
    {
        var category = gameMode.GetCategory();

        if (category == GameModeCategory.Colour)
        {
            // Colour sweep = instant match win
            return new DealResult
            {
                GameMode = gameMode,
                Multiplier = multiplier,
                AnnouncerTeam = announcerTeam,
                Team1CardPoints = team1CardPoints,
                Team2CardPoints = team2CardPoints,
                Team1MatchPoints = 0,
                Team2MatchPoints = 0,
                WasSweep = true,
                SweepingTeam = sweepingTeam,
                IsInstantWin = true
            };
        }

        // ToutAs sweep = 35, SansAs sweep = 90
        var sweepBonus = gameMode.GetSweepBonus();
        var points = sweepBonus * multiplier.GetMultiplier();

        return new DealResult
        {
            GameMode = gameMode,
            Multiplier = multiplier,
            AnnouncerTeam = announcerTeam,
            Team1CardPoints = team1CardPoints,
            Team2CardPoints = team2CardPoints,
            Team1MatchPoints = sweepingTeam == Team.Team1 ? points : 0,
            Team2MatchPoints = sweepingTeam == Team.Team2 ? points : 0,
            WasSweep = true,
            SweepingTeam = sweepingTeam,
            IsInstantWin = false
        };
    }

    private DealResult CalculateToutAs(
        GameMode gameMode,
        MultiplierState multiplier,
        Team announcerTeam,
        int team1CardPoints,
        int team2CardPoints)
    {
        var announcerPoints = announcerTeam == Team.Team1 ? team1CardPoints : team2CardPoints;
        var defenderPoints = announcerTeam == Team.Team1 ? team2CardPoints : team1CardPoints;

        int announcerMatchPoints;
        int defenderMatchPoints;

        // Announcer needs >= 129 to not lose outright
        if (announcerPoints < 129)
        {
            // Announcer loses - defender gets all 26
            announcerMatchPoints = 0;
            defenderMatchPoints = 26;
        }
        else
        {
            // Split scoring: divide by 10, round to nearest (standard rounding)
            // Per SPEC.md examples: 131 rounds to 13, 127 rounds to 13 (tie)
            var rawAnnouncerMatch = (int)Math.Round(announcerPoints / 10.0, MidpointRounding.AwayFromZero);
            var rawDefenderMatch = (int)Math.Round(defenderPoints / 10.0, MidpointRounding.AwayFromZero);

            // Check for effective tie (both round to same or 129-129)
            if (announcerPoints == 129 && defenderPoints == 129)
            {
                // Exact tie
                announcerMatchPoints = 0;
                defenderMatchPoints = 0;
            }
            else if (rawAnnouncerMatch == rawDefenderMatch)
            {
                // Rounds to tie (e.g., 131-127 -> 13-13)
                announcerMatchPoints = 0;
                defenderMatchPoints = 0;
            }
            else
            {
                // Cap at 20-6
                announcerMatchPoints = Math.Min(rawAnnouncerMatch, 20);
                defenderMatchPoints = Math.Max(rawDefenderMatch, 6);

                // Ensure total doesn't exceed 26
                if (announcerMatchPoints + defenderMatchPoints > 26)
                {
                    defenderMatchPoints = 26 - announcerMatchPoints;
                }
            }
        }

        // Apply multiplier
        announcerMatchPoints *= multiplier.GetMultiplier();
        defenderMatchPoints *= multiplier.GetMultiplier();

        return new DealResult
        {
            GameMode = gameMode,
            Multiplier = multiplier,
            AnnouncerTeam = announcerTeam,
            Team1CardPoints = team1CardPoints,
            Team2CardPoints = team2CardPoints,
            Team1MatchPoints = announcerTeam == Team.Team1 ? announcerMatchPoints : defenderMatchPoints,
            Team2MatchPoints = announcerTeam == Team.Team2 ? announcerMatchPoints : defenderMatchPoints,
            WasSweep = false,
            SweepingTeam = null,
            IsInstantWin = false
        };
    }

    private DealResult CalculateSansAs(
        GameMode gameMode,
        MultiplierState multiplier,
        Team announcerTeam,
        int team1CardPoints,
        int team2CardPoints)
    {
        return CalculateWinnerTakesAll(
            gameMode, multiplier, announcerTeam,
            team1CardPoints, team2CardPoints,
            threshold: 65,
            basePoints: 52);
    }

    private DealResult CalculateColour(
        GameMode gameMode,
        MultiplierState multiplier,
        Team announcerTeam,
        int team1CardPoints,
        int team2CardPoints)
    {
        return CalculateWinnerTakesAll(
            gameMode, multiplier, announcerTeam,
            team1CardPoints, team2CardPoints,
            threshold: 82,
            basePoints: 16);
    }

    private DealResult CalculateWinnerTakesAll(
        GameMode gameMode,
        MultiplierState multiplier,
        Team announcerTeam,
        int team1CardPoints,
        int team2CardPoints,
        int threshold,
        int basePoints)
    {
        var announcerPoints = announcerTeam == Team.Team1 ? team1CardPoints : team2CardPoints;
        var defenderPoints = announcerTeam == Team.Team1 ? team2CardPoints : team1CardPoints;

        int announcerMatchPoints;
        int defenderMatchPoints;

        // Check for tie
        if (announcerPoints == defenderPoints)
        {
            announcerMatchPoints = 0;
            defenderMatchPoints = 0;
        }
        else if (announcerPoints >= threshold)
        {
            // Announcer wins - takes all points
            announcerMatchPoints = basePoints;
            defenderMatchPoints = 0;
        }
        else
        {
            // Announcer loses - defender takes all points
            announcerMatchPoints = 0;
            defenderMatchPoints = basePoints;
        }

        // Apply multiplier
        announcerMatchPoints *= multiplier.GetMultiplier();
        defenderMatchPoints *= multiplier.GetMultiplier();

        return new DealResult
        {
            GameMode = gameMode,
            Multiplier = multiplier,
            AnnouncerTeam = announcerTeam,
            Team1CardPoints = team1CardPoints,
            Team2CardPoints = team2CardPoints,
            Team1MatchPoints = announcerTeam == Team.Team1 ? announcerMatchPoints : defenderMatchPoints,
            Team2MatchPoints = announcerTeam == Team.Team2 ? announcerMatchPoints : defenderMatchPoints,
            WasSweep = false,
            SweepingTeam = null,
            IsInstantWin = false
        };
    }
}

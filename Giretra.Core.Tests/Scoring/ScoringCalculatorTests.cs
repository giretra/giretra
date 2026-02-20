using Giretra.Core.GameModes;
using Giretra.Core.Players;
using Giretra.Core.Scoring;

namespace Giretra.Core.Tests.Scoring;

public class ScoringCalculatorTests
{
    private readonly ScoringCalculator _calculator = new();

    #region ToutAs Scoring Tests

    [Theory]
    [InlineData(199, 59, 20, 6)]   // Announcer wins big
    [InlineData(150, 108, 15, 11)] // Announcer wins
    [InlineData(131, 127, 0, 0)]   // Rounds to tie
    [InlineData(129, 129, 0, 0)]   // Exact tie
    [InlineData(209, 49, 26, 0)]   // Announcer dominates - gets all 26
    [InlineData(120, 138, 0, 26)]  // Announcer loses (< 129)
    public void ToutAs_Scoring_MatchesSpec(
        int announcerCardPoints,
        int defenderCardPoints,
        int expectedAnnouncerMatch,
        int expectedDefenderMatch)
    {
        // Announcer is Team1
        var result = _calculator.Calculate(
            GameMode.ToutAs,
            MultiplierState.Normal,
            Team.Team1,
            announcerCardPoints,
            defenderCardPoints,
            sweepingTeam: null);

        Assert.Equal(expectedAnnouncerMatch, result.Team1MatchPoints);
        Assert.Equal(expectedDefenderMatch, result.Team2MatchPoints);
    }

    [Fact]
    public void ToutAs_AnnouncerIsTeam2_PointsCorrectlyAssigned()
    {
        var result = _calculator.Calculate(
            GameMode.ToutAs,
            MultiplierState.Normal,
            Team.Team2,
            59,  // Team1 card points (defender)
            199, // Team2 card points (announcer)
            sweepingTeam: null);

        Assert.Equal(6, result.Team1MatchPoints);   // Defender
        Assert.Equal(20, result.Team2MatchPoints);  // Announcer
    }

    [Fact]
    public void ToutAs_Doubled_PointsMultiplied()
    {
        var result = _calculator.Calculate(
            GameMode.ToutAs,
            MultiplierState.Doubled,
            Team.Team1,
            199,
            59,
            sweepingTeam: null);

        Assert.Equal(40, result.Team1MatchPoints);  // 20 × 2
        Assert.Equal(12, result.Team2MatchPoints);  // 6 × 2
    }

    [Fact]
    public void ToutAs_Redoubled_PointsMultiplied()
    {
        var result = _calculator.Calculate(
            GameMode.ToutAs,
            MultiplierState.Redoubled,
            Team.Team1,
            199,
            59,
            sweepingTeam: null);

        Assert.Equal(80, result.Team1MatchPoints);  // 20 × 4
        Assert.Equal(24, result.Team2MatchPoints);  // 6 × 4
    }

    [Fact]
    public void ToutAs_Doubled_RoundingTie_AnnouncerWins()
    {
        // 131-127 rounds to 13-13 in normal mode (tie)
        // In doubled mode, announcer has more card points so must win: 14-12 × 2
        var result = _calculator.Calculate(
            GameMode.ToutAs,
            MultiplierState.Doubled,
            Team.Team1,
            131,
            127,
            sweepingTeam: null);

        Assert.Equal(28, result.Team1MatchPoints);  // 14 × 2
        Assert.Equal(24, result.Team2MatchPoints);  // 12 × 2
    }

    [Fact]
    public void ToutAs_Redoubled_RoundingTie_AnnouncerWins()
    {
        // 130-128 rounds to 13-13 in normal mode (tie)
        // In redoubled mode, announcer has more card points so must win: 14-12 × 4
        var result = _calculator.Calculate(
            GameMode.ToutAs,
            MultiplierState.Redoubled,
            Team.Team1,
            130,
            128,
            sweepingTeam: null);

        Assert.Equal(56, result.Team1MatchPoints);  // 14 × 4
        Assert.Equal(48, result.Team2MatchPoints);  // 12 × 4
    }

    [Fact]
    public void ToutAs_Doubled_ExactTie_StillZero()
    {
        // 129-129 exact tie remains 0-0 even when doubled
        var result = _calculator.Calculate(
            GameMode.ToutAs,
            MultiplierState.Doubled,
            Team.Team1,
            129,
            129,
            sweepingTeam: null);

        Assert.Equal(0, result.Team1MatchPoints);
        Assert.Equal(0, result.Team2MatchPoints);
    }

    #endregion

    #region SansAs Scoring Tests

    [Fact]
    public void SansAs_AnnouncerWins_Gets52Points()
    {
        var result = _calculator.Calculate(
            GameMode.SansAs,
            MultiplierState.Normal,
            Team.Team1,
            80,  // Announcer >= 65
            50,
            sweepingTeam: null);

        Assert.Equal(52, result.Team1MatchPoints);
        Assert.Equal(0, result.Team2MatchPoints);
    }

    [Fact]
    public void SansAs_AnnouncerLoses_DefenderGets52Points()
    {
        var result = _calculator.Calculate(
            GameMode.SansAs,
            MultiplierState.Normal,
            Team.Team1,
            60,  // Announcer < 65
            70,
            sweepingTeam: null);

        Assert.Equal(0, result.Team1MatchPoints);
        Assert.Equal(52, result.Team2MatchPoints);
    }

    [Fact]
    public void SansAs_Tie_ZeroPoints()
    {
        var result = _calculator.Calculate(
            GameMode.SansAs,
            MultiplierState.Normal,
            Team.Team1,
            65,
            65,
            sweepingTeam: null);

        Assert.Equal(0, result.Team1MatchPoints);
        Assert.Equal(0, result.Team2MatchPoints);
    }

    [Fact]
    public void SansAs_Doubled_104Points()
    {
        var result = _calculator.Calculate(
            GameMode.SansAs,
            MultiplierState.Doubled,
            Team.Team1,
            80,
            50,
            sweepingTeam: null);

        Assert.Equal(104, result.Team1MatchPoints);  // 52 × 2
    }

    #endregion

    #region Colour Scoring Tests

    [Fact]
    public void Colour_AnnouncerWins_Gets16Points()
    {
        var result = _calculator.Calculate(
            GameMode.ColourSpades,
            MultiplierState.Normal,
            Team.Team1,
            100, // Announcer >= 82
            62,
            sweepingTeam: null);

        Assert.Equal(16, result.Team1MatchPoints);
        Assert.Equal(0, result.Team2MatchPoints);
    }

    [Fact]
    public void Colour_AnnouncerLoses_DefenderGets16Points()
    {
        var result = _calculator.Calculate(
            GameMode.ColourHearts,
            MultiplierState.Normal,
            Team.Team1,
            70,  // Announcer < 82
            92,
            sweepingTeam: null);

        Assert.Equal(0, result.Team1MatchPoints);
        Assert.Equal(16, result.Team2MatchPoints);
    }

    [Fact]
    public void Colour_Tie_ZeroPoints()
    {
        var result = _calculator.Calculate(
            GameMode.ColourDiamonds,
            MultiplierState.Normal,
            Team.Team1,
            81,
            81,
            sweepingTeam: null);

        Assert.Equal(0, result.Team1MatchPoints);
        Assert.Equal(0, result.Team2MatchPoints);
    }

    [Fact]
    public void Colour_Doubled_32Points()
    {
        var result = _calculator.Calculate(
            GameMode.ColourHearts,
            MultiplierState.Doubled,
            Team.Team1,
            100,
            62,
            sweepingTeam: null);

        Assert.Equal(32, result.Team1MatchPoints);  // 16 × 2
    }

    [Fact]
    public void ColourClubs_AnnouncerWins_Gets32Points()
    {
        var result = _calculator.Calculate(
            GameMode.ColourClubs,
            MultiplierState.Normal,
            Team.Team1,
            100,
            62,
            sweepingTeam: null);

        Assert.Equal(32, result.Team1MatchPoints);  // Clubs count double
        Assert.Equal(0, result.Team2MatchPoints);
    }

    [Fact]
    public void ColourClubs_AnnouncerLoses_DefenderGets32Points()
    {
        var result = _calculator.Calculate(
            GameMode.ColourClubs,
            MultiplierState.Normal,
            Team.Team1,
            70,
            92,
            sweepingTeam: null);

        Assert.Equal(0, result.Team1MatchPoints);
        Assert.Equal(32, result.Team2MatchPoints);  // Clubs count double
    }

    [Fact]
    public void ColourClubs_Doubled_64Points()
    {
        var result = _calculator.Calculate(
            GameMode.ColourClubs,
            MultiplierState.Doubled,
            Team.Team1,
            100,
            62,
            sweepingTeam: null);

        Assert.Equal(64, result.Team1MatchPoints);  // 32 × 2
    }

    [Fact]
    public void Colour_Redoubled_64Points()
    {
        var result = _calculator.Calculate(
            GameMode.ColourSpades,
            MultiplierState.Redoubled,
            Team.Team1,
            100,
            62,
            sweepingTeam: null);

        Assert.Equal(64, result.Team1MatchPoints);  // 16 × 4
    }

    #endregion

    #region Sweep Tests

    [Fact]
    public void ToutAs_Sweep_35Points()
    {
        var result = _calculator.Calculate(
            GameMode.ToutAs,
            MultiplierState.Normal,
            Team.Team1,
            258,  // All card points
            0,
            sweepingTeam: Team.Team1);

        Assert.Equal(35, result.Team1MatchPoints);
        Assert.Equal(0, result.Team2MatchPoints);
        Assert.True(result.WasSweep);
        Assert.False(result.IsInstantWin);
    }

    [Fact]
    public void ToutAs_Sweep_Doubled_70Points()
    {
        var result = _calculator.Calculate(
            GameMode.ToutAs,
            MultiplierState.Doubled,
            Team.Team1,
            258,
            0,
            sweepingTeam: Team.Team1);

        Assert.Equal(70, result.Team1MatchPoints);  // 35 × 2
    }

    [Fact]
    public void SansAs_Sweep_90Points()
    {
        var result = _calculator.Calculate(
            GameMode.SansAs,
            MultiplierState.Normal,
            Team.Team1,
            130,
            0,
            sweepingTeam: Team.Team1);

        Assert.Equal(90, result.Team1MatchPoints);
        Assert.True(result.WasSweep);
        Assert.False(result.IsInstantWin);
    }

    [Fact]
    public void SansAs_Sweep_Doubled_180Points()
    {
        var result = _calculator.Calculate(
            GameMode.SansAs,
            MultiplierState.Doubled,
            Team.Team1,
            130,
            0,
            sweepingTeam: Team.Team1);

        Assert.Equal(180, result.Team1MatchPoints);  // 90 × 2
    }

    [Fact]
    public void Colour_Sweep_InstantWin()
    {
        var result = _calculator.Calculate(
            GameMode.ColourSpades,
            MultiplierState.Normal,
            Team.Team1,
            162,
            0,
            sweepingTeam: Team.Team1);

        Assert.True(result.WasSweep);
        Assert.True(result.IsInstantWin);
        Assert.Equal(Team.Team1, result.SweepingTeam);
    }

    [Fact]
    public void Colour_Sweep_ByDefender_StillInstantWin()
    {
        // Defender (Team2) sweeps while Team1 announced
        var result = _calculator.Calculate(
            GameMode.ColourHearts,
            MultiplierState.Normal,
            Team.Team1,  // Announcer
            0,
            162,
            sweepingTeam: Team.Team2);

        Assert.True(result.IsInstantWin);
        Assert.Equal(Team.Team2, result.SweepingTeam);
    }

    #endregion

    #region DealResult Properties Tests

    [Fact]
    public void DealResult_AnnouncerWon_ReturnsTrue_WhenAnnouncerHasMorePoints()
    {
        var result = _calculator.Calculate(
            GameMode.ColourSpades,
            MultiplierState.Normal,
            Team.Team1,
            100,
            62,
            sweepingTeam: null);

        Assert.True(result.AnnouncerWon);
    }

    [Fact]
    public void DealResult_AnnouncerWon_ReturnsFalse_WhenDefenderWins()
    {
        var result = _calculator.Calculate(
            GameMode.ColourSpades,
            MultiplierState.Normal,
            Team.Team1,
            70,
            92,
            sweepingTeam: null);

        Assert.False(result.AnnouncerWon);
    }

    [Fact]
    public void DealResult_GetMatchPoints_ReturnsCorrectTeamPoints()
    {
        var result = _calculator.Calculate(
            GameMode.ColourSpades,
            MultiplierState.Normal,
            Team.Team1,
            100,
            62,
            sweepingTeam: null);

        Assert.Equal(16, result.GetMatchPoints(Team.Team1));
        Assert.Equal(0, result.GetMatchPoints(Team.Team2));
    }

    [Fact]
    public void DealResult_GetCardPoints_ReturnsCorrectTeamPoints()
    {
        var result = _calculator.Calculate(
            GameMode.ColourSpades,
            MultiplierState.Normal,
            Team.Team1,
            100,
            62,
            sweepingTeam: null);

        Assert.Equal(100, result.GetCardPoints(Team.Team1));
        Assert.Equal(62, result.GetCardPoints(Team.Team2));
    }

    #endregion
}

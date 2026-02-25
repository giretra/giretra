using Giretra.Web.Services.Elo;

namespace Giretra.Web.Tests.Services.Elo;

public sealed class EloCalculationServiceTests
{
    private readonly EloCalculationService _sut = new();
    private static readonly Guid PlayerId = Guid.NewGuid();

    private static PlayerContext MakeContext(
        int currentElo = 1000,
        bool isBot = false,
        bool isWinner = true,
        double opponentCompositeElo = 1000,
        bool involvedBots = false,
        bool hasBotTeammate = false,
        int botOpponentCount = 0,
        int weeklyBotEloGained = 0)
    {
        return new PlayerContext(
            PlayerId, currentElo, isBot, isWinner,
            opponentCompositeElo, involvedBots, hasBotTeammate,
            botOpponentCount, weeklyBotEloGained);
    }

    #region Normal Match Delta

    [Fact]
    public void EqualRatings_HumanWin_DeltaApprox16()
    {
        var result = _sut.ComputeNormalMatchDelta(MakeContext(isWinner: true));

        Assert.Equal(16, result.EloChange);
        Assert.Equal(1016, result.EloAfter);
    }

    [Fact]
    public void EqualRatings_HumanLoss_DeltaApproxMinus16()
    {
        var result = _sut.ComputeNormalMatchDelta(MakeContext(isWinner: false));

        Assert.Equal(-16, result.EloChange);
        Assert.Equal(984, result.EloAfter);
    }

    [Fact]
    public void BotPlayer_AlwaysGetsZeroDelta()
    {
        var result = _sut.ComputeNormalMatchDelta(MakeContext(isBot: true, isWinner: true));

        Assert.Equal(0, result.EloChange);
        Assert.Equal(1000, result.EloAfter);
    }

    [Fact]
    public void BotGate_At200Gap_ReducesGain()
    {
        // Player 1200 vs opponents 1000 → gap=200, gate=1-200/400=0.5
        var result = _sut.ComputeNormalMatchDelta(MakeContext(
            currentElo: 1200,
            opponentCompositeElo: 1000,
            isWinner: true,
            involvedBots: true));

        // Expected ≈ 0.76, K*(1-0.76)=7.7, gate*0.5 ≈ 3.8 → small positive
        Assert.True(result.EloChange > 0);
        Assert.True(result.EloChange < 16);
    }

    [Fact]
    public void BotGate_At400PlusGap_ZeroGain()
    {
        // Player 5000 vs opponents 1000 → gap=4000, gate=1-4000/4000=0
        var result = _sut.ComputeNormalMatchDelta(MakeContext(
            currentElo: 5000,
            opponentCompositeElo: 1000,
            isWinner: true,
            involvedBots: true));

        Assert.Equal(0, result.EloChange);
    }

    [Fact]
    public void BotGate_OnLosses_NoEffect()
    {
        // Bot gate only applies to wins
        var withBots = _sut.ComputeNormalMatchDelta(MakeContext(
            currentElo: 1200,
            opponentCompositeElo: 1000,
            isWinner: false,
            involvedBots: true));

        var withoutBots = _sut.ComputeNormalMatchDelta(MakeContext(
            currentElo: 1200,
            opponentCompositeElo: 1000,
            isWinner: false,
            involvedBots: false));

        // Losses should be similar (only multipliers differ, not gate)
        // With no bot teammate/opponent, multipliers are 1.0, so deltas should be equal
        Assert.Equal(withoutBots.EloChange, withBots.EloChange);
    }

    [Fact]
    public void BotTeammate_WinMultiplier_AppliesCorrectly()
    {
        var withBotTeammate = _sut.ComputeNormalMatchDelta(MakeContext(
            isWinner: true,
            involvedBots: true,
            hasBotTeammate: true));

        var withoutBotTeammate = _sut.ComputeNormalMatchDelta(MakeContext(
            isWinner: true,
            involvedBots: false));

        // With BOT_TEAMMATE_WIN_MULT = 1.0, bot teammate does not reduce gains
        Assert.True(withBotTeammate.EloChange <= withoutBotTeammate.EloChange);
    }

    [Fact]
    public void BotTeammate_And_TwoBotOpponents_Stacking()
    {
        // Win with bot teammate and 2 bot opponents, all multipliers = 1.0
        var result = _sut.ComputeNormalMatchDelta(MakeContext(
            isWinner: true,
            involvedBots: true,
            hasBotTeammate: true,
            botOpponentCount: 2));

        // Base delta for equal ratings win ≈ 16, multipliers are 1.0, bot gate at 0 gap = 1.0
        Assert.True(result.EloChange > 0);
        Assert.True(result.EloChange <= 16);
    }

    [Fact]
    public void WeeklyCap_LimitsGain()
    {
        // Already gained 5995 this week, max is 6000 → only 5 remaining
        var result = _sut.ComputeNormalMatchDelta(MakeContext(
            isWinner: true,
            involvedBots: true,
            weeklyBotEloGained: 5995));

        Assert.True(result.EloChange <= 5);
        Assert.True(result.EloChange >= 0);
    }

    [Fact]
    public void WeeklyCap_AtLimit_ZeroGain()
    {
        var result = _sut.ComputeNormalMatchDelta(MakeContext(
            isWinner: true,
            involvedBots: true,
            weeklyBotEloGained: 6000));

        Assert.Equal(0, result.EloChange);
    }

    [Fact]
    public void WeeklyCap_DoesNotAffectLosses()
    {
        var withCap = _sut.ComputeNormalMatchDelta(MakeContext(
            isWinner: false,
            involvedBots: true,
            weeklyBotEloGained: 50));

        var withoutCap = _sut.ComputeNormalMatchDelta(MakeContext(
            isWinner: false,
            involvedBots: true,
            weeklyBotEloGained: 0));

        // Losses should be the same regardless of weekly cap
        Assert.Equal(withoutCap.EloChange, withCap.EloChange);
    }

    [Fact]
    public void HigherRatedWinner_GetsLessPoints()
    {
        var higher = _sut.ComputeNormalMatchDelta(MakeContext(
            currentElo: 1400, opponentCompositeElo: 1000, isWinner: true));

        var equal = _sut.ComputeNormalMatchDelta(MakeContext(
            currentElo: 1000, opponentCompositeElo: 1000, isWinner: true));

        Assert.True(higher.EloChange < equal.EloChange);
    }

    [Fact]
    public void LowerRatedWinner_GetsMorePoints()
    {
        var lower = _sut.ComputeNormalMatchDelta(MakeContext(
            currentElo: 800, opponentCompositeElo: 1200, isWinner: true));

        var equal = _sut.ComputeNormalMatchDelta(MakeContext(
            currentElo: 1000, opponentCompositeElo: 1000, isWinner: true));

        Assert.True(lower.EloChange > equal.EloChange);
    }

    #endregion

    #region Abandon Delta

    [Fact]
    public void Abandon_Abandoner_LosesMinus32()
    {
        var result = _sut.ComputeAbandonDelta(PlayerId, 1000, AbandonRole.Abandoner);

        Assert.Equal(-32, result.EloChange);
        Assert.Equal(968, result.EloAfter);
    }

    [Fact]
    public void Abandon_Opponent_GainsPlus10Capped()
    {
        var result = _sut.ComputeAbandonDelta(PlayerId, 1000, AbandonRole.Opponent);

        Assert.Equal(10, result.EloChange);
        Assert.Equal(1010, result.EloAfter);
    }

    [Fact]
    public void Abandon_TeammateOfAbandoner_ZeroChange()
    {
        var result = _sut.ComputeAbandonDelta(PlayerId, 1000, AbandonRole.TeammateOfAbandoner);

        Assert.Equal(0, result.EloChange);
        Assert.Equal(1000, result.EloAfter);
    }

    #endregion
}

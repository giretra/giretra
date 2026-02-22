using Giretra.Core.Players;

namespace Giretra.Core.Tests.Players;

public class PlayerPositionTests
{
    [Theory]
    [InlineData(PlayerPosition.Bottom, PlayerPosition.Left)]
    [InlineData(PlayerPosition.Left, PlayerPosition.Top)]
    [InlineData(PlayerPosition.Top, PlayerPosition.Right)]
    [InlineData(PlayerPosition.Right, PlayerPosition.Bottom)]
    public void Next_ReturnsClockwisePosition(PlayerPosition current, PlayerPosition expected)
    {
        Assert.Equal(expected, current.Next());
    }

    [Theory]
    [InlineData(PlayerPosition.Bottom, PlayerPosition.Right)]
    [InlineData(PlayerPosition.Left, PlayerPosition.Bottom)]
    [InlineData(PlayerPosition.Top, PlayerPosition.Left)]
    [InlineData(PlayerPosition.Right, PlayerPosition.Top)]
    public void Previous_ReturnsCounterClockwisePosition(PlayerPosition current, PlayerPosition expected)
    {
        Assert.Equal(expected, current.Previous());
    }

    [Theory]
    [InlineData(PlayerPosition.Bottom, PlayerPosition.Top)]
    [InlineData(PlayerPosition.Top, PlayerPosition.Bottom)]
    [InlineData(PlayerPosition.Left, PlayerPosition.Right)]
    [InlineData(PlayerPosition.Right, PlayerPosition.Left)]
    public void Teammate_ReturnsOppositePosition(PlayerPosition current, PlayerPosition expected)
    {
        Assert.Equal(expected, current.Teammate());
    }

    [Theory]
    [InlineData(PlayerPosition.Bottom, Team.Team1)]
    [InlineData(PlayerPosition.Top, Team.Team1)]
    [InlineData(PlayerPosition.Left, Team.Team2)]
    [InlineData(PlayerPosition.Right, Team.Team2)]
    public void GetTeam_ReturnsCorrectTeam(PlayerPosition position, Team expected)
    {
        Assert.Equal(expected, position.GetTeam());
    }

    [Fact]
    public void GetPlayOrder_ReturnsClockwiseFromDealerLeft()
    {
        var order = PlayerPosition.Right.GetPlayOrder().ToList();

        Assert.Equal(4, order.Count);
        Assert.Equal(PlayerPosition.Bottom, order[0]);
        Assert.Equal(PlayerPosition.Left, order[1]);
        Assert.Equal(PlayerPosition.Top, order[2]);
        Assert.Equal(PlayerPosition.Right, order[3]);
    }

    [Fact]
    public void Team_Opponent_ReturnsOpposingTeam()
    {
        Assert.Equal(Team.Team2, Team.Team1.Opponent());
        Assert.Equal(Team.Team1, Team.Team2.Opponent());
    }

    [Fact]
    public void Team_GetPositions_ReturnsCorrectPositions()
    {
        var team1Positions = Team.Team1.GetPositions();
        Assert.Equal(PlayerPosition.Bottom, team1Positions.First);
        Assert.Equal(PlayerPosition.Top, team1Positions.Second);

        var team2Positions = Team.Team2.GetPositions();
        Assert.Equal(PlayerPosition.Left, team2Positions.First);
        Assert.Equal(PlayerPosition.Right, team2Positions.Second);
    }

    [Fact]
    public void Teammate_IsOnSameTeam()
    {
        Assert.Equal(PlayerPosition.Bottom.GetTeam(), PlayerPosition.Bottom.Teammate().GetTeam());
        Assert.Equal(PlayerPosition.Left.GetTeam(), PlayerPosition.Left.Teammate().GetTeam());
    }

    [Fact]
    public void Next_AndPrevious_AreInverses()
    {
        foreach (var position in Enum.GetValues<PlayerPosition>())
        {
            Assert.Equal(position, position.Next().Previous());
            Assert.Equal(position, position.Previous().Next());
        }
    }

    [Fact]
    public void Teammate_IsInvolutory()
    {
        foreach (var position in Enum.GetValues<PlayerPosition>())
        {
            Assert.Equal(position, position.Teammate().Teammate());
        }
    }
}

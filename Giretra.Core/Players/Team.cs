namespace Giretra.Core.Players;

/// <summary>
/// Represents the two teams in the game.
/// Team1: Bottom and Top positions
/// Team2: Left and Right positions
/// </summary>
public enum Team
{
    Team1 = 0,
    Team2 = 1
}

public static class TeamExtensions
{
    /// <summary>
    /// Gets the opposing team.
    /// </summary>
    public static Team Opponent(this Team team)
        => team == Team.Team1 ? Team.Team2 : Team.Team1;

    /// <summary>
    /// Gets the two player positions that belong to this team.
    /// </summary>
    public static (PlayerPosition First, PlayerPosition Second) GetPositions(this Team team)
        => team == Team.Team1
            ? (PlayerPosition.Bottom, PlayerPosition.Top)
            : (PlayerPosition.Left, PlayerPosition.Right);
}

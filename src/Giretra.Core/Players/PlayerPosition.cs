namespace Giretra.Core.Players;

/// <summary>
/// Represents the four player positions at the table.
/// Team 1: Bottom and Top (seated opposite each other)
/// Team 2: Left and Right (seated opposite each other)
/// Play proceeds clockwise: Bottom → Left → Top → Right → Bottom
/// </summary>
public enum PlayerPosition
{
    Bottom = 0,
    Left = 1,
    Top = 2,
    Right = 3
}

public static class PlayerPositionExtensions
{
    /// <summary>
    /// Gets the next player position in clockwise order.
    /// </summary>
    public static PlayerPosition Next(this PlayerPosition position)
        => (PlayerPosition)(((int)position + 1) % 4);

    /// <summary>
    /// Gets the previous player position in counter-clockwise order.
    /// </summary>
    public static PlayerPosition Previous(this PlayerPosition position)
        => (PlayerPosition)(((int)position + 3) % 4);

    /// <summary>
    /// Gets the teammate's position (directly across the table).
    /// </summary>
    public static PlayerPosition Teammate(this PlayerPosition position)
        => (PlayerPosition)(((int)position + 2) % 4);

    /// <summary>
    /// Gets the team this player belongs to.
    /// </summary>
    public static Team GetTeam(this PlayerPosition position)
        => position is PlayerPosition.Bottom or PlayerPosition.Top
            ? Team.Team1
            : Team.Team2;

    /// <summary>
    /// Returns all positions in clockwise order starting from dealer's left.
    /// </summary>
    public static IEnumerable<PlayerPosition> GetPlayOrder(this PlayerPosition dealer)
    {
        var current = dealer.Next();
        for (int i = 0; i < 4; i++)
        {
            yield return current;
            current = current.Next();
        }
    }
}

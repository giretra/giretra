namespace Giretra.Web.Domain;

/// <summary>
/// The status of a game room.
/// </summary>
public enum RoomStatus
{
    /// <summary>
    /// Room is waiting for players to join.
    /// </summary>
    Waiting,

    /// <summary>
    /// Game is in progress.
    /// </summary>
    Playing,

    /// <summary>
    /// Game has completed.
    /// </summary>
    Completed
}

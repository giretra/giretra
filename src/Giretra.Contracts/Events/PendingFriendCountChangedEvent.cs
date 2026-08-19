namespace Giretra.Web.Models.Events;

/// <summary>
/// Event sent to a user when their pending friend request count changes.
/// </summary>
public sealed class PendingFriendCountChangedEvent
{
    /// <summary>
    /// The number of pending friend requests.
    /// </summary>
    public required int Count { get; init; }
}

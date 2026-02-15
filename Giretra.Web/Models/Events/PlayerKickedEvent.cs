using Giretra.Core.Players;

namespace Giretra.Web.Models.Events;

public sealed class PlayerKickedEvent
{
    public required string RoomId { get; init; }
    public required string PlayerName { get; init; }
    public required PlayerPosition Position { get; init; }
}

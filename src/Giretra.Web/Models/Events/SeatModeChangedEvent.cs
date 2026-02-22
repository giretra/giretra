using Giretra.Core.Players;
using Giretra.Web.Domain;

namespace Giretra.Web.Models.Events;

public sealed class SeatModeChangedEvent
{
    public required string RoomId { get; init; }
    public required PlayerPosition Position { get; init; }
    public required SeatAccessMode AccessMode { get; init; }
}

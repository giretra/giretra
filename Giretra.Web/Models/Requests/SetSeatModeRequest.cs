using Giretra.Core.Players;
using Giretra.Web.Domain;

namespace Giretra.Web.Models.Requests;

public sealed class SetSeatModeRequest
{
    public required PlayerPosition Position { get; init; }
    public required SeatAccessMode AccessMode { get; init; }
}

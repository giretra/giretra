using Giretra.Core.Players;

namespace Giretra.Web.Models.Requests;

public sealed class KickPlayerRequest
{
    public required PlayerPosition Position { get; init; }
}

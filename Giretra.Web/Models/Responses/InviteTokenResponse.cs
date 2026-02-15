using Giretra.Core.Players;

namespace Giretra.Web.Models.Responses;

public sealed class InviteTokenResponse
{
    public required PlayerPosition Position { get; init; }
    public required string Token { get; init; }
    public required string InviteUrl { get; init; }
}

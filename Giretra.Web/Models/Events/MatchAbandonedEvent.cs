using Giretra.Core.Players;

namespace Giretra.Web.Models.Events;

public sealed class MatchAbandonedEvent
{
    public required string GameId { get; init; }
    public required PlayerPosition Abandoner { get; init; }
    public required Team WinnerTeam { get; init; }
}

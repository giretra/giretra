using Giretra.Model.Enums;

namespace Giretra.Web.Models.Responses;

public sealed class MatchHistoryPlayerResponse
{
    public required string DisplayName { get; init; }
    public required PlayerPosition Position { get; init; }
    public required Team Team { get; init; }
    public required bool IsWinner { get; init; }
}

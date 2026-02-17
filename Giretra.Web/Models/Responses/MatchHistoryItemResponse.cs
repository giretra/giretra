using Giretra.Model.Enums;

namespace Giretra.Web.Models.Responses;

public sealed class MatchHistoryItemResponse
{
    public required Guid MatchId { get; init; }
    public required string RoomName { get; init; }
    public required int Team1FinalScore { get; init; }
    public required int Team2FinalScore { get; init; }
    public required Team Team { get; init; }
    public required PlayerPosition Position { get; init; }
    public required bool IsWinner { get; init; }
    public int? EloChange { get; init; }
    public required int TotalDeals { get; init; }
    public required bool WasAbandoned { get; init; }
    public int? DurationSeconds { get; init; }
    public required DateTimeOffset PlayedAt { get; init; }
    public required IReadOnlyList<MatchHistoryPlayerResponse> Players { get; init; }
}

using Giretra.Model.Enums;

namespace Giretra.Web.Models.Responses;

public sealed class AdminGameListResponse
{
    public required IReadOnlyList<AdminGameEntry> Games { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
}

public sealed class AdminGameEntry
{
    public required Guid Id { get; init; }
    public required string RoomName { get; init; }
    public required int Team1FinalScore { get; init; }
    public required int Team2FinalScore { get; init; }
    public Team? WinnerTeam { get; init; }
    public required int TotalDeals { get; init; }
    public required bool IsRanked { get; init; }
    public required bool WasAbandoned { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public int? DurationSeconds { get; init; }
    public required IReadOnlyList<AdminGamePlayerEntry> Players { get; init; }
}

public sealed class AdminGamePlayerEntry
{
    public required string DisplayName { get; init; }
    public Guid? UserId { get; init; }
    public required bool IsBot { get; init; }
    public required PlayerPosition Position { get; init; }
    public required Team Team { get; init; }
    public required bool IsWinner { get; init; }
    public int? EloChange { get; init; }
}

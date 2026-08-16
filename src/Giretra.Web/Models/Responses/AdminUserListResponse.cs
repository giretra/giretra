using Giretra.Model.Enums;

namespace Giretra.Web.Models.Responses;

public sealed class AdminUserListResponse
{
    public required IReadOnlyList<AdminUserEntry> Users { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
}

public sealed class AdminUserEntry
{
    public required Guid Id { get; init; }
    public required string Username { get; init; }
    public required string DisplayName { get; init; }
    public string? CustomDisplayName { get; init; }
    public string? Email { get; init; }
    public string? AvatarUrl { get; init; }
    public required UserRole Role { get; init; }
    public required bool IsBanned { get; init; }
    public string? BanReason { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? LastLoginAt { get; init; }
    public int? EloRating { get; init; }
    public int? GamesPlayed { get; init; }
    public int? GamesWon { get; init; }
    public required int BlockedByCount { get; init; }
}

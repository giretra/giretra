namespace Giretra.Web.Models.Responses;

public sealed class BlockedUserResponse
{
    public required Guid BlockId { get; init; }
    public required Guid UserId { get; init; }
    public required string Username { get; init; }
    public required string DisplayName { get; init; }
    public required DateTimeOffset BlockedAt { get; init; }
}

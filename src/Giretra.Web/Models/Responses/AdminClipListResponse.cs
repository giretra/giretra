namespace Giretra.Web.Models.Responses;

public sealed class AdminClipListResponse
{
    public required IReadOnlyList<AdminClipEntry> Items { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
}

/// <summary>A clip as moderators see it: the public view plus render diagnostics and the creator.</summary>
public sealed class AdminClipEntry
{
    public required ClipResponse Clip { get; init; }
    public required int Attempts { get; init; }
    public string? Error { get; init; }
    public DateTimeOffset? ClaimedAt { get; init; }
    public required Guid CreatedByPlayerId { get; init; }
    public required string CreatedByName { get; init; }
}

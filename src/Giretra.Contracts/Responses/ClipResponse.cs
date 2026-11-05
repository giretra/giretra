using Giretra.Core.Players;

namespace Giretra.Web.Models.Responses;

public enum ClipStatusResponse
{
    Pending,
    Rendering,
    Ready,
    Failed,
    Evicted,
    Removed
}

/// <summary>
/// A shareable clip. The public endpoint includes the replay payload (with names filtered
/// by each player's privacy setting); list endpoints leave it null.
/// </summary>
public sealed class ClipResponse
{
    public required string ShortId { get; init; }
    public required ClipStatusResponse Status { get; init; }
    public required Guid MatchId { get; init; }
    public required int DealNumber { get; init; }
    public required int FromTrick { get; init; }
    public required int ToTrick { get; init; }
    public required PlayerPosition ViewPosition { get; init; }
    public required bool ShowAllHands { get; init; }
    public required string Locale { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? RenderedAt { get; init; }
    public int? DurationMs { get; init; }
    public required int ViewCount { get; init; }

    /// <summary>Short human title, e.g. "Deal 3, tricks 2–4 · Haga & Lova vs Rakoto & Rasoa".</summary>
    public required string Title { get; init; }

    /// <summary>Absolute URL of the public landing page.</summary>
    public required string PageUrl { get; init; }
    /// <summary>Absolute URL of the poster image, null until rendered.</summary>
    public string? PosterUrl { get; init; }
    /// <summary>Absolute URL of the MP4, null until rendered.</summary>
    public string? VideoUrl { get; init; }

    /// <summary>Replay payload for the interactive public replay (public endpoint only).</summary>
    public DealReplayResponse? Replay { get; init; }
}

public sealed class ClipListResponse
{
    public required IReadOnlyList<ClipResponse> Items { get; init; }
}

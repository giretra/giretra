using Giretra.Web.Models.Responses;

namespace Giretra.Web.Models.Events;

/// <summary>
/// Event sent once a finished match is persisted and its shareable moments are computed.
/// Follows <see cref="MatchEndedEvent"/> (and <see cref="AchievementsEarnedEvent"/> when
/// any were earned); only ranked matches are persisted, so unranked matches never get it.
/// </summary>
public sealed class MatchArchivedEvent
{
    public required string GameId { get; init; }

    /// <summary>The persisted match id: what the replay and clip APIs take.</summary>
    public required Guid MatchId { get; init; }

    /// <summary>Suggested clips, best first, for both teams; clients keep their own.</summary>
    public required IReadOnlyList<ClipMomentResponse> Moments { get; init; }
}

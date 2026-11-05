using Giretra.Web.Models.Responses;

namespace Giretra.Web.Services.Replay;

public enum DealReplayStatus
{
    Ok,
    MatchNotFound,
    DealNotFound,
    /// <summary>The requesting user did not play in the match.</summary>
    Forbidden,
    /// <summary>The match was abandoned or unfinished, or the deal has no complete play record.</summary>
    NotReplayable,
}

public sealed record DealReplayResult(DealReplayStatus Status, DealReplayResponse? Replay)
{
    public static DealReplayResult Of(DealReplayStatus status) => new(status, null);
    public static DealReplayResult Success(DealReplayResponse replay) => new(DealReplayStatus.Ok, replay);
}

public interface IDealReplayService
{
    /// <summary>
    /// Builds the replay of one deal for a participant of the match. Only completed,
    /// non-abandoned matches are replayable.
    /// </summary>
    Task<DealReplayResult> GetReplayAsync(Guid matchId, int dealNumber, Guid userId);
}

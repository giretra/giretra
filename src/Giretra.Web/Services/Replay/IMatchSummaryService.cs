using Giretra.Web.Models.Responses;

namespace Giretra.Web.Services.Replay;

public enum MatchSummaryStatus
{
    Ok,
    MatchNotFound,
    /// <summary>The requesting user did not play in the match.</summary>
    Forbidden,
    /// <summary>The match is still running.</summary>
    NotCompleted,
}

public sealed record MatchSummaryResult(MatchSummaryStatus Status, MatchSummaryResponse? Summary)
{
    public static MatchSummaryResult Of(MatchSummaryStatus status) => new(status, null);
    public static MatchSummaryResult Success(MatchSummaryResponse summary) => new(MatchSummaryStatus.Ok, summary);
}

public interface IMatchSummaryService
{
    /// <summary>
    /// Builds the scoreboard of a finished match (abandoned ones included) for one of its
    /// participants.
    /// </summary>
    Task<MatchSummaryResult> GetSummaryAsync(Guid matchId, Guid userId);
}

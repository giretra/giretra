using Giretra.Web.Models.Responses;

namespace Giretra.Web.Services;

public interface IMatchHistoryService
{
    Task<MatchHistoryListResponse> GetMatchHistoryAsync(Guid userId, int page, int pageSize);
}

using Giretra.Web.Models.Responses;

namespace Giretra.Web.Services;

public interface IAdminGameService
{
    Task<AdminGameListResponse> GetGamesAsync(Guid? userId, int page, int pageSize);

    /// <summary>Per-deal breakdown of a match, or null if the match does not exist.</summary>
    Task<AdminGameDealsResponse?> GetGameDealsAsync(Guid matchId);
}

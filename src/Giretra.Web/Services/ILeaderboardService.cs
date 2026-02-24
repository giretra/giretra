using Giretra.Web.Models.Responses;

namespace Giretra.Web.Services;

public interface ILeaderboardService
{
    Task<LeaderboardResponse> GetLeaderboardAsync();
    Task<PlayerProfileResponse?> GetPlayerProfileAsync(Guid playerId);
}

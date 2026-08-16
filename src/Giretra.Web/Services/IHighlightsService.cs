using Giretra.Web.Models.Responses;

namespace Giretra.Web.Services;

public interface IHighlightsService
{
    Task<HighlightsResponse> GetHighlightsAsync(Guid userId);

    /// <summary>Public view of any player's highlights; null when the player does not exist.</summary>
    Task<HighlightsResponse?> GetPlayerHighlightsAsync(Guid playerId);
}

using Giretra.Core.Players;

namespace Giretra.Web.Models.Responses;

/// <summary>
/// Response DTO for a played card.
/// </summary>
public sealed class PlayedCardResponse
{
    /// <summary>
    /// The player who played the card.
    /// </summary>
    public required PlayerPosition Player { get; init; }

    /// <summary>
    /// The card that was played.
    /// </summary>
    public required CardResponse Card { get; init; }
}

using Giretra.Core.Players;
using Giretra.Web.Models.Responses;

namespace Giretra.Web.Models.Events;

/// <summary>
/// Event sent when a card is played.
/// </summary>
public sealed class CardPlayedEvent
{
    /// <summary>
    /// The game ID.
    /// </summary>
    public required string GameId { get; init; }

    /// <summary>
    /// The player who played the card.
    /// </summary>
    public required PlayerPosition Player { get; init; }

    /// <summary>
    /// The card that was played.
    /// </summary>
    public required CardResponse Card { get; init; }
}

using Giretra.Core.Players;

namespace Giretra.Web.Models.Responses;

/// <summary>
/// Response DTO for a trick.
/// </summary>
public sealed class TrickResponse
{
    /// <summary>
    /// The player who led the trick.
    /// </summary>
    public required PlayerPosition Leader { get; init; }

    /// <summary>
    /// The trick number (1-8).
    /// </summary>
    public required int TrickNumber { get; init; }

    /// <summary>
    /// The cards played in this trick.
    /// </summary>
    public required IReadOnlyList<PlayedCardResponse> PlayedCards { get; init; }

    /// <summary>
    /// Whether the trick is complete.
    /// </summary>
    public required bool IsComplete { get; init; }

    /// <summary>
    /// The winner of the trick (null if not complete).
    /// </summary>
    public PlayerPosition? Winner { get; init; }
}

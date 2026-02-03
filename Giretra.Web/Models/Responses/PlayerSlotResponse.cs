using Giretra.Core.Players;

namespace Giretra.Web.Models.Responses;

/// <summary>
/// Response DTO for a player slot in a room.
/// </summary>
public sealed class PlayerSlotResponse
{
    /// <summary>
    /// The position of this slot.
    /// </summary>
    public required PlayerPosition Position { get; init; }

    /// <summary>
    /// Whether this slot is occupied.
    /// </summary>
    public required bool IsOccupied { get; init; }

    /// <summary>
    /// Display name of the player (null if empty or AI).
    /// </summary>
    public string? PlayerName { get; init; }

    /// <summary>
    /// Whether this slot is held by an AI player.
    /// </summary>
    public bool IsAi { get; init; }
}

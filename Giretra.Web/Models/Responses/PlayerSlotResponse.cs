using Giretra.Core.Players;
using Giretra.Web.Domain;

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

    /// <summary>
    /// The AI agent type name (null if not AI).
    /// </summary>
    public string? AiType { get; init; }

    /// <summary>
    /// User-friendly display name for the AI agent type (null if not AI).
    /// </summary>
    public string? AiDisplayName { get; init; }

    /// <summary>
    /// Access mode for this seat (Public or InviteOnly).
    /// </summary>
    public SeatAccessMode AccessMode { get; init; }

    /// <summary>
    /// Whether this seat has an active invite token (only visible to owner).
    /// </summary>
    public bool HasInvite { get; init; }
}

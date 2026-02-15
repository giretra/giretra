using Giretra.Core.Players;

namespace Giretra.Web.Domain;

/// <summary>
/// Represents a connected client (player or watcher).
/// </summary>
public sealed class ConnectedClient
{
    /// <summary>
    /// Persistent user identity (survives reconnections).
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Unique identifier for this client connection.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Display name for the client.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// SignalR connection ID for real-time communication.
    /// </summary>
    public string? ConnectionId { get; set; }

    /// <summary>
    /// Whether this client is a player (not a watcher).
    /// </summary>
    public bool IsPlayer { get; init; }

    /// <summary>
    /// The player position if this client is a player.
    /// </summary>
    public PlayerPosition? Position { get; set; }

    /// <summary>
    /// When the client joined.
    /// </summary>
    public DateTime JoinedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Last activity timestamp for timeout detection.
    /// </summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
}

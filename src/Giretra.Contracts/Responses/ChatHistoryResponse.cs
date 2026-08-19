using Giretra.Web.Models.Events;

namespace Giretra.Web.Models.Responses;

/// <summary>
/// Response DTO for a room's chat history and status.
/// </summary>
public sealed class ChatHistoryResponse
{
    /// <summary>
    /// The chat messages, oldest first.
    /// </summary>
    public required IReadOnlyList<ChatMessageEvent> Messages { get; init; }

    /// <summary>
    /// Whether chat is currently enabled in the room.
    /// </summary>
    public required bool IsChatEnabled { get; init; }
}

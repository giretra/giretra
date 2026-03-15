namespace Giretra.Web.Models.Events;

public sealed class ChatMessageEvent
{
    public required long SequenceNumber { get; init; }
    public required string SenderName { get; init; }
    public required bool IsPlayer { get; init; }
    public required string Content { get; init; }
    public required DateTime SentAt { get; init; }
}

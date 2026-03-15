namespace Giretra.Web.Models.Events;

public sealed class ChatStatusChangedEvent
{
    public required bool IsChatEnabled { get; init; }
}

using Giretra.Web.Domain;

namespace Giretra.Web.Services;

public interface IChatService
{
    ChatMessage? SendMessage(string roomId, string clientId, string content);
    IReadOnlyList<ChatMessage> GetHistory(string roomId);
    bool IsChatEnabled(string roomId);
    void ClearRoom(string roomId);
}

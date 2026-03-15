using Giretra.Web.Domain;
using Giretra.Web.Models.Events;
using Giretra.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Giretra.Web.Hubs;

/// <summary>
/// SignalR hub for real-time game communication.
/// </summary>
[Authorize]
public sealed class GameHub : Hub
{
    private readonly IRoomService _roomService;
    private readonly IUserSyncService _userSyncService;
    private readonly IChatService _chatService;

    public GameHub(IRoomService roomService, IUserSyncService userSyncService, IChatService chatService)
    {
        _roomService = roomService;
        _userSyncService = userSyncService;
        _chatService = chatService;
    }

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            var user = await _userSyncService.SyncUserAsync(Context.User);
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{user.Id}");
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Joins a room for real-time updates.
    /// </summary>
    public async Task JoinRoom(string roomId, string clientId)
    {
        // Update client's connection ID
        _roomService.UpdateClientConnection(clientId, Context.ConnectionId);

        // Add to room group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"room_{roomId}");

        // Add to personal group (for direct messages)
        await Groups.AddToGroupAsync(Context.ConnectionId, $"client_{clientId}");
    }

    /// <summary>
    /// Leaves a room.
    /// </summary>
    public async Task LeaveRoom(string roomId, string clientId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"room_{roomId}");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"client_{clientId}");
    }

    /// <summary>
    /// Joins the lobby group for room list updates.
    /// </summary>
    public async Task JoinLobby()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "lobby");
    }

    /// <summary>
    /// Leaves the lobby group.
    /// </summary>
    public async Task LeaveLobby()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "lobby");
    }

    /// <summary>
    /// Sends a chat message to a room.
    /// </summary>
    public async Task SendChatMessage(string roomId, string clientId, string content)
    {
        var message = _chatService.SendMessage(roomId, clientId, content);
        if (message == null)
            throw new HubException("Unable to send message");

        var ev = MapToEvent(message);
        await Clients.Group($"room_{roomId}").SendAsync("ChatMessageReceived", ev);
    }

    /// <summary>
    /// Gets chat history and current status for a room.
    /// </summary>
    public object GetChatHistory(string roomId)
    {
        var messages = _chatService.GetHistory(roomId).Select(MapToEvent).ToList();

        return new { messages, isChatEnabled = _chatService.IsChatEnabled(roomId) };
    }

    private static ChatMessageEvent MapToEvent(ChatMessage m) => new()
    {
        SequenceNumber = m.SequenceNumber,
        SenderName = m.SenderName,
        IsPlayer = m.IsPlayer,
        Content = m.Content,
        SentAt = m.SentAt,
        IsSystem = m.IsSystem
    };

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _roomService.HandleDisconnect(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

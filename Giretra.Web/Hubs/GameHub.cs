using Giretra.Web.Services;
using Microsoft.AspNetCore.SignalR;

namespace Giretra.Web.Hubs;

/// <summary>
/// SignalR hub for real-time game communication.
/// </summary>
public sealed class GameHub : Hub
{
    private readonly IRoomService _roomService;

    public GameHub(IRoomService roomService)
    {
        _roomService = roomService;
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

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _roomService.HandleDisconnect(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
